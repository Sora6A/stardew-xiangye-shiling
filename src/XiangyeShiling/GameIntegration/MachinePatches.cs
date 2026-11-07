using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using XiangyeShiling.Menus;
using SObject = StardewValley.Object;

namespace XiangyeShiling.GameIntegration;

/// <summary>仅拦截两个独立设备 ID，保留其他原版机器。工具、爆炸、NPC 清路和位置移除分别保护。</summary>
internal static class MachinePatches
{
    private static MachineService Service = null!;
    private static ITranslationHelper Text = null!;
    public static void Install(string id, MachineService service, ITranslationHelper text)
    {
        Service = service;
        Text = text;
        var harmony = new Harmony(id);
        try
        {
            Patch(typeof(SObject), "checkForAction", nameof(Interact), new[] { typeof(Farmer), typeof(bool) });
            Patch(typeof(SObject), "performObjectDropInAction", nameof(NoVanillaInput));
            Patch(typeof(SObject), "performToolAction", nameof(ProtectObject));
            Patch(typeof(SObject), "onExplosion", nameof(ProtectObject));
            Patch(typeof(GameLocation), "checkDestroyItem", nameof(ProtectNpc));
            Patch(typeof(GameLocation), "removeObject", nameof(ProtectRemove));
            Patch(typeof(GameLocation), "destroyObject", nameof(ProtectDestroy), new[] { typeof(Vector2), typeof(bool), typeof(Farmer) });
            var placement = AccessTools.Method(typeof(SObject), "placementAction");
            harmony.Patch(placement, new HarmonyMethod(typeof(MachinePatches), nameof(BeforePlace)),
                new HarmonyMethod(typeof(MachinePatches), nameof(AfterPlace)));
        }
        catch { harmony.UnpatchAll(id); throw; }
        void Patch(Type type, string method, string prefix, Type[]? parameters = null) => harmony.Patch(
            AccessTools.Method(type, method, parameters) ?? throw new MissingMethodException(type.FullName, method),
            new HarmonyMethod(typeof(MachinePatches), prefix));
    }

    private static bool Interact(SObject __instance, Farmer who, bool justCheckingForActivity, ref bool __result)
    {
        if (!MachineService.TryGetKind(__instance, out _)) return true;
        __result = true;
        if (justCheckingForActivity) return false;
        if (Context.IsMultiplayer || !Context.IsMainPlayer) { Game1.showRedMessage(Text.Get("machine.single-player-only")); return false; }
        MachineService.Initialize(__instance);
        if (who == Game1.player && Game1.activeClickableMenu is null)
            Game1.activeClickableMenu = new MachineMenu(Service, __instance, Text);
        return false;
    }
    private static bool NoVanillaInput(SObject __instance, ref bool __result)
    {
        if (!MachineService.TryGetKind(__instance, out _)) return true;
        __result = false;
        return false;
    }
    private static bool ProtectObject(SObject __instance, ref bool __result)
    {
        if (!MachineService.BlocksRemoval(__instance)) return true;
        __result = false;
        return false;
    }
    // BUG-2026-019：原生清路在空格会传 null；必须让原方法返回自己的 false，不能截断 NPC 更新。
    private static bool ProtectNpc(SObject? o, ref bool __result)
    {
        if (!MachineService.BlocksRemoval(o)) return true;
        __result = false;
        return false;
    }
    private static bool ProtectRemove(GameLocation __instance, Vector2 location, ref SObject? __result)
    {
        if (!__instance.objects.TryGetValue(location, out SObject item) || !MachineService.BlocksRemoval(item)) return true;
        __result = null;
        return false;
    }
    private static bool ProtectDestroy(GameLocation __instance, Vector2 tileLocation) =>
        !__instance.objects.TryGetValue(tileLocation, out SObject item) || !MachineService.BlocksRemoval(item);
    private static bool BeforePlace(SObject __instance, GameLocation location, int x, int y, ref bool __result, out bool __state)
    {
        __state = false;
        if (!MachineService.TryGetKind(__instance, out _)) return true;
        // BUG-2026-009：原生对同 ID 的占用格可返回 true，却没有插入新对象。
        // 必须在适配边界拒绝任何占用格，不能依赖 Utility 的鼠标放置预检保护既有批次。
        if (!Context.IsMultiplayer && Context.IsMainPlayer && !location.objects.ContainsKey(new Vector2(x / 64, y / 64)) &&
            (!__instance.modData.ContainsKey(MachineService.StateKey) || (MachineService.TryRead(__instance, out var state) && state!.CanMove)))
        {
            __state = true;
            return true;
        }
        __result = false;
        return false;
    }
    private static void AfterPlace(SObject __instance, GameLocation location, int x, int y, bool __result, bool __state)
    {
        // __state 仅由本次自定义设备的空格预检设置；返回成功还须确认地图中出现对应的新对象。
        // 拒绝、原版物品或其他补丁留下的不同 ID 均不写状态。重放旧实例请求仍由执行器拒绝。
        if (__state && __result && location.objects.TryGetValue(new Vector2(x / 64, y / 64), out SObject placed)
            && placed.QualifiedItemId == __instance.QualifiedItemId && !ReferenceEquals(placed, __instance))
            MachineService.Initialize(placed, newlyPlaced: true);
    }
}
