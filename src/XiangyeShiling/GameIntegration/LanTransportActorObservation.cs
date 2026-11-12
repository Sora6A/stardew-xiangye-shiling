using System.Reflection;
using System.Security.Cryptography;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Network;

namespace XiangyeShiling.GameIntegration;

/// <summary>主线程上的本地观测结果。只记录通过当前原生连接核对的事件，不授予事务权限。</summary>
public sealed record LanTransportEvidence(long ActorId, string MessageType, ulong WorldId,
    long WorldGeneration, long ConnectionGeneration, Guid ConnectionId);

/// <summary>当前构建的 LAN 观测能力和计数。安装成功仍需要真实原版接收事件才能取得证据。</summary>
public sealed record LanTransportDiagnosticSnapshot(bool Configured, bool HookInstalled, bool Allowed,
    long Confirmed, long MissingScope, long ActorMismatch, long StaleConnection, LanTransportEvidence? LastConfirmed);

/// <summary>
/// 在经过核对的 Windows 1.6.15.24356 / SMAPI 4.4.0 组合上被动关联 LAN 连接与 SMAPI 事件。
/// 原版批准农工后维护的反向连接表是身份来源。消息原生头和 SMAPI JSON 都不能代替该表。
/// </summary>
internal sealed class LanTransportActorObservation
{
    private const string GameSha256 = "7F1E5B8E58D2758B78570BA771BBEB03D33522F62188BF6C32EDF0CF626DEAEE";
    private const string SmapiSha256 = "A6FE919F2E7ABEA3B219A4591EA1955903192B6BF757CF70860E4679EA1BAFBE";
    private const BindingFlags Members = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    private static readonly object InstallLock = new();
    private static NativeBinding? InstalledBinding;
    [ThreadStatic] private static NativeScope? CurrentScope;

    private readonly bool Configured;
    private readonly bool HookInstalled;
    private long Confirmed, MissingScope, ActorMismatch, StaleConnection;
    private LanTransportEvidence? LastConfirmed;

    internal LanTransportActorObservation(bool configured, IMonitor monitor, string modId)
    {
        this.Configured = configured;
        if (!configured) return;
        lock (InstallLock)
        {
            try
            {
                if (InstalledBinding is null && FindBinding() is { } binding)
                {
                    // 该补丁不替换原版或 SMAPI 逻辑，也不拦截其他模组。仅在本接收调用内暴露来源。
                    new Harmony(modId + ".LanTransportObservation").Patch(binding.ReceiveMethod,
                        prefix: new HarmonyMethod(typeof(LanTransportActorObservation), nameof(EnterReceive)),
                        finalizer: new HarmonyMethod(typeof(LanTransportActorObservation), nameof(LeaveReceive)));
                    Volatile.Write(ref InstalledBinding, binding);
                }
                this.HookInstalled = InstalledBinding is not null;
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                monitor.Log($"LAN transport observation unavailable ({ex.GetType().Name}).", LogLevel.Trace);
            }
        }
    }

    // 独立判据让无原生接收上下文、声明不符和代次过期可以在 SDK CLI 以普通数据验证。
    internal static ProvenanceDecision Classify(long? actor, long claim, long worldGeneration,
        long connectionGeneration, long currentWorldGeneration, long currentConnectionGeneration)
    {
        if (actor is null or 0) return ProvenanceDecision.MissingScope;
        if (actor != claim) return ProvenanceDecision.ActorMismatch;
        return worldGeneration == currentWorldGeneration && connectionGeneration == currentConnectionGeneration
            ? ProvenanceDecision.Match : ProvenanceDecision.Stale;
    }

    internal IngressTicket? Capture(long claim, long worldGeneration, long connectionGeneration)
    {
        NativeScope? scope = this.HookInstalled ? CurrentScope : null;
        switch (Classify(scope?.ActorId, claim, worldGeneration, connectionGeneration, worldGeneration, connectionGeneration))
        {
            case ProvenanceDecision.Match:
                return new(scope!, worldGeneration, connectionGeneration);
            case ProvenanceDecision.ActorMismatch:
                this.ActorMismatch++;
                break;
            default:
                this.MissingScope++;
                break;
        }
        return null;
    }

    internal void Discard(IngressTicket? ticket)
    {
        if (ticket is not null) this.StaleConnection++;
    }

    internal void Confirm(IngressTicket? ticket, long claim, string messageType, long worldGeneration,
        long connectionGeneration, Guid connectionId)
    {
        if (ticket is null) return;
        var scope = ticket.Scope;
        try
        {
            if (Classify(scope.ActorId, claim, ticket.WorldGeneration, ticket.ConnectionGeneration,
                    worldGeneration, connectionGeneration) != ProvenanceDecision.Match
                || InstalledBinding is not { } binding || !IsCurrentWorld(scope)
                || !binding.TryResolveActor(scope.Adapter, scope.Connection, out long actor) || actor != scope.ActorId
                || !Game1.getOnlineFarmers().Any(farmer => farmer.UniqueMultiplayerID == actor))
            {
                this.StaleConnection++;
                return;
            }
            this.Confirmed++;
            this.LastConfirmed = new(actor, messageType, scope.WorldId, worldGeneration, connectionGeneration, connectionId);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            this.StaleConnection++;
        }
    }

    internal LanTransportDiagnosticSnapshot Read(bool allowed, long worldGeneration, long connectionGeneration,
        IReadOnlyDictionary<long, Guid> connections)
    {
        allowed &= this.HookInstalled;
        return new(this.Configured, this.HookInstalled, allowed, this.Confirmed, this.MissingScope, this.ActorMismatch,
            this.StaleConnection, allowed && this.LastConfirmed is { } last && last.WorldGeneration == worldGeneration
                && last.ConnectionGeneration == connectionGeneration && connections.TryGetValue(last.ActorId, out Guid connectionId)
                && last.ConnectionId == connectionId ? last : null);
    }

    private static NativeBinding? FindBinding()
    {
        var game = typeof(Game1).Assembly;
        var smapi = typeof(IModHelper).Assembly;
        // 精确二进制只用于能力检测。本地相同版本号的其他平台/构建不因此获得已验收的来源。
        if (!OperatingSystem.IsWindows() || game.GetName().Version != new Version(1, 6, 15, 24356)
            || smapi.GetName().Version != new Version(4, 4, 0, 0)
            || !MatchesHash(game, GameSha256) || !MatchesHash(smapi, SmapiSha256)) return null;

        var lidgren = game.GetType("StardewValley.Network.LidgrenServer", throwOnError: true)!;
        var closure = lidgren.GetNestedType("<>c__DisplayClass23_0", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Expected native receive callback target is absent.");
        var send = closure.GetMethod("<parseDataMessageFromClient>b__0", Members, null, new[] { typeof(OutgoingMessage) }, null)
            ?? throw new InvalidOperationException("Expected native receive callback is absent.");
        var sender = closure.GetField("peer", Members) ?? throw new InvalidOperationException("Native sender field is absent.");
        var adapter = closure.GetField("<>4__this", Members) ?? throw new InvalidOperationException("Native adapter field is absent.");
        var peers = lidgren.GetField("peers", Members) ?? throw new InvalidOperationException("Native peer table is absent.");
        var hostServer = typeof(Server).GetField("gameServer", Members)
            ?? throw new InvalidOperationException("Native host owner field is absent.");
        var serverProcessing = smapi.GetType("StardewModdingAPI.Framework.SMultiplayer", throwOnError: true)!
            .GetMethod("OnServerProcessingMessage", Members, null,
                new[] { typeof(IncomingMessage), typeof(Action<OutgoingMessage>), typeof(Action) }, null)
            ?? throw new InvalidOperationException("Expected SMAPI dispatch method is absent.");
        var type = smapi.GetType("StardewModdingAPI.Framework.Networking.MessageType", throwOnError: true)!;
        byte modMessageType = Convert.ToByte(Enum.Parse(type, "ModMessage"));
        if (send.ReturnType != typeof(void) || serverProcessing.ReturnType != typeof(void) || adapter.FieldType != lidgren
            || sender.FieldType.FullName != "Lidgren.Network.NetConnection"
            || sender.FieldType.Assembly.GetName().Name != "Lidgren.Network"
            || hostServer.FieldType != typeof(IGameServer)) return null;

        var contains = peers.FieldType.GetMethod("ContainsRight", new[] { sender.FieldType });
        var getActor = peers.FieldType.GetMethod("get_Item", new[] { sender.FieldType });
        var getConnection = peers.FieldType.GetMethod("get_Item", new[] { typeof(long) });
        if (contains?.ReturnType != typeof(bool) || getActor?.ReturnType != typeof(long)
            || getConnection?.ReturnType != sender.FieldType) return null;
        return new(serverProcessing, send, closure, lidgren, sender, adapter, peers, hostServer, contains, getActor,
            getConnection, modMessageType, Thread.CurrentThread);
    }

    private static bool MatchesHash(Assembly assembly, string expected)
    {
        if (string.IsNullOrEmpty(assembly.Location)) return false;
        using var stream = File.OpenRead(assembly.Location);
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(stream)) == expected;
    }

    private static bool HostWorld() => Context.IsWorldReady && Context.IsMultiplayer && Context.IsMainPlayer
        && !Context.IsSplitScreen && Context.ScreenId == 0 && Game1.server is not null && Game1.uniqueIDForThisGame != 0;

    private static bool IsCurrentWorld(NativeScope scope) => HostWorld()
        && Game1.uniqueIDForThisGame == scope.WorldId && ReferenceEquals(Game1.server, scope.HostServer)
        && Game1.player.UniqueMultiplayerID == scope.HostId;

    private static void EnterReceive(IncomingMessage __0, Action<OutgoingMessage> __1, out NativeScope? __state)
    {
        __state = CurrentScope;
        CurrentScope = null; // 嵌套的非 LAN 调用不得借用外层连接。
        var binding = Volatile.Read(ref InstalledBinding);
        if (binding is null || !ReferenceEquals(Thread.CurrentThread, binding.Owner) || __0.MessageType != binding.ModMessageType) return;
        try
        {
            if (!HostWorld() || __1.Target is not { } target || target.GetType() != binding.ClosureType
                || __1.Method != binding.SendMethod || __1.GetInvocationList().Length != 1) return;
            var adapter = binding.Adapter.GetValue(target);
            var connection = binding.Sender.GetValue(target);
            if (adapter?.GetType() != binding.AdapterType || connection is null
                || !binding.TryResolveActor(adapter, connection, out long actor)) return;
            long hostId = Game1.player.UniqueMultiplayerID;
            if (hostId == 0 || actor == hostId) return;
            CurrentScope = new(actor, Game1.uniqueIDForThisGame, hostId, Game1.server, adapter, connection);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // 反射或环境不符只关闭观测，继续原来的网络处理。
        }
    }

    private static Exception? LeaveReceive(Exception? __exception, NativeScope? __state)
    {
        CurrentScope = __state;
        return __exception;
    }

    internal enum ProvenanceDecision { MissingScope, ActorMismatch, Stale, Match }
    internal sealed record NativeScope(long ActorId, ulong WorldId, long HostId, IGameServer HostServer,
        object Adapter, object Connection);
    internal sealed record IngressTicket(NativeScope Scope, long WorldGeneration, long ConnectionGeneration);

    private sealed record NativeBinding(MethodInfo ReceiveMethod, MethodInfo SendMethod, Type ClosureType,
        Type AdapterType, FieldInfo Sender, FieldInfo Adapter, FieldInfo Peers, FieldInfo HostServer,
        MethodInfo ContainsConnection, MethodInfo GetActor, MethodInfo GetConnection, byte ModMessageType, Thread Owner)
    {
        internal bool TryResolveActor(object adapter, object connection, out long actor)
        {
            actor = 0;
            if (Game1.server is null || !ReferenceEquals(this.HostServer.GetValue(adapter), Game1.server)
                || this.Peers.GetValue(adapter) is not { } peers
                || this.ContainsConnection.Invoke(peers, new[] { connection }) is not true
                || this.GetActor.Invoke(peers, new[] { connection }) is not long found || found == 0
                || !ReferenceEquals(this.GetConnection.Invoke(peers, new object[] { found }), connection)) return false;
            actor = found;
            return true;
        }
    }
}
