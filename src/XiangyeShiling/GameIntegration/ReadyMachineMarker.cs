using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using XiangyeShiling.Machines;
using SObject = StardewValley.Object;

namespace XiangyeShiling.GameIntegration;

/// <summary>在原版地图绘制过程中提示可领取的批次，不投递物品或维护原版机器字段。</summary>
internal static class ReadyMachineMarker
{
    private static readonly ConditionalWeakTable<SObject, BatchView> Views = new();
    // 原版设备的成品气泡。保持比一格窄，避免并排设备的标记互相覆盖。
    private static readonly Rectangle Bubble = new(141, 465, 20, 24);

    public static void Draw(SObject __instance, SpriteBatch spriteBatch, int x, int y, float alpha)
    {
        if (!MachineService.TryGetKind(__instance, out _) || !Context.IsWorldReady || !Context.IsMainPlayer || Context.IsMultiplayer || Game1.eventUp
            || alpha <= 0 || Game1.currentLocation?.objects.TryGetValue(new Vector2(x, y), out var placed) != true
            || !ReferenceEquals(placed, __instance))
            return;

        if (!__instance.modData.TryGetValue(MachineService.StateKey, out string raw)) return;
        Batch? batch = Views.GetValue(__instance, static _ => new BatchView()).Read(__instance, raw);
        if (batch is null || batch.ReadyAt > MachineService.Now()
            || ItemRegistry.GetData(batch.Output.ItemId) is not { } output)
            return;

        Rectangle source = output.GetSourceRect();
        if (source.Width <= 0 || source.Height <= 0) return;
        // 标记贴着设备自己的格子右上角。若悬在设备顶部，前后紧邻时会看起来属于后排设备。
        Vector2 center = Game1.GlobalToLocal(Game1.viewport, new Vector2(x * 64 + 47, y * 64 + 6));
        float layer = Math.Clamp(((y + 1) * 64) / 10000f + x / 50000f, 0f, .999f);
        spriteBatch.Draw(Game1.mouseCursors, center, Bubble, Color.White * alpha,
            0f, new Vector2(10, 12), 2f, SpriteEffects.None, layer + .00001f);
        spriteBatch.Draw(output.GetTexture(), center - new Vector2(0, 5), source, Color.White * alpha,
            0f, new Vector2(source.Width, source.Height) / 2, 24f / Math.Max(source.Width, source.Height),
            SpriteEffects.None, layer + .00002f);
    }

    private sealed class BatchView
    {
        private string? Raw;
        private Batch? Batch;

        // 序列化值没变时复用解析结果；每帧仍读取实时钟。领取或损坏会改变原值并使缓存失效。
        // 弱键不延长地图对象的生命周期，也不保留旧档或卸载地图的设备。
        public Batch? Read(SObject machine, string raw)
        {
            if (this.Raw == raw) return this.Batch;
            this.Batch = MachineService.TryRead(machine, out var state) ? state!.Batch : null;
            this.Raw = raw;
            return this.Batch;
        }
    }
}
