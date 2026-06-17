using SettlersOfIdlestan.Controller.Expand;
using SettlersOfIdlestan.Model.Buildings;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestanSkia.Services.Localization;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Renderers.Overlay.Popup;
using SettlersOfIdlestanSkia.Renderers.Overlay.Tabs;
using SettlersOfIdlestanSkia.Services;
using SkiaSharp;
using System;
using System.Linq;

namespace SettlersOfIdlestanSkia.Renderers.Overlay.Panels;

public sealed class PlayerCivilizationPanelRenderer : PanelRendererBase
{
    private const float PanelLeft    = 10f;
    private const float PanelWidth   = 240f;
    private const float PanelPadding = 12f;
    private const float BtnHeight    = 38f;
    private const float BtnSpacing   = 6f;
    private const float TitleSize    = 11f;
    private const float TitleHeight  = 20f;
    private const float ToggleWidth  = 46f;
    private const float ToggleHeight = 24f;
    private const float RowHeight    = 36f;
    private const float SepSpacing   = 8f;

    private readonly GameControllerService _gameControllerService;
    private readonly LocalizationService _localization;
    private readonly Action _closeAll;
    private readonly TradePopupRenderer _tradeRenderer;
    private readonly PrestigeRenderer _prestigeRenderer;
    private TargetSelectionService? _targetSelectionService;
    private readonly TooltipRenderer _tooltipRenderer;

    public bool IsCollapsed  => Collapsed;
    public void Collapse()   => Collapsed = true;
    public Action? OnExpanded { get; set; }

    private SKRect _tradeButtonRect    = SKRect.Empty;
    private SKRect _prestigeButtonRect = SKRect.Empty;
    private SKRect _wonderButtonRect   = SKRect.Empty;
    private SKRect _deepestMineButtonRect = SKRect.Empty;
    private SKRect _raidButtonRect     = SKRect.Empty;
    private readonly List<(SKRect rect, string pinKey, string tooltipKey)> _pinnedItemRects = new();
    private int _hoveredPinnedIndex = -1;

    private bool _hoveredTrade, _hoveredPrestige, _hoveredWonder, _hoveredDeepestMine, _hoveredRaid;
    private bool _wonderEnabled;
    private bool _deepestMineEnabled;
    private bool _disposed;
    private SKPaint? _btnRaidActivePaint;
    private SKPaint? _btnRaidActiveHoverPaint;

    // CivPanel-specific paints
    private SKPaint? _sectionTitlePaint;
    private SKPaint? _separatorPaint;
    private SKPaint? _btnPaint;
    private SKPaint? _btnHoverPaint;
    private SKPaint? _btnDisabledPaint;
    private SKPaint? _btnDisabledTxtPaint;
    private SKPaint? _rowLabelPaint;
    private SKPaint? _rowLabelDimPaint;

    // CivPanel-specific fonts (different sizes than base Font10/12/15)
    private SKFont? _sectionFont;
    private SKFont? _btnFont;
    private SKFont? _btnSmFont;
    private SKFont? _labelFont;

    public PlayerCivilizationPanelRenderer(
        GameControllerService gameControllerService,
        LocalizationService localization,
        Action closeAll,
        TradePopupRenderer tradeRenderer,
        PrestigeRenderer prestigeRenderer,
        TargetSelectionService? targetSelectionService,
        TooltipRenderer tooltipRenderer)
    {
        _gameControllerService = gameControllerService;
        _localization = localization;
        _closeAll = closeAll;
        _tradeRenderer = tradeRenderer;
        _prestigeRenderer = prestigeRenderer;
        _targetSelectionService = targetSelectionService;
        _tooltipRenderer = tooltipRenderer;
    }

    public override void Initialize(SKSize canvasSize)
    {
        base.Initialize(canvasSize);
        _sectionTitlePaint    = new SKPaint { Color = new SKColor(160, 160, 175),      IsAntialias = true };
        _separatorPaint       = new SKPaint { Color = new SKColor(60, 60, 80),         StrokeWidth = 0.8f, Style = SKPaintStyle.Stroke };
        _btnPaint             = new SKPaint { Color = new SKColor(46, 125, 50),        Style = SKPaintStyle.Fill, IsAntialias = true };
        _btnHoverPaint        = new SKPaint { Color = new SKColor(60, 150, 64),        Style = SKPaintStyle.Fill, IsAntialias = true };
        _btnRaidActivePaint      = new SKPaint { Color = new SKColor(170, 40, 40),    Style = SKPaintStyle.Fill, IsAntialias = true };
        _btnRaidActiveHoverPaint = new SKPaint { Color = new SKColor(200, 60, 60),    Style = SKPaintStyle.Fill, IsAntialias = true };
        _btnDisabledPaint     = new SKPaint { Color = new SKColor(70, 70, 78),         Style = SKPaintStyle.Fill, IsAntialias = true };
        _btnDisabledTxtPaint  = new SKPaint { Color = new SKColor(160, 160, 165),      IsAntialias = true };
        _rowLabelPaint        = new SKPaint { Color = new SKColor(215, 215, 225),      IsAntialias = true };
        _rowLabelDimPaint     = new SKPaint { Color = new SKColor(140, 140, 150, 160), IsAntialias = true };
    }

    public void ConnectTargetSelectionService(TargetSelectionService service)
        => _targetSelectionService = service;

    public override void Render(SKCanvas canvas, GameRenderContext context)
    {
        if (_disposed) return;
        if (context.GameState is not MainGameState) return;

        var civ = _gameControllerService.PlayerCivilization;
        if (civ == null) return;

        float s = context.UiScale;
        float prevScale = LastUiScale;
        UpdateScale(s);
        if (LastUiScale != prevScale)
        {
            _sectionFont?.Dispose(); _sectionFont = new SKFont { Size = TitleSize * s, Typeface = SkiaFonts.Regular };
            _btnFont?.Dispose();     _btnFont     = new SKFont { Size = 13f * s,       Typeface = SkiaFonts.Bold };
            _btnSmFont?.Dispose();   _btnSmFont   = new SKFont { Size = 11f * s,       Typeface = SkiaFonts.Bold };
            _labelFont?.Dispose();   _labelFont   = new SKFont { Size = 13f * s,       Typeface = SkiaFonts.Regular };
        }

        float panelLeft    = PanelLeft * s;
        float panelWidth   = PanelWidth * s;
        float panelPadding = PanelPadding * s;
        float btnHeight    = BtnHeight * s;
        float btnSpacing   = BtnSpacing * s;
        float titleSize    = TitleSize * s;
        float titleHeight  = TitleHeight * s;
        float rowHeight    = RowHeight * s;
        float sepSpacing   = SepSpacing * s;
        float collapseTabW = CollapseTabW * s;
        float collapseTabH = CollapseTabH * s;

        bool tradeVisible    = IsTradeVisible();
        bool prestigeVisible = IsPrestigeVisible();
        bool prestigeAvail   = prestigeVisible && IsPrestigeAvailable();
        int  prestigePoints  = prestigeVisible ? GetPrestigePoints() : 0;
        bool wonderVisible   = IsWonderVisible() && CanPlaceWonder();
        _wonderEnabled = wonderVisible && context.CurrentLayer == 0;
        bool deepestMineVisible = CanPlaceDeepestMine();
        _deepestMineEnabled = deepestMineVisible && context.CurrentLayer == 0;
        bool raidVisible   = IsRaidVisible();
        bool raidActive    = raidVisible && IsRaidActive();
        bool hasBarracks     = HasBuilt<Barracks>(civ);
        bool hasLabs         = HasBuilt<Laboratory>(civ);
        bool hasSmelters     = HasBuilt<Smelter>(civ);
        bool hasArsenals     = HasBuilt<Arsenal>(civ);

        var worldState = _gameControllerService.CurrentWorldState;
        var pinned = worldState?.AutomationSettings.PinnedToCivPanel ?? (IReadOnlySet<string>)new HashSet<string>();

        bool showActions  = tradeVisible || prestigeVisible || wonderVisible || deepestMineVisible || raidVisible;
        bool showControls = pinned.Any(k => IsKeyShowable(k, civ, worldState, hasBarracks, hasLabs, hasSmelters, hasArsenals));

        _tradeButtonRect = _prestigeButtonRect = _wonderButtonRect = _deepestMineButtonRect = _raidButtonRect = SKRect.Empty;
        _pinnedItemRects.Clear();

        if (!showActions && !showControls)
        {
            PanelBounds = SKRect.Empty;
            CollapseTabRect = SKRect.Empty;
            return;
        }

        float contentW = panelWidth - panelPadding * 2;
        float panelTop = (TopOverride > 0f ? TopOverride : PlayerResourcesOverlayRenderer.BarHeight * s) + 10f * s;
        float tabTop   = panelTop + 8f * s;

        if (Collapsed)
        {
            CollapseTabRect = new SKRect(0, tabTop, collapseTabW, tabTop + collapseTabH);
            PanelBounds = CollapseTabRect;
            DrawCollapseTabRect(canvas, CollapseTabRect, true);
            return;
        }

        // Measure total panel height
        float h = panelPadding;
        if (showActions)
        {
            int actionCount = (tradeVisible ? 1 : 0) + (prestigeVisible ? 1 : 0) + (wonderVisible ? 1 : 0) + (deepestMineVisible ? 1 : 0) + (raidVisible ? 1 : 0);
            int actionRows  = (actionCount + 1) / 2;
            h += titleHeight + actionRows * (btnHeight + btnSpacing);
        }
        if (showActions && showControls) h += sepSpacing * 2 + 1f;
        if (showControls)
        {
            h += titleHeight;
            foreach (var k in pinned)
                if (IsKeyShowable(k, civ, worldState, hasBarracks, hasLabs, hasSmelters, hasArsenals))
                    h += rowHeight;
        }
        h += panelPadding;

        PanelBounds = new SKRect(panelLeft, panelTop, panelLeft + panelWidth, panelTop + h);
        DrawPanelChrome(canvas, panelLeft, panelTop, panelWidth, h, cornerRadius: 8f);

        // Collapse handle — shifted left to slightly overlap the panel
        float tabOverlap = 6f * s;
        CollapseTabRect = new SKRect(panelLeft + panelWidth - tabOverlap, tabTop, panelLeft + panelWidth - tabOverlap + collapseTabW, tabTop + collapseTabH);
        DrawCollapseTabRect(canvas, CollapseTabRect, false);

        float x = panelLeft + panelPadding;
        float y = panelTop + panelPadding;

        if (showActions)
        {
            SkiaTextUtils.DrawText(canvas, _localization.Get("panel_civ_actions"), x, y + titleSize, _sectionFont, _sectionTitlePaint);
            y += titleHeight;

            float colGap = 6f * s;
            float colW   = (contentW - colGap) / 2f;
            int   actionCount = (tradeVisible ? 1 : 0) + (prestigeVisible ? 1 : 0) + (wonderVisible ? 1 : 0) + (deepestMineVisible ? 1 : 0) + (raidVisible ? 1 : 0);
            float actionsY   = y;
            int   btnIdx     = 0;

            SKRect BtnRect(int idx, bool allowFullWidth = true)
            {
                float col     = idx % 2;
                float row     = idx / 2;
                bool  lastOdd = allowFullWidth && idx == actionCount - 1 && actionCount % 2 == 1;
                float bw      = lastOdd ? contentW : colW;
                float bx      = x + col * (colW + colGap);
                float by      = actionsY + row * (btnHeight + btnSpacing);
                return new SKRect(bx, by, bx + bw, by + btnHeight);
            }

            if (tradeVisible)
            {
                _tradeButtonRect = BtnRect(btnIdx++);
                canvas.DrawRoundRect(_tradeButtonRect, 6 * s, 6 * s, _hoveredTrade ? _btnHoverPaint : _btnPaint);
                SkiaTextUtils.DrawText(canvas, _localization.Get("trade_action"), _tradeButtonRect.MidX, _tradeButtonRect.MidY + 4f * s, SKTextAlign.Center, _btnSmFont, TextPaint);
            }

            if (prestigeVisible)
            {
                _prestigeButtonRect = BtnRect(btnIdx++);
                canvas.DrawRoundRect(_prestigeButtonRect, 6 * s, 6 * s, prestigeAvail ? (_hoveredPrestige ? _btnHoverPaint : _btnPaint) : _btnDisabledPaint);
                string prestigeLabel = $"{_localization.Get("prestige_action")} (+{prestigePoints})";
                SkiaTextUtils.DrawText(canvas, prestigeLabel, _prestigeButtonRect.MidX, _prestigeButtonRect.MidY + 4f * s, SKTextAlign.Center, _btnSmFont, prestigeAvail ? TextPaint : _btnDisabledTxtPaint);
            }

            if (wonderVisible)
            {
                _wonderButtonRect = BtnRect(btnIdx++);
                canvas.DrawRoundRect(_wonderButtonRect, 6 * s, 6 * s, _wonderEnabled ? (_hoveredWonder ? _btnHoverPaint : _btnPaint) : _btnDisabledPaint);
                SkiaTextUtils.DrawText(canvas, _localization.Get("wonder_action_short"), _wonderButtonRect.MidX, _wonderButtonRect.MidY + 4f * s, SKTextAlign.Center, _btnSmFont, _wonderEnabled ? TextPaint : _btnDisabledTxtPaint);
            }

            if (deepestMineVisible)
            {
                _deepestMineButtonRect = BtnRect(btnIdx++, allowFullWidth: false);
                canvas.DrawRoundRect(_deepestMineButtonRect, 6 * s, 6 * s, _deepestMineEnabled ? (_hoveredDeepestMine ? _btnHoverPaint : _btnPaint) : _btnDisabledPaint);
                DrawWrappedButtonText(canvas, _deepestMineButtonRect, _localization.Get("deepest_mine_action_short"), _btnSmFont!, _deepestMineEnabled ? TextPaint! : _btnDisabledTxtPaint!, s);
            }

            if (raidVisible)
            {
                _raidButtonRect = BtnRect(btnIdx++);
                SKPaint raidBg = raidActive
                    ? (_hoveredRaid ? _btnRaidActiveHoverPaint! : _btnRaidActivePaint!)
                    : (_hoveredRaid ? _btnHoverPaint! : _btnPaint!);
                canvas.DrawRoundRect(_raidButtonRect, 6 * s, 6 * s, raidBg);
                string raidLabel = raidActive ? _localization.Get("raid_action_stop") : _localization.Get("raid_action");
                SkiaTextUtils.DrawText(canvas, raidLabel, _raidButtonRect.MidX, _raidButtonRect.MidY + 4f * s, SKTextAlign.Center, _btnSmFont, TextPaint);
            }

            y = actionsY + ((btnIdx + 1) / 2) * (btnHeight + btnSpacing);
        }

        if (showActions && showControls)
        {
            y += sepSpacing;
            canvas.DrawLine(x, y, x + contentW, y, _separatorPaint);
            y += sepSpacing + 1f;
        }

        if (showControls)
        {
            SkiaTextUtils.DrawText(canvas, _localization.Get("panel_civ_controls"), x, y + titleSize, _sectionFont, _sectionTitlePaint);
            y += titleHeight;

            foreach (var key in pinned)
            {
                if (!IsKeyShowable(key, civ, worldState, hasBarracks, hasLabs, hasSmelters, hasArsenals))
                    continue;

                int idx = _pinnedItemRects.Count;
                bool isHovered = _hoveredPinnedIndex == idx;

                SKRect toggleRect;
                string tooltipKey;

                switch (key)
                {
                    case AutomationRenderer.PinKeyBarracks:
                        toggleRect = DrawToggleRow(canvas, x, y, AreAllActiveNullable<Barracks>(civ), isHovered, _localization.Get("building_barracks_name"));
                        tooltipKey = "tooltip_toggle_barracks";
                        break;
                    case AutomationRenderer.PinKeyLaboratory:
                        toggleRect = DrawToggleRow(canvas, x, y, AreAllActiveNullable<Laboratory>(civ), isHovered, _localization.Get("building_laboratory_name"));
                        tooltipKey = "tooltip_toggle_lab";
                        break;
                    case AutomationRenderer.PinKeySmelter:
                        toggleRect = DrawToggleRow(canvas, x, y, AreAllActiveNullable<Smelter>(civ), isHovered, _localization.Get("building_smelter_name"));
                        tooltipKey = "tooltip_toggle_smelter";
                        break;
                    case AutomationRenderer.PinKeyArsenal:
                        toggleRect = DrawToggleRow(canvas, x, y, AreAllActiveNullable<Arsenal>(civ), isHovered, _localization.Get("building_arsenal_name"));
                        tooltipKey = "tooltip_toggle_arsenal";
                        break;
                    default:
                        toggleRect = DrawAutomationToggleRow(canvas, x, y, key, worldState!, isHovered, contentW);
                        tooltipKey = "tooltip_pin_to_civ_panel";
                        break;
                }
                _pinnedItemRects.Add((toggleRect, key, tooltipKey));
                y += rowHeight;
            }
        }

        // Tooltips — set each frame so they persist while hovering
        if (_hoveredTrade)
            _tooltipRenderer.SetTooltip(_localization.Get("tooltip_trade"), new SKPoint(_tradeButtonRect.Right, _tradeButtonRect.Top));
        else if (_hoveredRaid && raidActive)
            _tooltipRenderer.SetTooltip(_localization.Get("tooltip_raid_active"), new SKPoint(_raidButtonRect.Right, _raidButtonRect.Top));
        else if (_hoveredRaid)
            _tooltipRenderer.SetTooltip(_localization.Get("tooltip_raid"), new SKPoint(_raidButtonRect.Right, _raidButtonRect.Top));
        else if (_hoveredPrestige && prestigeAvail && prestigeVisible)
        {
            _tooltipRenderer.SetTooltip(_localization.Get("tooltip_prestige_next_island"), new SKPoint(_prestigeButtonRect.Right, _prestigeButtonRect.Top));
        }
        else if (_hoveredPrestige && !prestigeAvail && prestigeVisible)
        {
            var lines = new System.Collections.Generic.List<string>();
            if (!HasPrestigeImperialPort())
                lines.Add(_localization.Get("tooltip_prestige_no_imperial_port"));
            if (prestigePoints < PrestigeController.PrestigeRequiredPoints)
                lines.Add(_localization.GetFormated("tooltip_prestige_not_enough_points", prestigePoints, PrestigeController.PrestigeRequiredPoints));
            lines.Add(_localization.Get("tooltip_prestige_next_island"));
            _tooltipRenderer.SetTooltipLines(lines.ToArray(), new SKPoint(_prestigeButtonRect.Right, _prestigeButtonRect.Top));
        }
        else if (_hoveredWonder && _wonderEnabled)
            _tooltipRenderer.SetTooltip(_localization.Get("tooltip_wonder"), new SKPoint(_wonderButtonRect.Right, _wonderButtonRect.Top));
        else if (_hoveredWonder && !_wonderEnabled)
            _tooltipRenderer.SetTooltip(_localization.Get("tooltip_wonder_surface_only"), new SKPoint(_wonderButtonRect.Right, _wonderButtonRect.Top));
        else if (_hoveredDeepestMine && !_deepestMineEnabled)
            _tooltipRenderer.SetTooltip(_localization.Get("tooltip_deepest_mine_surface_only"), new SKPoint(_deepestMineButtonRect.Right, _deepestMineButtonRect.Top));
        else if (_hoveredDeepestMine)
            _tooltipRenderer.SetTooltip(_localization.Get("tooltip_deepest_mine"), new SKPoint(_deepestMineButtonRect.Right, _deepestMineButtonRect.Top));
        else if (_hoveredPinnedIndex >= 0 && _hoveredPinnedIndex < _pinnedItemRects.Count)
        {
            var (rect, _, tooltipKey) = _pinnedItemRects[_hoveredPinnedIndex];
            _tooltipRenderer.SetTooltip(_localization.Get(tooltipKey), new SKPoint(rect.Right, rect.Top));
        }
    }

    private static void DrawWrappedButtonText(SKCanvas canvas, SKRect rect, string text, SKFont font, SKPaint paint, float s)
    {
        float maxWidth = rect.Width - 8f * s;
        var layout = SkiaTextUtils.MeasureWrappedText(text, maxWidth, font);
        float lineHeight = font.Spacing;
        float baseline = rect.MidY + 4f * s - (layout.Lines.Count - 1) * lineHeight / 2f;
        foreach (var line in layout.Lines)
        {
            SkiaTextUtils.DrawText(canvas, line, rect.MidX, baseline, SKTextAlign.Center, font, paint);
            baseline += lineHeight;
        }
    }

    private SKRect DrawToggleRow(SKCanvas canvas, float x, float y, bool? isOn, bool isHovered, string label, bool isDimmed = false)
    {
        float s       = LastUiScale;
        float toggleW = ToggleWidth * s;
        float toggleH = ToggleHeight * s;
        float rowH    = RowHeight * s;
        float toggleY = y + (rowH - toggleH) / 2f;
        var trackRect = new SKRect(x, toggleY, x + toggleW, toggleY + toggleH);
        SkiaToggleUtils.Draw(canvas, trackRect, isOn, isHovered, isDimmed);
        SkiaTextUtils.DrawText(canvas, label, x + toggleW + 10f * s, y + rowH / 2f + 5f * s, _labelFont, isDimmed ? _rowLabelDimPaint : _rowLabelPaint);
        return trackRect;
    }

    public void HandlePointerMoved(SKPoint pos)
    {
        if (_disposed) return;
        _hoveredTrade       = !_tradeButtonRect.IsEmpty       && _tradeButtonRect.Contains(pos.X, pos.Y);
        _hoveredPrestige    = !_prestigeButtonRect.IsEmpty    && _prestigeButtonRect.Contains(pos.X, pos.Y);
        _hoveredWonder      = !_wonderButtonRect.IsEmpty      && _wonderButtonRect.Contains(pos.X, pos.Y);
        _hoveredDeepestMine = !_deepestMineButtonRect.IsEmpty && _deepestMineButtonRect.Contains(pos.X, pos.Y);
        _hoveredRaid        = !_raidButtonRect.IsEmpty        && _raidButtonRect.Contains(pos.X, pos.Y);

        _hoveredPinnedIndex = -1;
        for (int i = 0; i < _pinnedItemRects.Count; i++)
        {
            if (_pinnedItemRects[i].rect.Contains(pos.X, pos.Y)) { _hoveredPinnedIndex = i; break; }
        }
    }

    public bool HandlePointerPressed(SKPoint pos)
    {
        if (_disposed) return false;

        if (!CollapseTabRect.IsEmpty && CollapseTabRect.Contains(pos.X, pos.Y))
        {
            bool wasCollapsed = Collapsed;
            Collapsed = !Collapsed;
            if (wasCollapsed && !Collapsed)
                OnExpanded?.Invoke();
            return true;
        }

        if (!PanelBounds.Contains(pos.X, pos.Y)) return false;

        if (!_tradeButtonRect.IsEmpty && _tradeButtonRect.Contains(pos.X, pos.Y))
        {
            _closeAll();
            _tradeRenderer.Open();
            return true;
        }

        if (!_prestigeButtonRect.IsEmpty && _prestigeButtonRect.Contains(pos.X, pos.Y) && IsPrestigeAvailable())
        {
            _closeAll();
            _prestigeRenderer.Open();
            return true;
        }

        if (!_wonderButtonRect.IsEmpty && _wonderButtonRect.Contains(pos.X, pos.Y) && _wonderEnabled && _targetSelectionService != null)
        {
            _closeAll();
            var wonderController = _gameControllerService.MainGameController.WonderController;
            _targetSelectionService.EnterHexSelection("wonder_select_hex", wonderController.GetPlaceableHexes(),
                hex => wonderController.PlaceWonder(hex), TargetSelectionTheme.Friendly);
            return true;
        }

        if (!_deepestMineButtonRect.IsEmpty && _deepestMineButtonRect.Contains(pos.X, pos.Y) && _deepestMineEnabled && _targetSelectionService != null)
        {
            _closeAll();
            var deepestMineController = _gameControllerService.MainGameController.DeepestMineController;
            _targetSelectionService.EnterHexSelection("deepest_mine_select_hex", deepestMineController.GetPlaceableHexes(),
                hex => deepestMineController.PlaceDeepestMine(hex), TargetSelectionTheme.Friendly);
            return true;
        }

        if (!_raidButtonRect.IsEmpty && _raidButtonRect.Contains(pos.X, pos.Y))
        {
            var playerCiv = _gameControllerService.PlayerCivilization;
            if (IsRaidActive())
            {
                if (playerCiv != null)
                    _gameControllerService.MainGameController.MilitaryController.StopRaid(playerCiv);
            }
            else if (_targetSelectionService != null && playerCiv != null)
            {
                _closeAll();
                var militaryController = _gameControllerService.MainGameController.MilitaryController;
                var targets = militaryController.GetSelectableTargets(playerCiv);
                if (targets.Count > 0)
                    _targetSelectionService.EnterVertexSelection("raid_select_city", targets,
                        target => militaryController.StartRaid(playerCiv, target), TargetSelectionTheme.Hostile);
            }
            return true;
        }

        var civ = _gameControllerService.PlayerCivilization;
        var worldState = _gameControllerService.CurrentWorldState;

        for (int i = 0; i < _pinnedItemRects.Count; i++)
        {
            if (!_pinnedItemRects[i].rect.Contains(pos.X, pos.Y)) continue;
            string key = _pinnedItemRects[i].pinKey;
            HandlePinnedToggle(key, civ, worldState);
            return true;
        }

        return true;
    }

    private void HandlePinnedToggle(string key, Civilization? civ, SettlersOfIdlestan.Model.IslandMap.WorldState? worldState)
    {
        var settings = worldState?.AutomationSettings;
        switch (key)
        {
            case AutomationRenderer.PinKeyBarracks:      if (civ != null) ToggleAll<Barracks>(civ);    break;
            case AutomationRenderer.PinKeyLaboratory:    if (civ != null) ToggleAll<Laboratory>(civ);  break;
            case AutomationRenderer.PinKeySmelter:       if (civ != null) ToggleAll<Smelter>(civ);     break;
            case AutomationRenderer.PinKeyArsenal:       if (civ != null) ToggleAll<Arsenal>(civ);     break;
            case AutomationRenderer.PinKeyRoad:          if (settings != null) settings.RoadAutomationEnabled = !settings.RoadAutomationEnabled;                           break;
            case AutomationRenderer.PinKeyOutpost:       if (settings != null) settings.OutpostAutomationEnabled = !settings.OutpostAutomationEnabled;                     break;
            case AutomationRenderer.PinKeyProduction:    if (settings != null) settings.ProductionBuildingAutomationEnabled = !settings.ProductionBuildingAutomationEnabled; break;
            case AutomationRenderer.PinKeyArtisan:       if (settings != null) settings.ArtisanBuildingAutomationEnabled = !settings.ArtisanBuildingAutomationEnabled;     break;
            case AutomationRenderer.PinKeyLibrary:       if (settings != null) settings.LibraryBuildingAutomationEnabled = !settings.LibraryBuildingAutomationEnabled;     break;
            case AutomationRenderer.PinKeyMarket:        if (settings != null) settings.MarketBuildingAutomationEnabled = !settings.MarketBuildingAutomationEnabled;       break;
            case AutomationRenderer.PinKeySeaport:       if (settings != null) settings.SeaportBuildingAutomationEnabled = !settings.SeaportBuildingAutomationEnabled;     break;
            case AutomationRenderer.PinKeyMilReinforce:
                if (settings != null)
                {
                    settings.MilitaryReinforcementAutomationEnabled = !settings.MilitaryReinforcementAutomationEnabled;
                    if (!settings.MilitaryReinforcementAutomationEnabled && civ != null)
                        _gameControllerService.MainGameController.MilitaryController.ClearReinforcementFlows(civ);
                }
                break;
            case AutomationRenderer.PinKeyMilAttack:
                if (settings != null)
                {
                    settings.MilitaryAttackAutomationEnabled = !settings.MilitaryAttackAutomationEnabled;
                    if (!settings.MilitaryAttackAutomationEnabled && civ != null)
                        _gameControllerService.MainGameController.MilitaryController.ClearAttackFlows(civ);
                }
                break;
        }
    }

    private static bool IsKeyShowable(string key, Civilization civ,
        SettlersOfIdlestan.Model.IslandMap.WorldState? worldState,
        bool hasBarracks, bool hasLabs, bool hasSmelters, bool hasArsenals)
    {
        return key switch
        {
            AutomationRenderer.PinKeyBarracks     => hasBarracks,
            AutomationRenderer.PinKeyLaboratory   => hasLabs,
            AutomationRenderer.PinKeySmelter      => hasSmelters,
            AutomationRenderer.PinKeyArsenal      => hasArsenals,
            _ => worldState != null, // automation keys: always show if world state available
        };
    }

    private SKRect DrawAutomationToggleRow(SKCanvas canvas, float x, float y, string key,
        SettlersOfIdlestan.Model.IslandMap.WorldState worldState, bool isHovered, float contentW)
    {
        var settings = worldState.AutomationSettings;
        (bool value, string nameKey) = key switch
        {
            AutomationRenderer.PinKeyRoad         => (settings.RoadAutomationEnabled,                      "automation_road_name"),
            AutomationRenderer.PinKeyOutpost      => (settings.OutpostAutomationEnabled,                   "automation_outpost_name"),
            AutomationRenderer.PinKeyProduction   => (settings.ProductionBuildingAutomationEnabled,        "automation_production_name"),
            AutomationRenderer.PinKeyArtisan      => (settings.ArtisanBuildingAutomationEnabled,           "automation_artisan_name"),
            AutomationRenderer.PinKeyLibrary      => (settings.LibraryBuildingAutomationEnabled,           "automation_library_name"),
            AutomationRenderer.PinKeyMarket       => (settings.MarketBuildingAutomationEnabled,            "automation_market_name"),
            AutomationRenderer.PinKeySeaport      => (settings.SeaportBuildingAutomationEnabled,           "automation_seaport_name"),
            AutomationRenderer.PinKeyMilReinforce => (settings.MilitaryReinforcementAutomationEnabled,     "automation_military_reinforcement_name"),
            AutomationRenderer.PinKeyMilAttack    => (settings.MilitaryAttackAutomationEnabled,            "automation_military_attack_name"),
            _                                     => (false, key),
        };
        return DrawToggleRow(canvas, x, y, (bool?)value, isHovered, _localization.Get(nameKey));
    }

    private bool IsTradeVisible()
    {
        var civ = _gameControllerService.PlayerCivilization;
        if (civ == null) return false;
        try { return _gameControllerService.MainGameController.TradeController.IsTradeAvailable(civ.Index); }
        catch { return false; }
    }

    private bool IsPrestigeVisible()
    {
        try { return _gameControllerService.MainGameController.PrestigeController.PrestigeIsVisible(); }
        catch { return false; }
    }

    private bool IsPrestigeAvailable()
    {
        try { return _gameControllerService.MainGameController.PrestigeController.PrestigeIsAvailable(); }
        catch { return false; }
    }

    private bool HasPrestigeImperialPort()
    {
        try { return _gameControllerService.MainGameController.PrestigeController.HasImperialPort(); }
        catch { return true; }
    }

    private int GetPrestigePoints()
    {
        try { return _gameControllerService.MainGameController.PrestigeController.CalculatePrestigePoints(); }
        catch { return 0; }
    }

    private bool IsWonderVisible()
    {
        var civ = _gameControllerService.PlayerCivilization;
        if (civ == null) return false;
        try { return _gameControllerService.MainGameController.WonderController.HasWondersUnlocked(civ); }
        catch { return false; }
    }

    private bool CanPlaceWonder()
    {
        var civ = _gameControllerService.PlayerCivilization;
        if (civ == null) return false;
        try { return _gameControllerService.MainGameController.WonderController.CanPlaceWonder(civ); }
        catch { return false; }
    }

    private bool CanPlaceDeepestMine()
    {
        var civ = _gameControllerService.PlayerCivilization;
        if (civ == null) return false;
        try { return _gameControllerService.MainGameController.DeepestMineController.CanPlaceDeepestMine(civ); }
        catch { return false; }
    }

    private bool IsRaidVisible()
    {
        var civ = _gameControllerService.PlayerCivilization;
        if (civ == null) return false;
        try { return _gameControllerService.MainGameController.MilitaryController.IsRaidUnlocked(civ); }
        catch { return false; }
    }

    private bool IsRaidActive()
    {
        try { return _gameControllerService.MainGameController.MilitaryController.IsRaidActive(); }
        catch { return false; }
    }

    private static bool HasBuilt<T>(Civilization civ) where T : Building
        => civ.Cities.Any(c => c.Buildings.OfType<T>().Any(b => b.Level >= 1));

    private static bool? AreAllActiveNullable<T>(Civilization civ) where T : Building
    {
        var list = civ.Cities.SelectMany(c => c.Buildings.OfType<T>()).Where(b => b.Level >= 1).ToList();
        if (list.Count == 0) return false;
        bool allOn = list.All(b => b.ActivationStatus == ActivationStatus.ACTIVE);
        if (allOn) return true;
        bool anyOn = list.Any(b => b.ActivationStatus == ActivationStatus.ACTIVE);
        return anyOn ? null : false;
    }

    private static void ToggleAll<T>(Civilization civ) where T : Building
    {
        var list = civ.Cities.SelectMany(c => c.Buildings.OfType<T>()).Where(b => b.Level >= 1).ToList();
        bool allActive = list.All(b => b.ActivationStatus == ActivationStatus.ACTIVE);
        var next = allActive ? ActivationStatus.INACTIVE : ActivationStatus.ACTIVE;
        foreach (var b in list) b.ActivationStatus = next;
    }

    public override void Dispose()
    {
        if (_disposed) return;
        _sectionTitlePaint?.Dispose();
        _separatorPaint?.Dispose();
        _btnPaint?.Dispose();
        _btnHoverPaint?.Dispose();
        _btnDisabledPaint?.Dispose();
        _btnDisabledTxtPaint?.Dispose();
        _rowLabelPaint?.Dispose();
        _rowLabelDimPaint?.Dispose();
        _btnRaidActivePaint?.Dispose();
        _btnRaidActiveHoverPaint?.Dispose();
        _sectionFont?.Dispose();
        _btnFont?.Dispose();
        _btnSmFont?.Dispose();
        _labelFont?.Dispose();
        _disposed = true;
        base.Dispose();
    }
}
