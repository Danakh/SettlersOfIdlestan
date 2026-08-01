using SettlersOfIdlestan.Controller.Ascension;
using SettlersOfIdlestan.Controller.Expand;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Ascension;
using SettlersOfIdlestan.Model.Buildings;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Monsters;
using SettlersOfIdlestanSkia.Services.Localization;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Renderers.Overlay.Popup;
using SettlersOfIdlestanSkia.Renderers.Overlay.Tabs;
using SettlersOfIdlestanSkia.Services;
using SkiaSharp;
using Svg.Skia;
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
    private const float IconBtnSize  = 18f;
    private const float IconBtnGap   = 4f;
    private const float IconSvgSize  = 64f;

    private readonly GameControllerService _gameControllerService;
    private readonly LocalizationService _localization;
    private readonly Action _closeAll;
    private readonly TradePopupRenderer _tradeRenderer;
    private readonly PrestigeRenderer _prestigeRenderer;
    private TargetSelectionService? _targetSelectionService;
    private readonly TooltipRenderer _tooltipRenderer;
    private readonly ResourceManager _resourceManager;
    private readonly Action<int, float, float> _centerCameraOnMapPosition;

    public bool IsCollapsed  => Collapsed;
    public void Collapse()   => Collapsed = true;
    public Action? OnExpanded { get; set; }
    public UILayoutService? LayoutService { get; set; }
    private bool TabsAtBottom => LayoutService?.TabsAtBottom ?? false;

    private float _scrollOffsetPx;
    private float _totalContentHeight;
    private float _viewportHeight;
    private bool  _needsScroll;

    private SKRect _tradeButtonRect    = SKRect.Empty;
    private SKRect _prestigeButtonRect = SKRect.Empty;
    private SKRect _wonderButtonRect   = SKRect.Empty;
    private SKRect _greatLighthouseButtonRect = SKRect.Empty;
    private SKRect _deepestMineButtonRect = SKRect.Empty;
    private SKRect _raidButtonRect     = SKRect.Empty;
    private SKRect _warHeraldButtonRect = SKRect.Empty;
    private SKRect _locateHeroButtonRect = SKRect.Empty;
    private SKRect _spireButtonRect    = SKRect.Empty;
    private SKRect _relocationButtonRect = SKRect.Empty;
    private SKRect _walkOfGodButtonRect = SKRect.Empty;
    private SKRect _presenceOfGodButtonRect = SKRect.Empty;
    private readonly List<(SKRect rect, string pinKey, string tooltipKey)> _pinnedItemRects = new();
    private int _hoveredPinnedIndex = -1;

    private bool _hoveredTrade, _hoveredPrestige, _hoveredWonder, _hoveredDeepestMine, _hoveredRaid, _hoveredWarHerald, _hoveredLocateHero, _hoveredSpire, _hoveredRelocation, _hoveredWalkOfGod, _hoveredPresenceOfGod, _hoveredGreatLighthouse;
    private bool _wonderEnabled;
    private bool _greatLighthouseEnabled;
    private bool _deepestMineEnabled;
    private bool _spireEnabled;
    private bool _relocationEnabled;
    private bool _walkOfGodEnabled;
    private bool _presenceOfGodEnabled;
    private bool _disposed;
    private SKPaint? _btnRaidActivePaint;
    private SKPaint? _btnRaidActiveHoverPaint;
    private SKPaint? _iconTintPaint;
    private SKSvg? _attackIconSvg;
    private SKSvg? _defenseIconSvg;
    private SKSvg? _heroIconSvg;

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
        TooltipRenderer tooltipRenderer,
        ResourceManager resourceManager,
        Action<int, float, float> centerCameraOnMapPosition)
    {
        _gameControllerService = gameControllerService;
        _localization = localization;
        _closeAll = closeAll;
        _tradeRenderer = tradeRenderer;
        _prestigeRenderer = prestigeRenderer;
        _targetSelectionService = targetSelectionService;
        _tooltipRenderer = tooltipRenderer;
        _resourceManager = resourceManager;
        _centerCameraOnMapPosition = centerCameraOnMapPosition;
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
        _iconTintPaint        = new SKPaint { IsAntialias = true };
        _attackIconSvg  = _resourceManager.LoadImage("Resources.icons.military.attack.svg");
        _defenseIconSvg = _resourceManager.LoadImage("Resources.icons.military.defense.svg");
        _heroIconSvg    = _resourceManager.LoadImage("Resources.icons.military.hero-armor.svg");
    }

    public void ConnectTargetSelectionService(TargetSelectionService service)
        => _targetSelectionService = service;

    /// <summary>Défilement au clavier/molette de souris quand le contenu du panneau dépasse la hauteur disponible.</summary>
    public new void HandleScroll(float delta)
    {
        if (Collapsed || !_needsScroll) return;
        const float step = 60f;
        float dir       = delta > 0 ? -1f : 1f;
        float maxScroll = Math.Max(0f, _totalContentHeight - _viewportHeight);
        _scrollOffsetPx = Math.Clamp(_scrollOffsetPx + dir * step, 0f, maxScroll);
    }

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
        bool greatLighthouseVisible = IsGreatLighthouseVisible() && CanPlaceGreatLighthouse();
        _greatLighthouseEnabled = greatLighthouseVisible && context.CurrentLayer == 0;
        bool deepestMineVisible = CanPlaceDeepestMine();
        _deepestMineEnabled = deepestMineVisible && context.CurrentLayer == 0;
        bool spireVisible   = CanPlaceSpire();
        _spireEnabled = spireVisible && context.CurrentLayer == LayerState.UnderworldZ;
        bool raidVisible   = IsRaidVisible();
        bool raidActive    = raidVisible && IsRaidActive();
        bool warHeraldVisible = IsWarHeraldVisible();
        var adventurersGuildCity = GetAdventurersGuildCity(civ);
        bool locateHeroVisible = adventurersGuildCity != null;
        bool relocationVisible = IsRelocationVisible();
        _relocationEnabled = relocationVisible && CanAffordRelocation();
        var ascensionController = _gameControllerService.MainGameController.AscensionController;
        bool walkOfGodVisible = ascensionController.IsPowerUnlocked(AscensionPowerId.WalkOfGod);
        _walkOfGodEnabled = walkOfGodVisible && context.CurrentLayer == 0 && ascensionController.GetWalkOfGodTargetHexes().Count > 0 && ascensionController.CanUseWalkOfGod();
        bool presenceOfGodVisible = ascensionController.IsPowerUnlocked(AscensionPowerId.PresenceOfGod);
        _presenceOfGodEnabled = presenceOfGodVisible && context.CurrentLayer == 0 && ascensionController.GetPresenceOfGodTargetHexes().Count > 0 && ascensionController.CanUsePresenceOfGod();
        bool hasBarracks     = HasBuilt<Barracks>(civ);
        bool hasArsenal      = HasBuilt<Arsenal>(civ);
        bool hasLabs         = HasBuilt<Laboratory>(civ);
        bool hasSmelters     = HasBuilt<Smelter>(civ);
        bool hasWeaponSmiths  = HasBuilt<WeaponSmith>(civ);
        bool hasArmorSmiths   = HasBuilt<ArmorSmith>(civ);
        bool hasAlchimistHuts = HasBuilt<AlchimistHut>(civ);

        var worldState = _gameControllerService.CurrentWorldState;
        var pinned = _gameControllerService.CurrentGameState?.Settings.PinnedCivPanelKeys ?? (IReadOnlySet<string>)new HashSet<string>();

        bool showActions  = tradeVisible || prestigeVisible || wonderVisible || greatLighthouseVisible || deepestMineVisible || spireVisible || raidVisible || warHeraldVisible || locateHeroVisible || relocationVisible || walkOfGodVisible || presenceOfGodVisible;
        bool showControls = pinned.Any(k => IsKeyShowable(k, civ, worldState, hasBarracks, hasArsenal, hasLabs, hasSmelters, hasWeaponSmiths, hasArmorSmiths, hasAlchimistHuts));

        // Single source of truth for the action-button count — reused for both the
        // panel height measurement and the button-grid layout so they can't drift apart.
        // Trade / Raid / War Herald / Locate Hero are drawn as small icon buttons on the title row, not in this grid.
        int actionCount = (prestigeVisible ? 1 : 0) + (wonderVisible ? 1 : 0) + (greatLighthouseVisible ? 1 : 0) + (deepestMineVisible ? 1 : 0) + (spireVisible ? 1 : 0) + (relocationVisible ? 1 : 0) + (walkOfGodVisible ? 1 : 0) + (presenceOfGodVisible ? 1 : 0);

        _tradeButtonRect = _prestigeButtonRect = _wonderButtonRect = _greatLighthouseButtonRect = _deepestMineButtonRect = _spireButtonRect = _raidButtonRect = _warHeraldButtonRect = _locateHeroButtonRect = _relocationButtonRect = _walkOfGodButtonRect = _presenceOfGodButtonRect = SKRect.Empty;
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
            int actionRows  = (actionCount + 1) / 2;
            h += titleHeight + actionRows * (btnHeight + btnSpacing);
        }
        if (showActions && showControls) h += sepSpacing * 2 + 1f;
        if (showControls)
        {
            h += titleHeight;
            foreach (var k in pinned)
                if (IsKeyShowable(k, civ, worldState, hasBarracks, hasArsenal, hasLabs, hasSmelters, hasWeaponSmiths, hasArmorSmiths, hasAlchimistHuts))
                    h += rowHeight;
        }
        h += panelPadding;

        // Global elevator when the panel is taller than the available vertical space.
        float maxPanelHeight = TabsAtBottom
            ? Math.Max(0f, CanvasSize.Height - panelTop - UILayoutService.MobileTabBarHeight - 8f * s)
            : Math.Max(0f, CanvasSize.Height - panelTop - 10f * s);
        _needsScroll = h > maxPanelHeight;
        float panelHeight = _needsScroll ? maxPanelHeight : h;
        _totalContentHeight = h;
        _viewportHeight     = panelHeight;
        _scrollOffsetPx = Math.Clamp(_scrollOffsetPx, 0f, Math.Max(0f, h - panelHeight));

        PanelBounds = new SKRect(panelLeft, panelTop, panelLeft + panelWidth, panelTop + panelHeight);
        DrawPanelChrome(canvas, panelLeft, panelTop, panelWidth, panelHeight, cornerRadius: 8f);

        // Collapse handle — shifted left to slightly overlap the panel
        float tabOverlap = 6f * s;
        CollapseTabRect = new SKRect(panelLeft + panelWidth - tabOverlap, tabTop, panelLeft + panelWidth - tabOverlap + collapseTabW, tabTop + collapseTabH);
        DrawCollapseTabRect(canvas, CollapseTabRect, false);

        if (_needsScroll)
        {
            canvas.Save();
            canvas.ClipRect(new SKRect(panelLeft, panelTop, panelLeft + panelWidth, panelTop + panelHeight));
            canvas.Translate(0, -_scrollOffsetPx);
        }

        float x = panelLeft + panelPadding;
        float y = panelTop + panelPadding;

        if (showActions)
        {
            SkiaTextUtils.DrawText(canvas, _localization.Get("panel_civ_actions"), x, y + titleSize, _sectionFont, _sectionTitlePaint);

            // Small icon buttons (locate hero / war herald / raid), right-aligned on the Actions title row.
            float iconBtnSize = IconBtnSize * s;
            float iconGap     = IconBtnGap * s;
            float iconY       = y + (titleHeight - iconBtnSize) / 2f;
            float iconRight   = x + contentW;

            if (locateHeroVisible)
            {
                _locateHeroButtonRect = new SKRect(iconRight - iconBtnSize, iconY, iconRight, iconY + iconBtnSize);
                DrawIconButton(canvas, _locateHeroButtonRect, _heroIconSvg, _hoveredLocateHero ? _btnHoverPaint! : _btnPaint!, s);
                iconRight -= iconBtnSize + iconGap;
            }
            if (warHeraldVisible)
            {
                _warHeraldButtonRect = new SKRect(iconRight - iconBtnSize, iconY, iconRight, iconY + iconBtnSize);
                DrawIconButton(canvas, _warHeraldButtonRect, _defenseIconSvg, _hoveredWarHerald ? _btnHoverPaint! : _btnPaint!, s);
                iconRight -= iconBtnSize + iconGap;
            }
            if (raidVisible)
            {
                _raidButtonRect = new SKRect(iconRight - iconBtnSize, iconY, iconRight, iconY + iconBtnSize);
                SKPaint raidIconBg = raidActive
                    ? (_hoveredRaid ? _btnRaidActiveHoverPaint! : _btnRaidActivePaint!)
                    : (_hoveredRaid ? _btnHoverPaint! : _btnPaint!);
                DrawIconButton(canvas, _raidButtonRect, _attackIconSvg, raidIconBg, s);
                iconRight -= iconBtnSize + iconGap;
            }
            if (tradeVisible)
            {
                _tradeButtonRect = new SKRect(iconRight - iconBtnSize, iconY, iconRight, iconY + iconBtnSize);
                DrawIconButtonChar(canvas, _tradeButtonRect, "💰", _hoveredTrade ? _btnHoverPaint! : _btnPaint!, s);
                iconRight -= iconBtnSize + iconGap;
            }

            y += titleHeight;

            float colGap = 6f * s;
            float colW   = (contentW - colGap) / 2f;
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

            if (prestigeVisible)
            {
                _prestigeButtonRect = BtnRect(btnIdx++);
                canvas.DrawRoundRect(_prestigeButtonRect, 6 * s, 6 * s, prestigeAvail ? (_hoveredPrestige ? _btnHoverPaint : _btnPaint) : _btnDisabledPaint);
                string prestigeLabel = $"{_localization.Get("prestige_action")} (+{SkiaTextUtils.FormatNumber(prestigePoints)})";
                SkiaTextUtils.DrawText(canvas, prestigeLabel, _prestigeButtonRect.MidX, _prestigeButtonRect.MidY + 4f * s, SKTextAlign.Center, _btnSmFont, prestigeAvail ? TextPaint : _btnDisabledTxtPaint);
            }

            if (wonderVisible)
            {
                _wonderButtonRect = BtnRect(btnIdx++);
                canvas.DrawRoundRect(_wonderButtonRect, 6 * s, 6 * s, _wonderEnabled ? (_hoveredWonder ? _btnHoverPaint : _btnPaint) : _btnDisabledPaint);
                SkiaTextUtils.DrawText(canvas, _localization.Get("wonder_action_short"), _wonderButtonRect.MidX, _wonderButtonRect.MidY + 4f * s, SKTextAlign.Center, _btnSmFont, _wonderEnabled ? TextPaint : _btnDisabledTxtPaint);
            }

            if (greatLighthouseVisible)
            {
                _greatLighthouseButtonRect = BtnRect(btnIdx++);
                canvas.DrawRoundRect(_greatLighthouseButtonRect, 6 * s, 6 * s, _greatLighthouseEnabled ? (_hoveredGreatLighthouse ? _btnHoverPaint : _btnPaint) : _btnDisabledPaint);
                SkiaTextUtils.DrawText(canvas, _localization.Get("great_lighthouse_action_short"), _greatLighthouseButtonRect.MidX, _greatLighthouseButtonRect.MidY + 4f * s, SKTextAlign.Center, _btnSmFont, _greatLighthouseEnabled ? TextPaint : _btnDisabledTxtPaint);
            }

            if (deepestMineVisible)
            {
                _deepestMineButtonRect = BtnRect(btnIdx++, allowFullWidth: false);
                canvas.DrawRoundRect(_deepestMineButtonRect, 6 * s, 6 * s, _deepestMineEnabled ? (_hoveredDeepestMine ? _btnHoverPaint : _btnPaint) : _btnDisabledPaint);
                DrawWrappedButtonText(canvas, _deepestMineButtonRect, _localization.Get("deepest_mine_action_short"), _btnSmFont!, _deepestMineEnabled ? TextPaint! : _btnDisabledTxtPaint!, s);
            }

            if (spireVisible)
            {
                _spireButtonRect = BtnRect(btnIdx++, allowFullWidth: false);
                canvas.DrawRoundRect(_spireButtonRect, 6 * s, 6 * s, _spireEnabled ? (_hoveredSpire ? _btnHoverPaint : _btnPaint) : _btnDisabledPaint);
                DrawWrappedButtonText(canvas, _spireButtonRect, _localization.Get("spire_action_short"), _btnSmFont!, _spireEnabled ? TextPaint! : _btnDisabledTxtPaint!, s);
            }

            if (relocationVisible)
            {
                _relocationButtonRect = BtnRect(btnIdx++);
                canvas.DrawRoundRect(_relocationButtonRect, 6 * s, 6 * s, _relocationEnabled ? (_hoveredRelocation ? _btnHoverPaint : _btnPaint) : _btnDisabledPaint);
                SkiaTextUtils.DrawText(canvas, _localization.Get("relocation_action_short"), _relocationButtonRect.MidX, _relocationButtonRect.MidY + 4f * s, SKTextAlign.Center, _btnSmFont, _relocationEnabled ? TextPaint : _btnDisabledTxtPaint);
            }

            if (walkOfGodVisible)
            {
                _walkOfGodButtonRect = BtnRect(btnIdx++);
                canvas.DrawRoundRect(_walkOfGodButtonRect, 6 * s, 6 * s, _walkOfGodEnabled ? (_hoveredWalkOfGod ? _btnHoverPaint : _btnPaint) : _btnDisabledPaint);
                string walkOfGodLabel = $"{_localization.Get("walkofgod_action_short")} ({ascensionController.GetWalkOfGodCost()})";
                SkiaTextUtils.DrawText(canvas, walkOfGodLabel, _walkOfGodButtonRect.MidX, _walkOfGodButtonRect.MidY + 4f * s, SKTextAlign.Center, _btnSmFont, _walkOfGodEnabled ? TextPaint : _btnDisabledTxtPaint);
            }

            if (presenceOfGodVisible)
            {
                _presenceOfGodButtonRect = BtnRect(btnIdx++);
                canvas.DrawRoundRect(_presenceOfGodButtonRect, 6 * s, 6 * s, _presenceOfGodEnabled ? (_hoveredPresenceOfGod ? _btnHoverPaint : _btnPaint) : _btnDisabledPaint);
                string presenceOfGodLabel = $"{_localization.Get("presenceofgod_action_short")} ({ascensionController.GetPresenceOfGodCost()})";
                SkiaTextUtils.DrawText(canvas, presenceOfGodLabel, _presenceOfGodButtonRect.MidX, _presenceOfGodButtonRect.MidY + 4f * s, SKTextAlign.Center, _btnSmFont, _presenceOfGodEnabled ? TextPaint : _btnDisabledTxtPaint);
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
                if (!IsKeyShowable(key, civ, worldState, hasBarracks, hasArsenal, hasLabs, hasSmelters, hasWeaponSmiths, hasArmorSmiths, hasAlchimistHuts))
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
                    case AutomationRenderer.PinKeyArsenal:
                        toggleRect = DrawToggleRow(canvas, x, y, AreAllActiveNullable<Arsenal>(civ), isHovered, _localization.Get("building_arsenal_name"));
                        tooltipKey = "tooltip_toggle_arsenal";
                        break;
                    case AutomationRenderer.PinKeyLaboratory:
                        toggleRect = DrawToggleRow(canvas, x, y, AreAllActiveNullable<Laboratory>(civ), isHovered, _localization.Get("building_laboratory_name"));
                        tooltipKey = "tooltip_toggle_lab";
                        break;
                    case AutomationRenderer.PinKeySmelter:
                        toggleRect = DrawToggleRow(canvas, x, y, AreAllActiveNullable<Smelter>(civ), isHovered, _localization.Get("building_smelter_name"));
                        tooltipKey = "tooltip_toggle_smelter";
                        break;
                    case AutomationRenderer.PinKeyWeaponSmith:
                        toggleRect = DrawToggleRow(canvas, x, y, AreAllActiveNullable<WeaponSmith>(civ), isHovered, _localization.Get("building_weaponsmith_name"));
                        tooltipKey = "tooltip_toggle_weaponsmith";
                        break;
                    case AutomationRenderer.PinKeyArmorSmith:
                        toggleRect = DrawToggleRow(canvas, x, y, AreAllActiveNullable<ArmorSmith>(civ), isHovered, _localization.Get("building_armorsmith_name"));
                        tooltipKey = "tooltip_toggle_armorsmith";
                        break;
                    case AutomationRenderer.PinKeyAlchimistHut:
                        toggleRect = DrawToggleRow(canvas, x, y, AreAllActiveNullable<AlchimistHut>(civ), isHovered, _localization.Get("building_alchimisthut_name"));
                        tooltipKey = "tooltip_toggle_alchimisthut";
                        break;
                    default:
                        toggleRect = DrawAutomationToggleRow(canvas, x, y, key, worldState!, isHovered, contentW);
                        tooltipKey = GetAutomationPinDescKey(key);
                        break;
                }
                _pinnedItemRects.Add((toggleRect, key, tooltipKey));
                y += rowHeight;
            }
        }

        if (_needsScroll)
        {
            canvas.Restore();
            float scrollW  = 5f * s;
            float trackX   = panelLeft + panelWidth - scrollW - 2f * s;
            float trackTop = panelTop + 4f * s;
            float trackH   = panelHeight - 8f * s;
            DrawScrollbar(canvas, trackX, trackTop, trackH, (int)MathF.Ceiling(h), (int)MathF.Ceiling(panelHeight), (int)_scrollOffsetPx);
        }

        // Tooltips — set each frame so they persist while hovering
        float TipY(float contentY) => _needsScroll ? contentY - _scrollOffsetPx : contentY;
        if (_hoveredTrade)
        {
            _tooltipRenderer.SetTooltipLines(new[]
            {
                _localization.Get("trade_action"),
                _localization.Get("tooltip_trade")
            }, new SKPoint(_tradeButtonRect.Right, TipY(_tradeButtonRect.Top)));
        }
        else if (_hoveredRaid && raidActive)
        {
            int currentUpkeep = worldState?.AutomationSettings.RaidCurrentUpkeep ?? 0;
            _tooltipRenderer.SetTooltipLines(new[]
            {
                _localization.Get("raid_action_stop"),
                _localization.Get("tooltip_raid_active"),
                _localization.GetFormated("raid_upkeep_cost_current", currentUpkeep)
            }, new SKPoint(_raidButtonRect.Right, TipY(_raidButtonRect.Top)));
        }
        else if (_hoveredRaid)
        {
            _tooltipRenderer.SetTooltipLines(new[]
            {
                _localization.Get("raid_action"),
                _localization.Get("tooltip_raid"),
                _localization.Get("raid_upkeep_cost")
            }, new SKPoint(_raidButtonRect.Right, TipY(_raidButtonRect.Top)));
        }
        else if (_hoveredWarHerald)
        {
            _tooltipRenderer.SetTooltipLines(new[]
            {
                _localization.Get("warherald_action_short"),
                _localization.Get("tooltip_warherald")
            }, new SKPoint(_warHeraldButtonRect.Right, TipY(_warHeraldButtonRect.Top)));
        }
        else if (_hoveredLocateHero)
        {
            _tooltipRenderer.SetTooltipLines(new[]
            {
                _localization.Get("locate_hero_action"),
                _localization.Get("tooltip_locate_hero")
            }, new SKPoint(_locateHeroButtonRect.Right, TipY(_locateHeroButtonRect.Top)));
        }
        else if (_hoveredPrestige && prestigeAvail && prestigeVisible)
        {
            _tooltipRenderer.SetTooltip(_localization.Get("tooltip_prestige_next_island"), new SKPoint(_prestigeButtonRect.Right, TipY(_prestigeButtonRect.Top)));
        }
        else if (_hoveredPrestige && !prestigeAvail && prestigeVisible)
        {
            var lines = new System.Collections.Generic.List<string>();
            if (!HasPrestigeImperialPort())
                lines.Add(_localization.Get("tooltip_prestige_no_imperial_port"));
            if (prestigePoints < PrestigeController.PrestigeRequiredPoints)
                lines.Add(_localization.GetFormated("tooltip_prestige_not_enough_points", SkiaTextUtils.FormatNumber(prestigePoints), SkiaTextUtils.FormatNumber(PrestigeController.PrestigeRequiredPoints)));
            lines.Add(_localization.Get("tooltip_prestige_next_island"));
            _tooltipRenderer.SetTooltipLines(lines.ToArray(), new SKPoint(_prestigeButtonRect.Right, TipY(_prestigeButtonRect.Top)));
        }
        else if (_hoveredWonder && _wonderEnabled)
            _tooltipRenderer.SetTooltip(_localization.Get("tooltip_wonder"), new SKPoint(_wonderButtonRect.Right, TipY(_wonderButtonRect.Top)));
        else if (_hoveredWonder && !_wonderEnabled)
            _tooltipRenderer.SetTooltip(_localization.Get("tooltip_wonder_surface_only"), new SKPoint(_wonderButtonRect.Right, TipY(_wonderButtonRect.Top)));
        else if (_hoveredGreatLighthouse && _greatLighthouseEnabled)
            _tooltipRenderer.SetTooltip(_localization.Get("tooltip_great_lighthouse"), new SKPoint(_greatLighthouseButtonRect.Right, TipY(_greatLighthouseButtonRect.Top)));
        else if (_hoveredGreatLighthouse && !_greatLighthouseEnabled)
            _tooltipRenderer.SetTooltip(_localization.Get("tooltip_great_lighthouse_surface_only"), new SKPoint(_greatLighthouseButtonRect.Right, TipY(_greatLighthouseButtonRect.Top)));
        else if (_hoveredDeepestMine && !_deepestMineEnabled)
            _tooltipRenderer.SetTooltip(_localization.Get("tooltip_deepest_mine_surface_only"), new SKPoint(_deepestMineButtonRect.Right, TipY(_deepestMineButtonRect.Top)));
        else if (_hoveredDeepestMine)
            _tooltipRenderer.SetTooltip(_localization.Get("tooltip_deepest_mine"), new SKPoint(_deepestMineButtonRect.Right, TipY(_deepestMineButtonRect.Top)));
        else if (_hoveredSpire && !_spireEnabled)
            _tooltipRenderer.SetTooltip(_localization.Get("tooltip_spire_underworld_only"), new SKPoint(_spireButtonRect.Right, TipY(_spireButtonRect.Top)));
        else if (_hoveredSpire)
            _tooltipRenderer.SetTooltip(_localization.Get("tooltip_spire"), new SKPoint(_spireButtonRect.Right, TipY(_spireButtonRect.Top)));
        else if (_hoveredRelocation && _relocationEnabled)
            _tooltipRenderer.SetTooltip(_localization.Get("tooltip_relocation"), new SKPoint(_relocationButtonRect.Right, TipY(_relocationButtonRect.Top)));
        else if (_hoveredRelocation && !_relocationEnabled)
            _tooltipRenderer.SetTooltip(_localization.Get("tooltip_relocation_insufficient_resources"), new SKPoint(_relocationButtonRect.Right, TipY(_relocationButtonRect.Top)));
        else if (_hoveredWalkOfGod)
        {
            var walkOfGodLines = new System.Collections.Generic.List<string> { _localization.Get("tooltip_walkofgod") };
            int walkOfGodCost = ascensionController.GetWalkOfGodCost();
            walkOfGodLines.Add(_localization.GetFormated("tooltip_walkofgod_cost", walkOfGodCost));
            if (!ascensionController.CanUseWalkOfGod())
                walkOfGodLines.Add(_localization.Get("tooltip_walkofgod_insufficient_prestige"));
            if (ascensionController.GetWalkOfGodTargetHexes().Count == 0)
                walkOfGodLines.Add(_localization.Get("tooltip_walkofgod_no_dominion"));
            _tooltipRenderer.SetTooltipLines(walkOfGodLines.ToArray(), new SKPoint(_walkOfGodButtonRect.Right, TipY(_walkOfGodButtonRect.Top)));
        }
        else if (_hoveredPresenceOfGod)
        {
            var presenceOfGodLines = new System.Collections.Generic.List<string> { _localization.Get("tooltip_presenceofgod") };
            int presenceOfGodCost = ascensionController.GetPresenceOfGodCost();
            presenceOfGodLines.Add(_localization.GetFormated("tooltip_presenceofgod_cost", presenceOfGodCost));
            if (!ascensionController.CanUsePresenceOfGod())
                presenceOfGodLines.Add(_localization.Get("tooltip_presenceofgod_insufficient_prestige"));
            _tooltipRenderer.SetTooltipLines(presenceOfGodLines.ToArray(), new SKPoint(_presenceOfGodButtonRect.Right, TipY(_presenceOfGodButtonRect.Top)));
        }
        else if (_hoveredPinnedIndex >= 0 && _hoveredPinnedIndex < _pinnedItemRects.Count)
        {
            var (rect, _, tooltipKey) = _pinnedItemRects[_hoveredPinnedIndex];
            _tooltipRenderer.SetTooltip(_localization.Get(tooltipKey), new SKPoint(rect.Right, TipY(rect.Top)));
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

        // Rects are stored in unscrolled content coordinates (as drawn before the canvas
        // translate applied while scrolling) — convert the pointer into that same space,
        // and only while it's actually over the visible (clipped) part of the panel.
        bool inViewport = !_needsScroll || (pos.Y >= PanelBounds.Top && pos.Y <= PanelBounds.Bottom);
        float py = inViewport ? pos.Y + _scrollOffsetPx : float.NegativeInfinity;

        _hoveredTrade       = !_tradeButtonRect.IsEmpty       && _tradeButtonRect.Contains(pos.X, py);
        _hoveredPrestige    = !_prestigeButtonRect.IsEmpty    && _prestigeButtonRect.Contains(pos.X, py);
        _hoveredWonder      = !_wonderButtonRect.IsEmpty      && _wonderButtonRect.Contains(pos.X, py);
        _hoveredGreatLighthouse = !_greatLighthouseButtonRect.IsEmpty && _greatLighthouseButtonRect.Contains(pos.X, py);
        _hoveredDeepestMine = !_deepestMineButtonRect.IsEmpty && _deepestMineButtonRect.Contains(pos.X, py);
        _hoveredSpire       = !_spireButtonRect.IsEmpty       && _spireButtonRect.Contains(pos.X, py);
        _hoveredRaid        = !_raidButtonRect.IsEmpty        && _raidButtonRect.Contains(pos.X, py);
        _hoveredWarHerald   = !_warHeraldButtonRect.IsEmpty   && _warHeraldButtonRect.Contains(pos.X, py);
        _hoveredLocateHero  = !_locateHeroButtonRect.IsEmpty  && _locateHeroButtonRect.Contains(pos.X, py);
        _hoveredRelocation  = !_relocationButtonRect.IsEmpty  && _relocationButtonRect.Contains(pos.X, py);
        _hoveredWalkOfGod   = !_walkOfGodButtonRect.IsEmpty   && _walkOfGodButtonRect.Contains(pos.X, py);
        _hoveredPresenceOfGod = !_presenceOfGodButtonRect.IsEmpty && _presenceOfGodButtonRect.Contains(pos.X, py);

        _hoveredPinnedIndex = -1;
        for (int i = 0; i < _pinnedItemRects.Count; i++)
        {
            if (_pinnedItemRects[i].rect.Contains(pos.X, py)) { _hoveredPinnedIndex = i; break; }
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

        // Rects are stored in unscrolled content coordinates — convert the (already
        // viewport-clamped, thanks to the PanelBounds check above) pointer into that space.
        float py = _needsScroll ? pos.Y + _scrollOffsetPx : pos.Y;

        if (!_tradeButtonRect.IsEmpty && _tradeButtonRect.Contains(pos.X, py))
        {
            _closeAll();
            _tradeRenderer.Open();
            return true;
        }

        if (!_prestigeButtonRect.IsEmpty && _prestigeButtonRect.Contains(pos.X, py) && IsPrestigeAvailable())
        {
            _closeAll();
            _prestigeRenderer.Open();
            return true;
        }

        if (!_wonderButtonRect.IsEmpty && _wonderButtonRect.Contains(pos.X, py) && _wonderEnabled && _targetSelectionService != null)
        {
            _closeAll();
            var wonderController = _gameControllerService.MainGameController.WonderController;
            _targetSelectionService.EnterHexSelection("wonder_select_hex", wonderController.GetPlaceableHexes(),
                hex => wonderController.PlaceWonder(hex), TargetSelectionTheme.Friendly);
            return true;
        }

        if (!_greatLighthouseButtonRect.IsEmpty && _greatLighthouseButtonRect.Contains(pos.X, py) && _greatLighthouseEnabled && _targetSelectionService != null)
        {
            _closeAll();
            var greatLighthouseController = _gameControllerService.MainGameController.GreatLighthouseController;
            _targetSelectionService.EnterHexSelection("great_lighthouse_select_hex", greatLighthouseController.GetPlaceableHexes(),
                hex => greatLighthouseController.PlaceGreatLighthouse(hex), TargetSelectionTheme.Friendly);
            return true;
        }

        if (!_deepestMineButtonRect.IsEmpty && _deepestMineButtonRect.Contains(pos.X, py) && _deepestMineEnabled && _targetSelectionService != null)
        {
            _closeAll();
            var deepestMineController = _gameControllerService.MainGameController.DeepestMineController;
            _targetSelectionService.EnterHexSelection("deepest_mine_select_hex", deepestMineController.GetPlaceableHexes(),
                hex => deepestMineController.PlaceDeepestMine(hex), TargetSelectionTheme.Friendly);
            return true;
        }

        if (!_spireButtonRect.IsEmpty && _spireButtonRect.Contains(pos.X, py) && _spireEnabled && _targetSelectionService != null)
        {
            _closeAll();
            var spireController = _gameControllerService.MainGameController.CorruptionSpireController;
            var spireHexes = spireController.GetPlaceableHexes();
            var spireHexLabels = spireHexes.ToDictionary(hex => hex,
                hex => _localization.GetFormated("map_switch_corruption_level", spireController.GetCorruptionLevel(hex)));
            _targetSelectionService.EnterHexSelection("spire_select_hex", spireHexes,
                hex => spireController.PlaceCorruptionSpire(hex), TargetSelectionTheme.Friendly, spireHexLabels);
            return true;
        }

        if (!_raidButtonRect.IsEmpty && _raidButtonRect.Contains(pos.X, py))
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
                var cityTargets = militaryController.GetSelectableTargets(playerCiv);
                var monsterTargets = militaryController.GetSelectableMonsterTargets();
                if (cityTargets.Count > 0 || monsterTargets.Count > 0)
                    _targetSelectionService.EnterMixedSelection("raid_select_city",
                        cityTargets, target => militaryController.StartRaid(playerCiv, target),
                        monsterTargets, target => militaryController.StartMonsterRaid(playerCiv, target),
                        TargetSelectionTheme.Hostile);
            }
            return true;
        }

        if (!_warHeraldButtonRect.IsEmpty && _warHeraldButtonRect.Contains(pos.X, py) && _targetSelectionService != null)
        {
            var playerCiv = _gameControllerService.PlayerCivilization;
            if (playerCiv != null)
            {
                _closeAll();
                var militaryController = _gameControllerService.MainGameController.MilitaryController;
                var allyTargets = militaryController.GetWarHeraldTargets(playerCiv);
                if (allyTargets.Count > 0)
                    _targetSelectionService.EnterVertexSelection("warherald_select_target", allyTargets,
                        target => militaryController.StartWarHeraldRaid(playerCiv, target),
                        TargetSelectionTheme.Friendly);
            }
            return true;
        }

        if (!_locateHeroButtonRect.IsEmpty && _locateHeroButtonRect.Contains(pos.X, py))
        {
            var playerCiv = _gameControllerService.PlayerCivilization;
            var guildCity = playerCiv != null ? GetAdventurersGuildCity(playerCiv) : null;
            if (guildCity != null)
            {
                var activeAdventurer = _gameControllerService.CityBuildingService?.GetActiveAdventurer();
                if (activeAdventurer != null)
                {
                    var (wx, wy) = HexToWorld(activeAdventurer.Position);
                    _centerCameraOnMapPosition(activeAdventurer.Position.Z, wx, wy);
                }
                else
                {
                    var (wx, wy) = VertexToWorld(guildCity.Position);
                    _centerCameraOnMapPosition(guildCity.Position.Z, wx, wy);
                }
            }
            return true;
        }

        if (!_relocationButtonRect.IsEmpty && _relocationButtonRect.Contains(pos.X, py) && _relocationEnabled && _targetSelectionService != null)
        {
            var playerCiv = _gameControllerService.PlayerCivilization;
            if (playerCiv != null)
            {
                _closeAll();
                var cityBuilderController = _gameControllerService.MainGameController.CityBuilderController;
                var cityTargets = playerCiv.Cities.Select(c => c.Position).ToList();
                if (cityTargets.Count > 0)
                    _targetSelectionService.EnterVertexSelection("relocation_select_city", cityTargets,
                        source =>
                        {
                            var city = playerCiv.Cities.FirstOrDefault(c => c.Position.Equals(source));
                            if (city == null) return;
                            var destinations = cityBuilderController.GetRelocationTargets(city);
                            if (destinations.Count == 0) return;
                            _targetSelectionService.EnterVertexSelection("relocation_select_destination", destinations,
                                destination => cityBuilderController.RelocateCity(city, destination),
                                TargetSelectionTheme.Friendly);
                        }, TargetSelectionTheme.Friendly);
            }
            return true;
        }

        if (!_walkOfGodButtonRect.IsEmpty && _walkOfGodButtonRect.Contains(pos.X, py) && _walkOfGodEnabled && _targetSelectionService != null)
        {
            _closeAll();
            var ascensionController = _gameControllerService.MainGameController.AscensionController;
            _targetSelectionService.EnterHexSelection("walkofgod_select_hex", ascensionController.GetWalkOfGodTargetHexes(),
                hex => ascensionController.ChangeTerrainRandomly(hex), TargetSelectionTheme.Friendly);
            return true;
        }

        if (!_presenceOfGodButtonRect.IsEmpty && _presenceOfGodButtonRect.Contains(pos.X, py) && _presenceOfGodEnabled && _targetSelectionService != null)
        {
            _closeAll();
            var ascensionControllerPresence = _gameControllerService.MainGameController.AscensionController;
            _targetSelectionService.EnterHexSelection("presenceofgod_select_hex", ascensionControllerPresence.GetPresenceOfGodTargetHexes(),
                hex => ascensionControllerPresence.ApplyPresenceOfGod(hex), TargetSelectionTheme.Friendly);
            return true;
        }

        var civ = _gameControllerService.PlayerCivilization;
        var worldState = _gameControllerService.CurrentWorldState;

        for (int i = 0; i < _pinnedItemRects.Count; i++)
        {
            if (!_pinnedItemRects[i].rect.Contains(pos.X, py)) continue;
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
            case AutomationRenderer.PinKeyArsenal:       if (civ != null) ToggleAll<Arsenal>(civ);     break;
            case AutomationRenderer.PinKeyLaboratory:    if (civ != null) ToggleAll<Laboratory>(civ);  break;
            case AutomationRenderer.PinKeySmelter:       if (civ != null) ToggleAll<Smelter>(civ);     break;
            case AutomationRenderer.PinKeyWeaponSmith:   if (civ != null) ToggleAll<WeaponSmith>(civ); break;
            case AutomationRenderer.PinKeyArmorSmith:    if (civ != null) ToggleAll<ArmorSmith>(civ);  break;
            case AutomationRenderer.PinKeyAlchimistHut:  if (civ != null) ToggleAll<AlchimistHut>(civ); break;
            case AutomationRenderer.PinKeyRoad:          if (settings != null) settings.RoadAutomationEnabled = !settings.RoadAutomationEnabled;                           break;
            case AutomationRenderer.PinKeyOutpost:       if (settings != null) settings.OutpostAutomationEnabled = !settings.OutpostAutomationEnabled;                     break;
            case AutomationRenderer.PinKeyRoadUnderworld:    if (settings != null) settings.RoadAutomationEnabledUnderworld = !settings.RoadAutomationEnabledUnderworld;       break;
            case AutomationRenderer.PinKeyOutpostUnderworld: if (settings != null) settings.OutpostAutomationEnabledUnderworld = !settings.OutpostAutomationEnabledUnderworld; break;
            case AutomationRenderer.PinKeyProduction:    if (settings != null) settings.ProductionBuildingAutomationEnabled = !settings.ProductionBuildingAutomationEnabled; break;
            case AutomationRenderer.PinKeyArtisan:       if (settings != null) settings.ArtisanBuildingAutomationEnabled = !settings.ArtisanBuildingAutomationEnabled;     break;
            case AutomationRenderer.PinKeyLibrary:       if (settings != null) settings.LibraryBuildingAutomationEnabled = !settings.LibraryBuildingAutomationEnabled;     break;
            case AutomationRenderer.PinKeyMarket:        if (settings != null) settings.MarketBuildingAutomationEnabled = !settings.MarketBuildingAutomationEnabled;       break;
            case AutomationRenderer.PinKeySeaport:       if (settings != null) settings.SeaportBuildingAutomationEnabled = !settings.SeaportBuildingAutomationEnabled;     break;
            case AutomationRenderer.PinKeyMilBuildings:  if (settings != null) settings.MilitaryBuildingAutomationEnabled = !settings.MilitaryBuildingAutomationEnabled;   break;
            case AutomationRenderer.PinKeyMilReinforce:
                if (settings != null)
                {
                    settings.MilitaryReinforcementAutomationEnabled = !settings.MilitaryReinforcementAutomationEnabled;
                    if (!settings.MilitaryReinforcementAutomationEnabled && civ != null)
                        _gameControllerService.MainGameController.MilitaryController.ClearReinforcementFlows(civ);
                }
                break;
            case AutomationRenderer.PinKeyMilVendetta:
                if (settings != null)
                {
                    settings.MilitaryVendettaAutomationEnabled = !settings.MilitaryVendettaAutomationEnabled;
                    if (civ != null)
                        _gameControllerService.MainGameController.MilitaryController.StopRaid(civ);
                }
                break;
            case AutomationRenderer.PinKeyRestrictSoldierProduction:
                ToggleRestrictSoldierProductionByLayer(settings, IslandMap.SurfaceLayer);
                break;
            case AutomationRenderer.PinKeyRestrictSoldierProductionUnderworld:
                ToggleRestrictSoldierProductionByLayer(settings, LayerState.UnderworldZ);
                break;
            case AutomationRenderer.PinKeyRestrictSoldierProductionAbyss:
                ToggleRestrictSoldierProductionByLayer(settings, LayerState.AbyssZ);
                break;
        }
    }

    private static void ToggleRestrictSoldierProductionByLayer(AutomationSettings? settings, int layerZ)
    {
        if (settings == null) return;
        var byLayer = settings.RestrictSoldierProductionToFreeSoldiersByLayer;
        bool current = byLayer.TryGetValue(layerZ, out var v) && v;
        byLayer[layerZ] = !current;
    }

    private static bool IsRestrictSoldierProductionByLayer(AutomationSettings settings, int layerZ)
        => settings.RestrictSoldierProductionToFreeSoldiersByLayer.TryGetValue(layerZ, out var v) && v;

    private static bool IsKeyShowable(string key, Civilization civ,
        SettlersOfIdlestan.Model.IslandMap.WorldState? worldState,
        bool hasBarracks, bool hasArsenal, bool hasLabs, bool hasSmelters,
        bool hasWeaponSmiths, bool hasArmorSmiths, bool hasAlchimistHuts)
    {
        return key switch
        {
            AutomationRenderer.PinKeyBarracks     => hasBarracks,
            AutomationRenderer.PinKeyArsenal      => hasArsenal,
            AutomationRenderer.PinKeyLaboratory   => hasLabs,
            AutomationRenderer.PinKeySmelter      => hasSmelters,
            AutomationRenderer.PinKeyWeaponSmith  => hasWeaponSmiths,
            AutomationRenderer.PinKeyArmorSmith   => hasArmorSmiths,
            AutomationRenderer.PinKeyAlchimistHut => hasAlchimistHuts,
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
            AutomationRenderer.PinKeyRoadUnderworld    => (settings.RoadAutomationEnabledUnderworld,        "automation_road_underworld_name"),
            AutomationRenderer.PinKeyOutpostUnderworld => (settings.OutpostAutomationEnabledUnderworld,     "automation_outpost_underworld_name"),
            AutomationRenderer.PinKeyProduction   => (settings.ProductionBuildingAutomationEnabled,        "automation_production_name"),
            AutomationRenderer.PinKeyArtisan      => (settings.ArtisanBuildingAutomationEnabled,           "automation_artisan_name"),
            AutomationRenderer.PinKeyLibrary      => (settings.LibraryBuildingAutomationEnabled,           "automation_library_name"),
            AutomationRenderer.PinKeyMarket       => (settings.MarketBuildingAutomationEnabled,            "automation_market_name"),
            AutomationRenderer.PinKeySeaport      => (settings.SeaportBuildingAutomationEnabled,           "automation_seaport_name"),
            AutomationRenderer.PinKeyMilBuildings => (settings.MilitaryBuildingAutomationEnabled,          "automation_military_buildings_name"),
            AutomationRenderer.PinKeyMilReinforce => (settings.MilitaryReinforcementAutomationEnabled,     "automation_military_reinforcement_name"),
            AutomationRenderer.PinKeyMilVendetta  => (settings.MilitaryVendettaAutomationEnabled,          "automation_military_vendetta_name"),
            AutomationRenderer.PinKeyRestrictSoldierProduction =>
                (IsRestrictSoldierProductionByLayer(settings, IslandMap.SurfaceLayer), "automation_restrict_soldier_production_name"),
            AutomationRenderer.PinKeyRestrictSoldierProductionUnderworld =>
                (IsRestrictSoldierProductionByLayer(settings, LayerState.UnderworldZ), "automation_restrict_soldier_production_underworld_name"),
            AutomationRenderer.PinKeyRestrictSoldierProductionAbyss =>
                (IsRestrictSoldierProductionByLayer(settings, LayerState.AbyssZ), "automation_restrict_soldier_production_abyss_name"),
            _                                     => (false, key),
        };
        return DrawToggleRow(canvas, x, y, (bool?)value, isHovered, _localization.Get(nameKey));
    }

    /// <summary>
    /// Clé de localisation de la description (au lieu du générique "tooltip_pin_to_civ_panel", qui
    /// n'a de sens que sur la case à cocher de la pin — pas une fois l'élément déjà épinglé) pour un
    /// pin d'automatisme générique affiché via <see cref="DrawAutomationToggleRow"/>. Miroir de son
    /// switch nameKey (suffixe "_desc" au lieu de "_name").
    /// </summary>
    private static string GetAutomationPinDescKey(string key) => key switch
    {
        AutomationRenderer.PinKeyRoad         => "automation_road_desc",
        AutomationRenderer.PinKeyOutpost      => "automation_outpost_desc",
        AutomationRenderer.PinKeyRoadUnderworld    => "automation_road_underworld_desc",
        AutomationRenderer.PinKeyOutpostUnderworld => "automation_outpost_underworld_desc",
        AutomationRenderer.PinKeyProduction   => "automation_production_desc",
        AutomationRenderer.PinKeyArtisan      => "automation_artisan_desc",
        AutomationRenderer.PinKeyLibrary      => "automation_library_desc",
        AutomationRenderer.PinKeyMarket       => "automation_market_desc",
        AutomationRenderer.PinKeySeaport      => "automation_seaport_desc",
        AutomationRenderer.PinKeyMilBuildings => "automation_military_buildings_desc",
        AutomationRenderer.PinKeyMilReinforce => "automation_military_reinforcement_desc",
        AutomationRenderer.PinKeyMilVendetta  => "automation_military_vendetta_desc",
        AutomationRenderer.PinKeyRestrictSoldierProduction           => "automation_restrict_soldier_production_desc",
        AutomationRenderer.PinKeyRestrictSoldierProductionUnderworld => "automation_restrict_soldier_production_underworld_desc",
        AutomationRenderer.PinKeyRestrictSoldierProductionAbyss      => "automation_restrict_soldier_production_abyss_desc",
        _ => "tooltip_pin_to_civ_panel",
    };

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

    private bool IsGreatLighthouseVisible()
    {
        var civ = _gameControllerService.PlayerCivilization;
        if (civ == null) return false;
        try { return _gameControllerService.MainGameController.GreatLighthouseController.HasGreatLighthouseUnlocked(civ); }
        catch { return false; }
    }

    private bool CanPlaceGreatLighthouse()
    {
        var civ = _gameControllerService.PlayerCivilization;
        if (civ == null) return false;
        try { return _gameControllerService.MainGameController.GreatLighthouseController.CanPlaceGreatLighthouse(civ); }
        catch { return false; }
    }

    private bool CanPlaceDeepestMine()
    {
        var civ = _gameControllerService.PlayerCivilization;
        if (civ == null) return false;
        try { return _gameControllerService.MainGameController.DeepestMineController.CanPlaceDeepestMine(civ); }
        catch { return false; }
    }

    private bool CanPlaceSpire()
    {
        var civ = _gameControllerService.PlayerCivilization;
        if (civ == null) return false;
        try { return _gameControllerService.MainGameController.CorruptionSpireController.CanPlaceCorruptionSpire(civ); }
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

    private bool IsWarHeraldVisible()
    {
        var civ = _gameControllerService.PlayerCivilization;
        if (civ == null) return false;
        try { return _gameControllerService.MainGameController.MilitaryController.IsWarHeraldUnlocked(civ); }
        catch { return false; }
    }

    private static City? GetAdventurersGuildCity(Civilization civ)
        => civ.Cities.FirstOrDefault(c => c.Buildings.OfType<AdventurersGuild>().Any(b => b.Level > 0));

    private static (float x, float y) HexToWorld(HexCoord hex)
    {
        float sqrt3 = MathF.Sqrt(3f);
        float x = GameConstants.HexSize * sqrt3 * (hex.Q + hex.R / 2f);
        float y = GameConstants.HexSize * -3f / 2f * hex.R;
        return (x, y);
    }

    private static (float x, float y) VertexToWorld(Vertex v)
    {
        var (x1, y1) = HexToWorld(v.Hex1);
        var (x2, y2) = HexToWorld(v.Hex2);
        var (x3, y3) = HexToWorld(v.Hex3);
        return ((x1 + x2 + x3) / 3f, (y1 + y2 + y3) / 3f);
    }

    private void DrawIconButton(SKCanvas canvas, SKRect rect, SKSvg? svg, SKPaint bgPaint, float s)
    {
        canvas.DrawRoundRect(rect, 5f * s, 5f * s, bgPaint);
        var picture = svg?.Picture;
        if (picture == null || _iconTintPaint == null) return;

        float iconSize = rect.Width * 0.6f;
        float scale    = iconSize / IconSvgSize;
        _iconTintPaint.ColorFilter = SKColorFilter.CreateBlendMode(SKColors.White, SKBlendMode.SrcIn);
        canvas.Save();
        canvas.Translate(rect.MidX - iconSize / 2f, rect.MidY - iconSize / 2f);
        canvas.Scale(scale);
        canvas.SaveLayer(new SKRect(0, 0, IconSvgSize, IconSvgSize), _iconTintPaint);
        canvas.DrawPicture(picture);
        canvas.Restore();
        canvas.Restore();
    }

    private void DrawIconButtonChar(SKCanvas canvas, SKRect rect, string glyph, SKPaint bgPaint, float s)
    {
        canvas.DrawRoundRect(rect, 5f * s, 5f * s, bgPaint);
        using var font = new SKFont { Size = rect.Height * 0.6f, Typeface = SkiaFonts.Emoji };
        SkiaTextUtils.DrawText(canvas, glyph, rect.MidX, rect.MidY + font.Size * 0.35f, SKTextAlign.Center, font, TextPaint);
    }

    private bool IsRelocationVisible()
    {
        var civ = _gameControllerService.PlayerCivilization;
        if (civ == null) return false;
        try { return _gameControllerService.MainGameController.CityBuilderController.IsRelocationUnlocked(civ); }
        catch { return false; }
    }

    private bool CanAffordRelocation()
    {
        var civ = _gameControllerService.PlayerCivilization;
        if (civ == null) return false;
        try { return civ.CanPayResourceCost(CityBuilderController.RelocationCost()); }
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
        _iconTintPaint?.Dispose();
        _sectionFont?.Dispose();
        _btnFont?.Dispose();
        _btnSmFont?.Dispose();
        _labelFont?.Dispose();
        _disposed = true;
        base.Dispose();
    }
}
