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

public sealed class TradeRenderer : IDisposable
{
    private const float PopupWidth  = 700;
    private const float PopupHeight = 560;
    private const float Padding     = 18;
    private const float HeaderHeight = 42;
    private const float ColGap      = 16;
    private const float ColWidth    = (PopupWidth - 2 * Padding - ColGap) / 2; // 324
    private const float RowHeight   = 44;
    private const float CheckboxSize = 16;
    private const float CheckboxGap  = 4;
    private const float MultBtnW    = 42;
    private const float MultBtnH    = 24;
    private const float MultBtnGap  = 5;
    private const float BtnW        = 110;
    private const float BtnH        = 28;
    private const float IconSize    = 18f;
    private const float GoldCapsuleW = 150;

    private readonly GameControllerService _gameControllerService;
    private readonly LocalizationService _localization;
    private readonly TooltipRenderer _tooltipRenderer;
    private readonly Dictionary<Resource, SKSvg?> _resourceIcons = new();

    private readonly Dictionary<SKRect, Resource> _seaportL3Rects    = [];
    private readonly Dictionary<SKRect, Resource> _seaportL4Rects    = [];
    private readonly Dictionary<Resource, SKRect> _seaportL3AllRects = [];
    private readonly Dictionary<Resource, SKRect> _seaportL4AllRects = [];
    private readonly Dictionary<SKRect, Resource> _sellBtnRects      = [];
    private readonly Dictionary<SKRect, string>   _disabledSellRects = [];
    private readonly Dictionary<SKRect, Resource> _buyBtnRects       = [];
    private readonly Dictionary<SKRect, string>   _disabledBuyRects  = [];

    private SKRect _closeButtonRect  = SKRect.Empty;
    private SKRect _multButton1Rect  = SKRect.Empty;
    private SKRect _multButton10Rect = SKRect.Empty;
    private SKRect _multButton100Rect = SKRect.Empty;
    private SKSize _canvasSize;
    private bool _disposed;

    private Resource? _pendingEnhanceResource;
    private Resource? _pendingAutoTradeResource;
    private SKRect _confirmPopupRect = SKRect.Empty;
    private SKRect _confirmYesRect   = SKRect.Empty;
    private SKRect _confirmNoRect    = SKRect.Empty;

    private Resource? _hoveredL3Checkbox;
    private Resource? _hoveredL4Checkbox;
    private int _packMultiplier = 1;
    private int? _temporaryMultiplier;
    private int ActiveMultiplier => _temporaryMultiplier ?? _packMultiplier;
    private SKPoint _lastPointerPosition;

    private readonly PopupChrome _chrome = new();
    private readonly SKPaint _disabledPaint      = new() { Color = new SKColor(70, 70, 76, 220), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _textPaint          = new() { Color = SKColors.White, IsAntialias = true };
    private readonly SKPaint _mutedTextPaint     = new() { Color = new SKColor(190, 190, 195), IsAntialias = true };
    private readonly SKFont  _titleFont          = new() { Size = 20, Typeface = SkiaFonts.Bold };
    private readonly SKFont  _font               = new() { Size = 13, Typeface = SkiaFonts.Regular };
    private readonly SKFont  _boldFont           = new() { Size = 13, Typeface = SkiaFonts.Bold };
    private readonly SKFont  _smallFont          = new() { Size = 11, Typeface = SkiaFonts.Regular };
    private readonly SKPaint _activeButtonPaint  = new() { Color = new SKColor(46, 125, 50), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _disabledBtnPaint   = new() { Color = new SKColor(90, 90, 96),  Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _multActiveFill     = new() { Color = new SKColor(60, 100, 160), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _multInactiveFill   = new() { Color = new SKColor(38, 38, 50),   Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _multTempFill       = new() { Color = new SKColor(140, 80, 0),   Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _multActiveBorder   = new() { Color = new SKColor(100, 150, 220), Style = SKPaintStyle.Stroke, StrokeWidth = 1,    IsAntialias = true };
    private readonly SKPaint _multInactiveBorder = new() { Color = new SKColor(80, 80, 95),   Style = SKPaintStyle.Stroke, StrokeWidth = 1,    IsAntialias = true };
    private readonly SKPaint _multTempBorder     = new() { Color = new SKColor(220, 140, 30), Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f, IsAntialias = true };
    private readonly SKPaint _rowBorderPaint     = new() { Color = new SKColor(255, 255, 255, 100), Style = SKPaintStyle.Stroke, StrokeWidth = 1, IsAntialias = true };
    private readonly SKPaint _rowFillPaint       = new() { Color = new SKColor(55, 55, 65, 245), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _checkboxFillPaint  = new() { Style = SKPaintStyle.Fill,   IsAntialias = true };
    private readonly SKPaint _checkboxBorderPaint = new() { Style = SKPaintStyle.Stroke, IsAntialias = true };
    private readonly SKPaint _confirmDimPaint    = new() { Color = new SKColor(0, 0, 0, 160), Style = SKPaintStyle.Fill };
    private readonly SKPaint _confirmBgPaint     = new() { Color = new SKColor(30, 30, 38, 250), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _confirmBorderPaint = new() { Style = SKPaintStyle.Stroke, StrokeWidth = 2, IsAntialias = true };
    private readonly SKPaint _confirmYesPaint    = new() { Color = new SKColor(46, 125, 50), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _confirmNoPaint     = new() { Color = new SKColor(140, 50, 50), Style = SKPaintStyle.Fill, IsAntialias = true };

    public bool IsOpen { get; private set; }

    public TradeRenderer(GameControllerService gameControllerService, LocalizationService localization, TooltipRenderer tooltipRenderer, ResourceManager resourceManager)
    {
        _gameControllerService = gameControllerService;
        _localization = localization;
        _tooltipRenderer = tooltipRenderer;
        foreach (Resource resource in Enum.GetValues(typeof(Resource)))
        {
            string name = resource.ToString().ToLower();
            try { _resourceIcons[resource] = resourceManager.LoadImage($"Resources.icons.resources.{name}.svg"); }
            catch { _resourceIcons[resource] = null; }
        }
    }

    public void Initialize(SKSize canvasSize) => _canvasSize = canvasSize;

    public void Open()
    {
        _pendingEnhanceResource = null;
        _pendingAutoTradeResource = null;
        IsOpen = true;
    }

    public void Close()
    {
        IsOpen = false;
        _pendingEnhanceResource = null;
        _pendingAutoTradeResource = null;
        _hoveredL3Checkbox = null;
        _hoveredL4Checkbox = null;
        _temporaryMultiplier = null;
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

    public void Render(SKCanvas canvas)
    {
        if (!IsOpen || _disposed) return;

        _seaportL3Rects.Clear();
        _seaportL4Rects.Clear();
        _seaportL3AllRects.Clear();
        _seaportL4AllRects.Clear();
        _sellBtnRects.Clear();
        _disabledSellRects.Clear();
        _buyBtnRects.Clear();
        _disabledBuyRects.Clear();

        var popup = GetPopupRect();
        _chrome.DrawBackground(canvas, popup, _canvasSize);
        SkiaTextUtils.DrawText(canvas, _localization.Get("trade_title"), popup.MidX, popup.Top + 28, SKTextAlign.Center, _titleFont, _textPaint);
        _closeButtonRect = PopupChrome.GetCloseRect(popup);
        _chrome.DrawCloseButton(canvas, _closeButtonRect);

        float contentTop = popup.Top + HeaderHeight + Padding;
        float leftX  = popup.Left + Padding;
        float rightX = leftX + ColWidth + ColGap;

        DrawSellSide(canvas, leftX, contentTop);
        DrawBuySide(canvas, rightX, contentTop);
        DrawBottomBar(canvas, popup);

        if (_pendingEnhanceResource != null)
            DrawConfirmationPopup(canvas, popup, _pendingEnhanceResource.Value, isAutoTrade: false);
        else if (_pendingAutoTradeResource != null)
            DrawConfirmationPopup(canvas, popup, _pendingAutoTradeResource.Value, isAutoTrade: true);
        else
            SetTradeTooltip();
    }

    public void HandlePointerMoved(SKPoint position)
    {
        if (!IsOpen) return;
        _lastPointerPosition = position;
        _hoveredL3Checkbox = null;
        _hoveredL4Checkbox = null;
        foreach (var (res, rect) in _seaportL3AllRects)
            if (rect.Contains(position.X, position.Y)) _hoveredL3Checkbox = res;
        foreach (var (res, rect) in _seaportL4AllRects)
            if (rect.Contains(position.X, position.Y)) _hoveredL4Checkbox = res;
    }

    public bool HandlePointerPressed(SKPoint position, PointerButton button)
    {
        if (!IsOpen) return false;

        if (_pendingEnhanceResource != null)
        {
            if (_confirmYesRect.Contains(position.X, position.Y))
            {
                var civ = _gameControllerService.PlayerCivilization;
                if (civ != null)
                    _gameControllerService.MainGameController.TradeController.SetSeaportEnhancedResource(civ.Index, _pendingEnhanceResource.Value);
                _pendingEnhanceResource = null;
            }
            else if (_confirmNoRect.Contains(position.X, position.Y) || !_confirmPopupRect.Contains(position.X, position.Y))
                _pendingEnhanceResource = null;
            return true;
        }

        if (_pendingAutoTradeResource != null)
        {
            if (_confirmYesRect.Contains(position.X, position.Y))
            {
                var civ = _gameControllerService.PlayerCivilization;
                if (civ != null)
                    _gameControllerService.MainGameController.TradeController.AddSeaportAutoTradeResource(civ.Index, _pendingAutoTradeResource.Value);
                _pendingAutoTradeResource = null;
            }
            else if (_confirmNoRect.Contains(position.X, position.Y) || !_confirmPopupRect.Contains(position.X, position.Y))
                _pendingAutoTradeResource = null;
            return true;
        }

        if (_closeButtonRect.Contains(position.X, position.Y)) { Close(); return true; }

        if (_multButton1Rect.Contains(position.X, position.Y))   { _packMultiplier = 1;   return true; }
        if (_multButton10Rect.Contains(position.X, position.Y))  { _packMultiplier = 10;  return true; }
        if (_multButton100Rect.Contains(position.X, position.Y)) { _packMultiplier = 100; return true; }

        foreach (var (rect, resource) in _sellBtnRects)
        {
            if (rect.Contains(position.X, position.Y))
            {
                var civ = _gameControllerService.PlayerCivilization;
                if (civ != null)
                {
                    var tc = _gameControllerService.MainGameController.TradeController;
                    int sellRate = tc.GetSellRate(civ.Index, resource);
                    if (civ.GetResourceQuantity(resource) >= sellRate * ActiveMultiplier
                        && tc.CanRecieveTrade(civ, Resource.Gold, ActiveMultiplier))
                        tc.SellResource(civ.Index, resource, ActiveMultiplier);
                }
                return true;
            }
        }

        foreach (var (rect, resource) in _buyBtnRects)
        {
            if (rect.Contains(position.X, position.Y))
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

        foreach (var (rect, resource) in _seaportL3Rects)
        {
            if (rect.Contains(position.X, position.Y))
            {
                var civ = _gameControllerService.PlayerCivilization;
                if (civ != null && !civ.SeaportEnhancedResources.Contains(resource))
                    _pendingEnhanceResource = resource;
                return true;
            }
        }

        foreach (var (rect, resource) in _seaportL4Rects)
        {
            if (rect.Contains(position.X, position.Y)) { _pendingAutoTradeResource = resource; return true; }
        }

        if (!GetPopupRect().Contains(position.X, position.Y)) { Close(); return false; }
        return true;
    }

    // ── Sell side ─────────────────────────────────────────────────────────────────

    private void DrawSellSide(SKCanvas canvas, float x, float y)
    {
        var civ = _gameControllerService.PlayerCivilization;
        if (civ == null) return;
        var tc = _gameControllerService.MainGameController.TradeController;

        int marketLevel = tc.GetMaxMarketLevel(civ.Index);
        bool showL3 = marketLevel >= 2;
        bool showL4 = marketLevel >= 3;
        float checkboxOffset = showL4 ? CheckboxSize * 2 + CheckboxGap * 3
                             : showL3 ? CheckboxSize + CheckboxGap * 2
                             : 0f;

        SkiaTextUtils.DrawText(canvas, _localization.Get("trade_give"), x + ColWidth / 2, y + 16, SKTextAlign.Center, _boldFont, _textPaint);

        float rowY = y + 26;
        foreach (var resource in ResourceUtils.BasicResources.Where(r => tc.CanTradeResource(civ, r)))
        {
            int sellRate    = tc.GetSellRate(civ.Index, resource);
            int units       = sellRate * ActiveMultiplier;
            int available   = civ.GetResourceQuantity(resource);
            int maxQty      = civ.GetResourceMaxQuantity(resource);
            bool canSell    = available >= units && tc.CanRecieveTrade(civ, Resource.Gold, ActiveMultiplier);

            var row = new SKRect(x, rowY, x + ColWidth, rowY + RowHeight);
            DrawRowBackground(canvas, row, canSell);

            if (showL3) DrawSeaportL3Checkbox(canvas, row, resource, civ.SeaportEnhancedResources, civ);
            if (showL4 && civ.SeaportEnhancedResources.Contains(resource))
                DrawSeaportL4Checkbox(canvas, row, resource, civ.SeaportAutoTradeResources, civ);

            float iconX = row.Left + 8 + checkboxOffset;
            DrawIcon(canvas, resource, iconX, row.MidY);

            string sellName = _localization.Get($"resource_{resource.ToString().ToLower()}");
            SkiaTextUtils.DrawText(canvas, sellName, iconX + IconSize + 5, row.MidY + 5, _font, canSell ? _textPaint : _mutedTextPaint);

            SkiaTextUtils.DrawText(canvas, $"{available}/{maxQty}", row.MidX, row.MidY + 5, SKTextAlign.Center, _smallFont, _mutedTextPaint);

            string btnText = string.Format(_localization.Get("trade_sell_button"), units, ActiveMultiplier);
            var btn = DrawActionButton(canvas, row, btnText, canSell);
            _sellBtnRects[btn] = resource;
            if (!canSell)
                _disabledSellRects[btn] = available < units ? "trade_tooltip_no_offers" : "trade_tooltip_storage_full";

            rowY += RowHeight + 4;
        }
    }

    // ── Buy side ──────────────────────────────────────────────────────────────────

    private void DrawBuySide(SKCanvas canvas, float x, float y)
    {
        var civ = _gameControllerService.PlayerCivilization;
        if (civ == null) return;
        var tc = _gameControllerService.MainGameController.TradeController;

        var prestigeState = _gameControllerService.CurrentGameState?.PrestigeState;
        var map = PrestigeMapController.DefaultMap;
        var buyable = ResourceUtils.BasicResources
            .Concat(Enum.GetValues<Resource>()
                .Where(r => !ResourceUtils.BasicResources.Contains(r) && r != Resource.Gold)
                .Where(r => !ResourceUtils.AdvancedResources.Contains(r)
                            || (prestigeState?.IsResourceDiscovered(r, map) ?? false)))
            .ToList();

        SkiaTextUtils.DrawText(canvas, _localization.Get("trade_advanced_title"), x + ColWidth / 2, y + 16, SKTextAlign.Center, _boldFont, _textPaint);

        float rowY = y + 26;
        foreach (var resource in buyable)
        {
            int cost   = tc.BuyRate(resource) * ActiveMultiplier;
            bool canBuy = tc.CanBuyResource(civ.Index, resource, ActiveMultiplier);
            int qty    = civ.GetResourceQuantity(resource);
            int maxQty = civ.GetResourceMaxQuantity(resource);

            var row = new SKRect(x, rowY, x + ColWidth, rowY + RowHeight);
            DrawRowBackground(canvas, row, canBuy);

            DrawIcon(canvas, resource, row.Left + 8, row.MidY);

            string buyName = _localization.Get($"resource_{resource.ToString().ToLower()}");
            SkiaTextUtils.DrawText(canvas, buyName, row.Left + 8 + IconSize + 5, row.MidY + 5, _font, canBuy ? _textPaint : _mutedTextPaint);

            SkiaTextUtils.DrawText(canvas, $"{qty}/{maxQty}", row.MidX, row.MidY + 5, SKTextAlign.Center, _smallFont, _mutedTextPaint);

            string btnText = string.Format(_localization.Get("trade_buy_button"), cost, ActiveMultiplier);
            var btn = DrawActionButton(canvas, row, btnText, canBuy);
            _buyBtnRects[btn] = resource;
            if (!canBuy)
            {
                bool noGold = civ.GetResourceQuantity(Resource.Gold) < cost;
                _disabledBuyRects[btn] = noGold ? "trade_tooltip_no_gold" : "trade_tooltip_storage_full";
            }

            rowY += RowHeight + 4;
        }
    }

    // ── Shared row helpers ────────────────────────────────────────────────────────

    private void DrawRowBackground(SKCanvas canvas, SKRect row, bool active)
    {
        canvas.DrawRoundRect(row, 5, 5, active ? _rowFillPaint : _disabledPaint);
        canvas.DrawRoundRect(row, 5, 5, _rowBorderPaint);
    }

    private void DrawIcon(SKCanvas canvas, Resource resource, float iconX, float midY)
    {
        _resourceIcons.TryGetValue(resource, out var svg);
        if (svg?.Picture is not { } picture) return;
        float scale = IconSize / 32f;
        canvas.Save();
        canvas.Translate(iconX, midY - IconSize / 2f);
        canvas.Scale(scale);
        canvas.DrawPicture(picture);
        canvas.Restore();
    }

    private SKRect DrawActionButton(SKCanvas canvas, SKRect row, string text, bool active)
    {
        float btnX = row.Right - 6 - BtnW;
        float btnY = row.MidY - BtnH / 2f;
        var btn = new SKRect(btnX, btnY, btnX + BtnW, btnY + BtnH);
        canvas.DrawRoundRect(btn, 5, 5, active ? _activeButtonPaint : _disabledBtnPaint);
        SkiaTextUtils.DrawText(canvas, text, btn.MidX, btn.MidY + 5, SKTextAlign.Center, _font, active ? _textPaint : _mutedTextPaint);
        return btn;
    }

    // ── Bottom bar: gold capsule + multiplier buttons ─────────────────────────────

    private void DrawBottomBar(SKCanvas canvas, SKRect popup)
    {
        float barY = popup.Bottom - Padding - MultBtnH;

        // Gold capsule
        var civ = _gameControllerService.PlayerCivilization;
        if (civ != null)
        {
            int goldQty = civ.GetResourceQuantity(Resource.Gold);
            int goldMax = civ.GetResourceMaxQuantity(Resource.Gold);
            var capsule = new SKRect(popup.Left + Padding, barY, popup.Left + Padding + GoldCapsuleW, barY + MultBtnH);
            canvas.DrawRoundRect(capsule, 4, 4, _rowFillPaint);
            canvas.DrawRoundRect(capsule, 4, 4, _rowBorderPaint);

            const float goldIconSize = 14f;
            float iconX = capsule.Left + 6;
            DrawIconSized(canvas, Resource.Gold, iconX, capsule.MidY, goldIconSize);

            string qtyText = $"{goldQty}/{goldMax}";
            SkiaTextUtils.DrawText(canvas, qtyText, iconX + goldIconSize + 5, capsule.MidY + 4, _smallFont, _textPaint);
        }

        // Multiplicateurs
        float totalW = MultBtnW * 3 + MultBtnGap * 2;
        float startX = popup.Right - Padding - totalW;
        _multButton1Rect   = new SKRect(startX,                                barY, startX + MultBtnW,                  barY + MultBtnH);
        _multButton10Rect  = new SKRect(startX + MultBtnW + MultBtnGap,        barY, startX + MultBtnW * 2 + MultBtnGap,      barY + MultBtnH);
        _multButton100Rect = new SKRect(startX + MultBtnW * 2 + MultBtnGap * 2, barY, startX + totalW,                    barY + MultBtnH);

        int active = ActiveMultiplier;
        bool isTemp = _temporaryMultiplier.HasValue;
        DrawMultButton(canvas, _multButton1Rect,   "×1",   _packMultiplier == 1,   isTemp && active == 1);
        DrawMultButton(canvas, _multButton10Rect,  "×10",  _packMultiplier == 10,  isTemp && active == 10);
        DrawMultButton(canvas, _multButton100Rect, "×100", _packMultiplier == 100, isTemp && active == 100);
    }

    private void DrawMultButton(SKCanvas canvas, SKRect rect, string label, bool isActive, bool isTemporary = false)
    {
        var fill   = isTemporary ? _multTempFill   : isActive ? _multActiveFill   : _multInactiveFill;
        var border = isTemporary ? _multTempBorder : isActive ? _multActiveBorder : _multInactiveBorder;
        canvas.DrawRoundRect(rect, 4, 4, fill);
        canvas.DrawRoundRect(rect, 4, 4, border);
        SkiaTextUtils.DrawText(canvas, label, rect.MidX, rect.MidY + 5, SKTextAlign.Center, _font,
            (isActive || isTemporary) ? _textPaint : _mutedTextPaint);
    }

    private void DrawIconSized(SKCanvas canvas, Resource resource, float iconX, float midY, float size)
    {
        _resourceIcons.TryGetValue(resource, out var svg);
        if (svg?.Picture is not { } picture) return;
        float scale = size / 32f;
        canvas.Save();
        canvas.Translate(iconX, midY - size / 2f);
        canvas.Scale(scale);
        canvas.DrawPicture(picture);
        canvas.Restore();
    }

    // ── Seaport checkboxes ───────────────────────────────────────────────────────

    private void DrawSeaportL3Checkbox(SKCanvas canvas, SKRect row, Resource resource, IReadOnlyList<Resource> enhanced, Civilization civ)
    {
        float cx = row.Left + CheckboxGap;
        float cy = row.MidY - CheckboxSize / 2;
        var cb = new SKRect(cx, cy, cx + CheckboxSize, cy + CheckboxSize);
        _seaportL3AllRects[resource] = cb;

        bool isOn     = enhanced.Contains(resource);
        bool canOn    = _gameControllerService.MainGameController.TradeController.CanEnhanceSeaportResource(civ.Index, resource);
        bool hovered  = _hoveredL3Checkbox == resource;

        _checkboxFillPaint.Color   = isOn ? new SKColor(180, 140, 0, 200) : (canOn && hovered) ? new SKColor(80, 70, 0, 180) : new SKColor(50, 50, 50, 150);
        _checkboxBorderPaint.Color = isOn ? SKColors.Gold : canOn ? (hovered ? new SKColor(255, 230, 100) : new SKColor(200, 180, 80)) : new SKColor(100, 100, 100);
        _checkboxBorderPaint.StrokeWidth = hovered && canOn ? 2f : 1.5f;
        canvas.DrawRoundRect(cb, 3, 3, _checkboxFillPaint);
        canvas.DrawRoundRect(cb, 3, 3, _checkboxBorderPaint);
        if (isOn)  SkiaTextUtils.DrawText(canvas, "4", cb.MidX, cb.MidY + 5, SKTextAlign.Center, _font, _textPaint);
        if (!isOn && canOn) _seaportL3Rects[cb] = resource;
    }

    private void DrawSeaportL4Checkbox(SKCanvas canvas, SKRect row, Resource resource, IReadOnlyList<Resource> autoTrade, Civilization civ)
    {
        float cx = row.Left + CheckboxSize + CheckboxGap * 2;
        float cy = row.MidY - CheckboxSize / 2;
        var cb = new SKRect(cx, cy, cx + CheckboxSize, cy + CheckboxSize);
        _seaportL4AllRects[resource] = cb;

        bool isOn    = autoTrade.Contains(resource);
        bool canOn   = _gameControllerService.MainGameController.TradeController.CanActivateSeaportAutoTrade(civ.Index, resource);
        bool hovered = _hoveredL4Checkbox == resource;

        _checkboxFillPaint.Color   = isOn ? new SKColor(0, 130, 130, 200) : (canOn && hovered) ? new SKColor(0, 80, 80, 180) : new SKColor(50, 50, 50, 150);
        _checkboxBorderPaint.Color = isOn ? new SKColor(0, 220, 220) : canOn ? (hovered ? new SKColor(0, 220, 220) : new SKColor(0, 160, 160)) : new SKColor(80, 80, 80);
        _checkboxBorderPaint.StrokeWidth = hovered && canOn ? 2f : 1.5f;
        canvas.DrawRoundRect(cb, 3, 3, _checkboxFillPaint);
        canvas.DrawRoundRect(cb, 3, 3, _checkboxBorderPaint);
        if (isOn)  SkiaTextUtils.DrawText(canvas, "A", cb.MidX, cb.MidY + 5, SKTextAlign.Center, _font, _textPaint);
        if (!isOn && canOn) _seaportL4Rects[cb] = resource;
    }

    // ── Tooltip ──────────────────────────────────────────────────────────────────

    private void SetTradeTooltip()
    {
        var civ = _gameControllerService.PlayerCivilization;
        if (civ == null) return;
        var pos = _lastPointerPosition;

        if (_hoveredL3Checkbox != null && _seaportL3AllRects.TryGetValue(_hoveredL3Checkbox.Value, out var l3Rect))
        {
            bool isOn    = civ.SeaportEnhancedResources.Contains(_hoveredL3Checkbox.Value);
            bool canOn   = _gameControllerService.MainGameController.TradeController.CanEnhanceSeaportResource(civ.Index, _hoveredL3Checkbox.Value);
            string line1 = isOn ? _localization.Get("trade_tooltip_l3_active") : canOn ? _localization.Get("trade_tooltip_l3_available") : _localization.Get("trade_tooltip_l3_unavailable");
            string[] lines = canOn && !isOn ? [line1, _localization.Get("trade_seaport_confirm_permanent")] : [line1];
            _tooltipRenderer.SetTooltipLines(lines, new SKPoint(l3Rect.Right, l3Rect.Top));
            return;
        }

        if (_hoveredL4Checkbox != null && _seaportL4AllRects.TryGetValue(_hoveredL4Checkbox.Value, out var l4Rect))
        {
            bool isOn    = civ.SeaportAutoTradeResources.Contains(_hoveredL4Checkbox.Value);
            bool canOn   = _gameControllerService.MainGameController.TradeController.CanActivateSeaportAutoTrade(civ.Index, _hoveredL4Checkbox.Value);
            string line1 = isOn ? _localization.Get("trade_tooltip_l4_active") : canOn ? _localization.Get("trade_tooltip_l4_available") : _localization.Get("trade_tooltip_l4_unavailable");
            string[] lines = canOn && !isOn ? [line1, _localization.Get("trade_seaport_autotrade_confirm_permanent")] : [line1];
            _tooltipRenderer.SetTooltipLines(lines, new SKPoint(l4Rect.Right, l4Rect.Top));
            return;
        }

        foreach (var (rect, key) in _disabledSellRects)
            if (rect.Contains(pos.X, pos.Y)) { _tooltipRenderer.SetTooltip(_localization.Get(key), new SKPoint(rect.Right, rect.Top)); return; }

        foreach (var (rect, key) in _disabledBuyRects)
            if (rect.Contains(pos.X, pos.Y)) { _tooltipRenderer.SetTooltip(_localization.Get(key), new SKPoint(rect.Right, rect.Top)); return; }
    }

    // ── Confirmation popup ───────────────────────────────────────────────────────

    private void DrawConfirmationPopup(SKCanvas canvas, SKRect parent, Resource resource, bool isAutoTrade)
    {
        float w = 440, h = 140;
        float px = parent.MidX - w / 2;
        float py = parent.MidY - h / 2;
        _confirmPopupRect = new SKRect(px, py, px + w, py + h);

        canvas.DrawRect(parent, _confirmDimPaint);
        _confirmBorderPaint.Color = isAutoTrade ? new SKColor(0, 200, 200) : SKColors.Gold;
        canvas.DrawRoundRect(_confirmPopupRect, 8, 8, _confirmBgPaint);
        canvas.DrawRoundRect(_confirmPopupRect, 8, 8, _confirmBorderPaint);

        string resourceName  = _localization.Get($"resource_{resource.ToString().ToLower()}");
        string msgKey        = isAutoTrade ? "trade_seaport_autotrade_confirm"           : "trade_seaport_confirm";
        string permanentKey  = isAutoTrade ? "trade_seaport_autotrade_confirm_permanent" : "trade_seaport_confirm_permanent";
        SkiaTextUtils.DrawText(canvas, string.Format(_localization.Get(msgKey), resourceName), _confirmPopupRect.MidX, py + 42, SKTextAlign.Center, _font, _textPaint);
        SkiaTextUtils.DrawText(canvas, _localization.Get(permanentKey), _confirmPopupRect.MidX, py + 64, SKTextAlign.Center, _smallFont, _mutedTextPaint);

        float btnW = 100, btnH = 32, btnY = py + h - 16 - btnH;
        _confirmYesRect = new SKRect(_confirmPopupRect.MidX - btnW - 8, btnY, _confirmPopupRect.MidX - 8,          btnY + btnH);
        _confirmNoRect  = new SKRect(_confirmPopupRect.MidX + 8,        btnY, _confirmPopupRect.MidX + 8 + btnW, btnY + btnH);
        canvas.DrawRoundRect(_confirmYesRect, 6, 6, _confirmYesPaint);
        canvas.DrawRoundRect(_confirmNoRect,  6, 6, _confirmNoPaint);
        SkiaTextUtils.DrawText(canvas, _localization.Get("trade_seaport_confirm_yes"), _confirmYesRect.MidX, _confirmYesRect.MidY + 5, SKTextAlign.Center, _boldFont, _textPaint);
        SkiaTextUtils.DrawText(canvas, _localization.Get("trade_seaport_confirm_no"),  _confirmNoRect.MidX,  _confirmNoRect.MidY  + 5, SKTextAlign.Center, _boldFont, _textPaint);
    }

    // ── Layout ───────────────────────────────────────────────────────────────────

    private SKRect GetPopupRect()
    {
        float w = Math.Min(PopupWidth,  _canvasSize.Width  - 30);
        float h = Math.Min(PopupHeight, _canvasSize.Height - 30);
        return new SKRect((_canvasSize.Width - w) / 2, (_canvasSize.Height - h) / 2,
                          (_canvasSize.Width + w) / 2, (_canvasSize.Height + h) / 2);
    }

    // ── Dispose ──────────────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _chrome.Dispose();
        _disabledPaint.Dispose();   _textPaint.Dispose();       _mutedTextPaint.Dispose();
        _titleFont.Dispose();       _font.Dispose();             _boldFont.Dispose();        _smallFont.Dispose();
        _activeButtonPaint.Dispose(); _disabledBtnPaint.Dispose();
        _multActiveFill.Dispose();  _multInactiveFill.Dispose(); _multTempFill.Dispose();
        _multActiveBorder.Dispose(); _multInactiveBorder.Dispose(); _multTempBorder.Dispose();
        _rowBorderPaint.Dispose();  _rowFillPaint.Dispose();
        _checkboxFillPaint.Dispose(); _checkboxBorderPaint.Dispose();
        _confirmDimPaint.Dispose(); _confirmBgPaint.Dispose();   _confirmBorderPaint.Dispose();
        _confirmYesPaint.Dispose(); _confirmNoPaint.Dispose();
        _disposed = true;
    }
}
