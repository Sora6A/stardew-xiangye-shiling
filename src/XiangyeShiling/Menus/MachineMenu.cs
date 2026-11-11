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
    private const int PanelWidth = 720;
    private const int PanelHeight = 460;
    private const int ViewportMargin = 16;
    private const int GuideId = 1;
    private const int CloseId = 2;
    private const int PreviousId = 3;
    private const int NextId = 4;
    private const int FirstRecipeId = 10;
    private readonly MachineService Service;
    private readonly SObject Machine;
    private readonly ITranslationHelper Text;
    private readonly IReadOnlyList<MachineRecipe> Recipes;
    private string Feedback = "";
    private Color FeedbackColor = Color.DarkRed;
    private bool GuideOpen;
    private int GuidePage;
    private readonly MenuActionSession Session = new();
    private Point LayoutViewport;
    private float LayoutScale = 1f;
    private int LastClickTick = -1;
    public MachineMenu(MachineService service, SObject machine, ITranslationHelper text)
        : base(0, 0, PanelWidth, PanelHeight, true)
    {
        this.Service = service;
        this.Machine = machine;
        this.Text = text;
        MachineService.TryGetKind(machine, out var kind);
        this.Recipes = service.Recipes.Where(r => r.Machine == kind).ToArray();
        this.FitViewport();
        if (Game1.options.snappyMenus && Game1.options.gamepadControls)
            this.snapToDefaultClickableComponent();
    }

    // 视口使用游戏 UI 单位。重排只更新几何，保留同一菜单的指南页和事务请求。
    private void FitViewport()
    {
        Point viewport = new(Game1.uiViewport.Width, Game1.uiViewport.Height);
        if (viewport == this.LayoutViewport || viewport.X <= 0 || viewport.Y <= 0) return;
        this.LayoutViewport = viewport;
        // 原版关闭按钮伸出面板顶部和右侧，边距为这部分留出空间。
        this.LayoutScale = Math.Min(1f, Math.Min(viewport.X / (PanelWidth + 2f * ViewportMargin),
            viewport.Y / (PanelHeight + 2f * ViewportMargin)));
        this.width = this.ScaleOffset(PanelWidth);
        this.height = this.ScaleOffset(PanelHeight);
        this.xPositionOnScreen = (viewport.X - this.width) / 2;
        this.yPositionOnScreen = (viewport.Y - this.height) / 2;
        if (this.upperRightCloseButton is { } close)
        {
            close.bounds = this.BoundsAt(PanelWidth - 36, -8, 48, 48);
            close.baseScale = close.scale = 4f * this.LayoutScale;
        }
        this.populateClickableComponentList();
        if (Game1.options.gamepadControls)
            this.snapCursorToCurrentSnappedComponent();
    }

    public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds) => this.FitViewport();

    private int ScaleOffset(int logicalOffset) => (int)MathF.Round(logicalOffset * this.LayoutScale);
    private Rectangle BoundsAt(int x, int y, int width, int height)
    {
        int left = this.ScaleOffset(x);
        int top = this.ScaleOffset(y);
        return new(this.xPositionOnScreen + left, this.yPositionOnScreen + top,
            this.ScaleOffset(x + width) - left, this.ScaleOffset(y + height) - top);
    }
    private Vector2 PointAt(float x, float y) => new(this.xPositionOnScreen + x * this.LayoutScale,
        this.yPositionOnScreen + y * this.LayoutScale);
    private Rectangle ActionBounds(int index) => this.BoundsAt(440, 150 + index * 120, 210, 62);
    private Rectangle GuideBounds => this.BoundsAt(PanelWidth - 224, 40, 180, 46);
    private Rectangle PreviousBounds => this.BoundsAt(44, PanelHeight - 74, 150, 46);
    private Rectangle NextBounds => this.BoundsAt(PanelWidth - 194, PanelHeight - 74, 150, 46);

    public override void populateClickableComponentList()
    {
        int? selected = this.currentlySnappedComponent?.myID;
        int first = this.GuideOpen ? (this.GuidePage == 0 ? NextId : PreviousId)
            : this.Recipes.Count > 0 ? FirstRecipeId : -1;
        var guide = new ClickableComponent(this.GuideBounds, "Guide")
        {
            myID = GuideId, rightNeighborID = CloseId, downNeighborID = first
        };
        this.allClickableComponents = new() { guide };
        if (this.upperRightCloseButton is { } close)
        {
            close.myID = CloseId;
            close.leftNeighborID = GuideId;
            close.downNeighborID = first;
            this.allClickableComponents.Add(close);
        }

        // 指南只暴露本页控件。键盘与手柄都走点击入口，由页码和事务会话决定能否执行。
        if (this.GuideOpen)
        {
            this.allClickableComponents.Add(new(this.PreviousBounds, "Previous")
            { myID = PreviousId, upNeighborID = GuideId, rightNeighborID = NextId });
            this.allClickableComponents.Add(new(this.NextBounds, "Next")
            { myID = NextId, upNeighborID = GuideId, leftNeighborID = PreviousId });
        }
        else
        {
            for (int i = 0; i < this.Recipes.Count; i++)
                this.allClickableComponents.Add(new(this.ActionBounds(i), this.Recipes[i].Id)
                {
                    myID = FirstRecipeId + i,
                    upNeighborID = i == 0 ? GuideId : FirstRecipeId + i - 1,
                    downNeighborID = i + 1 < this.Recipes.Count ? FirstRecipeId + i + 1 : -1
                });
        }

        this.currentlySnappedComponent = selected.HasValue ? this.getComponentWithID(selected.Value) ?? guide : null;
    }

    public override void snapToDefaultClickableComponent()
    {
        if (this.allClickableComponents is null)
            this.populateClickableComponentList();
        this.currentlySnappedComponent = this.getComponentWithID(GuideId);
        this.snapCursorToCurrentSnappedComponent();
    }

    public override void applyMovementKey(int direction)
    {
        this.FitViewport();
        base.applyMovementKey(direction);
    }

    private void SetGuideOpen(bool open)
    {
        this.GuideOpen = open;
        if (open) this.GuidePage = 0;
        this.populateClickableComponentList();
        if (Game1.options.gamepadControls)
            this.snapCursorToCurrentSnappedComponent();
    }

    private void TurnGuidePage(int page)
    {
        this.GuidePage = page;
        this.populateClickableComponentList();
    }

    private IReadOnlyList<(Ingredient Ingredient, int Missing)> Missing(MachineRecipe recipe) => recipe.Inputs
        .Select(input => (Ingredient: input, Missing: Math.Max(0, input.Quantity - Game1.player.Items.CountId(input.ItemId))))
        .Where(value => value.Missing > 0).ToArray();

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        this.FitViewport();
        this.LastClickTick = Game1.ticks;
        if (this.upperRightCloseButton?.containsPoint(x, y) == true) { this.exitThisMenu(); return; }
        // 先处理只读导航，不能让指南上的点击落到背后的加工/领取按钮；成功锁定也不能阻止查看说明。
        if (this.GuideBounds.Contains(x, y)) { this.SetGuideOpen(!this.GuideOpen); return; }
        if (this.GuideOpen)
        {
            if (this.PreviousBounds.Contains(x, y) && this.GuidePage > 0) this.TurnGuidePage(0);
            if (this.NextBounds.Contains(x, y) && this.GuidePage < 1) this.TurnGuidePage(1);
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
            this.Feedback = result.Message;
            if (result.Applied)
                this.Feedback += " " + this.Text.Get(phase == MachinePhase.Idle ? "machine.close-to-process" : "machine.reopen");
            this.FeedbackColor = result.Applied ? Color.DarkGreen : Color.DarkRed;
            this.Session.Complete(result.Applied);
            if (result.Applied) Game1.playSound("coin");
            return;
        }
    }
    public override void receiveKeyPress(Keys key)
    {
        if (key == Keys.None) return;
        this.FitViewport();
        // 原版先把手柄 A 当作光标处点击，再发送绑定的键。如果绑定 Enter 或 Space，同帧只确认一次。
        if (Game1.options.gamepadControls && this.LastClickTick == Game1.ticks
            && key == Utility.mapGamePadButtonToKey(Buttons.A)) return;
        if (key == Keys.Escape || Game1.options.doesInputListContain(Game1.options.menuButton, key))
        {
            if (this.GuideOpen) this.SetGuideOpen(false);
            else base.receiveKeyPress(key);
            return;
        }
        if (key is Keys.Enter or Keys.Space)
        {
            if (this.currentlySnappedComponent is null)
                this.snapToDefaultClickableComponent();
            if (this.currentlySnappedComponent is { } target)
                this.receiveLeftClick(target.bounds.Center.X, target.bounds.Center.Y);
            return;
        }
        int direction = key switch { Keys.Up => 0, Keys.Right => 1, Keys.Down => 2, Keys.Left => 3, _ => -1 };
        if (direction >= 0) this.applyMovementKey(direction);
        else this.applyMovementKey(key);
    }

    private void Button(SpriteBatch b, Rectangle bounds, string label, bool enabled = true)
    {
        Color color = enabled ? Color.White : Color.Gray;
        if (this.currentlySnappedComponent?.bounds == bounds)
            color = Color.Lerp(color, Color.Goldenrod, 0.3f);
        this.DrawBox(b, bounds, color);
        Vector2 size = Game1.smallFont.MeasureString(label);
        float scale = Math.Min(this.LayoutScale, (bounds.Width - 24 * this.LayoutScale) / Math.Max(1f, size.X));
        Utility.drawTextWithShadow(b, label, Game1.smallFont,
            new Vector2(bounds.Center.X - size.X * scale / 2, bounds.Center.Y - size.Y * scale / 2), Game1.textColor, scale: scale);
    }

    private void DrawBox(SpriteBatch b, Rectangle bounds, Color color) => drawTextureBox(b, Game1.menuTexture,
        new Rectangle(0, 256, 60, 60), bounds.X, bounds.Y, bounds.Width, bounds.Height, color, this.LayoutScale);

    private void DrawText(SpriteBatch b, string text, SpriteFont font, float x, float y, Color color, float scale = 1f) =>
        Utility.drawTextWithShadow(b, text, font, this.PointAt(x, y), color, scale: scale * this.LayoutScale);

    private string CompletionLabel(long readyAt, int currentDay)
    {
        BatchCompletion completion = BatchCompletion.FromDeadline(readyAt, currentDay);
        string key = completion.IsTonight ? "machine.finish-tonight" : completion.DaysAhead switch
        {
            0 => "machine.finish-today",
            1 => "machine.finish-tomorrow",
            _ => "machine.finish-later"
        };
        return this.Text.Get(key, new { time = Game1.getTimeOfDayString(completion.Clock), days = completion.DaysAhead });
    }

    private void DrawGuide(SpriteBatch b)
    {
        this.DrawText(b, this.Text.Get("guide.title"), Game1.dialogueFont, 44, 36, Game1.textColor);
        this.Button(b, this.GuideBounds, this.Text.Get("guide.back"));
        this.DrawText(b, this.Text.Get(this.GuidePage == 0 ? "guide.sources-title" : "guide.routes-title"),
            Game1.smallFont, 44, 100, Game1.textColor);
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
        string[] lines = blocks.Select(text => Game1.parseText(text, Game1.smallFont, PanelWidth - 88)).ToArray();
        float totalHeight = lines.Sum(text => Game1.smallFont.MeasureString(text).Y) + 18 * (lines.Length - 1);
        float scale = Math.Min(1f, (PanelHeight - 214) / Math.Max(1f, totalHeight));
        float y = 124;
        foreach (string line in lines)
        {
            this.DrawText(b, line, Game1.smallFont, 44, y, Game1.textColor, scale);
            y += (Game1.smallFont.MeasureString(line).Y + 18) * scale;
        }
        this.Button(b, this.PreviousBounds, this.Text.Get("guide.previous"), this.GuidePage > 0);
        this.Button(b, this.NextBounds, this.Text.Get("guide.next"), this.GuidePage < 1);
        string page = this.Text.Get("guide.page", new { current = this.GuidePage + 1, total = 2 });
        this.DrawText(b, page, Game1.smallFont, (PanelWidth - Game1.smallFont.MeasureString(page).X) / 2f,
            PanelHeight - 62, Game1.textColor);
    }
    public override void draw(SpriteBatch b)
    {
        // 游戏内 UI 缩放也会改变逻辑视口，绘制与输入入口都读取最新尺寸。
        this.FitViewport();
        b.Draw(Game1.fadeToBlackRect, new Rectangle(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height), Color.Black * .6f);
        this.DrawBox(b, this.BoundsAt(0, 0, PanelWidth, PanelHeight), Color.White);
        if (this.GuideOpen) { this.DrawGuide(b); base.draw(b); this.drawMouse(b); return; }
        this.DrawText(b, this.Machine.DisplayName, Game1.dialogueFont, 44, 36, Game1.textColor);
        this.Button(b, this.GuideBounds, this.Text.Get("guide.open"));
        if (!MachineService.TryRead(this.Machine, out var state))
            this.DrawText(b, Game1.parseText(this.Text.Get("machine.invalid-state"), Game1.smallFont, 600), Game1.smallFont, 44, 121, Color.DarkRed);
        else
        {
            long now = MachineService.Now();
            int today = Game1.Date.TotalDays;
            MachinePhase phase = state!.PhaseAt(now);
            string status = this.Text.Get("phase." + phase);
            if (phase == MachinePhase.Processing) status += " · " + this.Text.Get("machine.minutes", new { minutes = state.Batch!.ReadyAt - now });
            this.DrawText(b, status, Game1.smallFont, 44, 100, Game1.textColor);
            for (int i = 0; i < this.Recipes.Count; i++)
            {
                MachineRecipe recipe = this.Recipes[i];
                bool missing = phase == MachinePhase.Idle && this.Missing(recipe).Count > 0;
                Product product = state.Batch?.Output ?? recipe.Output;
                string name = ItemRegistry.GetDataOrErrorItem(product.ItemId).DisplayName;
                int rowY = 148 + i * 120;
                this.DrawText(b, $"{name} × {product.Quantity}", Game1.smallFont, 44, rowY, Game1.textColor);
                string inputs = string.Join("  ", recipe.Inputs.Select(input =>
                    $"{ItemRegistry.GetDataOrErrorItem(input.ItemId).DisplayName} {Game1.player.Items.CountId(input.ItemId)}/{input.Quantity}"));
                this.DrawText(b, inputs, Game1.smallFont, 44, rowY + 32, missing ? Color.DarkRed : Game1.textColor);
                Rectangle bounds = this.ActionBounds(i);
                string timing = phase switch
                {
                    MachinePhase.Idle => this.Text.Get("machine.unlocked-time", new { minutes = recipe.Minutes }) + " · "
                        + this.CompletionLabel(now + recipe.Minutes, today),
                    MachinePhase.Processing => this.CompletionLabel(state.Batch!.ReadyAt, today),
                    _ => ""
                };
                if (timing.Length > 0)
                {
                    float scale = Math.Min(1f, 384 / Math.Max(1f, Game1.smallFont.MeasureString(timing).X));
                    this.DrawText(b, timing, Game1.smallFont, 44, rowY + 64, Game1.textColor, scale);
                }
                this.Button(b, bounds, this.Text.Get(this.Session.IsComplete ? "machine.done" : phase == MachinePhase.Ready ? "machine.collect"
                    : phase == MachinePhase.Processing ? "phase.Processing" : missing ? "machine.materials-missing" : "machine.start"),
                    !this.Session.IsComplete && phase != MachinePhase.Processing && !missing);
            }
        }
        if (this.Feedback.Length > 0) this.DrawText(b, Game1.parseText(this.Feedback, Game1.smallFont, 630), Game1.smallFont,
            44, 356, this.FeedbackColor);
        base.draw(b);
        this.drawMouse(b);
    }
}
