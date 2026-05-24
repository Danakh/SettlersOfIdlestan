using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Services.Localization;
using SettlersOfIdlestanSkia.Services;
using SkiaSharp;

namespace SettlersOfIdlestanSkia.Renderers;

public sealed class TradeRenderer : IDisposable
{
    private const float PopupWidth = 560;
    private const float PopupHeight = 500;
    private const float Padding = 18;
    private const float HeaderHeight = 42;
    private const float RowHeight = 34;
    private const float ColumnWidth = 230;
    private const float ButtonHeight = 36;
    private const float CloseSize = 28;
    private const float CheckboxSize = 16;
    private const float CheckboxGap = 4;

    private readonly GameControllerService _gameControllerService;
    private readonly ILocalizationService _localization;
    private readonly Dictionary<Resource, int> _offered = [];
    private readonly Dictionary<Resource, int> _requested = [];
    private readonly Dictionary<SKRect, Resource> _offerRects = [];
    private readonly Dictionary<SKRect, Resource> _requestRects = [];
    private readonly Dictionary<SKRect, Resource> _seaportL3Rects = [];
    private readonly Dictionary<SKRect, Resource> _seaportL4Rects = [];
    private SKRect _tradeButtonRect = SKRect.Empty;
    private SKRect _closeButtonRect = SKRect.Empty;
    private SKSize _canvasSize;
    private bool _disposed;

    private Resource? _pendingEnhanceResource;
    private Resource? _pendingAutoTradeResource;
    private SKRect _confirmPopupRect = SKRect.Empty;
    private SKRect _confirmYesRect = SKRect.Empty;
    private SKRect _confirmNoRect = SKRect.Empty;

    private Resource? _hoveredL3Checkbox;
    private Resource? _hoveredL4Checkbox;

    private readonly SKPaint _overlayPaint = new() { Color = new SKColor(0, 0, 0, 120), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _backgroundPaint = new() { Color = new SKColor(24, 24, 30, 245), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _borderPaint = new() { Color = SKColors.Gold, StrokeWidth = 2, Style = SKPaintStyle.Stroke, IsAntialias = true };
    private readonly SKPaint _panelPaint = new() { Color = new SKColor(38, 38, 46, 245), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _disabledPaint = new() { Color = new SKColor(70, 70, 76, 220), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _textPaint = new() { Color = SKColors.White, IsAntialias = true };
    private readonly SKPaint _mutedTextPaint = new() { Color = new SKColor(190, 190, 195), IsAntialias = true };
    private readonly SKFont _titleFont = new() { Size = 20, Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold) };
    private readonly SKFont _font = new() { Size = 13, Typeface = SKTypeface.FromFamilyName("Arial") };
    private readonly SKFont _boldFont = new() { Size = 13, Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold) };
    private readonly SKFont _smallFont = new() { Size = 11, Typeface = SKTypeface.FromFamilyName("Arial") };

    public bool IsOpen { get; private set; }

    public TradeRenderer(GameControllerService gameControllerService, ILocalizationService localization)
    {
        _gameControllerService = gameControllerService;
        _localization = localization;
    }

    public void Initialize(SKSize canvasSize)
    {
        _canvasSize = canvasSize;
    }

    public void Open()
    {
        _offered.Clear();
        _requested.Clear();
        _pendingEnhanceResource = null;
        _pendingAutoTradeResource = null;
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
    }

    public void Render(SKCanvas canvas)
    {
        if (!IsOpen || _disposed)
            return;

        _offerRects.Clear();
        _requestRects.Clear();
        _seaportL3Rects.Clear();
        _seaportL4Rects.Clear();

        canvas.DrawRect(new SKRect(0, 0, _canvasSize.Width, _canvasSize.Height), _overlayPaint);

        var popup = GetPopupRect();
        canvas.DrawRoundRect(popup, 10, 10, _backgroundPaint);
        canvas.DrawRoundRect(popup, 10, 10, _borderPaint);

        canvas.DrawText(_localization.Get("trade_title"), popup.MidX, popup.Top + 28, SKTextAlign.Center, _titleFont, _textPaint);

        _closeButtonRect = new SKRect(popup.Right - Padding - CloseSize, popup.Top + 10, popup.Right - Padding, popup.Top + 10 + CloseSize);
        DrawCloseButton(canvas, _closeButtonRect);

        float columnsTop = popup.Top + HeaderHeight + Padding;
        float leftX = popup.Left + Padding;
        float rightX = popup.Right - Padding - ColumnWidth;

        var civ = _gameControllerService.PlayerCivilization;
        var tradeController = _gameControllerService.MainGameController.TradeController;
        int seaportLevel = civ != null ? tradeController.GetMaxSeaportLevel(civ.Index) : 0;
        var enhancedResources = civ?.SeaportEnhancedResources ?? [];
        var autoTradeResources = civ?.SeaportAutoTradeResources ?? [];

        DrawColumn(canvas, leftX, columnsTop, _localization.Get("trade_give"), _offered, _requested, _offerRects, true,
            seaportLevel, enhancedResources, autoTradeResources);
        DrawColumn(canvas, rightX, columnsTop, _localization.Get("trade_receive"), _requested, _offered, _requestRects, false,
            0, [], []);

        bool canTrade = CanTrade();
        _tradeButtonRect = new SKRect(popup.MidX - 70, popup.Bottom - Padding - ButtonHeight, popup.MidX + 70, popup.Bottom - Padding);
        using var buttonPaint = new SKPaint
        {
            Color = canTrade ? new SKColor(46, 125, 50) : new SKColor(90, 90, 96),
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };
        canvas.DrawRoundRect(_tradeButtonRect, 7, 7, buttonPaint);
        var buttonTextPaint = canTrade ? _textPaint : _mutedTextPaint;
        canvas.DrawText(_localization.Get("trade_action"), _tradeButtonRect.MidX, _tradeButtonRect.MidY + 5, SKTextAlign.Center, _boldFont, buttonTextPaint);

        if (_pendingEnhanceResource != null)
            DrawConfirmationPopup(canvas, popup, _pendingEnhanceResource.Value, isAutoTrade: false);
        else if (_pendingAutoTradeResource != null)
            DrawConfirmationPopup(canvas, popup, _pendingAutoTradeResource.Value, isAutoTrade: true);
    }

    public void HandlePointerMoved(SKPoint position)
    {
        if (!IsOpen) return;

        _hoveredL3Checkbox = null;
        _hoveredL4Checkbox = null;

        foreach (var (rect, resource) in _seaportL3Rects)
            if (rect.Contains(position.X, position.Y))
                _hoveredL3Checkbox = resource;

        foreach (var (rect, resource) in _seaportL4Rects)
            if (rect.Contains(position.X, position.Y))
                _hoveredL4Checkbox = resource;
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
            {
                _pendingEnhanceResource = null;
            }
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
            {
                _pendingAutoTradeResource = null;
            }
            return true;
        }

        if (_closeButtonRect.Contains(position.X, position.Y))
        {
            Close();
            return true;
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
            Close();
            return true;
        }

        return GetPopupRect().Contains(position.X, position.Y);
    }

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
        if (civ == null)
            return;

        var tradeController = _gameControllerService.MainGameController.TradeController;
        var resources = Enum.GetValues(typeof(Resource))
            .Cast<Resource>()
            .Where(resource => tradeController.CanTradeResource(civ, resource))
            .ToList();

        float height = resources.Count * RowHeight + 78;
        var columnRect = new SKRect(x, y, x + ColumnWidth, y + height);
        canvas.DrawRoundRect(columnRect, 6, 6, _panelPaint);

        canvas.DrawText(title, columnRect.MidX, y + 22, SKTextAlign.Center, _boldFont, _textPaint);

        bool showL3 = isOfferColumn && seaportLevel >= 3;
        bool showL4 = isOfferColumn && seaportLevel >= 4;
        float checkboxOffset = showL4 ? (CheckboxSize * 2 + CheckboxGap * 3)
                             : showL3 ? (CheckboxSize + CheckboxGap * 2)
                             : 0;

        float currentY = y + 38;
        foreach (Resource resource in resources)
        {
            bool isReceiveBlocked = !isOfferColumn && !CanAddRequest(civ, resource);
            bool isDisabled = oppositeValues.ContainsKey(resource) || isReceiveBlocked;
            var rowRect = new SKRect(x + 10, currentY, x + ColumnWidth - 10, currentY + RowHeight - 4);
            hitRects[rowRect] = resource;

            using var resourcePaint = new SKPaint
            {
                Color = isDisabled ? new SKColor(70, 70, 76) : IslandMainRenderer.ResourceColors[resource],
                Style = SKPaintStyle.Fill,
                IsAntialias = true
            };
            canvas.DrawRoundRect(rowRect, 5, 5, isDisabled ? _disabledPaint : resourcePaint);
            using var rowBorderPaint = new SKPaint { Color = new SKColor(255, 255, 255, 100), Style = SKPaintStyle.Stroke, StrokeWidth = 1, IsAntialias = true };
            canvas.DrawRoundRect(rowRect, 5, 5, rowBorderPaint);

            if (showL3)
                DrawSeaportL3Checkbox(canvas, rowRect, resource, enhancedResources, civ);

            if (showL4 && enhancedResources.Contains(resource))
                DrawSeaportL4Checkbox(canvas, rowRect, resource, autoTradeResources, civ);

            string resourceText = _localization.Get($"resource_{resource.ToString().ToLower()}");
            int amount = values.GetValueOrDefault(resource);
            string amountText = amount > 0 ? amount.ToString() : "+";
            var rowTextPaint = isDisabled ? _mutedTextPaint : _textPaint;
            float textX = rowRect.Left + 8 + checkboxOffset;
            canvas.DrawText(resourceText, textX, rowRect.MidY + 5, _font, rowTextPaint);
            canvas.DrawText(amountText, rowRect.Right - 8, rowRect.MidY + 5, SKTextAlign.Right, _boldFont, rowTextPaint);

            currentY += RowHeight;
        }

        string packLabel = isOfferColumn
            ? string.Format(_localization.Get("trade_offer_packs"), GetOfferPackCount())
            : string.Format(_localization.Get("trade_request_packs"), GetRequestPackCount());
        canvas.DrawText(packLabel, columnRect.MidX, columnRect.Bottom - 14, SKTextAlign.Center, _boldFont, _textPaint);
    }

    private void DrawSeaportL3Checkbox(SKCanvas canvas, SKRect rowRect, Resource resource, List<Resource> enhancedResources, Civilization civ)
    {
        float cx = rowRect.Left + CheckboxGap;
        float cy = rowRect.MidY - CheckboxSize / 2;
        var cbRect = new SKRect(cx, cy, cx + CheckboxSize, cy + CheckboxSize);

        bool isEnhanced = enhancedResources.Contains(resource);
        bool canEnhance = _gameControllerService.MainGameController.TradeController.CanEnhanceSeaportResource(civ.Index, resource);
        bool isHovered = _hoveredL3Checkbox == resource;

        SKColor borderColor = isEnhanced ? SKColors.Gold
            : canEnhance ? (isHovered ? new SKColor(255, 230, 100) : new SKColor(200, 180, 80))
            : new SKColor(100, 100, 100);
        SKColor fillColor = isEnhanced ? new SKColor(180, 140, 0, 200)
            : (canEnhance && isHovered) ? new SKColor(80, 70, 0, 180)
            : new SKColor(50, 50, 50, 150);

        using var cbBorder = new SKPaint { Color = borderColor, Style = SKPaintStyle.Stroke, StrokeWidth = isHovered ? 2f : 1.5f, IsAntialias = true };
        using var cbFill = new SKPaint { Color = fillColor, Style = SKPaintStyle.Fill, IsAntialias = true };

        canvas.DrawRoundRect(cbRect, 3, 3, cbFill);
        canvas.DrawRoundRect(cbRect, 3, 3, cbBorder);

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

        bool isActive = autoTradeResources.Contains(resource);
        bool canActivate = _gameControllerService.MainGameController.TradeController.CanActivateSeaportAutoTrade(civ.Index, resource);
        bool isHovered = _hoveredL4Checkbox == resource;

        SKColor borderColor = isActive ? new SKColor(0, 220, 220)
            : canActivate ? (isHovered ? new SKColor(0, 220, 220) : new SKColor(0, 160, 160))
            : new SKColor(80, 80, 80);
        SKColor fillColor = isActive ? new SKColor(0, 130, 130, 200)
            : (canActivate && isHovered) ? new SKColor(0, 80, 80, 180)
            : new SKColor(50, 50, 50, 150);

        using var cbBorder = new SKPaint { Color = borderColor, Style = SKPaintStyle.Stroke, StrokeWidth = isHovered ? 2f : 1.5f, IsAntialias = true };
        using var cbFill = new SKPaint { Color = fillColor, Style = SKPaintStyle.Fill, IsAntialias = true };

        canvas.DrawRoundRect(cbRect, 3, 3, cbFill);
        canvas.DrawRoundRect(cbRect, 3, 3, cbBorder);

        if (isActive)
            canvas.DrawText("A", cbRect.MidX, cbRect.MidY + 5, SKTextAlign.Center, _font, _textPaint);

        if (!isActive && canActivate)
            _seaportL4Rects[cbRect] = resource;
    }

    private void DrawConfirmationPopup(SKCanvas canvas, SKRect parent, Resource resource, bool isAutoTrade)
    {
        float w = 440, h = 140;
        float px = parent.MidX - w / 2;
        float py = parent.MidY - h / 2;
        _confirmPopupRect = new SKRect(px, py, px + w, py + h);

        using var dimPaint = new SKPaint { Color = new SKColor(0, 0, 0, 160), Style = SKPaintStyle.Fill };
        canvas.DrawRect(parent, dimPaint);

        using var bgPaint = new SKPaint { Color = new SKColor(30, 30, 38, 250), Style = SKPaintStyle.Fill, IsAntialias = true };
        using var borderPaint = new SKPaint { Color = isAutoTrade ? new SKColor(0, 200, 200) : SKColors.Gold, Style = SKPaintStyle.Stroke, StrokeWidth = 2, IsAntialias = true };
        canvas.DrawRoundRect(_confirmPopupRect, 8, 8, bgPaint);
        canvas.DrawRoundRect(_confirmPopupRect, 8, 8, borderPaint);

        string resourceName = _localization.Get($"resource_{resource.ToString().ToLower()}");
        string msgKey = isAutoTrade ? "trade_seaport_autotrade_confirm" : "trade_seaport_confirm";
        string permanentKey = isAutoTrade ? "trade_seaport_autotrade_confirm_permanent" : "trade_seaport_confirm_permanent";
        canvas.DrawText(string.Format(_localization.Get(msgKey), resourceName), _confirmPopupRect.MidX, py + 42, SKTextAlign.Center, _font, _textPaint);
        canvas.DrawText(_localization.Get(permanentKey), _confirmPopupRect.MidX, py + 64, SKTextAlign.Center, _smallFont, _mutedTextPaint);

        float btnW = 100, btnH = 32;
        float btnY = py + h - 16 - btnH;
        _confirmYesRect = new SKRect(_confirmPopupRect.MidX - btnW - 8, btnY, _confirmPopupRect.MidX - 8, btnY + btnH);
        _confirmNoRect = new SKRect(_confirmPopupRect.MidX + 8, btnY, _confirmPopupRect.MidX + 8 + btnW, btnY + btnH);

        using var yesPaint = new SKPaint { Color = new SKColor(46, 125, 50), Style = SKPaintStyle.Fill, IsAntialias = true };
        using var noPaint = new SKPaint { Color = new SKColor(140, 50, 50), Style = SKPaintStyle.Fill, IsAntialias = true };
        canvas.DrawRoundRect(_confirmYesRect, 6, 6, yesPaint);
        canvas.DrawRoundRect(_confirmNoRect, 6, 6, noPaint);
        canvas.DrawText(_localization.Get("trade_seaport_confirm_yes"), _confirmYesRect.MidX, _confirmYesRect.MidY + 5, SKTextAlign.Center, _boldFont, _textPaint);
        canvas.DrawText(_localization.Get("trade_seaport_confirm_no"), _confirmNoRect.MidX, _confirmNoRect.MidY + 5, SKTextAlign.Center, _boldFont, _textPaint);
    }

    private void DrawCloseButton(SKCanvas canvas, SKRect rect)
    {
        using var closePaint = new SKPaint { Color = new SKColor(90, 50, 50, 230), Style = SKPaintStyle.Fill, IsAntialias = true };
        canvas.DrawRoundRect(rect, 5, 5, closePaint);
        canvas.DrawText("X", rect.MidX, rect.MidY + 6, SKTextAlign.Center, _boldFont, _textPaint);
    }

    private void AddOffer(Resource resource)
    {
        if (_requested.ContainsKey(resource))
            return;

        var civ = _gameControllerService.PlayerCivilization;
        if (civ == null)
            return;

        int rate = _gameControllerService.MainGameController.TradeController.TradeRate(civ.Index, resource);
        int current = _offered.GetValueOrDefault(resource);
        if (civ.GetResourceQuantity(resource) >= current + rate)
            _offered[resource] = current + rate;
    }

    private void AddRequest(Resource resource)
    {
        if (_offered.ContainsKey(resource))
            return;

        var civ = _gameControllerService.PlayerCivilization;
        if (civ == null || !CanAddRequest(civ, resource))
            return;

        _requested[resource] = _requested.GetValueOrDefault(resource) + 1;
    }

    private void RemoveOffer(Resource resource)
    {
        var civ = _gameControllerService.PlayerCivilization;
        if (civ == null || !_offered.ContainsKey(resource))
            return;

        int rate = _gameControllerService.MainGameController.TradeController.TradeRate(civ.Index, resource);
        int remaining = _offered[resource] - rate;
        if (remaining > 0)
            _offered[resource] = remaining;
        else
            _offered.Remove(resource);
    }

    private void RemoveRequest(Resource resource)
    {
        if (!_requested.ContainsKey(resource))
            return;

        int remaining = _requested[resource] - 1;
        if (remaining > 0)
            _requested[resource] = remaining;
        else
            _requested.Remove(resource);
    }

    private bool CanAddRequest(Civilization civ, Resource resource)
    {
        int requestedQuantity = _requested.GetValueOrDefault(resource);
        return _gameControllerService.MainGameController.TradeController.CanRecieveTrade(civ, resource, requestedQuantity + 1);
    }

    private int GetOfferPackCount()
    {
        var civ = _gameControllerService.PlayerCivilization;
        if (civ == null)
            return 0;

        return _offered.Sum(kv => kv.Value / _gameControllerService.MainGameController.TradeController.TradeRate(civ.Index, kv.Key));
    }

    private int GetRequestPackCount() => _requested.Sum(kv => kv.Value);

    private bool CanTrade()
    {
        var civ = _gameControllerService.PlayerCivilization;
        if (civ == null)
            return false;

        int offerPacks = GetOfferPackCount();
        return offerPacks > 0
            && offerPacks == GetRequestPackCount()
            && _offered.All(kv => civ.GetResourceQuantity(kv.Key) >= kv.Value)
            && _requested.All(kv => _gameControllerService.MainGameController.TradeController.CanRecieveTrade(civ, kv.Key, kv.Value));
    }

    private void ExecuteTrade()
    {
        var civ = _gameControllerService.PlayerCivilization;
        if (civ == null)
            return;

        var offers = _offered
            .SelectMany(kv => Enumerable.Repeat(kv.Key, kv.Value / _gameControllerService.MainGameController.TradeController.TradeRate(civ.Index, kv.Key)))
            .ToList();
        var requests = _requested.SelectMany(kv => Enumerable.Repeat(kv.Key, kv.Value)).ToList();

        for (int i = 0; i < offers.Count && i < requests.Count; i++)
        {
            _gameControllerService.MainGameController.TradeController.Trade(civ.Index, offers[i], requests[i]);
        }
    }

    private SKRect GetPopupRect()
    {
        float width = Math.Min(PopupWidth, _canvasSize.Width - 30);
        float height = Math.Min(PopupHeight, _canvasSize.Height - 30);
        float x = (_canvasSize.Width - width) / 2;
        float y = (_canvasSize.Height - height) / 2;
        return new SKRect(x, y, x + width, y + height);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _overlayPaint.Dispose();
        _backgroundPaint.Dispose();
        _borderPaint.Dispose();
        _panelPaint.Dispose();
        _disabledPaint.Dispose();
        _textPaint.Dispose();
        _mutedTextPaint.Dispose();
        _titleFont.Dispose();
        _font.Dispose();
        _boldFont.Dispose();
        _smallFont.Dispose();
        _disposed = true;
    }
}
