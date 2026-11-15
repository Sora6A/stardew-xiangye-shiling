using System.Diagnostics;
using System.Reflection;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Network;

namespace GameHarness;

/// <summary>
/// 从标题画面启动本地专用合作档，或通过游戏自己的 LAN 客户端加入已有农工。
/// 调用方只可在主线程驱动，并在 Ready 后核对实际连接与角色；这里不准备背包、机器或存档。
/// </summary>
internal sealed class NativeNetworkBootstrap
{
    internal const string FarmName = "XYSNET02";
    internal const ulong FixtureId = 450604963;
    internal const long HostId = -3294125852957351781;
    internal const string ClientName = "XYSNETClient";
    internal const long ClientId = -5889529773947908937;
    internal const string RecoveryFarmName = "XYSNET03";
    internal const ulong RecoveryFixtureId = 620261112;
    internal const string BedtimeFarmName = "XYSNET04";
    internal const ulong BedtimeFixtureId = 620261114;
    internal const string TransactionFarmName = "XYSNET05";
    internal const ulong TransactionFixtureId = 620261116;
    internal const string NormalLanFarmName = "XYSNET08";
    internal const ulong NormalLanFixtureId = 620261122;
    internal const string MachineRecoveryFarmName = "XYSNET06";
    internal const ulong MachineRecoveryFixtureId = 620261118;
    internal const string MachinePostDebitFarmName = "XYSNET07";
    internal const ulong MachinePostDebitFixtureId = 620261120;

    private readonly IMonitor Monitor;
    private readonly bool Host;
    private readonly string BoundFarmName;
    private readonly ulong BoundFixtureId;
    private readonly Stopwatch Watch = new();
    private int LoadDelay = -1;
    private FarmhandMenu? JoinMenu;
    private bool JoinSelected;
    private Options? WorldOptions;
    private bool OriginalPauseSetting;

    internal bool Ready { get; private set; }
    internal string? Failure { get; private set; }

    internal NativeNetworkBootstrap(IMonitor monitor, Config config)
    {
        bool ownership = config.Scenario == "NativeInventoryOwnership" && config.FarmName == FarmName && config.SaveFixtureId == FixtureId;
        bool recovery = config.Scenario == "NativeInventoryRecovery" && IsRecoveryFixture(config);
        bool observation = config.Scenario == "LanTransportObservation" && config.FarmName == FarmName && config.SaveFixtureId == FixtureId;
        bool transaction = IsTransactionFixture(config);
        bool machineRecovery = config.Scenario == "LanMachineRecovery"
            && ((config.FarmName == MachineRecoveryFarmName && config.SaveFixtureId == MachineRecoveryFixtureId)
                || (config.FarmName == MachinePostDebitFarmName && config.SaveFixtureId == MachinePostDebitFixtureId));
        bool supported = observation || transaction || machineRecovery || (config.EnableNativeInventoryOwnership && (ownership || recovery));
        if (!config.Enabled || config.OpenPreview || !supported
            || config.NetworkRole is not ("Host" or "Client"))
            throw new InvalidOperationException("Native network bootstrap requires a dedicated fixture and Host or Client role.");

        this.Monitor = monitor;
        this.Host = config.NetworkRole == "Host";
        this.BoundFarmName = config.FarmName;
        this.BoundFixtureId = config.SaveFixtureId;
    }

    internal static bool IsRecoveryFixture(Config config) =>
        (config.FarmName == RecoveryFarmName && config.SaveFixtureId == RecoveryFixtureId)
        || (config.FarmName == BedtimeFarmName && config.SaveFixtureId == BedtimeFixtureId);

    internal static bool IsTransactionFixture(Config config) => config.Scenario == "LanMachineTransaction"
        && ((config.FarmName == TransactionFarmName && config.SaveFixtureId == TransactionFixtureId)
            || (config.FarmName == NormalLanFarmName && config.SaveFixtureId == NormalLanFixtureId));

    // 和既有夹具一致，等待标题资源初始化后再调用原版加载入口。
    internal void QueueStart()
    {
        this.LoadDelay = 180;
        this.Watch.Start();
    }

    internal void QueueClientReconnect()
    {
        if (this.Host || !this.Ready || Context.IsWorldReady || Game1.client is not null || Game1.server is not null)
            throw new InvalidOperationException("Reconnect requires the same initialized farmhand process at title.");
        this.Ready = false;
        this.JoinSelected = false;
        this.JoinMenu = null;
        this.LoadDelay = 180;
        this.Watch.Restart();
    }

    internal void Tick()
    {
        if (!this.Watch.IsRunning || this.Ready || this.Failure is not null) return;

        try
        {
            if (this.Watch.Elapsed > TimeSpan.FromSeconds(45))
                throw new InvalidOperationException("Timed out starting the native loopback world.");

            if (this.LoadDelay > 0 && --this.LoadDelay == 0)
            {
                this.Open();
                return;
            }

            if (!Context.IsWorldReady)
            {
                this.TrySelectFarmhand();
                return;
            }

            if (Game1.player.farmName.Value != this.BoundFarmName || Game1.uniqueIDForThisGame != this.BoundFixtureId
                || Context.IsMainPlayer != this.Host || Context.IsSplitScreen || Context.ScreenId != 0
                || Game1.player.UniqueMultiplayerID != (this.Host ? HostId : ClientId))
                throw new InvalidOperationException("Loaded the wrong world, screen, or native player role.");

            if (!Context.IsMultiplayer || (this.Host ? Game1.server is null : Game1.client is null)) return;

            this.WorldOptions = Game1.options;
            this.OriginalPauseSetting = this.WorldOptions.pauseWhenOutOfFocus;
            this.WorldOptions.pauseWhenOutOfFocus = false;
            this.Ready = true;
            this.Monitor.Log($"Native network bootstrap ready ({(this.Host ? "Host" : "Client")}).", LogLevel.Info);
        }
        catch (Exception error)
        {
            this.Failure = error.Message;
            this.Monitor.Log(error.ToString(), LogLevel.Error);
        }
    }

    // 两个独立进程同时更新时不能依赖桌面焦点。结束时还原本进程已绑定世界的内存选项。
    internal void RestoreOptions()
    {
        if (this.WorldOptions is null) return;
        this.WorldOptions.pauseWhenOutOfFocus = this.OriginalPauseSetting;
        this.WorldOptions = null;
    }

    private void Open()
    {
        if (Context.IsWorldReady || Game1.client is not null || Game1.server is not null)
            throw new InvalidOperationException("Refuse to replace an active game or connection.");

        if (this.Host)
        {
            string saveName = $"{this.BoundFarmName}_{this.BoundFixtureId}";
            if (!File.Exists(Path.Combine(Constants.SavesPath, saveName, saveName))
                || !File.Exists(Path.Combine(Constants.SavesPath, saveName, "SaveGameInfo")))
                throw new InvalidOperationException("The dedicated cooperative fixture is missing.");

            // 对应原版 HostFileSlot.Activate。加载器从这个存档启用原版服务器。
            Game1.multiplayerMode = Game1.multiplayerServer;
            Game1.activeClickableMenu = null;
            SaveGame.Load(saveName);
            return;
        }

        // 原版农工菜单负责批准与初始世界接收。直接连接免去等待标题菜单绘制，且只连接环回地址。
        // 原版多人实例由 SMAPI 替换。使用该实例保留实际客户端的 SMAPI 网络接线。
        var field = typeof(Game1).GetField("multiplayer", BindingFlags.Static | BindingFlags.NonPublic);
        if (field?.GetValue(null) is not Multiplayer multiplayer)
            throw new InvalidOperationException("The game's multiplayer instance is unavailable.");
        Client client = multiplayer.InitClient(new LidgrenClient("127.0.0.1"));
        this.JoinMenu = new FarmhandMenu(client);
        Game1.activeClickableMenu = this.JoinMenu;
        client.connect();
    }

    private void TrySelectFarmhand()
    {
        if (this.JoinMenu is null || this.JoinSelected || !ReferenceEquals(Game1.activeClickableMenu, this.JoinMenu)) return;

        var existing = this.JoinMenu.MenuSlots.OfType<FarmhandMenu.FarmhandSlot>().SingleOrDefault(slot =>
            slot.Farmer.Name == ClientName && slot.Farmer.UniqueMultiplayerID == ClientId);
        if (existing is null) return;

        this.JoinSelected = true;
        existing.Activate();
    }
}
