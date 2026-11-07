using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using XiangyeShiling.GameIntegration;
using XiangyeShiling.Machines;
using SObject = StardewValley.Object;

namespace XiangyeShiling.Menus;

/// <summary>
/// 两台设备共用单人菜单：缺料预览与两页获取/制作指南只读本地资料，不获取事务请求。
/// 界面库存数量只是即时提示；实际输入/时间/数量仍由服务在提交前重新读取和核验。
/// </summary>
public sealed class MachineMenu : IClickableMenu
{
    private readonly MachineService Service;
    private readonly SObject Machine;
    private readonly ITranslationHelper Text;
    private readonly IReadOnlyList<MachineRecipe> Recipes;
    private string Feedback = "";
    private Color FeedbackColor = Color.DarkRed;
    private bool GuideOpen;
    private int GuidePage;
    private readonly MenuActionSession Session = new();
    public MachineMenu(MachineService service, SObject machine, ITranslationHelper text)
        : base(Game1.uiViewport.Width / 2 - 360, Game1.uiViewport.Height / 2 - 230, 720, 460, true)
    {
        this.Service = service;
        this.Machine = machine;
        this.Text = text;
        MachineService.TryGetKind(machine, out var kind);
        this.Recipes = service.Recipes.Where(r => r.Machine == kind).ToArray();
    }
    private Rectangle ActionBounds(int index) => new(this.xPositionOnScreen + 440, this.yPositionOnScreen + 150 + index * 120, 210, 62);
    private Rectangle GuideBounds => new(this.xPositionOnScreen + this.width - 224, this.yPositionOnScreen + 40, 180, 46);
    private Rectangle PreviousBounds => new(this.xPositionOnScreen + 44, this.yPositionOnScreen + this.height - 74, 150, 46);
    private Rectangle NextBounds => new(this.xPositionOnScreen + this.width - 194, this.yPositionOnScreen + this.height - 74, 150, 46);
    private IReadOnlyList<(Ingredient Ingredient, int Missing)> Missing(MachineRecipe recipe) => recipe.Inputs
        .Select(input => (Ingredient: input, Missing: Math.Max(0, input.Quantity - Game1.player.Items.CountId(input.ItemId))))
        .Where(value => value.Missing > 0).ToArray();

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        if (this.upperRightCloseButton?.containsPoint(x, y) == true) { this.exitThisMenu(); return; }
        // 先处理只读导航，不能让指南上的点击落到背后的加工/领取按钮；成功锁定也不能阻止查看说明。
        if (this.GuideBounds.Contains(x, y)) { this.GuideOpen = !this.GuideOpen; this.GuidePage = 0; return; }
        if (this.GuideOpen)
        {
            if (this.PreviousBounds.Contains(x, y) && this.GuidePage > 0) this.GuidePage--;
            if (this.NextBounds.Contains(x, y) && this.GuidePage < 1) this.GuidePage++;
            return;
        }
        if (this.Session.IsComplete) return;
        if (!MachineService.TryRead(this.Machine, out var state)) return;
        MachinePhase phase = state!.PhaseAt(MachineService.Now());
        for (int i = 0; i < this.Recipes.Count; i++)
        {
            if (!this.ActionBounds(i).Contains(x, y) || phase == MachinePhase.Processing) continue;
            var missing = this.Missing(this.Recipes[i]);
            if (phase == MachinePhase.Idle && missing.Count > 0)
            {
                string ingredients = string.Join(", ", missing.Select(value =>
                    $"{ItemRegistry.GetDataOrErrorItem(value.Ingredient.ItemId).DisplayName} × {value.Missing}"));
                this.Feedback = this.Text.Get("machine.missing-details", new { ingredients });
                this.FeedbackColor = Color.DarkRed;
                return;
            }
            Request? request = this.Session.Acquire(state, Game1.player.UniqueMultiplayerID);
            if (request is null) return;
            OperationResult result = this.Service.Execute(this.Machine, Game1.player, request,
                phase == MachinePhase.Idle ? this.Recipes[i].Id : null);
            this.Feedback = result.Message + (result.Applied ? " " + this.Text.Get("machine.reopen") : "");
            this.FeedbackColor = result.Applied ? Color.DarkGreen : Color.DarkRed;
            this.Session.Complete(result.Applied);
            if (result.Applied) Game1.playSound("coin");
            return;
        }
    }
    public override void receiveKeyPress(Keys key)
    {
        if (this.GuideOpen && key == Keys.Escape) { this.GuideOpen = false; return; }
        base.receiveKeyPress(key);
    }

    private void Button(SpriteBatch b, Rectangle bounds, string label, bool enabled = true)
    {
        drawTextureBox(b, bounds.X, bounds.Y, bounds.Width, bounds.Height, enabled ? Color.White : Color.Gray);
        Vector2 size = Game1.smallFont.MeasureString(label);
        float scale = Math.Min(1f, (bounds.Width - 24) / Math.Max(1f, size.X));
        Utility.drawTextWithShadow(b, label, Game1.smallFont,
            new Vector2(bounds.Center.X - size.X * scale / 2, bounds.Center.Y - size.Y * scale / 2), Game1.textColor, scale: scale);
    }

    private void DrawGuide(SpriteBatch b)
    {
        Vector2 origin = new(this.xPositionOnScreen + 44, this.yPositionOnScreen + 36);
        Utility.drawTextWithShadow(b, this.Text.Get("guide.title"), Game1.dialogueFont, origin, Game1.textColor);
        this.Button(b, this.GuideBounds, this.Text.Get("guide.back"));
        Utility.drawTextWithShadow(b, this.Text.Get(this.GuidePage == 0 ? "guide.sources-title" : "guide.routes-title"),
            Game1.smallFont, origin + new Vector2(0, 64), Game1.textColor);
        // 来源说明不复制价格/限量；配方名称、数量与工时直接读当前已加载的登记源生成数据。
        // 当前只显示P1两条路线，不把planned食品描述成可制作。
        string[] blocks = this.GuidePage == 0
            ? new string[] { this.Text.Get("guide.pierre"), this.Text.Get("guide.robin"), this.Text.Get("guide.gus") }
            : this.Service.Recipes.Select(recipe => this.Text.Get("guide.route", new
            {
                station = ItemRegistry.GetDataOrErrorItem("(BC)" + MachineService.Prefix + "_"
                    + (recipe.Machine == MachineKind.BeanWorkshop ? "BeanWorkshop" : "FermentationCrock")).DisplayName,
                inputs = string.Join(" + ", recipe.Inputs.Select(input =>
                    $"{ItemRegistry.GetDataOrErrorItem(input.ItemId).DisplayName} × {input.Quantity}")),
                output = $"{ItemRegistry.GetDataOrErrorItem(recipe.Output.ItemId).DisplayName} × {recipe.Output.Quantity}",
                minutes = recipe.Minutes
            }).ToString()).Concat(new[] { this.Text.Get("guide.kitchen").ToString(), this.Text.Get("guide.operation").ToString() }).ToArray();
        string[] lines = blocks.Select(text => Game1.parseText(text, Game1.smallFont, this.width - 88)).ToArray();
        float totalHeight = lines.Sum(text => Game1.smallFont.MeasureString(text).Y) + 18 * (lines.Length - 1);
        float scale = Math.Min(1f, (this.height - 214) / Math.Max(1f, totalHeight));
        float y = this.yPositionOnScreen + 124;
        foreach (string line in lines)
        {
            Utility.drawTextWithShadow(b, line, Game1.smallFont, new Vector2(origin.X, y), Game1.textColor, scale: scale);
            y += (Game1.smallFont.MeasureString(line).Y + 18) * scale;
        }
        this.Button(b, this.PreviousBounds, this.Text.Get("guide.previous"), this.GuidePage > 0);
        this.Button(b, this.NextBounds, this.Text.Get("guide.next"), this.GuidePage < 1);
        string page = this.Text.Get("guide.page", new { current = this.GuidePage + 1, total = 2 });
        Utility.drawTextWithShadow(b, page, Game1.smallFont,
            new Vector2(this.xPositionOnScreen + this.width / 2f - Game1.smallFont.MeasureString(page).X / 2,
                this.yPositionOnScreen + this.height - 62), Game1.textColor);
    }
    public override void draw(SpriteBatch b)
    {
        b.Draw(Game1.fadeToBlackRect, new Rectangle(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height), Color.Black * .6f);
        drawTextureBox(b, this.xPositionOnScreen, this.yPositionOnScreen, this.width, this.height, Color.White);
        if (this.GuideOpen) { this.DrawGuide(b); base.draw(b); this.drawMouse(b); return; }
        Vector2 origin = new(this.xPositionOnScreen + 44, this.yPositionOnScreen + 36);
        Utility.drawTextWithShadow(b, this.Machine.DisplayName, Game1.dialogueFont, origin, Game1.textColor);
        this.Button(b, this.GuideBounds, this.Text.Get("guide.open"));
        if (!MachineService.TryRead(this.Machine, out var state))
            Utility.drawTextWithShadow(b, Game1.parseText(this.Text.Get("machine.invalid-state"), Game1.smallFont, 600), Game1.smallFont, origin + new Vector2(0, 85), Color.DarkRed);
        else
        {
            MachinePhase phase = state!.PhaseAt(MachineService.Now());
            string status = this.Text.Get("phase." + phase);
            if (phase == MachinePhase.Processing) status += " · " + this.Text.Get("machine.minutes", new { minutes = Math.Max(0, state.Batch!.ReadyAt - MachineService.Now()) });
            Utility.drawTextWithShadow(b, status, Game1.smallFont, origin + new Vector2(0, 64), Game1.textColor);
            for (int i = 0; i < this.Recipes.Count; i++)
            {
                MachineRecipe recipe = this.Recipes[i];
                bool missing = phase == MachinePhase.Idle && this.Missing(recipe).Count > 0;
                Product product = state.Batch?.Output ?? recipe.Output;
                string name = ItemRegistry.GetDataOrErrorItem(product.ItemId).DisplayName;
                Vector2 row = origin + new Vector2(0, 112 + i * 120);
                Utility.drawTextWithShadow(b, $"{name} × {product.Quantity}", Game1.smallFont, row, Game1.textColor);
                string inputs = string.Join("  ", recipe.Inputs.Select(input =>
                    $"{ItemRegistry.GetDataOrErrorItem(input.ItemId).DisplayName} {Game1.player.Items.CountId(input.ItemId)}/{input.Quantity}"));
                Utility.drawTextWithShadow(b, inputs, Game1.smallFont, row + new Vector2(0, 32), missing ? Color.DarkRed : Game1.textColor);
                Utility.drawTextWithShadow(b, this.Text.Get("machine.unlocked-time", new { minutes = recipe.Minutes }), Game1.smallFont, row + new Vector2(0, 64), Game1.textColor);
                Rectangle bounds = this.ActionBounds(i);
                drawTextureBox(b, bounds.X, bounds.Y, bounds.Width, bounds.Height, this.Session.IsComplete || phase == MachinePhase.Processing || missing ? Color.Gray : Color.White);
                Utility.drawTextWithShadow(b, this.Text.Get(this.Session.IsComplete ? "machine.done" : phase == MachinePhase.Ready ? "machine.collect"
                    : phase == MachinePhase.Processing ? "phase.Processing" : missing ? "machine.materials-missing" : "machine.start"), Game1.smallFont,
                    new Vector2(bounds.X + 26, bounds.Y + 15), Game1.textColor);
            }
        }
        if (this.Feedback.Length > 0) Utility.drawTextWithShadow(b, Game1.parseText(this.Feedback, Game1.smallFont, 630), Game1.smallFont,
            origin + new Vector2(0, 320), this.FeedbackColor);
        base.draw(b);
        this.drawMouse(b);
    }
}
