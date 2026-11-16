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
/// 两台设备共用菜单：缺料预览、配方分页与获取/制作指南只读本地资料，不获取事务请求。
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
    private MachineRecipePage RecipePage;
    private MachineState? DisplayedState;
    private string Feedback = "";
    private Color FeedbackColor = Color.DarkRed;
    private bool GuideOpen;
    private int GuidePage;
    private readonly MenuActionSession Session = new();
    private readonly bool Farmhand = Context.IsMultiplayer && !Context.IsMainPlayer;
    private Request? FarmhandRequest;
    private string? FarmhandRecipe;
    private bool FarmhandFenced;
    private double RetryAt;
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
        this.RecipePage = new(this.Recipes.Count);
        this.DisplayedState = MachineService.TryRead(this.Machine, out var state) ? state : null;
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
        this.RefreshComponents();
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
    private int VisibleActionCount => this.DisplayedState is null ? 0
        : this.DisplayedState.Batch is not null ? 1 : this.RecipePage.VisibleCount;
    private bool HasMultipleRecipePages => this.DisplayedState is { Batch: null } && this.RecipePage.Count > 1;
    private bool ShowPageControls => this.GuideOpen || this.HasMultipleRecipePages;
    private int GuidePageCount => 1 + new MachineRecipePage(this.Service.Recipes.Count).Count;
    private int PageIndex => this.GuideOpen ? this.GuidePage : this.RecipePage.Index;
    private int PageCount => this.GuideOpen ? this.GuidePageCount : this.RecipePage.Count;

    private MachineState? ReadState()
    {
        var latest = MachineService.TryRead(this.Machine, out var state) ? state : null;
        bool layoutChanged = (this.DisplayedState is null) != (latest is null)
            || (this.DisplayedState?.Batch is null) != (latest?.Batch is null);
        this.DisplayedState = latest;
        if (layoutChanged && !this.GuideOpen) this.RefreshComponents();
        return latest;
    }

    private void RefreshComponents()
    {
        this.populateClickableComponentList();
        if (Game1.options.gamepadControls)
            this.snapCursorToCurrentSnappedComponent();
    }

    public override void populateClickableComponentList()
    {
        int? selected = this.currentlySnappedComponent?.myID;
        int pageButton = this.PageIndex + 1 < this.PageCount ? NextId : PreviousId;
        int actionCount = this.VisibleActionCount;
        int first = this.GuideOpen ? pageButton : actionCount > 0 ? FirstRecipeId : -1;
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

        // 只暴露当前页的控件，键盘、手柄与鼠标共用同一个点击入口。
        if (!this.GuideOpen)
        {
            for (int i = 0; i < actionCount; i++)
                this.allClickableComponents.Add(new(this.ActionBounds(i), this.GetRecipe(i)?.Id ?? "Collect")
                {
                    myID = FirstRecipeId + i,
                    upNeighborID = i == 0 ? GuideId : FirstRecipeId + i - 1,
                    downNeighborID = i + 1 < actionCount ? FirstRecipeId + i + 1 : this.ShowPageControls ? pageButton : -1
                });
        }
        if (this.ShowPageControls)
        {
            int above = this.GuideOpen ? GuideId : FirstRecipeId + actionCount - 1;
            this.allClickableComponents.Add(new(this.PreviousBounds, "Previous")
            { myID = PreviousId, upNeighborID = above, rightNeighborID = NextId });
            this.allClickableComponents.Add(new(this.NextBounds, "Next")
            { myID = NextId, upNeighborID = above, leftNeighborID = PreviousId });
        }

        this.currentlySnappedComponent = selected.HasValue ? this.getComponentWithID(selected.Value)
            ?? (!this.GuideOpen && selected.Value >= FirstRecipeId && actionCount > 0
                ? this.getComponentWithID(FirstRecipeId + actionCount - 1) : guide) : null;
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
        this.RefreshComponents();
    }

    private void TurnPage(int direction)
    {
        if (!this.ShowPageControls) return;
        int previousPage = this.PageIndex;
        if (this.GuideOpen) this.GuidePage = Math.Clamp(this.GuidePage + direction, 0, this.GuidePageCount - 1);
        else this.RecipePage = this.RecipePage.Move(direction);
        if (this.PageIndex != previousPage) this.RefreshComponents();
    }

    private MachineRecipe? GetRecipe(int visibleRow) => this.DisplayedState is { Batch: null }
        ? this.Recipes[this.RecipePage.FirstRecipe + visibleRow] : null;

    private IReadOnlyList<(Ingredient Ingredient, int Missing)> Missing(MachineRecipe recipe) => recipe.Inputs
        .Select(input => (Ingredient: input, Missing: Math.Max(0, input.Quantity - Game1.player.Items.CountId(input.ItemId))))
        .Where(value => value.Missing > 0).ToArray();

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        this.FitViewport();
        var state = this.ReadState();
        this.LastClickTick = Game1.ticks;
        if (this.upperRightCloseButton?.containsPoint(x, y) == true) { this.CloseOrCancel(); return; }
        // 先处理只读导航，不能让指南上的点击落到背后的加工/领取按钮；成功锁定也不能阻止查看说明。
        if (this.GuideBounds.Contains(x, y)) { this.SetGuideOpen(!this.GuideOpen); return; }
        if (this.ShowPageControls)
        {
            if (this.PreviousBounds.Contains(x, y)) { this.TurnPage(-1); return; }
            if (this.NextBounds.Contains(x, y)) { this.TurnPage(1); return; }
        }
        if (this.GuideOpen || this.Session.IsComplete || this.FarmhandRequest is not null || this.FarmhandFenced
            || state is null || state.PhaseAt(MachineService.Now()) == MachinePhase.Processing) return;
        for (int i = 0; i < this.VisibleActionCount; i++)
        {
            if (!this.ActionBounds(i).Contains(x, y)) continue;
            MachineRecipe? recipe = this.GetRecipe(i);
            if (recipe is not null && this.Missing(recipe) is { Count: > 0 } missing)
            {
                string ingredients = string.Join(", ", missing.Select(value =>
                    $"{ItemRegistry.GetDataOrErrorItem(value.Ingredient.ItemId).DisplayName} × {value.Missing}"));
                this.Feedback = this.Text.Get("machine.missing-details", new { ingredients });
                this.FeedbackColor = Color.DarkRed;
                return;
            }
            string? recipeId = recipe?.Id;
            Request? request = this.Session.Acquire(state, Game1.player.UniqueMultiplayerID, recipeId);
            if (request is null)
            {
                this.Feedback = this.Text.Get("machine.reopen");
                this.FeedbackColor = Color.DarkRed;
                return;
            }
            if (!this.Farmhand)
                this.ShowResult(this.Service.Execute(this.Machine, Game1.player, request, recipeId), recipeId is not null);
            else if (this.Service.TryQueueLanMenuOperation(this.Machine, request, recipeId, this.IsCurrentInteraction))
            {
                this.FarmhandRequest = request;
                this.FarmhandRecipe = recipeId;
                this.RetryAt = 0;
                this.ShowNetworkFeedback("machine.network-awaiting");
            }
            else this.ShowNetworkFeedback("machine.network-busy", failed: true);
            return;
        }
    }

    private bool IsCurrentInteraction() => ReferenceEquals(Game1.activeClickableMenu, this);

    private void ShowResult(OperationResult result, bool starting)
    {
        this.Feedback = result.Message;
        if (result.Applied)
            this.Feedback += " " + this.Text.Get(starting ? "machine.close-to-process" : "machine.reopen");
        this.FeedbackColor = result.Applied ? Color.DarkGreen : Color.DarkRed;
        this.Session.Complete(result.Applied);
        if (result.Applied) Game1.playSound("coin");
    }

    private void ShowNetworkFeedback(string key, bool failed = false)
    {
        this.Feedback = this.Text.Get(key);
        this.FeedbackColor = failed ? Color.DarkRed : Game1.textColor;
    }

    private void CloseOrCancel()
    {
        if (this.FarmhandRequest is not { } request) this.exitThisMenu();
        else if (this.Service.TryCancelLanFixtureOperation(request.Id)) this.ShowNetworkFeedback("machine.network-cancelling");
    }

    public override bool readyToClose() => this.FarmhandRequest is null && base.readyToClose();

    public override void receiveScrollWheelAction(int direction)
    {
        this.FitViewport();
        this.ReadState();
        if (direction != 0) this.TurnPage(-Math.Sign(direction));
    }

    public override void receiveGamePadButton(Buttons button)
    {
        if (button is Buttons.LeftShoulder or Buttons.RightShoulder)
        {
            this.FitViewport();
            this.ReadState();
            this.TurnPage(button == Buttons.LeftShoulder ? -1 : 1);
            return;
        }
        if (button == Buttons.B && this.FarmhandRequest is not null)
        {
            if (this.GuideOpen) this.SetGuideOpen(false);
            else this.CloseOrCancel();
            return;
        }
        base.receiveGamePadButton(button);
    }

    public override void update(GameTime time)
    {
        base.update(time);
        if (this.FarmhandRequest is not { } request) return;
        var status = this.Service.ReadLanMenuStatus(request.Id);
        if (status is FarmhandRequestStatus.AwaitingHostReservation or FarmhandRequestStatus.Cancelling)
        {
            double now = time.TotalGameTime.TotalMilliseconds;
            // Only resend the same intent while its local session still owns it. An applied operation never retries here.
            if (now >= this.RetryAt)
            {
                if (this.RetryAt != 0)
                    this.Service.TryQueueLanMenuOperation(this.Machine, request, this.FarmhandRecipe, this.IsCurrentInteraction);
                this.RetryAt = now + 1500;
            }
            this.ShowNetworkFeedback(status == FarmhandRequestStatus.Cancelling ? "machine.network-cancelling" : "machine.network-awaiting");
            return;
        }
        if (status == FarmhandRequestStatus.AwaitingHostResult)
        {
            this.ShowNetworkFeedback("machine.network-finishing");
            return;
        }

        this.FarmhandRequest = null;
        if (status == FarmhandRequestStatus.Applied)
        {
            bool starting = this.FarmhandRecipe is not null;
            this.ShowResult(new(true, this.Text.Get(starting ? "machine.started" : "machine.collected")), starting);
        }
        else if (status is FarmhandRequestStatus.NoGrant or FarmhandRequestStatus.Cancelled)
        {
            this.Session.ReleaseRefusedRequest();
            this.ShowNetworkFeedback(status == FarmhandRequestStatus.NoGrant ? "machine.network-refused" : "machine.network-cancelled",
                failed: status == FarmhandRequestStatus.NoGrant);
        }
        else
        {
            this.FarmhandFenced = true;
            this.ShowNetworkFeedback("machine.network-unconfirmed", failed: true);
        }
        this.FarmhandRecipe = null;
    }

    public override void receiveKeyPress(Keys key)
    {
        if (key == Keys.None) return;
        this.FitViewport();
        this.ReadState();
        // 原版先把手柄 A 当作光标处点击，再发送绑定的键。如果绑定 Enter 或 Space，同帧只确认一次。
        if (Game1.options.gamepadControls && this.LastClickTick == Game1.ticks
            && key == Utility.mapGamePadButtonToKey(Buttons.A)) return;
        if (key == Keys.Escape || Game1.options.doesInputListContain(Game1.options.menuButton, key))
        {
            if (this.GuideOpen) this.SetGuideOpen(false);
            else if (this.FarmhandRequest is not null) this.CloseOrCancel();
            else base.receiveKeyPress(key);
            return;
        }
        if (key is Keys.PageUp or Keys.PageDown)
        {
            this.TurnPage(key == Keys.PageUp ? -1 : 1);
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

    private void DrawRecipeText(SpriteBatch b, string text, float y, Color color)
    {
        float scale = Math.Min(1f, 384 / Math.Max(1f, Game1.smallFont.MeasureString(text).X));
        this.DrawText(b, text, Game1.smallFont, 44, y, color, scale);
    }

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
        IEnumerable<string> blocks;
        if (this.GuidePage == 0)
            blocks = new string[] { this.Text.Get("guide.pierre"), this.Text.Get("guide.robin"), this.Text.Get("guide.gus") };
        else
        {
            var routes = new MachineRecipePage(this.Service.Recipes.Count, this.GuidePage - 1);
            blocks = this.Service.Recipes.Skip(routes.FirstRecipe).Take(routes.VisibleCount).Select(recipe =>
                this.Text.Get("guide.route", new
                {
                    station = ItemRegistry.GetDataOrErrorItem("(BC)" + MachineService.Prefix + "_"
                        + (recipe.Machine == MachineKind.BeanWorkshop ? "BeanWorkshop" : "FermentationCrock")).DisplayName,
                    inputs = string.Join(" + ", recipe.Inputs.Select(input =>
                        $"{ItemRegistry.GetDataOrErrorItem(input.ItemId).DisplayName} × {input.Quantity}")),
                    output = $"{ItemRegistry.GetDataOrErrorItem(recipe.Output.ItemId).DisplayName} × {recipe.Output.Quantity}",
                    minutes = recipe.Minutes
                }).ToString());
            if (this.GuidePage == this.GuidePageCount - 1)
                blocks = blocks.Concat(new[] { this.Text.Get("guide.kitchen").ToString(), this.Text.Get("guide.operation").ToString() });
        }
        string[] lines = blocks.Select(text => Game1.parseText(text, Game1.smallFont, PanelWidth - 88)).ToArray();
        float totalHeight = lines.Sum(text => Game1.smallFont.MeasureString(text).Y) + 18 * (lines.Length - 1);
        float scale = Math.Min(1f, (PanelHeight - 214) / Math.Max(1f, totalHeight));
        float y = 124;
        foreach (string line in lines)
        {
            this.DrawText(b, line, Game1.smallFont, 44, y, Game1.textColor, scale);
            y += (Game1.smallFont.MeasureString(line).Y + 18) * scale;
        }
        this.DrawPageControls(b);
    }

    private void DrawPageControls(SpriteBatch b)
    {
        this.Button(b, this.PreviousBounds, this.Text.Get("guide.previous"), this.PageIndex > 0);
        this.Button(b, this.NextBounds, this.Text.Get("guide.next"), this.PageIndex + 1 < this.PageCount);
        string page = this.Text.Get("guide.page", new { current = this.PageIndex + 1, total = this.PageCount });
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
        if (this.ReadState() is not { } state)
            this.DrawText(b, Game1.parseText(this.Text.Get("machine.invalid-state"), Game1.smallFont, 600), Game1.smallFont, 44, 121, Color.DarkRed);
        else
        {
            long now = MachineService.Now();
            int today = Game1.Date.TotalDays;
            MachinePhase phase = state.PhaseAt(now);
            string status = this.Text.Get("phase." + phase);
            if (phase == MachinePhase.Processing) status += " · " + this.Text.Get("machine.minutes", new { minutes = state.Batch!.ReadyAt - now });
            this.DrawText(b, status, Game1.smallFont, 44, 100, Game1.textColor);
            for (int i = 0; i < this.VisibleActionCount; i++)
            {
                MachineRecipe? recipe = this.GetRecipe(i);
                bool missing = recipe is not null && this.Missing(recipe).Count > 0;
                Product product = recipe is null ? state.Batch!.Output : recipe.Output;
                string name = ItemRegistry.GetDataOrErrorItem(product.ItemId).DisplayName;
                int rowY = 148 + i * 120;
                this.DrawRecipeText(b, $"{name} × {product.Quantity}", rowY, Game1.textColor);
                if (recipe is not null)
                {
                    string inputs = string.Join("  ", recipe.Inputs.Select(input =>
                        $"{ItemRegistry.GetDataOrErrorItem(input.ItemId).DisplayName} {Game1.player.Items.CountId(input.ItemId)}/{input.Quantity}"));
                    this.DrawRecipeText(b, inputs, rowY + 32, missing ? Color.DarkRed : Game1.textColor);
                    string timing = this.Text.Get("machine.unlocked-time", new { minutes = recipe.Minutes }) + " · "
                        + this.CompletionLabel(now + recipe.Minutes, today);
                    this.DrawRecipeText(b, timing, rowY + 64, Game1.textColor);
                }
                else if (phase == MachinePhase.Processing)
                    this.DrawRecipeText(b, this.CompletionLabel(state.Batch!.ReadyAt, today), rowY + 64, Game1.textColor);
                Rectangle bounds = this.ActionBounds(i);
                this.Button(b, bounds, this.Text.Get(this.Session.IsComplete ? "machine.done"
                    : this.FarmhandRequest is not null ? "machine.network-pending" : phase == MachinePhase.Ready ? "machine.collect"
                    : phase == MachinePhase.Processing ? "phase.Processing" : missing ? "machine.materials-missing" : "machine.start"),
                    !this.Session.IsComplete && this.FarmhandRequest is null && !this.FarmhandFenced
                    && phase != MachinePhase.Processing && !missing);
            }
        }
        if (this.Feedback.Length > 0)
        {
            string feedback = Game1.parseText(this.Feedback, Game1.smallFont, 630);
            float scale = this.HasMultipleRecipePages ? Math.Min(1f, 30 / Math.Max(1f, Game1.smallFont.MeasureString(feedback).Y)) : 1f;
            this.DrawText(b, feedback, Game1.smallFont, 44, 356, this.FeedbackColor, scale);
        }
        if (this.HasMultipleRecipePages) this.DrawPageControls(b);
        base.draw(b);
        this.drawMouse(b);
    }
}
