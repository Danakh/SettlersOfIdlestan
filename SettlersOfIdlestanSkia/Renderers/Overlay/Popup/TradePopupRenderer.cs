using SettlersOfIdlestan.Controller;
using SettlersOfIdlestan.Controller.Expand;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestanSkia.Services.Localization;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Renderers.Overlay;
using SettlersOfIdlestanSkia.Services;
using SkiaSharp;
using Svg.Skia;

namespace SettlersOfIdlestanSkia.Renderers.Overlay.Popup;

public sealed class TradePopupRenderer : PopupRendererBase
{
    protected override float PopupWidth    => 700;
    protected override float PopupHeight   => 560;
    protected override float TitleFontSize => 20f;

    private const float Padding      = 18;
    private const float HeaderHeight = 42;
    private const float ColGap       = 16;
    private const float ColWidth     = (700 - 2 * Padding - ColGap) / 2; // 324
    private const float RowHeight    = 44;
    private const float MultBtnW     = 42;
    private const float MultBtnH     = 24;
    private const float MultBtnGap   = 5;
    private const float BtnW         = 110;
    private const float BtnH         = 28;
    private const float IconSize     = 18f;
    private const float GoldCapsuleW = 150;

    private readonly GameControllerService _gameControllerService;
    private readonly LocalizationService   _localization;
    private readonly TooltipRenderer       _tooltipRenderer;
    private readonly Dictionary<Resource, SKSvg?> _resourceIcons = new();

    private readonly Dictionary<SKRect, Resource> _sellBtnRects      = [];
    private readonly Dictionary<SKRect, string>   _disabledSellRects = [];
    private readonly Dictionary<SKRect, Resource> _buyBtnRects       = [];
    private readonly Dictionary<SKRect, string>   _disabledBuyRects  = [];

    private SKRect  _popupRect         = SKRect.Empty;
    private SKRect  _closeButtonRect   = SKRect.Empty;
    private SKRect  _multButton1Rect   = SKRect.Empty;
    private SKRect  _multButton10Rect  = SKRect.Empty;
    private SKRect  _multButton100Rect = SKRect.Empty;
    private SKRect  _viewportRect      = SKRect.Empty;
    private SKRect  _scrollTrackRect   = SKRect.Empty;
    private SKRect  _scrollThumbRect   = SKRect.Empty;

    private int       _packMultiplier = 1;
    private int?      _temporaryMultiplier;
    private int       ActiveMultiplier => _temporaryMultiplier ?? _packMultiplier;
    private SKPoint   _lastPointerPosition;

    // Scroll state
    private float _currentS              = 1f;
    private float _scrollOffsetPx        = 0f;
    private float _totalContentH         = 0f;
    private float _viewportH             = 0f;
    private bool  _isDraggingScrollbar   = false;
    private float _scrollDragStartY      = 0f;
    private float _scrollDragStartOffset = 0f;

    private readonly SKFont _smallFont = new() { Size = 11, Typeface = SkiaFonts.Regular };

    // Trade-specific paints
    private readonly SKPaint _disabledPaint       = new() { Color = new SKColor(70,  70,  76,  220), Style = SKPaintStyle.Fill,   IsAntialias = true };
    private readonly SKPaint _activeButtonPaint   = new() { Color = new SKColor(46,  125, 50),        Style = SKPaintStyle.Fill,   IsAntialias = true };
    private readonly SKPaint _disabledBtnPaint    = new() { Color = new SKColor(90,  90,  96),        Style = SKPaintStyle.Fill,   IsAntialias = true };
    private readonly SKPaint _multActiveFill      = new() { Color = new SKColor(60,  100, 160),       Style = SKPaintStyle.Fill,   IsAntialias = true };
    private readonly SKPaint _multInactiveFill    = new() { Color = new SKColor(38,  38,  50),        Style = SKPaintStyle.Fill,   IsAntialias = true };
    private readonly SKPaint _multTempFill        = new() { Color = new SKColor(140, 80,  0),         Style = SKPaintStyle.Fill,   IsAntialias = true };
    private readonly SKPaint _multActiveBorder    = new() { Color = new SKColor(100, 150, 220),       Style = SKPaintStyle.Stroke, StrokeWidth = 1,    IsAntialias = true };
    private readonly SKPaint _multInactiveBorder  = new() { Color = new SKColor(80,  80,  95),        Style = SKPaintStyle.Stroke, StrokeWidth = 1,    IsAntialias = true };
    private readonly SKPaint _multTempBorder      = new() { Color = new SKColor(220, 140, 30),        Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f, IsAntialias = true };
    private readonly SKPaint _rowBorderPaint      = new() { Color = new SKColor(255, 255, 255, 100),  Style = SKPaintStyle.Stroke, StrokeWidth = 1,    IsAntialias = true };
    private readonly SKPaint _rowFillPaint        = new() { Color = new SKColor(55,  55,  65,  245),  Style = SKPaintStyle.Fill,   IsAntialias = true };
    private readonly SKPaint _scrollTrackPaint    = new() { Color = new SKColor(50,  50,  65,  200),  Style = SKPaintStyle.Fill,   IsAntialias = true };
    private readonly SKPaint _scrollThumbPaint    = new() { Color = new SKColor(130, 130, 165, 210),  Style = SKPaintStyle.Fill,   IsAntialias = true };
    private readonly SKPaint _maxStockPaint       = new() { Color = new SKColor(220, 60,  60),        IsAntialias = true };

    public TradePopupRenderer(
        GameControllerService gameControllerService,
        LocalizationService   localization,
        TooltipRenderer       tooltipRenderer,
        ResourceManager       resourceManager)
    {
        _gameControllerService = gameControllerService;
        _localization          = localization;
        _tooltipRenderer       = tooltipRenderer;
        foreach (Resource resource in Enum.GetValues(typeof(Resource)))
        {
            string name = resource.ToString().ToLower();
            _resourceIcons[resource] = resourceManager.LoadImage($"Resources.icons.resources.{name}.svg");
        }
    }

    public override void Initialize(SKSize canvasSize) => base.Initialize(canvasSize);

    protected override void OnFontsUpdated(float s) => _smallFont.Size = 11 * s;

    public override void Close()
    {
        base.Close();
        _temporaryMultiplier      = null;
        _isDraggingScrollbar      = false;
        _scrollOffsetPx           = 0f;
    }

    public void HandleKeyDown(string key)
    {
        if (key == "Control") _temporaryMultiplier = 10;
        else if (key == "Shift") _temporaryMultiplier = 100;
    }

    public void HandleKeyUp(string key)
    {
        if (key is "Control" or "Shift") _temporaryMultiplier = null;
    }

    public void HandleScroll(float delta)
    {
        if (!IsOpen) return;
        float step = (RowHeight + 4) * _currentS;
        float dir  = delta > 0 ? -1f : 1f;
        float maxScroll = Math.Max(0, _totalContentH - _viewportH);
        _scrollOffsetPx = Math.Clamp(_scrollOffsetPx + dir * step, 0, maxScroll);
    }

    public void HandlePointerReleased(SKPoint position)
    {
        _isDraggingScrollbar = false;
    }

    public void Render(SKCanvas canvas, float scale = 1f)
    {
        if (!IsOpen || Disposed) return;

        // Scale is constrained by width only — height is handled via scrolling
        const float margin = 20f;
        float s = Math.Min(scale, (CanvasSize.Width - margin) / PopupWidth);
        _currentS = s;
        UpdateFonts(s);

        _sellBtnRects.Clear();
        _disabledSellRects.Clear();
        _buyBtnRects.Clear();
        _disabledBuyRects.Clear();

        // Popup height is capped to available screen height; width is fully scaled
        float naturalH   = PopupHeight * s;
        float availableH = CanvasSize.Height - margin;
        float popupH     = Math.Min(naturalH, availableH);
        float popupW     = PopupWidth * s;
        float px         = (CanvasSize.Width  - popupW) / 2;
        float py         = (CanvasSize.Height - popupH) / 2;
        var   popup      = new SKRect(px, py, px + popupW, py + popupH);
        _popupRect = popup;

        float headerH  = (HeaderHeight + Padding) * s;
        float footerH  = (Padding + MultBtnH) * s;
        float viewportH = popupH - headerH - footerH;
        _viewportH = viewportH;

        _totalContentH = ComputeContentHeight(s);
        float maxScroll = Math.Max(0, _totalContentH - viewportH);
        _scrollOffsetPx = Math.Clamp(_scrollOffsetPx, 0, maxScroll);
        bool needsScroll = _totalContentH > viewportH + 1f;

        // Chrome (fixed)
        DrawBackground(canvas, popup, s);
        SkiaTextUtils.DrawText(canvas, _localization.Get("trade_title"), popup.MidX, popup.Top + 28 * s, SKTextAlign.Center, TitleFont!, TextPaint);
        _closeButtonRect = GetCloseRect(popup, s);
        DrawCloseButton(canvas, _closeButtonRect, s);

        // Scrollable content area
        _viewportRect = new SKRect(popup.Left, popup.Top + headerH, popup.Right, popup.Bottom - footerH);
        float contentTop = popup.Top + headerH;
        float leftX      = popup.Left + Padding * s;
        float rightX     = leftX + (ColWidth + ColGap) * s;

        canvas.Save();
        canvas.ClipRect(_viewportRect);
        canvas.Translate(0, -_scrollOffsetPx);
        DrawSellSide(canvas, leftX, contentTop, s);
        DrawBuySide(canvas, rightX, contentTop, s);
        canvas.Restore();

        // Fixed footer
        DrawBottomBar(canvas, popup, s);

        // Scrollbar
        if (needsScroll)
            DrawScrollbar(canvas, popup, headerH, viewportH, maxScroll, s);

        SetTradeTooltip();
    }

    // Returns total height of the scrollable content (max of both columns)
    private float ComputeContentHeight(float s)
    {
        var civ = _gameControllerService.PlayerCivilization;
        if (civ == null) return 0;

        int sellCount = GetSellableResources(civ).Count;
        int buyCount  = GetBuyableResources(civ).Count;

        int maxRows = Math.Max(sellCount, buyCount);
        return (26 + maxRows * (RowHeight + 4)) * s;
    }

    private void DrawScrollbar(SKCanvas canvas, SKRect popup, float headerH, float viewportH, float maxScroll, float s)
    {
        float scrollW  = 5f * s;
        float trackX   = popup.Right - scrollW - 6 * s;
        float trackTop = popup.Top + headerH;
        float trackH   = viewportH;

        _scrollTrackRect = new SKRect(trackX, trackTop, trackX + scrollW, trackTop + trackH);

        float thumbRatio = viewportH / _totalContentH;
        float thumbH     = Math.Max(20f * s, thumbRatio * trackH);
        float thumbTop   = trackTop + (_scrollOffsetPx / maxScroll) * (trackH - thumbH);
        _scrollThumbRect = new SKRect(trackX, thumbTop, trackX + scrollW, thumbTop + thumbH);

        canvas.DrawRoundRect(_scrollTrackRect, 3 * s, 3 * s, _scrollTrackPaint);
        canvas.DrawRoundRect(_scrollThumbRect, 3 * s, 3 * s, _scrollThumbPaint);
    }

    public void HandlePointerMoved(SKPoint position)
    {
        if (!IsOpen) return;
        _lastPointerPosition = position;

        if (_isDraggingScrollbar)
        {
            float dy         = position.Y - _scrollDragStartY;
            float thumbRange = _scrollTrackRect.Height - _scrollThumbRect.Height;
            float maxScroll  = Math.Max(0, _totalContentH - _viewportH);
            float scrollPerPx = thumbRange > 0 ? maxScroll / thumbRange : 0;
            _scrollOffsetPx = Math.Clamp(_scrollDragStartOffset + dy * scrollPerPx, 0, maxScroll);
            return;
        }
    }

    public bool HandlePointerPressed(SKPoint position, PointerButton button)
    {
        if (!IsOpen) return false;

        if (_closeButtonRect.Contains(position.X, position.Y)) { Close(); return true; }

        // Scrollbar
        if (!_scrollThumbRect.IsEmpty && _scrollThumbRect.Contains(position.X, position.Y))
        {
            _isDraggingScrollbar   = true;
            _scrollDragStartY      = position.Y;
            _scrollDragStartOffset = _scrollOffsetPx;
            return true;
        }
        if (!_scrollTrackRect.IsEmpty && _scrollTrackRect.Contains(position.X, position.Y))
        {
            float relY      = position.Y - _scrollTrackRect.Top;
            float maxScroll = Math.Max(0, _totalContentH - _viewportH);
            _scrollOffsetPx = Math.Clamp(relY / _scrollTrackRect.Height * maxScroll, 0, maxScroll);
            return true;
        }

        if (_multButton1Rect.Contains(position.X, position.Y))   { _packMultiplier = 1;   return true; }
        if (_multButton10Rect.Contains(position.X, position.Y))  { _packMultiplier = 10;  return true; }
        if (_multButton100Rect.Contains(position.X, position.Y)) { _packMultiplier = 100; return true; }

        // Content-area interactions require scroll adjustment
        if (_viewportRect.Contains(position.X, position.Y))
        {
            var adj = new SKPoint(position.X, position.Y + _scrollOffsetPx);

            foreach (var (rect, resource) in _sellBtnRects)
            {
                if (rect.Contains(adj.X, adj.Y))
                {
                    var civ = _gameControllerService.PlayerCivilization;
                    if (civ != null)
                    {
                        var tc        = _gameControllerService.MainGameController.TradeController;
                        int sellRate  = tc.GetSellRate(civ.Index, resource);
                        int goldYield = tc.GetSellGoldYield(civ.Index, resource, ActiveMultiplier);
                        if (civ.GetResourceQuantity(resource) >= sellRate * ActiveMultiplier
                            && tc.CanRecieveTrade(civ, Resource.Gold, goldYield))
                            tc.SellResource(civ.Index, resource, ActiveMultiplier);
                    }
                    return true;
                }
            }

            foreach (var (rect, resource) in _buyBtnRects)
            {
                if (rect.Contains(adj.X, adj.Y))
                {
                    var civ = _gameControllerService.PlayerCivilization;
                    if (civ != null)
                    {
                        var tc = _gameControllerService.MainGameController.TradeController;
                        if (tc.CanBuyResource(civ.Index, resource, ActiveMultiplier))
                            tc.BuyResource(civ.Index, resource, ActiveMultiplier);
                    }
                    return true;
                }
            }
        }

        if (!_popupRect.Contains(position.X, position.Y)) { Close(); return false; }
        return true;
    }

    // ── Sell side ─────────────────────────────────────────────────────────────────

    // Ressources de base vendables + Minerai/Verre/Acier si la recherche Comptoirs Avancés (vente) est
    // complétée. Verre et Acier nécessitent en plus d'avoir été découverts dans la carte de prestige — sans
    // quoi ils ne peuvent être ni achetés ni vendus (voir GetBuyableResources).
    // Ordre = ordre de l'enum Resource, pour rester cohérent avec la colonne d'achat.
    private List<Resource> GetSellableResources(Civilization civ)
    {
        var tc = _gameControllerService.MainGameController.TradeController;
        var prestigeState = _gameControllerService.CurrentGameState?.PrestigeState;
        var map = PrestigeMapController.DefaultMap;
        bool glassSteelDiscovered = prestigeState?.IsResourceDiscovered(Resource.Glass, map) ?? false;
        bool steelDiscovered = prestigeState?.IsResourceDiscovered(Resource.Steel, map) ?? false;

        var sellable = ResourceUtils.BasicResources.Where(r => tc.CanTradeResource(civ, r)).ToList();
        if (tc.IsOreGlassTradeUnlocked(civ.Index))
        {
            if (tc.CanTradeResource(civ, Resource.Ore)) sellable.Add(Resource.Ore);
            if (glassSteelDiscovered && tc.CanTradeResource(civ, Resource.Glass)) sellable.Add(Resource.Glass);
        }
        if (steelDiscovered && tc.IsSteelTradeUnlocked(civ.Index) && tc.CanTradeResource(civ, Resource.Steel))
            sellable.Add(Resource.Steel);
        return sellable;
    }

    // Ressources de base achetables + ressources avancées découvertes dans la carte de prestige (Verre,
    // Acier, Cristal, Mithril). Contrairement à la vente, l'achat ne dépend pas de la recherche Comptoirs
    // Avancés : seule la découverte de la ressource (et le stockage débloqué) conditionne son apparition.
    private List<Resource> GetBuyableResources(Civilization civ)
    {
        var tc = _gameControllerService.MainGameController.TradeController;
        var prestigeState = _gameControllerService.CurrentGameState?.PrestigeState;
        var map = PrestigeMapController.DefaultMap;
        return ResourceUtils.BasicResources
            .Concat(Enum.GetValues<Resource>()
                .Where(r => !ResourceUtils.BasicResources.Contains(r) && r != Resource.Gold)
                .Where(r => !ResourceUtils.ConsumableResources.Contains(r))
                .Where(r => !ResourceUtils.AdvancedResources.Contains(r)
                            || (prestigeState?.IsResourceDiscovered(r, map) ?? false)))
            .Where(r => tc.CanTradeResource(civ, r))
            .ToList();
    }

    private void DrawSellSide(SKCanvas canvas, float x, float y, float s)
    {
        var civ = _gameControllerService.PlayerCivilization;
        if (civ == null) return;
        var tc = _gameControllerService.MainGameController.TradeController;

        SkiaTextUtils.DrawText(canvas, _localization.Get("trade_give"), x + ColWidth * s / 2, y + 16 * s, SKTextAlign.Center, BtnFont!, TextPaint);

        float rowY = y + 26 * s;
        foreach (var resource in GetSellableResources(civ))
        {
            int  sellRate  = tc.GetSellRate(civ.Index, resource);
            int  units     = sellRate * ActiveMultiplier;
            int  goldYield = tc.GetSellGoldYield(civ.Index, resource, ActiveMultiplier);
            int  available = civ.GetResourceQuantity(resource);
            int  maxQty    = civ.GetResourceMaxQuantity(resource);
            bool canSell   = available >= units && tc.CanRecieveTrade(civ, Resource.Gold, goldYield);

            var row = new SKRect(x, rowY, x + ColWidth * s, rowY + RowHeight * s);
            DrawRowBackground(canvas, row, canSell, s);

            float iconX = row.Left + 8 * s;
            DrawIcon(canvas, resource, iconX, row.MidY, s);

            string sellName = _localization.Get($"resource_{resource.ToString().ToLower()}");
            SkiaTextUtils.DrawText(canvas, sellName, iconX + (IconSize + 5) * s, row.MidY + 5 * s, BodyFont!, canSell ? TextPaint : SubtlePaint);

            bool isAtMax = available >= maxQty;
            SkiaTextUtils.DrawText(canvas, $"{available}/{maxQty}", row.MidX, row.MidY + 5 * s, SKTextAlign.Center, _smallFont, isAtMax ? _maxStockPaint : SubtlePaint);

            string btnText = string.Format(_localization.Get("trade_sell_button"), units, goldYield);
            var    btn     = DrawActionButton(canvas, row, btnText, canSell, s);
            _sellBtnRects[btn] = resource;
            if (!canSell)
                _disabledSellRects[btn] = available < units ? "trade_tooltip_no_offers" : "trade_tooltip_storage_full";

            rowY += (RowHeight + 4) * s;
        }
    }

    // ── Buy side ──────────────────────────────────────────────────────────────────

    private void DrawBuySide(SKCanvas canvas, float x, float y, float s)
    {
        var civ = _gameControllerService.PlayerCivilization;
        if (civ == null) return;
        var tc = _gameControllerService.MainGameController.TradeController;

        var buyable = GetBuyableResources(civ);

        SkiaTextUtils.DrawText(canvas, _localization.Get("trade_advanced_title"), x + ColWidth * s / 2, y + 16 * s, SKTextAlign.Center, BtnFont!, TextPaint);

        float rowY = y + 26 * s;
        foreach (var resource in buyable)
        {
            int  cost   = tc.BuyRate(resource) * ActiveMultiplier;
            bool canBuy = tc.CanBuyResource(civ.Index, resource, ActiveMultiplier);
            int  qty    = civ.GetResourceQuantity(resource);
            int  maxQty = civ.GetResourceMaxQuantity(resource);

            var row = new SKRect(x, rowY, x + ColWidth * s, rowY + RowHeight * s);
            DrawRowBackground(canvas, row, canBuy, s);

            DrawIcon(canvas, resource, row.Left + 8 * s, row.MidY, s);

            string buyName = _localization.Get($"resource_{resource.ToString().ToLower()}");
            SkiaTextUtils.DrawText(canvas, buyName, row.Left + (8 + IconSize + 5) * s, row.MidY + 5 * s, BodyFont!, canBuy ? TextPaint : SubtlePaint);

            bool isAtMax = qty >= maxQty;
            SkiaTextUtils.DrawText(canvas, $"{qty}/{maxQty}", row.MidX, row.MidY + 5 * s, SKTextAlign.Center, _smallFont, isAtMax ? _maxStockPaint : SubtlePaint);

            string btnText = string.Format(_localization.Get("trade_buy_button"), cost, ActiveMultiplier);
            var    btn     = DrawActionButton(canvas, row, btnText, canBuy, s);
            _buyBtnRects[btn] = resource;
            if (!canBuy)
            {
                bool noGold = civ.GetResourceQuantity(Resource.Gold) < cost;
                _disabledBuyRects[btn] = noGold ? "trade_tooltip_no_gold" : "trade_tooltip_storage_full";
            }

            rowY += (RowHeight + 4) * s;
        }
    }

    // ── Row helpers ───────────────────────────────────────────────────────────────

    private void DrawRowBackground(SKCanvas canvas, SKRect row, bool active, float s)
    {
        canvas.DrawRoundRect(row, 5 * s, 5 * s, active ? _rowFillPaint : _disabledPaint);
        canvas.DrawRoundRect(row, 5 * s, 5 * s, _rowBorderPaint);
    }

    private void DrawIcon(SKCanvas canvas, Resource resource, float iconX, float midY, float s)
    {
        _resourceIcons.TryGetValue(resource, out var svg);
        if (svg?.Picture is not { } picture) return;
        float svgScale = IconSize * s / 32f;
        canvas.Save();
        canvas.Translate(iconX, midY - IconSize * s / 2f);
        canvas.Scale(svgScale);
        canvas.DrawPicture(picture);
        canvas.Restore();
    }

    private SKRect DrawActionButton(SKCanvas canvas, SKRect row, string text, bool active, float s)
    {
        float btnX = row.Right - (6 + BtnW) * s;
        float btnY = row.MidY - BtnH * s / 2f;
        var   btn  = new SKRect(btnX, btnY, btnX + BtnW * s, btnY + BtnH * s);
        canvas.DrawRoundRect(btn, 5 * s, 5 * s, active ? _activeButtonPaint : _disabledBtnPaint);
        SkiaTextUtils.DrawText(canvas, text, btn.MidX, btn.MidY + 5 * s, SKTextAlign.Center, BodyFont!, active ? TextPaint : SubtlePaint);
        return btn;
    }

    // ── Bottom bar ────────────────────────────────────────────────────────────────

    private void DrawBottomBar(SKCanvas canvas, SKRect popup, float s)
    {
        float barY = popup.Bottom - (Padding + MultBtnH) * s;

        var civ = _gameControllerService.PlayerCivilization;
        if (civ != null)
        {
            int goldQty = civ.GetResourceQuantity(Resource.Gold);
            int goldMax = civ.GetResourceMaxQuantity(Resource.Gold);
            var capsule = new SKRect(popup.Left + Padding * s, barY, popup.Left + (Padding + GoldCapsuleW) * s, barY + MultBtnH * s);
            canvas.DrawRoundRect(capsule, 4 * s, 4 * s, _rowFillPaint);
            canvas.DrawRoundRect(capsule, 4 * s, 4 * s, _rowBorderPaint);

            float goldIconSize = 14f * s;
            float iconX = capsule.Left + 6 * s;
            DrawIconSized(canvas, Resource.Gold, iconX, capsule.MidY, goldIconSize);

            string qtyText = $"{goldQty}/{goldMax}";
            bool goldAtMax = goldQty >= goldMax;
            SkiaTextUtils.DrawText(canvas, qtyText, iconX + goldIconSize + 5 * s, capsule.MidY + 4 * s, _smallFont, goldAtMax ? _maxStockPaint : TextPaint);
        }

        float totalW = (MultBtnW * 3 + MultBtnGap * 2) * s;
        float startX = popup.Right - Padding * s - totalW;
        _multButton1Rect   = new SKRect(startX,                                        barY, startX + MultBtnW * s,                    barY + MultBtnH * s);
        _multButton10Rect  = new SKRect(startX + (MultBtnW + MultBtnGap) * s,          barY, startX + (MultBtnW * 2 + MultBtnGap) * s, barY + MultBtnH * s);
        _multButton100Rect = new SKRect(startX + (MultBtnW * 2 + MultBtnGap * 2) * s, barY, startX + totalW,                          barY + MultBtnH * s);

        int  active = ActiveMultiplier;
        bool isTemp = _temporaryMultiplier.HasValue;
        DrawMultButton(canvas, _multButton1Rect,   "×1",   _packMultiplier == 1,   isTemp && active == 1,   s);
        DrawMultButton(canvas, _multButton10Rect,  "×10",  _packMultiplier == 10,  isTemp && active == 10,  s);
        DrawMultButton(canvas, _multButton100Rect, "×100", _packMultiplier == 100, isTemp && active == 100, s);
    }

    private void DrawMultButton(SKCanvas canvas, SKRect rect, string label, bool isActive, bool isTemporary, float s)
    {
        var fill   = isTemporary ? _multTempFill   : isActive ? _multActiveFill   : _multInactiveFill;
        var border = isTemporary ? _multTempBorder : isActive ? _multActiveBorder : _multInactiveBorder;
        canvas.DrawRoundRect(rect, 4 * s, 4 * s, fill);
        canvas.DrawRoundRect(rect, 4 * s, 4 * s, border);
        SkiaTextUtils.DrawText(canvas, label, rect.MidX, rect.MidY + 5 * s, SKTextAlign.Center, BodyFont!,
            (isActive || isTemporary) ? TextPaint : SubtlePaint);
    }

    private void DrawIconSized(SKCanvas canvas, Resource resource, float iconX, float midY, float size)
    {
        _resourceIcons.TryGetValue(resource, out var svg);
        if (svg?.Picture is not { } picture) return;
        float svgScale = size / 32f;
        canvas.Save();
        canvas.Translate(iconX, midY - size / 2f);
        canvas.Scale(svgScale);
        canvas.DrawPicture(picture);
        canvas.Restore();
    }

    // ── Tooltip ──────────────────────────────────────────────────────────────────

    // Converts a natural-space Y to screen-space Y (accounting for scroll)
    private float ToScreenY(float naturalY) => naturalY - _scrollOffsetPx;

    private void SetTradeTooltip()
    {
        var civ = _gameControllerService.PlayerCivilization;
        if (civ == null) return;
        // Scroll-adjust the pointer position for content hit tests
        var adj = new SKPoint(_lastPointerPosition.X, _lastPointerPosition.Y + _scrollOffsetPx);

        foreach (var (rect, key) in _disabledSellRects)
            if (rect.Contains(adj.X, adj.Y)) { _tooltipRenderer.SetTooltip(_localization.Get(key), new SKPoint(rect.Right, ToScreenY(rect.Top))); return; }

        foreach (var (rect, key) in _disabledBuyRects)
            if (rect.Contains(adj.X, adj.Y)) { _tooltipRenderer.SetTooltip(_localization.Get(key), new SKPoint(rect.Right, ToScreenY(rect.Top))); return; }
    }

    // ── Dispose ──────────────────────────────────────────────────────────────────

    public override void Dispose()
    {
        if (Disposed) return;
        _smallFont.Dispose();
        _disabledPaint.Dispose();   _activeButtonPaint.Dispose(); _disabledBtnPaint.Dispose();
        _multActiveFill.Dispose();  _multInactiveFill.Dispose();  _multTempFill.Dispose();
        _multActiveBorder.Dispose(); _multInactiveBorder.Dispose(); _multTempBorder.Dispose();
        _rowBorderPaint.Dispose();  _rowFillPaint.Dispose();
        _scrollTrackPaint.Dispose(); _scrollThumbPaint.Dispose();
        _maxStockPaint.Dispose();
        base.Dispose();
    }
}
