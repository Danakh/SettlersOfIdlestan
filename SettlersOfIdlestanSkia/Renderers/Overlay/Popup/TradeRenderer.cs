using SettlersOfIdlestan.Controller;
using SettlersOfIdlestan.Controller.Expand;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Services.Localization;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Renderers.Overlay;
using SettlersOfIdlestanSkia.Services;
using SkiaSharp;
using Svg.Skia;

namespace SettlersOfIdlestanSkia.Renderers.Overlay.Popup;

public sealed class TradeRenderer : IDisposable
{
    private const float PopupWidth = 560;
    private const float PopupHeight = 500;
    private const float Padding = 18;
    private const float HeaderHeight = 42;
    private const float TabBarHeight = 32;
    private const float RowHeight = 34;
    private const float PurchaseRowHeight = 44;
    private const float ColumnWidth = 230;
    private const float ButtonHeight = 36;
    private const float CloseSize = 28;
    private const float CheckboxSize = 16;
    private const float CheckboxGap = 4;
    private const float MultBtnW = 42;
    private const float MultBtnH = 24;
    private const float MultBtnGap = 5;

    private enum TradeTab { Commerce, Purchase }

    private readonly GameControllerService _gameControllerService;
    private readonly ILocalizationService _localization;
    private readonly TooltipRenderer _tooltipRenderer;
    private readonly Dictionary<Resource, SKSvg?> _resourceIcons = new();
    private readonly Dictionary<Resource, int> _offered = [];
    private readonly Dictionary<Resource, int> _requested = [];
    private readonly Dictionary<SKRect, Resource> _offerRects = [];
    private readonly Dictionary<SKRect, Resource> _requestRects = [];
    private readonly Dictionary<SKRect, Resource> _seaportL3Rects = [];
    private readonly Dictionary<SKRect, Resource> _seaportL4Rects = [];
    private readonly Dictionary<Resource, SKRect> _seaportL3AllRects = [];
    private readonly Dictionary<Resource, SKRect> _seaportL4AllRects = [];
    private readonly Dictionary<SKRect, Resource> _purchaseBuyRects = [];
    private SKRect _tradeButtonRect = SKRect.Empty;
    private SKRect _closeButtonRect = SKRect.Empty;
    private SKRect _multButton1Rect = SKRect.Empty;
    private SKRect _multButton10Rect = SKRect.Empty;
    private SKRect _multButton100Rect = SKRect.Empty;
    private SKRect _tabCommerceRect = SKRect.Empty;
    private SKRect _tabPurchaseRect = SKRect.Empty;
    private SKSize _canvasSize;
    private bool _disposed;

    private Resource? _pendingEnhanceResource;
    private Resource? _pendingAutoTradeResource;
    private SKRect _confirmPopupRect = SKRect.Empty;
    private SKRect _confirmYesRect = SKRect.Empty;
    private SKRect _confirmNoRect = SKRect.Empty;

    private Resource? _hoveredL3Checkbox;
    private Resource? _hoveredL4Checkbox;
    private int _packMultiplier = 1;
    private int? _temporaryMultiplier;
    private int ActiveMultiplier => _temporaryMultiplier ?? _packMultiplier;
    private SKPoint _lastPointerPosition;
    private readonly Dictionary<SKRect, string> _disabledRowRects = [];
    private readonly Dictionary<SKRect, string> _disabledBuyRects = [];
    private TradeTab _activeTab = TradeTab.Commerce;

    private readonly PopupChrome _chrome = new();
    private readonly SKPaint _panelPaint = new() { Color = new SKColor(38, 38, 46, 245), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _disabledPaint = new() { Color = new SKColor(70, 70, 76, 220), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _textPaint = new() { Color = SKColors.White, IsAntialias = true };
    private readonly SKPaint _mutedTextPaint = new() { Color = new SKColor(190, 190, 195), IsAntialias = true };
    private readonly SKFont _titleFont = new() { Size = 20, Typeface = SkiaFonts.Bold };
    private readonly SKFont _font = new() { Size = 13, Typeface = SkiaFonts.Regular };
    private readonly SKFont _boldFont = new() { Size = 13, Typeface = SkiaFonts.Bold };
    private readonly SKFont _smallFont = new() { Size = 11, Typeface = SkiaFonts.Regular };

    private readonly SKPaint _tradeActiveButtonPaint = new() { Color = new SKColor(46, 125, 50), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _tradeDisabledButtonPaint = new() { Color = new SKColor(90, 90, 96), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _multActiveFillPaint = new() { Color = new SKColor(60, 100, 160), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _multInactiveFillPaint = new() { Color = new SKColor(38, 38, 50), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _multTempFillPaint = new() { Color = new SKColor(140, 80, 0), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _multActiveBorderPaint = new() { Color = new SKColor(100, 150, 220), Style = SKPaintStyle.Stroke, StrokeWidth = 1, IsAntialias = true };
    private readonly SKPaint _multInactiveBorderPaint = new() { Color = new SKColor(80, 80, 95), Style = SKPaintStyle.Stroke, StrokeWidth = 1, IsAntialias = true };
    private readonly SKPaint _multTempBorderPaint = new() { Color = new SKColor(220, 140, 30), Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f, IsAntialias = true };
    private readonly SKPaint _rowBorderPaint = new() { Color = new SKColor(255, 255, 255, 100), Style = SKPaintStyle.Stroke, StrokeWidth = 1, IsAntialias = true };
    private readonly SKPaint _resourceRowPaint = new() { Color = new SKColor(55, 55, 65, 245), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _checkboxFillPaint = new() { Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _checkboxBorderPaint = new() { Style = SKPaintStyle.Stroke, IsAntialias = true };
    private readonly SKPaint _confirmDimPaint = new() { Color = new SKColor(0, 0, 0, 160), Style = SKPaintStyle.Fill };
    private readonly SKPaint _confirmBgPaint = new() { Color = new SKColor(30, 30, 38, 250), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _confirmBorderPaint = new() { Style = SKPaintStyle.Stroke, StrokeWidth = 2, IsAntialias = true };
    private readonly SKPaint _confirmYesPaint = new() { Color = new SKColor(46, 125, 50), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _confirmNoPaint = new() { Color = new SKColor(140, 50, 50), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _tabActivePaint = new() { Color = new SKColor(55, 55, 70, 245), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _tabInactivePaint = new() { Color = new SKColor(30, 30, 38, 200), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _tabBorderPaint = new() { Color = new SKColor(130, 130, 150), Style = SKPaintStyle.Stroke, StrokeWidth = 1, IsAntialias = true };

    public bool IsOpen { get; private set; }

    public TradeRenderer(GameControllerService gameControllerService, ILocalizationService localization, TooltipRenderer tooltipRenderer, ResourceManager resourceManager)
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
        _offered.Clear();
        _requested.Clear();
        _pendingEnhanceResource = null;
        _pendingAutoTradeResource = null;
        _activeTab = TradeTab.Commerce;
        IsOpen = true;
    }

    public void Close()
    {
        IsOpen = false;
        _offered.Clear();
        _requested.Clear();
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
        if (key is "Control" or "Shift")
            _temporaryMultiplier = null;
    }

    public void Render(SKCanvas canvas)
    {
        if (!IsOpen || _disposed)
            return;

        _offerRects.Clear();
        _requestRects.Clear();
        _seaportL3Rects.Clear();
        _seaportL4Rects.Clear();
        _seaportL3AllRects.Clear();
        _seaportL4AllRects.Clear();
        _purchaseBuyRects.Clear();
        _disabledRowRects.Clear();
        _disabledBuyRects.Clear();

        var popup = GetPopupRect();
        _chrome.DrawBackground(canvas, popup, _canvasSize);

        canvas.DrawText(_localization.Get("trade_title"), popup.MidX, popup.Top + 28, SKTextAlign.Center, _titleFont, _textPaint);

        _closeButtonRect = PopupChrome.GetCloseRect(popup);
        _chrome.DrawCloseButton(canvas, _closeButtonRect);

        DrawTabs(canvas, popup);

        if (_activeTab == TradeTab.Commerce)
        {
            DrawCommerceTab(canvas, popup);

            if (_pendingEnhanceResource != null)
                DrawConfirmationPopup(canvas, popup, _pendingEnhanceResource.Value, isAutoTrade: false);
            else if (_pendingAutoTradeResource != null)
                DrawConfirmationPopup(canvas, popup, _pendingAutoTradeResource.Value, isAutoTrade: true);
            else
                SetTradeTooltip();
        }
        else
        {
            DrawPurchaseTab(canvas, popup);
            SetTradeTooltip();
        }
    }

    public void HandlePointerMoved(SKPoint position)
    {
        if (!IsOpen) return;

        _lastPointerPosition = position;
        _hoveredL3Checkbox = null;
        _hoveredL4Checkbox = null;

        foreach (var (res, rect) in _seaportL3AllRects)
            if (rect.Contains(position.X, position.Y))
                _hoveredL3Checkbox = res;

        foreach (var (res, rect) in _seaportL4AllRects)
            if (rect.Contains(position.X, position.Y))
                _hoveredL4Checkbox = res;
    }

    public bool HandlePointerPressed(SKPoint position, PointerButton button)
    {
        if (!IsOpen)
            return false;

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

        if (_closeButtonRect.Contains(position.X, position.Y))
        {
            Close();
            return true;
        }

        if (_tabCommerceRect.Contains(position.X, position.Y))
        {
            _activeTab = TradeTab.Commerce;
            return true;
        }
        if (_tabPurchaseRect.Contains(position.X, position.Y))
        {
            _activeTab = TradeTab.Purchase;
            _offered.Clear();
            _requested.Clear();
            return true;
        }

        if (_multButton1Rect.Contains(position.X, position.Y)) { _packMultiplier = 1; return true; }
        if (_multButton10Rect.Contains(position.X, position.Y)) { _packMultiplier = 10; return true; }
        if (_multButton100Rect.Contains(position.X, position.Y)) { _packMultiplier = 100; return true; }

        if (_activeTab == TradeTab.Purchase)
        {
            foreach (var (rect, resource) in _purchaseBuyRects)
            {
                if (rect.Contains(position.X, position.Y))
                {
                    var civ = _gameControllerService.PlayerCivilization;
                    if (civ != null)
                    {
                        var tc = _gameControllerService.MainGameController.TradeController;
                        if (tc.CanBuyAdvancedResource(civ.Index, resource, ActiveMultiplier))
                            tc.BuyAdvancedResource(civ.Index, resource, ActiveMultiplier);
                    }
                    return true;
                }
            }
            if (!GetPopupRect().Contains(position.X, position.Y))
            {
                Close();
                return false;
            }
            return true;
        }

        // Commerce tab interactions
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
            if (rect.Contains(position.X, position.Y))
            {
                _pendingAutoTradeResource = resource;
                return true;
            }
        }

        foreach (var (rect, resource) in _offerRects)
        {
            if (rect.Contains(position.X, position.Y))
            {
                if (button == PointerButton.Right)
                    RemoveOffer(resource);
                else
                    AddOffer(resource);
                return true;
            }
        }

        foreach (var (rect, resource) in _requestRects)
        {
            if (rect.Contains(position.X, position.Y))
            {
                if (button == PointerButton.Right)
                    RemoveRequest(resource);
                else
                    AddRequest(resource);
                return true;
            }
        }

        if (_tradeButtonRect.Contains(position.X, position.Y) && CanTrade())
        {
            ExecuteTrade();
            _offered.Clear();
            _requested.Clear();
            return true;
        }

        if (!GetPopupRect().Contains(position.X, position.Y))
        {
            Close();
            return false;
        }
        return true;
    }

    // ── Tabs ─────────────────────────────────────────────────────────────────────

    private void DrawTabs(SKCanvas canvas, SKRect popup)
    {
        float tabY = popup.Top + HeaderHeight;
        float tabW = (popup.Width - 2 * Padding) / 2;
        float leftTabX = popup.Left + Padding;
        float rightTabX = leftTabX + tabW;

        _tabCommerceRect = new SKRect(leftTabX, tabY, leftTabX + tabW, tabY + TabBarHeight);
        _tabPurchaseRect = new SKRect(rightTabX, tabY, rightTabX + tabW, tabY + TabBarHeight);

        DrawTab(canvas, _tabCommerceRect, _localization.Get("trade_tab_trade"), _activeTab == TradeTab.Commerce);
        DrawTab(canvas, _tabPurchaseRect, _localization.Get("trade_tab_purchase"), _activeTab == TradeTab.Purchase);
    }

    private void DrawTab(SKCanvas canvas, SKRect rect, string label, bool isActive)
    {
        canvas.DrawRect(rect, isActive ? _tabActivePaint : _tabInactivePaint);
        canvas.DrawRect(rect, _tabBorderPaint);
        canvas.DrawText(label, rect.MidX, rect.MidY + 5, SKTextAlign.Center, _boldFont, isActive ? _textPaint : _mutedTextPaint);
    }

    // ── Commerce tab ─────────────────────────────────────────────────────────────

    private void DrawCommerceTab(SKCanvas canvas, SKRect popup)
    {
        float columnsTop = popup.Top + HeaderHeight + TabBarHeight + Padding;
        float leftX = popup.Left + Padding;
        float rightX = popup.Right - Padding - ColumnWidth;

        var civ = _gameControllerService.PlayerCivilization;
        var tradeController = _gameControllerService.MainGameController.TradeController;
        int marketLevel = civ != null ? tradeController.GetMaxMarketLevel(civ.Index) : 0;
        var enhancedResources = civ?.SeaportEnhancedResources ?? [];
        var autoTradeResources = civ?.SeaportAutoTradeResources ?? [];

        DrawColumn(canvas, leftX, columnsTop, _localization.Get("trade_give"), _offered, _requested, _offerRects, true,
            marketLevel, enhancedResources, autoTradeResources);
        DrawColumn(canvas, rightX, columnsTop, _localization.Get("trade_receive"), _requested, _offered, _requestRects, false,
            0, [], []);

        DrawMultiplierButtons(canvas, popup);

        bool canTrade = CanTrade();
        _tradeButtonRect = new SKRect(popup.MidX - 70, popup.Bottom - Padding - ButtonHeight, popup.MidX + 70, popup.Bottom - Padding);
        canvas.DrawRoundRect(_tradeButtonRect, 7, 7, canTrade ? _tradeActiveButtonPaint : _tradeDisabledButtonPaint);
        canvas.DrawText(_localization.Get("trade_action"), _tradeButtonRect.MidX, _tradeButtonRect.MidY + 5, SKTextAlign.Center, _boldFont,
            canTrade ? _textPaint : _mutedTextPaint);
    }

    // ── Purchase tab ─────────────────────────────────────────────────────────────

    private void DrawPurchaseTab(SKCanvas canvas, SKRect popup)
    {
        _tradeButtonRect = SKRect.Empty;

        var civ = _gameControllerService.PlayerCivilization;
        if (civ == null) return;
        var tc = _gameControllerService.MainGameController.TradeController;

        var prestigeState = _gameControllerService.CurrentGameState?.PrestigeState;
        var map = PrestigeMapController.DefaultMap;
        var advancedResources = Enum.GetValues<Resource>()
            .Where(r => !ResourceUtils.BasicResources.Contains(r) && r != Resource.Gold)
            .Where(r => !ResourceUtils.AdvancedResources.Contains(r)
                        || (prestigeState?.IsResourceDiscovered(r, map) ?? false))
            .ToList();

        float currentY = popup.Top + HeaderHeight + TabBarHeight + Padding;
        float contentX = popup.Left + Padding;
        float contentWidth = popup.Width - 2 * Padding;

        canvas.DrawText(_localization.Get("trade_advanced_title"), popup.MidX, currentY + 16, SKTextAlign.Center, _boldFont, _textPaint);
        currentY += 32;

        int goldAmount = civ.GetResourceQuantity(Resource.Gold);
        string goldText = string.Format(_localization.Get("trade_gold_available"), goldAmount);
        canvas.DrawText(goldText, contentX, currentY + 14, _smallFont, _mutedTextPaint);
        currentY += 28;

        foreach (var resource in advancedResources)
        {
            int cost = tc.BuyRate(resource) * ActiveMultiplier;
            bool canBuy = tc.CanBuyAdvancedResource(civ.Index, resource, ActiveMultiplier);

            var rowRect = new SKRect(contentX, currentY, contentX + contentWidth, currentY + PurchaseRowHeight);
            canvas.DrawRoundRect(rowRect, 5, 5, canBuy ? _resourceRowPaint : _disabledPaint);
            canvas.DrawRoundRect(rowRect, 5, 5, _rowBorderPaint);

            const float iconSize = 18f;
            float iconX = rowRect.Left + 10;
            _resourceIcons.TryGetValue(resource, out var svg);
            var picture = svg?.Picture;
            if (picture != null)
            {
                float scale = iconSize / 32f;
                canvas.Save();
                canvas.Translate(iconX, rowRect.MidY - iconSize / 2f);
                canvas.Scale(scale);
                canvas.DrawPicture(picture);
                canvas.Restore();
            }

            var rowTextPaint = canBuy ? _textPaint : _mutedTextPaint;
            string resourceName = _localization.Get($"resource_{resource.ToString().ToLower()}");
            canvas.DrawText(resourceName, iconX + iconSize + 6, rowRect.MidY + 5, _font, rowTextPaint);

            string qtyText = $"{civ.GetResourceQuantity(resource)}/{civ.GetResourceMaxQuantity(resource)}";
            canvas.DrawText(qtyText, rowRect.MidX + 20, rowRect.MidY + 5, SKTextAlign.Center, _smallFont, _mutedTextPaint);

            float btnW = 138;
            float btnH = 28;
            float btnX = rowRect.Right - 10 - btnW;
            float btnY = rowRect.MidY - btnH / 2f;
            var btnRect = new SKRect(btnX, btnY, btnX + btnW, btnY + btnH);

            canvas.DrawRoundRect(btnRect, 5, 5, canBuy ? _tradeActiveButtonPaint : _tradeDisabledButtonPaint);
            string costText = string.Format(_localization.Get("trade_buy_cost"), cost);
            canvas.DrawText(costText, btnRect.MidX, btnRect.MidY + 5, SKTextAlign.Center, _font, canBuy ? _textPaint : _mutedTextPaint);

            _purchaseBuyRects[btnRect] = resource;
            if (!canBuy)
            {
                bool notEnoughGold = civ.GetResourceQuantity(Resource.Gold) < cost;
                _disabledBuyRects[btnRect] = notEnoughGold ? "trade_tooltip_no_gold" : "trade_tooltip_storage_full";
            }
            currentY += PurchaseRowHeight + 8;
        }

        DrawMultiplierButtons(canvas, popup);
    }

    // ── Columns ─────────────────────────────────────────────────────────────────

    private void DrawColumn(
        SKCanvas canvas,
        float x,
        float y,
        string title,
        Dictionary<Resource, int> values,
        Dictionary<Resource, int> oppositeValues,
        Dictionary<SKRect, Resource> hitRects,
        bool isOfferColumn,
        int seaportLevel,
        List<Resource> enhancedResources,
        List<Resource> autoTradeResources)
    {
        var civ = _gameControllerService.PlayerCivilization;
        if (civ == null) return;

        var tradeController = _gameControllerService.MainGameController.TradeController;

        // Offer column: basic resources only. Receive column: basic resources + Gold.
        IEnumerable<Resource> resourceFilter = isOfferColumn
            ? ResourceUtils.BasicResources
            : ResourceUtils.BasicResources.Append(Resource.Gold);

        var resources = resourceFilter
            .Where(r => tradeController.CanTradeResource(civ, r))
            .ToList();

        float height = resources.Count * RowHeight + 78;
        var columnRect = new SKRect(x, y, x + ColumnWidth, y + height);
        canvas.DrawRoundRect(columnRect, 6, 6, _panelPaint);
        canvas.DrawText(title, columnRect.MidX, y + 22, SKTextAlign.Center, _boldFont, _textPaint);

        bool showL3 = isOfferColumn && seaportLevel >= 2;
        bool showL4 = isOfferColumn && seaportLevel >= 3;
        float checkboxOffset = showL4 ? (CheckboxSize * 2 + CheckboxGap * 3)
                             : showL3 ? (CheckboxSize + CheckboxGap * 2)
                             : 0f;

        float currentY = y + 38;
        foreach (Resource resource in resources)
        {
            bool isReceiveBlocked = !isOfferColumn && !CanAddRequest(civ, resource);
            bool isDisabled = oppositeValues.ContainsKey(resource) || isReceiveBlocked;
            var rowRect = new SKRect(x + 10, currentY, x + ColumnWidth - 10, currentY + RowHeight - 4);
            hitRects[rowRect] = resource;

            if (isDisabled)
            {
                string tooltipKey = isOfferColumn ? "trade_tooltip_already_requested"
                    : oppositeValues.ContainsKey(resource) ? "trade_tooltip_already_offered"
                    : "trade_tooltip_storage_full";
                _disabledRowRects[rowRect] = tooltipKey;
            }

            canvas.DrawRoundRect(rowRect, 5, 5, isDisabled ? _disabledPaint : _resourceRowPaint);
            canvas.DrawRoundRect(rowRect, 5, 5, _rowBorderPaint);

            if (showL3)
                DrawSeaportL3Checkbox(canvas, rowRect, resource, enhancedResources, civ);
            if (showL4 && enhancedResources.Contains(resource))
                DrawSeaportL4Checkbox(canvas, rowRect, resource, autoTradeResources, civ);

            const float iconSize = 18f;
            float iconX = rowRect.Left + 8 + checkboxOffset;
            _resourceIcons.TryGetValue(resource, out var svg);
            var picture = svg?.Picture;
            if (picture != null)
            {
                float scale = iconSize / 32f;
                canvas.Save();
                canvas.Translate(iconX, rowRect.MidY - iconSize / 2f);
                canvas.Scale(scale);
                canvas.DrawPicture(picture);
                canvas.Restore();
            }

            string resourceText = _localization.Get($"resource_{resource.ToString().ToLower()}");
            int amount = values.GetValueOrDefault(resource);
            string amountText = amount > 0 ? amount.ToString() : "+";
            var rowTextPaint = isDisabled ? _mutedTextPaint : _textPaint;

            string? ratioText = null;
            if (isOfferColumn)
            {
                int rate = tradeController.TradeRate(civ.Index, resource);
                ratioText = $"({rate}:1)";
            }
            else if (resource == Resource.Gold)
            {
                ratioText = $"(x1:x{TradeController.GoldPackValue})";
            }

            float nameY = ratioText != null ? rowRect.MidY - 1 : rowRect.MidY + 5;
            canvas.DrawText(resourceText, iconX + iconSize + 4, nameY, _font, rowTextPaint);
            if (ratioText != null)
                canvas.DrawText(ratioText, iconX + iconSize + 4, rowRect.MidY + 11, _smallFont, _mutedTextPaint);
            canvas.DrawText(amountText, rowRect.Right - 8, rowRect.MidY + 5, SKTextAlign.Right, _boldFont, rowTextPaint);

            currentY += RowHeight;
        }

        string packLabel = isOfferColumn
            ? string.Format(_localization.Get("trade_offer_packs"), GetOfferPackCount())
            : string.Format(_localization.Get("trade_request_packs"), GetRequestPackCount());
        canvas.DrawText(packLabel, columnRect.MidX, columnRect.Bottom - 14, SKTextAlign.Center, _boldFont, _textPaint);
    }

    // ── Seaport checkboxes ───────────────────────────────────────────────────────

    private void DrawSeaportL3Checkbox(SKCanvas canvas, SKRect rowRect, Resource resource, List<Resource> enhancedResources, Civilization civ)
    {
        float cx = rowRect.Left + CheckboxGap;
        float cy = rowRect.MidY - CheckboxSize / 2;
        var cbRect = new SKRect(cx, cy, cx + CheckboxSize, cy + CheckboxSize);
        _seaportL3AllRects[resource] = cbRect;

        bool isEnhanced = enhancedResources.Contains(resource);
        bool canEnhance = _gameControllerService.MainGameController.TradeController.CanEnhanceSeaportResource(civ.Index, resource);
        bool isHovered = _hoveredL3Checkbox == resource;

        SKColor borderColor = isEnhanced ? SKColors.Gold
            : canEnhance ? (isHovered ? new SKColor(255, 230, 100) : new SKColor(200, 180, 80))
            : new SKColor(100, 100, 100);
        SKColor fillColor = isEnhanced ? new SKColor(180, 140, 0, 200)
            : (canEnhance && isHovered) ? new SKColor(80, 70, 0, 180)
            : new SKColor(50, 50, 50, 150);

        _checkboxFillPaint.Color = fillColor;
        _checkboxBorderPaint.Color = borderColor;
        _checkboxBorderPaint.StrokeWidth = isHovered && canEnhance ? 2f : 1.5f;
        canvas.DrawRoundRect(cbRect, 3, 3, _checkboxFillPaint);
        canvas.DrawRoundRect(cbRect, 3, 3, _checkboxBorderPaint);

        if (isEnhanced)
            canvas.DrawText("4", cbRect.MidX, cbRect.MidY + 5, SKTextAlign.Center, _font, _textPaint);

        if (!isEnhanced && canEnhance)
            _seaportL3Rects[cbRect] = resource;
    }

    private void DrawSeaportL4Checkbox(SKCanvas canvas, SKRect rowRect, Resource resource, List<Resource> autoTradeResources, Civilization civ)
    {
        float cx = rowRect.Left + CheckboxSize + CheckboxGap * 2;
        float cy = rowRect.MidY - CheckboxSize / 2;
        var cbRect = new SKRect(cx, cy, cx + CheckboxSize, cy + CheckboxSize);
        _seaportL4AllRects[resource] = cbRect;

        bool isActive = autoTradeResources.Contains(resource);
        bool canActivate = _gameControllerService.MainGameController.TradeController.CanActivateSeaportAutoTrade(civ.Index, resource);
        bool isHovered = _hoveredL4Checkbox == resource;

        SKColor borderColor = isActive ? new SKColor(0, 220, 220)
            : canActivate ? (isHovered ? new SKColor(0, 220, 220) : new SKColor(0, 160, 160))
            : new SKColor(80, 80, 80);
        SKColor fillColor = isActive ? new SKColor(0, 130, 130, 200)
            : (canActivate && isHovered) ? new SKColor(0, 80, 80, 180)
            : new SKColor(50, 50, 50, 150);

        _checkboxFillPaint.Color = fillColor;
        _checkboxBorderPaint.Color = borderColor;
        _checkboxBorderPaint.StrokeWidth = isHovered && canActivate ? 2f : 1.5f;
        canvas.DrawRoundRect(cbRect, 3, 3, _checkboxFillPaint);
        canvas.DrawRoundRect(cbRect, 3, 3, _checkboxBorderPaint);

        if (isActive)
            canvas.DrawText("A", cbRect.MidX, cbRect.MidY + 5, SKTextAlign.Center, _font, _textPaint);

        if (!isActive && canActivate)
            _seaportL4Rects[cbRect] = resource;
    }

    // ── Tooltip ──────────────────────────────────────────────────────────────────

    private void SetTradeTooltip()
    {
        var civ = _gameControllerService.PlayerCivilization;
        if (civ == null) return;

        var pos = _lastPointerPosition;

        // Seaport checkboxes
        if (_hoveredL3Checkbox != null && _seaportL3AllRects.TryGetValue(_hoveredL3Checkbox.Value, out var l3Rect))
        {
            bool isEnhanced = civ.SeaportEnhancedResources.Contains(_hoveredL3Checkbox.Value);
            bool canEnhance = _gameControllerService.MainGameController.TradeController.CanEnhanceSeaportResource(civ.Index, _hoveredL3Checkbox.Value);
            string line1 = isEnhanced ? _localization.Get("trade_tooltip_l3_active")
                         : canEnhance ? _localization.Get("trade_tooltip_l3_available")
                         : _localization.Get("trade_tooltip_l3_unavailable");
            string[] lines = canEnhance && !isEnhanced
                ? [line1, _localization.Get("trade_seaport_confirm_permanent")]
                : [line1];
            _tooltipRenderer.SetTooltipLines(lines, new SKPoint(l3Rect.Right, l3Rect.Top));
            return;
        }

        if (_hoveredL4Checkbox != null && _seaportL4AllRects.TryGetValue(_hoveredL4Checkbox.Value, out var l4Rect))
        {
            bool isActive = civ.SeaportAutoTradeResources.Contains(_hoveredL4Checkbox.Value);
            bool canActivate = _gameControllerService.MainGameController.TradeController.CanActivateSeaportAutoTrade(civ.Index, _hoveredL4Checkbox.Value);
            string line1 = isActive   ? _localization.Get("trade_tooltip_l4_active")
                         : canActivate ? _localization.Get("trade_tooltip_l4_available")
                         : _localization.Get("trade_tooltip_l4_unavailable");
            string[] lines = canActivate && !isActive
                ? [line1, _localization.Get("trade_seaport_autotrade_confirm_permanent")]
                : [line1];
            _tooltipRenderer.SetTooltipLines(lines, new SKPoint(l4Rect.Right, l4Rect.Top));
            return;
        }

        // Disabled resource rows (offer / receive columns)
        foreach (var (rect, tooltipKey) in _disabledRowRects)
        {
            if (rect.Contains(pos.X, pos.Y))
            {
                _tooltipRenderer.SetTooltip(_localization.Get(tooltipKey), new SKPoint(rect.Right, rect.Top));
                return;
            }
        }

        // Trade button (commerce tab)
        if (_tradeButtonRect != SKRect.Empty && !CanTrade() && _tradeButtonRect.Contains(pos.X, pos.Y))
        {
            _tooltipRenderer.SetTooltip(GetTradeDisabledReason(), new SKPoint(_tradeButtonRect.MidX, _tradeButtonRect.Top));
            return;
        }

        // Disabled buy buttons (purchase tab)
        foreach (var (rect, tooltipKey) in _disabledBuyRects)
        {
            if (rect.Contains(pos.X, pos.Y))
            {
                _tooltipRenderer.SetTooltip(_localization.Get(tooltipKey), new SKPoint(rect.Right, rect.Top));
                return;
            }
        }
    }

    private string GetTradeDisabledReason()
    {
        if (_offered.Count == 0 && _requested.Count == 0)
            return _localization.Get("trade_tooltip_nothing_selected");
        if (_offered.Count == 0)
            return _localization.Get("trade_tooltip_no_offers");
        if (_requested.Count == 0)
            return _localization.Get("trade_tooltip_no_requests");

        int offerPacks = GetOfferPackCount();
        int requestPacks = GetRequestPackCount();
        if (offerPacks != requestPacks)
            return string.Format(_localization.Get("trade_tooltip_packs_mismatch"), offerPacks, requestPacks);

        var civ = _gameControllerService.PlayerCivilization;
        if (civ != null && _offered.Any(kv => civ.GetResourceQuantity(kv.Key) < kv.Value))
            return _localization.Get("trade_tooltip_no_offers");

        return _localization.Get("trade_tooltip_storage_full");
    }

    // ── Multiplier buttons ───────────────────────────────────────────────────────

    private void DrawMultiplierButtons(SKCanvas canvas, SKRect popup)
    {
        float totalW = MultBtnW * 3 + MultBtnGap * 2;
        float btnY = popup.Bottom - Padding - ButtonHeight - MultBtnGap - MultBtnH;
        float startX = popup.Right - Padding - totalW;

        _multButton1Rect   = new SKRect(startX,                         btnY, startX + MultBtnW,             btnY + MultBtnH);
        _multButton10Rect  = new SKRect(startX + MultBtnW + MultBtnGap, btnY, startX + MultBtnW * 2 + MultBtnGap,     btnY + MultBtnH);
        _multButton100Rect = new SKRect(startX + MultBtnW * 2 + MultBtnGap * 2, btnY, startX + totalW, btnY + MultBtnH);

        int active = ActiveMultiplier;
        bool isTemp = _temporaryMultiplier.HasValue;
        DrawMultButton(canvas, _multButton1Rect,   "×1",   _packMultiplier == 1,   isTemp && active == 1);
        DrawMultButton(canvas, _multButton10Rect,  "×10",  _packMultiplier == 10,  isTemp && active == 10);
        DrawMultButton(canvas, _multButton100Rect, "×100", _packMultiplier == 100, isTemp && active == 100);
    }

    private void DrawMultButton(SKCanvas canvas, SKRect rect, string label, bool isActive, bool isTemporary = false)
    {
        var fill   = isTemporary ? _multTempFillPaint   : isActive ? _multActiveFillPaint   : _multInactiveFillPaint;
        var border = isTemporary ? _multTempBorderPaint : isActive ? _multActiveBorderPaint : _multInactiveBorderPaint;
        canvas.DrawRoundRect(rect, 4, 4, fill);
        canvas.DrawRoundRect(rect, 4, 4, border);
        canvas.DrawText(label, rect.MidX, rect.MidY + 5, SKTextAlign.Center, _font,
            (isActive || isTemporary) ? _textPaint : _mutedTextPaint);
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

        string resourceName = _localization.Get($"resource_{resource.ToString().ToLower()}");
        string msgKey = isAutoTrade ? "trade_seaport_autotrade_confirm" : "trade_seaport_confirm";
        string permanentKey = isAutoTrade ? "trade_seaport_autotrade_confirm_permanent" : "trade_seaport_confirm_permanent";
        canvas.DrawText(string.Format(_localization.Get(msgKey), resourceName), _confirmPopupRect.MidX, py + 42, SKTextAlign.Center, _font, _textPaint);
        canvas.DrawText(_localization.Get(permanentKey), _confirmPopupRect.MidX, py + 64, SKTextAlign.Center, _smallFont, _mutedTextPaint);

        float btnW = 100, btnH = 32;
        float btnY = py + h - 16 - btnH;
        _confirmYesRect = new SKRect(_confirmPopupRect.MidX - btnW - 8, btnY, _confirmPopupRect.MidX - 8, btnY + btnH);
        _confirmNoRect  = new SKRect(_confirmPopupRect.MidX + 8, btnY, _confirmPopupRect.MidX + 8 + btnW, btnY + btnH);

        canvas.DrawRoundRect(_confirmYesRect, 6, 6, _confirmYesPaint);
        canvas.DrawRoundRect(_confirmNoRect,  6, 6, _confirmNoPaint);
        canvas.DrawText(_localization.Get("trade_seaport_confirm_yes"), _confirmYesRect.MidX, _confirmYesRect.MidY + 5, SKTextAlign.Center, _boldFont, _textPaint);
        canvas.DrawText(_localization.Get("trade_seaport_confirm_no"),  _confirmNoRect.MidX,  _confirmNoRect.MidY  + 5, SKTextAlign.Center, _boldFont, _textPaint);
    }

    // ── Misc drawing ─────────────────────────────────────────────────────────────

    // ── Offer / request management ───────────────────────────────────────────────

    private void AddOffer(Resource resource)
    {
        if (_requested.ContainsKey(resource)) return;
        var civ = _gameControllerService.PlayerCivilization;
        if (civ == null) return;
        int rate = _gameControllerService.MainGameController.TradeController.TradeRate(civ.Index, resource);
        int current = _offered.GetValueOrDefault(resource);
        int needed = rate * ActiveMultiplier;
        if (civ.GetResourceQuantity(resource) >= current + needed)
            _offered[resource] = current + needed;
    }

    private void AddRequest(Resource resource)
    {
        if (_offered.ContainsKey(resource)) return;
        var civ = _gameControllerService.PlayerCivilization;
        if (civ == null) return;
        int current = _requested.GetValueOrDefault(resource);
        if (_gameControllerService.MainGameController.TradeController.CanRecieveTrade(civ, resource, current + ActiveMultiplier))
            _requested[resource] = current + ActiveMultiplier;
    }

    private void RemoveOffer(Resource resource)
    {
        var civ = _gameControllerService.PlayerCivilization;
        if (civ == null || !_offered.ContainsKey(resource)) return;
        int rate = _gameControllerService.MainGameController.TradeController.TradeRate(civ.Index, resource);
        for (int i = 0; i < ActiveMultiplier; i++)
        {
            int remaining = _offered.GetValueOrDefault(resource) - rate;
            if (remaining > 0)
                _offered[resource] = remaining;
            else
            {
                _offered.Remove(resource);
                break;
            }
        }
    }

    private void RemoveRequest(Resource resource)
    {
        if (!_requested.ContainsKey(resource)) return;
        for (int i = 0; i < ActiveMultiplier; i++)
        {
            int remaining = _requested.GetValueOrDefault(resource) - 1;
            if (remaining > 0)
                _requested[resource] = remaining;
            else
            {
                _requested.Remove(resource);
                break;
            }
        }
    }

    private bool CanAddRequest(Civilization civ, Resource resource)
    {
        int requestedQuantity = _requested.GetValueOrDefault(resource);
        return _gameControllerService.MainGameController.TradeController.CanRecieveTrade(civ, resource, requestedQuantity + 1);
    }

    // ── Pack counts / trade validation ───────────────────────────────────────────

    private int GetOfferPackCount()
    {
        var civ = _gameControllerService.PlayerCivilization;
        if (civ == null) return 0;
        return _offered.Sum(kv => kv.Value / _gameControllerService.MainGameController.TradeController.TradeRate(civ.Index, kv.Key));
    }

    private int GetRequestPackCount()
    {
        var civ = _gameControllerService.PlayerCivilization;
        var tc = _gameControllerService.MainGameController.TradeController;
        if (civ == null) return 0;
        return _requested.Sum(kv =>
            kv.Value * (kv.Key == Resource.Gold ? tc.GoldPackCost(civ.Index) : tc.ReceiveRate(kv.Key)));
    }

    private bool CanTrade()
    {
        var civ = _gameControllerService.PlayerCivilization;
        if (civ == null) return false;
        int offerPacks = GetOfferPackCount();
        return offerPacks > 0
            && offerPacks == GetRequestPackCount()
            && _offered.All(kv => civ.GetResourceQuantity(kv.Key) >= kv.Value)
            && _requested.All(kv => _gameControllerService.MainGameController.TradeController.CanRecieveTrade(civ, kv.Key, kv.Value));
    }

    private void ExecuteTrade()
    {
        var civ = _gameControllerService.PlayerCivilization;
        if (civ == null) return;
        var tc = _gameControllerService.MainGameController.TradeController;

        // Build queue of offer packs (one entry per pack = TradeRate units of that resource)
        var offerQueue = new Queue<Resource>(
            _offered.SelectMany(kv => Enumerable.Repeat(kv.Key, kv.Value / tc.TradeRate(civ.Index, kv.Key)))
        );

        foreach (var (toRes, toCount) in _requested.ToList())
        {
            int packCostPerUnit = toRes == Resource.Gold ? tc.GoldPackCost(civ.Index) : tc.ReceiveRate(toRes);
            for (int unit = 0; unit < toCount; unit++)
            {
                var packsConsumed = new Dictionary<Resource, int>();
                for (int p = 0; p < packCostPerUnit; p++)
                {
                    if (offerQueue.TryDequeue(out var fromRes))
                        packsConsumed[fromRes] = packsConsumed.GetValueOrDefault(fromRes) + 1;
                }
                var offerAmounts = packsConsumed.ToDictionary(
                    kv => kv.Key,
                    kv => kv.Value * tc.TradeRate(civ.Index, kv.Key)
                );
                tc.TradeMultiForSingle(civ.Index, offerAmounts, toRes, 1);
            }
        }
    }

    // ── Layout ───────────────────────────────────────────────────────────────────

    private SKRect GetPopupRect()
    {
        float width  = Math.Min(PopupWidth,  _canvasSize.Width  - 30);
        float height = Math.Min(PopupHeight, _canvasSize.Height - 30);
        float x = (_canvasSize.Width  - width)  / 2;
        float y = (_canvasSize.Height - height) / 2;
        return new SKRect(x, y, x + width, y + height);
    }

    // ── Dispose ──────────────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _chrome.Dispose();
        _panelPaint.Dispose();
        _disabledPaint.Dispose();
        _textPaint.Dispose();
        _mutedTextPaint.Dispose();
        _titleFont.Dispose();
        _font.Dispose();
        _boldFont.Dispose();
        _smallFont.Dispose();
        _tradeActiveButtonPaint.Dispose();
        _tradeDisabledButtonPaint.Dispose();
        _multActiveFillPaint.Dispose();
        _multInactiveFillPaint.Dispose();
        _multTempFillPaint.Dispose();
        _multActiveBorderPaint.Dispose();
        _multInactiveBorderPaint.Dispose();
        _multTempBorderPaint.Dispose();
        _rowBorderPaint.Dispose();
        _resourceRowPaint.Dispose();
        _checkboxFillPaint.Dispose();
        _checkboxBorderPaint.Dispose();
        _confirmDimPaint.Dispose();
        _confirmBgPaint.Dispose();
        _confirmBorderPaint.Dispose();
        _confirmYesPaint.Dispose();
        _confirmNoPaint.Dispose();
        _tabActivePaint.Dispose();
        _tabInactivePaint.Dispose();
        _tabBorderPaint.Dispose();
        _disposed = true;
    }
}
