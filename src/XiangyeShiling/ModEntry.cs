using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley.GameData.Objects;
using StardewValley;
using XiangyeShiling.GameIntegration;
using XiangyeShiling.Machines;

namespace XiangyeShiling;

/// <summary>注册内容诊断与 P1 单人机器适配；远端背包协议通过实测前拒绝联机加工。</summary>
/// <remarks>
/// 接手入口见 docs/ARCHITECTURE.md 与 docs/HANDOFF.md；原型支持边界以实测记录为准。
/// </remarks>
public sealed class ModEntry : Mod
{
    private const string ContentPackId = "Sora6A.XiangyeShiling.Content";
    private const string SoybeanId = "Sora6A.XiangyeShiling_Soybean";
    private bool HasCheckedContent;
    private MachineService Machines = null!;

    public override object GetApi() => this.Machines;

    public override void Entry(IModHelper helper)
    {
        var recipes = helper.Data.ReadJsonFile<List<MachineRecipe>>("assets/machine-recipes.json")
            ?? throw new InvalidOperationException("Missing generated machine recipes.");
        this.Machines = new MachineService(this.Monitor, helper.Translation, recipes);
        // Read-only handshake/rejection only; this service has no reference to Machines or its transaction API.
        this.Machines.BindNetworkDiagnostics(new ReadOnlyProtocolService(helper, this.Monitor, this.ModManifest,
            helper.ReadConfig<NetworkDiagnosticOptions>()));
        MachinePatches.Install(this.ModManifest.UniqueID, this.Machines, helper.Translation);
        helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
        helper.Events.GameLoop.SaveLoaded += this.OnSaveLoaded;
        helper.Events.GameLoop.DayStarted += this.OnDayStarted;
        helper.Events.GameLoop.ReturnedToTitle += (_, _) => { this.HasCheckedContent = false; this.Machines.ResetSession(); };
        helper.Events.World.ObjectListChanged += (_, e) => { foreach (var pair in e.Added) MachineService.Initialize(pair.Value); };
        helper.Events.Player.Warped += (_, e) => { if (e.IsLocalPlayer) this.InitializeLocation(e.NewLocation); };
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        if (!this.Helper.ModRegistry.IsLoaded(ContentPackId))
            this.Monitor.Log(this.Helper.Translation.Get("startup.missing-content"), LogLevel.Error);
        else
            this.Monitor.Log(this.Helper.Translation.Get("startup.scaffold"), LogLevel.Info);
    }

    private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
    {
        this.CheckContentOnce();
        foreach (GameLocation location in Game1.locations) this.InitializeLocation(location);
        this.InitializeLocation(Game1.currentLocation);
    }

    private void InitializeLocation(GameLocation location)
    {
        foreach (var machine in location.objects.Values) MachineService.Initialize(machine);
    }

    private void OnDayStarted(object? sender, DayStartedEventArgs e)
    {
        // 新游戏与读档均需要覆盖；具体事件可能都触发，由 CheckContentOnce 去重。
        this.CheckContentOnce();
    }

    /// <summary>每次进入存档最多诊断一次；失败也不在每天开始时刷屏，回标题后允许重试。</summary>
    private void CheckContentOnce()
    {
        if (this.HasCheckedContent)
            return;
        this.HasCheckedContent = true;
        try
        {
            var objects = this.Helper.GameContent.Load<Dictionary<string, ObjectData>>("Data/Objects");
            if (!objects.TryGetValue(SoybeanId, out ObjectData? soybean) || soybean.Name != SoybeanId)
                throw new InvalidOperationException($"Missing or conflicting object data: {SoybeanId}.");

            Texture2D texture = this.Helper.GameContent.Load<Texture2D>(soybean.Texture ?? "Maps/springobjects");
            int spriteCount = (texture.Width / 16) * (texture.Height / 16);
            if (soybean.SpriteIndex < 0 || soybean.SpriteIndex >= spriteCount)
                throw new InvalidOperationException($"Soybean sprite index is outside the texture: {soybean.SpriteIndex}.");

            this.Monitor.Log(this.Helper.Translation.Get("startup.content-ok"), LogLevel.Info);
        }
        catch (Exception ex)
        {
            this.Monitor.Log($"{this.Helper.Translation.Get("startup.content-failed")}\n{ex}", LogLevel.Error);
        }
    }
}
