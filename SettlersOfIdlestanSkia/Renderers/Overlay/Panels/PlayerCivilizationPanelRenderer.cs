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
    private const float IconBtnSize  = 24f;
    private const float IconBtnGap   = 6f;
    private const float IconSvgSize  = 64f;
    private const float ActionsHeaderHeight = 30f;

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
    private float _scrollRegionTop;

    private SKRect _tradeButtonRect    = SKRect.Empty;
    private SKRect _prestigeButtonRect = SKRect.Empty;
    private SKRect _wonderButtonRect   = SKRect.Empty;
    private SKRect _greatLighthouseButtonRect = SKRect.Empty;
    private SKRect _observatoryButtonRect = SKRect.Empty;
    private SKRect _necropolisButtonRect = SKRect.Empty;
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

    private bool _hoveredTrade, _hoveredPrestige, _hoveredWonder, _hoveredDeepestMine, _hoveredRaid, _hoveredWarHerald, _hoveredLocateHero, _hoveredSpire, _hoveredRelocation, _hoveredWalkOfGod, _hoveredPresenceOfGod, _hoveredGreatLighthouse, _hoveredObservatory, _hoveredNecropolis;
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

    // ── Visibilité et disponibilité des actions ───────────────────────────────
    //
    // Source de vérité unique, partagée par le rendu Skia, son hit-testing, l'instantané destiné
    // à l'hôte et l'exécution des actions. Ces règles étaient auparavant évaluées dans Render et
    // mémorisées dans des champs : le clic les relisait donc avec une frame de retard, et une
    // fois le panneau porté par l'hôte, Render ne tourne plus du tout et les champs restaient
    // figés sur leur dernière valeur.

    private AscensionController Ascension => _gameControllerService.MainGameController.AscensionController;

    private int CurrentLayer => _gameControllerService.CurrentWorldState?.CurrentViewedLayer ?? IslandMap.SurfaceLayer;

    private bool WonderVisible => IsWonderVisible() && CanPlaceWonder();
    private bool WonderEnabled => WonderVisible && CurrentLayer == IslandMap.SurfaceLayer;

    private bool GreatLighthouseVisible => IsGreatLighthouseVisible() && CanPlaceGreatLighthouse();
    private bool GreatLighthouseEnabled => GreatLighthouseVisible && CurrentLayer == IslandMap.SurfaceLayer;

    private bool ObservatoryVisible => CanPlaceObservatory();
    private bool ObservatoryEnabled => ObservatoryVisible && CurrentLayer == IslandMap.SurfaceLayer;

    // La Nécropole se bâtit sur des Os Divins, qui n'existent que sur les îles de l'Abysse.
    private bool NecropolisVisible => CanPlaceNecropolis();
    private bool NecropolisEnabled => NecropolisVisible && CurrentLayer == LayerState.AbyssZ;

    private bool DeepestMineVisible => CanPlaceDeepestMine();
    private bool DeepestMineEnabled => DeepestMineVisible && CurrentLayer == IslandMap.SurfaceLayer;

    private bool SpireVisible => CanPlaceSpire();
    private bool SpireEnabled => SpireVisible && CurrentLayer == LayerState.UnderworldZ;

    private bool RelocationVisible => IsRelocationVisible();
    private bool RelocationEnabled => RelocationVisible && CanAffordRelocation();

    private bool WalkOfGodVisible => Ascension.IsPowerUnlocked(AscensionPowerId.WalkOfGod);
    private bool WalkOfGodEnabled => WalkOfGodVisible
                                  && CurrentLayer == IslandMap.SurfaceLayer
                                  && Ascension.GetWalkOfGodTargetHexes().Count > 0
                                  && Ascension.CanUseWalkOfGod();

    private bool PresenceOfGodVisible => Ascension.IsPowerUnlocked(AscensionPowerId.PresenceOfGod);
    private bool PresenceOfGodEnabled => PresenceOfGodVisible
                                      && CurrentLayer == IslandMap.SurfaceLayer
                                      && Ascension.GetPresenceOfGodTargetHexes().Count > 0
                                      && Ascension.CanUsePresenceOfGod();

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
        bool wonderVisible          = WonderVisible;
        bool greatLighthouseVisible = GreatLighthouseVisible;
        bool observatoryVisible     = ObservatoryVisible;
        bool necropolisVisible      = NecropolisVisible;
        bool deepestMineVisible     = DeepestMineVisible;
        bool spireVisible           = SpireVisible;
        // Capturées une fois pour la frame : le dessin du bouton et son infobulle doivent
        // s'accorder, et certaines de ces règles parcourent la carte entière.
        bool wonderEnabled          = wonderVisible          && CurrentLayer == IslandMap.SurfaceLayer;
        bool greatLighthouseEnabled = greatLighthouseVisible && CurrentLayer == IslandMap.SurfaceLayer;
        bool observatoryEnabled     = observatoryVisible     && CurrentLayer == IslandMap.SurfaceLayer;
        bool necropolisEnabled      = necropolisVisible      && CurrentLayer == LayerState.AbyssZ;
        bool deepestMineEnabled     = deepestMineVisible     && CurrentLayer == IslandMap.SurfaceLayer;
        bool spireEnabled           = spireVisible           && CurrentLayer == LayerState.UnderworldZ;
        bool raidVisible   = IsRaidVisible();
        bool raidActive    = raidVisible && IsRaidActive();
        bool warHeraldVisible = IsWarHeraldVisible();
        var adventurersGuildCity = GetAdventurersGuildCity(civ);
        bool locateHeroVisible = adventurersGuildCity != null;
        bool relocationVisible = RelocationVisible;
        bool relocationEnabled = RelocationEnabled;
        var ascensionController = Ascension;
        bool walkOfGodVisible = WalkOfGodVisible;
        bool walkOfGodEnabled = WalkOfGodEnabled;
        bool presenceOfGodVisible = PresenceOfGodVisible;
        bool presenceOfGodEnabled = PresenceOfGodEnabled;
        bool hasBarracks     = HasBuilt<Barracks>(civ);
        bool hasArsenal      = HasBuilt<Arsenal>(civ);
        bool hasLabs         = HasBuilt<Laboratory>(civ);
        bool hasSmelters     = HasBuilt<Smelter>(civ);
        bool hasWeaponSmiths  = HasBuilt<WeaponSmith>(civ);
        bool hasArmorSmiths   = HasBuilt<ArmorSmith>(civ);
        bool hasAlchimistHuts = HasBuilt<AlchimistHut>(civ);

        var worldState = _gameControllerService.CurrentWorldState;
        var pinned = _gameControllerService.CurrentGameState?.Settings.PinnedCivPanelKeys ?? (IReadOnlySet<string>)new HashSet<string>();

        bool showActions  = tradeVisible || prestigeVisible || wonderVisible || greatLighthouseVisible || observatoryVisible || necropolisVisible || deepestMineVisible || spireVisible || raidVisible || warHeraldVisible || locateHeroVisible || relocationVisible || walkOfGodVisible || presenceOfGodVisible;
        bool showControls = pinned.Any(k => IsKeyShowable(k, civ, worldState, hasBarracks, hasArsenal, hasLabs, hasSmelters, hasWeaponSmiths, hasArmorSmiths, hasAlchimistHuts));

        // Single source of truth for the action-button count — reused for both the
        // panel height measurement and the button-grid layout so they can't drift apart.
        // Trade / Raid / War Herald / Locate Hero are drawn as small icon buttons on the title row, not in this grid.
        int actionCount = (prestigeVisible ? 1 : 0) + (wonderVisible ? 1 : 0) + (greatLighthouseVisible ? 1 : 0) + (observatoryVisible ? 1 : 0) + (necropolisVisible ? 1 : 0) + (deepestMineVisible ? 1 : 0) + (spireVisible ? 1 : 0) + (relocationVisible ? 1 : 0) + (walkOfGodVisible ? 1 : 0) + (presenceOfGodVisible ? 1 : 0);

        _tradeButtonRect = _prestigeButtonRect = _wonderButtonRect = _greatLighthouseButtonRect = _observatoryButtonRect = _necropolisButtonRect = _deepestMineButtonRect = _spireButtonRect = _raidButtonRect = _warHeraldButtonRect = _locateHeroButtonRect = _relocationButtonRect = _walkOfGodButtonRect = _presenceOfGodButtonRect = SKRect.Empty;
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

        float actionsHeaderHeight = ActionsHeaderHeight * s;

        // Measure total panel height. The Actions header (title + trade/raid/war herald/locate
        // hero icon buttons) is pinned above the scrollable area, so it's tracked separately
        // from the rest of the content, which can scroll under it.
        float fixedHeaderHeight = panelPadding + (showActions ? actionsHeaderHeight : 0f);
        float scrollableHeight  = 0f;
        if (showActions)
        {
            int actionRows  = (actionCount + 1) / 2;
            scrollableHeight += actionRows * (btnHeight + btnSpacing);
        }
        if (showActions && showControls) scrollableHeight += sepSpacing * 2 + 1f;
        if (showControls)
        {
            scrollableHeight += titleHeight;
            foreach (var k in pinned)
                if (IsKeyShowable(k, civ, worldState, hasBarracks, hasArsenal, hasLabs, hasSmelters, hasWeaponSmiths, hasArmorSmiths, hasAlchimistHuts))
                    scrollableHeight += rowHeight;
        }
        scrollableHeight += panelPadding;

        float h = fixedHeaderHeight + scrollableHeight;

        // Global elevator when the panel is taller than the available vertical space.
        float maxPanelHeight = TabsAtBottom
            ? Math.Max(0f, CanvasSize.Height - panelTop - UILayoutService.MobileTabBarHeight - 8f * s)
            : Math.Max(0f, CanvasSize.Height - panelTop - 10f * s);
        _needsScroll = h > maxPanelHeight;
        float panelHeight = _needsScroll ? maxPanelHeight : h;
        float scrollViewportHeight = Math.Max(0f, panelHeight - fixedHeaderHeight);
        _totalContentHeight = scrollableHeight;
        _viewportHeight     = scrollViewportHeight;
        _scrollOffsetPx = Math.Clamp(_scrollOffsetPx, 0f, Math.Max(0f, scrollableHeight - scrollViewportHeight));

        PanelBounds = new SKRect(panelLeft, panelTop, panelLeft + panelWidth, panelTop + panelHeight);
        DrawPanelChrome(canvas, panelLeft, panelTop, panelWidth, panelHeight, cornerRadius: 8f);

        // Collapse handle — shifted left to slightly overlap the panel
        float tabOverlap = 6f * s;
        CollapseTabRect = new SKRect(panelLeft + panelWidth - tabOverlap, tabTop, panelLeft + panelWidth - tabOverlap + collapseTabW, tabTop + collapseTabH);
        DrawCollapseTabRect(canvas, CollapseTabRect, false);

        float x = panelLeft + panelPadding;
        float y = panelTop + panelPadding;

        // Actions header (title + trade/raid/war herald/locate hero icon buttons) is drawn
        // before the scroll clip/translate below, so it stays pinned above the elevator and
        // remains reachable even when the pinned-controls list pushes the panel to scroll.
        if (showActions)
        {
            SkiaTextUtils.DrawText(canvas, _localization.Get("panel_civ_actions"), x, y + titleSize, _sectionFont, _sectionTitlePaint);

            // Small icon buttons (locate hero / war herald / raid / trade), right-aligned on the Actions title row.
            float iconBtnSize = IconBtnSize * s;
            float iconGap     = IconBtnGap * s;
            float iconY       = y + (actionsHeaderHeight - iconBtnSize) / 2f;
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

            y += actionsHeaderHeight;
        }

        _scrollRegionTop = y;

        if (_needsScroll)
        {
            canvas.Save();
            canvas.ClipRect(new SKRect(panelLeft, _scrollRegionTop, panelLeft + panelWidth, panelTop + panelHeight));
            canvas.Translate(0, -_scrollOffsetPx);
        }

        if (showActions)
        {
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
                canvas.DrawRoundRect(_wonderButtonRect, 6 * s, 6 * s, wonderEnabled ? (_hoveredWonder ? _btnHoverPaint : _btnPaint) : _btnDisabledPaint);
                SkiaTextUtils.DrawText(canvas, _localization.Get("wonder_action_short"), _wonderButtonRect.MidX, _wonderButtonRect.MidY + 4f * s, SKTextAlign.Center, _btnSmFont, wonderEnabled ? TextPaint : _btnDisabledTxtPaint);
            }

            if (greatLighthouseVisible)
            {
                _greatLighthouseButtonRect = BtnRect(btnIdx++);
                canvas.DrawRoundRect(_greatLighthouseButtonRect, 6 * s, 6 * s, greatLighthouseEnabled ? (_hoveredGreatLighthouse ? _btnHoverPaint : _btnPaint) : _btnDisabledPaint);
                SkiaTextUtils.DrawText(canvas, _localization.Get("great_lighthouse_action_short"), _greatLighthouseButtonRect.MidX, _greatLighthouseButtonRect.MidY + 4f * s, SKTextAlign.Center, _btnSmFont, greatLighthouseEnabled ? TextPaint : _btnDisabledTxtPaint);
            }

            if (observatoryVisible)
            {
                _observatoryButtonRect = BtnRect(btnIdx++, allowFullWidth: false);
                canvas.DrawRoundRect(_observatoryButtonRect, 6 * s, 6 * s, observatoryEnabled ? (_hoveredObservatory ? _btnHoverPaint : _btnPaint) : _btnDisabledPaint);
                DrawWrappedButtonText(canvas, _observatoryButtonRect, _localization.Get("observatory_action_short"), _btnSmFont!, observatoryEnabled ? TextPaint! : _btnDisabledTxtPaint!, s);
            }

            if (necropolisVisible)
            {
                _necropolisButtonRect = BtnRect(btnIdx++, allowFullWidth: false);
                canvas.DrawRoundRect(_necropolisButtonRect, 6 * s, 6 * s, necropolisEnabled ? (_hoveredNecropolis ? _btnHoverPaint : _btnPaint) : _btnDisabledPaint);
                DrawWrappedButtonText(canvas, _necropolisButtonRect, _localization.Get("necropolis_action_short"), _btnSmFont!, necropolisEnabled ? TextPaint! : _btnDisabledTxtPaint!, s);
            }

            if (deepestMineVisible)
            {
                _deepestMineButtonRect = BtnRect(btnIdx++, allowFullWidth: false);
                canvas.DrawRoundRect(_deepestMineButtonRect, 6 * s, 6 * s, deepestMineEnabled ? (_hoveredDeepestMine ? _btnHoverPaint : _btnPaint) : _btnDisabledPaint);
                DrawWrappedButtonText(canvas, _deepestMineButtonRect, _localization.Get("deepest_mine_action_short"), _btnSmFont!, deepestMineEnabled ? TextPaint! : _btnDisabledTxtPaint!, s);
            }

            if (spireVisible)
            {
                _spireButtonRect = BtnRect(btnIdx++, allowFullWidth: false);
                canvas.DrawRoundRect(_spireButtonRect, 6 * s, 6 * s, spireEnabled ? (_hoveredSpire ? _btnHoverPaint : _btnPaint) : _btnDisabledPaint);
                DrawWrappedButtonText(canvas, _spireButtonRect, _localization.Get("spire_action_short"), _btnSmFont!, spireEnabled ? TextPaint! : _btnDisabledTxtPaint!, s);
            }

            if (relocationVisible)
            {
                _relocationButtonRect = BtnRect(btnIdx++);
                canvas.DrawRoundRect(_relocationButtonRect, 6 * s, 6 * s, relocationEnabled ? (_hoveredRelocation ? _btnHoverPaint : _btnPaint) : _btnDisabledPaint);
                SkiaTextUtils.DrawText(canvas, _localization.Get("relocation_action_short"), _relocationButtonRect.MidX, _relocationButtonRect.MidY + 4f * s, SKTextAlign.Center, _btnSmFont, relocationEnabled ? TextPaint : _btnDisabledTxtPaint);
            }

            if (walkOfGodVisible)
            {
                _walkOfGodButtonRect = BtnRect(btnIdx++);
                canvas.DrawRoundRect(_walkOfGodButtonRect, 6 * s, 6 * s, walkOfGodEnabled ? (_hoveredWalkOfGod ? _btnHoverPaint : _btnPaint) : _btnDisabledPaint);
                // Le premier usage depuis le dernier prestige est gratuit : afficher « (0) » sur le
                // bouton se lit comme une erreur, pas comme une bonne nouvelle.
                int walkOfGodButtonCost = ascensionController.GetWalkOfGodCost();
                string walkOfGodLabel = walkOfGodButtonCost == 0
                    ? $"{_localization.Get("walkofgod_action_short")} ({_localization.Get("cost_free")})"
                    : $"{_localization.Get("walkofgod_action_short")} ({walkOfGodButtonCost})";
                SkiaTextUtils.DrawText(canvas, walkOfGodLabel, _walkOfGodButtonRect.MidX, _walkOfGodButtonRect.MidY + 4f * s, SKTextAlign.Center, _btnSmFont, walkOfGodEnabled ? TextPaint : _btnDisabledTxtPaint);
            }

            if (presenceOfGodVisible)
            {
                _presenceOfGodButtonRect = BtnRect(btnIdx++);
                canvas.DrawRoundRect(_presenceOfGodButtonRect, 6 * s, 6 * s, presenceOfGodEnabled ? (_hoveredPresenceOfGod ? _btnHoverPaint : _btnPaint) : _btnDisabledPaint);
                string presenceOfGodLabel = $"{_localization.Get("presenceofgod_action_short")} ({ascensionController.GetPresenceOfGodCost()})";
                SkiaTextUtils.DrawText(canvas, presenceOfGodLabel, _presenceOfGodButtonRect.MidX, _presenceOfGodButtonRect.MidY + 4f * s, SKTextAlign.Center, _btnSmFont, presenceOfGodEnabled ? TextPaint : _btnDisabledTxtPaint);
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

                var (value, nameKey, tooltipKey) = ResolvePinnedToggle(key, civ, worldState);
                var toggleRect = DrawToggleRow(canvas, x, y, value, isHovered, _localization.Get(nameKey));
                _pinnedItemRects.Add((toggleRect, key, tooltipKey));
                y += rowHeight;
            }
        }

        if (_needsScroll)
        {
            canvas.Restore();
            float scrollW  = 5f * s;
            float trackX   = panelLeft + panelWidth - scrollW - 2f * s;
            float trackTop = _scrollRegionTop + 4f * s;
            float trackH   = scrollViewportHeight - 8f * s;
            DrawScrollbar(canvas, trackX, trackTop, trackH, (int)MathF.Ceiling(scrollableHeight), (int)MathF.Ceiling(scrollViewportHeight), (int)_scrollOffsetPx);
        }

        // Tooltips — set each frame so they persist while hovering
        // The Actions header icon buttons (trade/raid/war herald/locate hero) sit above the
        // scroll clip/translate, so their rects are already in screen space; everything else
        // was drawn inside the scrolled block and needs the offset added back.
        float TipY(float contentY) => _needsScroll ? contentY - _scrollOffsetPx : contentY;
        if (_hoveredTrade)
        {
            _tooltipRenderer.SetTooltipLines(new[]
            {
                _localization.Get("trade_action"),
                _localization.Get("tooltip_trade")
            }, new SKPoint(_tradeButtonRect.Right, _tradeButtonRect.Top));
        }
        else if (_hoveredRaid && raidActive)
        {
            int currentUpkeep = worldState?.AutomationSettings.RaidCurrentUpkeep ?? 0;
            _tooltipRenderer.SetTooltipLines(new[]
            {
                _localization.Get("raid_action_stop"),
                _localization.Get("tooltip_raid_active"),
                _localization.GetFormated("raid_upkeep_cost_current", currentUpkeep)
            }, new SKPoint(_raidButtonRect.Right, _raidButtonRect.Top));
        }
        else if (_hoveredRaid)
        {
            _tooltipRenderer.SetTooltipLines(new[]
            {
                _localization.Get("raid_action"),
                _localization.Get("tooltip_raid"),
                _localization.Get("raid_upkeep_cost")
            }, new SKPoint(_raidButtonRect.Right, _raidButtonRect.Top));
        }
        else if (_hoveredWarHerald)
        {
            _tooltipRenderer.SetTooltipLines(new[]
            {
                _localization.Get("warherald_action_short"),
                _localization.Get("tooltip_warherald")
            }, new SKPoint(_warHeraldButtonRect.Right, _warHeraldButtonRect.Top));
        }
        else if (_hoveredLocateHero)
        {
            _tooltipRenderer.SetTooltipLines(new[]
            {
                _localization.Get("locate_hero_action"),
                _localization.Get("tooltip_locate_hero")
            }, new SKPoint(_locateHeroButtonRect.Right, _locateHeroButtonRect.Top));
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
        else if (_hoveredWonder && wonderEnabled)
            _tooltipRenderer.SetTooltip(_localization.Get("tooltip_wonder"), new SKPoint(_wonderButtonRect.Right, TipY(_wonderButtonRect.Top)));
        else if (_hoveredWonder && !wonderEnabled)
            _tooltipRenderer.SetTooltip(_localization.Get("tooltip_wonder_surface_only"), new SKPoint(_wonderButtonRect.Right, TipY(_wonderButtonRect.Top)));
        else if (_hoveredGreatLighthouse && greatLighthouseEnabled)
            _tooltipRenderer.SetTooltip(_localization.Get("tooltip_great_lighthouse"), new SKPoint(_greatLighthouseButtonRect.Right, TipY(_greatLighthouseButtonRect.Top)));
        else if (_hoveredGreatLighthouse && !greatLighthouseEnabled)
            _tooltipRenderer.SetTooltip(_localization.Get("tooltip_great_lighthouse_surface_only"), new SKPoint(_greatLighthouseButtonRect.Right, TipY(_greatLighthouseButtonRect.Top)));
        else if (_hoveredObservatory && observatoryEnabled)
            _tooltipRenderer.SetTooltip(_localization.Get("tooltip_observatory"), new SKPoint(_observatoryButtonRect.Right, TipY(_observatoryButtonRect.Top)));
        else if (_hoveredObservatory && !observatoryEnabled)
            _tooltipRenderer.SetTooltip(_localization.Get("tooltip_observatory_surface_only"), new SKPoint(_observatoryButtonRect.Right, TipY(_observatoryButtonRect.Top)));
        else if (_hoveredNecropolis && necropolisEnabled)
            _tooltipRenderer.SetTooltip(_localization.Get("tooltip_necropolis"), new SKPoint(_necropolisButtonRect.Right, TipY(_necropolisButtonRect.Top)));
        else if (_hoveredNecropolis && !necropolisEnabled)
            _tooltipRenderer.SetTooltip(_localization.Get("tooltip_necropolis_abyss_only"), new SKPoint(_necropolisButtonRect.Right, TipY(_necropolisButtonRect.Top)));
        else if (_hoveredDeepestMine && !deepestMineEnabled)
            _tooltipRenderer.SetTooltip(_localization.Get("tooltip_deepest_mine_surface_only"), new SKPoint(_deepestMineButtonRect.Right, TipY(_deepestMineButtonRect.Top)));
        else if (_hoveredDeepestMine)
            _tooltipRenderer.SetTooltip(_localization.Get("tooltip_deepest_mine"), new SKPoint(_deepestMineButtonRect.Right, TipY(_deepestMineButtonRect.Top)));
        else if (_hoveredSpire && !spireEnabled)
            _tooltipRenderer.SetTooltip(_localization.Get("tooltip_spire_underworld_only"), new SKPoint(_spireButtonRect.Right, TipY(_spireButtonRect.Top)));
        else if (_hoveredSpire)
            _tooltipRenderer.SetTooltip(_localization.Get("tooltip_spire"), new SKPoint(_spireButtonRect.Right, TipY(_spireButtonRect.Top)));
        else if (_hoveredRelocation && relocationEnabled)
            _tooltipRenderer.SetTooltip(_localization.Get("tooltip_relocation"), new SKPoint(_relocationButtonRect.Right, TipY(_relocationButtonRect.Top)));
        else if (_hoveredRelocation && !relocationEnabled)
            _tooltipRenderer.SetTooltip(_localization.Get("tooltip_relocation_insufficient_resources"), new SKPoint(_relocationButtonRect.Right, TipY(_relocationButtonRect.Top)));
        else if (_hoveredWalkOfGod)
        {
            var walkOfGodLines = new System.Collections.Generic.List<string> { _localization.Get("tooltip_walkofgod") };
            // Ce que la marche va produire dépend de la race jouée — voir AscensionController.ApplyWalkOfGod.
            if (ascensionController.FavouredTerrain is { } favouredTerrain)
                walkOfGodLines.Add(_localization.GetFormated("tooltip_walkofgod_favoured_terrain",
                    _localization.Get($"hex_tooltip_terrain_{favouredTerrain.ToString().ToLowerInvariant()}")));
            int walkOfGodCost = ascensionController.GetWalkOfGodCost();
            walkOfGodLines.Add(walkOfGodCost == 0
                ? _localization.Get("tooltip_walkofgod_cost_free")
                : _localization.GetFormated("tooltip_walkofgod_cost", walkOfGodCost));
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

        // The Actions header icon buttons (trade/raid/war herald/locate hero) are pinned above
        // the scroll clip/translate, so their rects are in plain screen space.
        _hoveredTrade       = !_tradeButtonRect.IsEmpty       && _tradeButtonRect.Contains(pos.X, pos.Y);
        _hoveredRaid        = !_raidButtonRect.IsEmpty        && _raidButtonRect.Contains(pos.X, pos.Y);
        _hoveredWarHerald   = !_warHeraldButtonRect.IsEmpty   && _warHeraldButtonRect.Contains(pos.X, pos.Y);
        _hoveredLocateHero  = !_locateHeroButtonRect.IsEmpty  && _locateHeroButtonRect.Contains(pos.X, pos.Y);

        // Everything else is stored in unscrolled content coordinates (as drawn before the
        // canvas translate applied while scrolling) — convert the pointer into that same space,
        // and only while it's actually over the visible (clipped) scrollable part of the panel.
        bool inViewport = !_needsScroll || (pos.Y >= _scrollRegionTop && pos.Y <= PanelBounds.Bottom);
        float py = inViewport ? pos.Y + _scrollOffsetPx : float.NegativeInfinity;

        _hoveredPrestige    = !_prestigeButtonRect.IsEmpty    && _prestigeButtonRect.Contains(pos.X, py);
        _hoveredWonder      = !_wonderButtonRect.IsEmpty      && _wonderButtonRect.Contains(pos.X, py);
        _hoveredGreatLighthouse = !_greatLighthouseButtonRect.IsEmpty && _greatLighthouseButtonRect.Contains(pos.X, py);
        _hoveredObservatory = !_observatoryButtonRect.IsEmpty && _observatoryButtonRect.Contains(pos.X, py);
        _hoveredNecropolis  = !_necropolisButtonRect.IsEmpty  && _necropolisButtonRect.Contains(pos.X, py);
        _hoveredDeepestMine = !_deepestMineButtonRect.IsEmpty && _deepestMineButtonRect.Contains(pos.X, py);
        _hoveredSpire       = !_spireButtonRect.IsEmpty       && _spireButtonRect.Contains(pos.X, py);
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

        // The Actions header icon buttons (trade/raid/war herald/locate hero) are pinned above
        // the scroll clip/translate, so they're hit-tested against the raw pointer position.
        if (!_tradeButtonRect.IsEmpty && _tradeButtonRect.Contains(pos.X, pos.Y)) { DoTrade(); return true; }

        // Everything else below is stored in unscrolled content coordinates — convert the
        // (already viewport-clamped, thanks to the PanelBounds check above) pointer into that space.
        float py = _needsScroll ? pos.Y + _scrollOffsetPx : pos.Y;

        if (!_prestigeButtonRect.IsEmpty        && _prestigeButtonRect.Contains(pos.X, py))        { DoPrestige();        return true; }
        if (!_wonderButtonRect.IsEmpty          && _wonderButtonRect.Contains(pos.X, py))          { DoWonder();          return true; }
        if (!_greatLighthouseButtonRect.IsEmpty && _greatLighthouseButtonRect.Contains(pos.X, py)) { DoGreatLighthouse(); return true; }
        if (!_observatoryButtonRect.IsEmpty      && _observatoryButtonRect.Contains(pos.X, py))      { DoObservatory();     return true; }
        if (!_necropolisButtonRect.IsEmpty       && _necropolisButtonRect.Contains(pos.X, py))       { DoNecropolis();      return true; }
        if (!_deepestMineButtonRect.IsEmpty     && _deepestMineButtonRect.Contains(pos.X, py))     { DoDeepestMine();     return true; }
        if (!_spireButtonRect.IsEmpty           && _spireButtonRect.Contains(pos.X, py))           { DoSpire();           return true; }
        if (!_raidButtonRect.IsEmpty            && _raidButtonRect.Contains(pos.X, pos.Y))         { DoRaid();            return true; }
        if (!_warHeraldButtonRect.IsEmpty       && _warHeraldButtonRect.Contains(pos.X, pos.Y))    { DoWarHerald();       return true; }
        if (!_locateHeroButtonRect.IsEmpty      && _locateHeroButtonRect.Contains(pos.X, pos.Y))   { DoLocateHero();      return true; }
        if (!_relocationButtonRect.IsEmpty      && _relocationButtonRect.Contains(pos.X, py))      { DoRelocation();      return true; }
        if (!_walkOfGodButtonRect.IsEmpty       && _walkOfGodButtonRect.Contains(pos.X, py))       { DoWalkOfGod();       return true; }
        if (!_presenceOfGodButtonRect.IsEmpty   && _presenceOfGodButtonRect.Contains(pos.X, py))   { DoPresenceOfGod();   return true; }

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

    // ── Actions ───────────────────────────────────────────────────────────────
    //
    // Chaque action porte sa propre garde de disponibilité plutôt que de la laisser à l'appelant :
    // elles sont déclenchées aussi bien par le hit-testing Skia que par le panneau porté par
    // l'hôte, et une garde dupliquée finirait par diverger entre les deux.

    private void DoTrade()
    {
        if (!IsTradeVisible()) return;
        _closeAll();
        _tradeRenderer.Open();
    }

    private void DoPrestige()
    {
        if (!IsPrestigeAvailable()) return;
        _closeAll();
        _prestigeRenderer.Open();
    }

    private void DoWonder()
    {
        if (!WonderEnabled || _targetSelectionService == null) return;
        _closeAll();
        var wonderController = _gameControllerService.MainGameController.WonderController;
        _targetSelectionService.EnterHexSelection("wonder_select_hex", wonderController.GetPlaceableHexes(),
            hex => wonderController.PlaceWonder(hex), TargetSelectionTheme.Friendly);
    }

    private void DoGreatLighthouse()
    {
        if (!GreatLighthouseEnabled || _targetSelectionService == null) return;
        _closeAll();
        var greatLighthouseController = _gameControllerService.MainGameController.GreatLighthouseController;
        _targetSelectionService.EnterHexSelection("great_lighthouse_select_hex", greatLighthouseController.GetPlaceableHexes(),
            hex => greatLighthouseController.PlaceGreatLighthouse(hex), TargetSelectionTheme.Friendly);
    }

    private void DoObservatory()
    {
        if (!ObservatoryEnabled || _targetSelectionService == null) return;
        _closeAll();
        var observatoryController = _gameControllerService.MainGameController.ObservatoryController;
        _targetSelectionService.EnterHexSelection("observatory_select_hex", observatoryController.GetPlaceableHexes(),
            hex => observatoryController.PlaceObservatory(hex), TargetSelectionTheme.Friendly);
    }

    private void DoNecropolis()
    {
        if (!NecropolisEnabled || _targetSelectionService == null) return;
        _closeAll();
        var necropolisController = _gameControllerService.MainGameController.NecropolisController;
        _targetSelectionService.EnterHexSelection("necropolis_select_hex", necropolisController.GetPlaceableHexes(),
            hex => necropolisController.PlaceNecropolis(hex), TargetSelectionTheme.Friendly);
    }

    private void DoDeepestMine()
    {
        if (!DeepestMineEnabled || _targetSelectionService == null) return;
        _closeAll();
        var deepestMineController = _gameControllerService.MainGameController.DeepestMineController;
        _targetSelectionService.EnterHexSelection("deepest_mine_select_hex", deepestMineController.GetPlaceableHexes(),
            hex => deepestMineController.PlaceDeepestMine(hex), TargetSelectionTheme.Friendly);
    }

    private void DoSpire()
    {
        if (!SpireEnabled || _targetSelectionService == null) return;
        _closeAll();
        var spireController = _gameControllerService.MainGameController.CorruptionSpireController;
        var spireHexes = spireController.GetPlaceableHexes();
        var spireHexLabels = spireHexes.ToDictionary(hex => hex,
            hex => _localization.GetFormated("map_switch_corruption_level", spireController.GetCorruptionLevel(hex)));
        _targetSelectionService.EnterHexSelection("spire_select_hex", spireHexes,
            hex => spireController.PlaceCorruptionSpire(hex), TargetSelectionTheme.Friendly, spireHexLabels);
    }

    /// <summary>Lance un pillage, ou arrête celui en cours si le bouton est déjà actif.</summary>
    private void DoRaid()
    {
        if (!IsRaidVisible()) return;
        var playerCiv = _gameControllerService.PlayerCivilization;
        if (playerCiv == null) return;

        if (IsRaidActive())
        {
            _gameControllerService.MainGameController.MilitaryController.StopRaid(playerCiv);
            return;
        }

        if (_targetSelectionService == null) return;
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

    private void DoWarHerald()
    {
        if (!IsWarHeraldVisible() || _targetSelectionService == null) return;
        var playerCiv = _gameControllerService.PlayerCivilization;
        if (playerCiv == null) return;

        _closeAll();
        var militaryController = _gameControllerService.MainGameController.MilitaryController;
        var allyTargets = militaryController.GetWarHeraldTargets(playerCiv);
        if (allyTargets.Count > 0)
            _targetSelectionService.EnterVertexSelection("warherald_select_target", allyTargets,
                target => militaryController.StartWarHeraldRaid(playerCiv, target),
                TargetSelectionTheme.Friendly);
    }

    /// <summary>Recentre la caméra sur l'aventurier en vadrouille, ou à défaut sur sa guilde.</summary>
    private void DoLocateHero()
    {
        var playerCiv = _gameControllerService.PlayerCivilization;
        var guildCity = playerCiv != null ? GetAdventurersGuildCity(playerCiv) : null;
        if (guildCity == null) return;

        var activeAdventurer = _gameControllerService.CityBuildingService?.GetActiveAdventurer();
        if (activeAdventurer != null)
        {
            var (ax, ay) = HexToWorld(activeAdventurer.Position);
            _centerCameraOnMapPosition(activeAdventurer.Position.Z, ax, ay);
            return;
        }

        var (wx, wy) = VertexToWorld(guildCity.Position);
        _centerCameraOnMapPosition(guildCity.Position.Z, wx, wy);
    }

    private void DoRelocation()
    {
        if (!RelocationEnabled || _targetSelectionService == null) return;
        var playerCiv = _gameControllerService.PlayerCivilization;
        if (playerCiv == null) return;

        _closeAll();
        var cityBuilderController = _gameControllerService.MainGameController.CityBuilderController;
        var cityTargets = playerCiv.Cities.Select(c => c.Position).ToList();
        if (cityTargets.Count == 0) return;

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

    private void DoWalkOfGod()
    {
        if (!WalkOfGodEnabled || _targetSelectionService == null) return;
        _closeAll();
        var ascension = Ascension;
        _targetSelectionService.EnterHexSelection("walkofgod_select_hex", ascension.GetWalkOfGodTargetHexes(),
            hex => ascension.ApplyWalkOfGod(hex), TargetSelectionTheme.Friendly);
    }

    private void DoPresenceOfGod()
    {
        if (!PresenceOfGodEnabled || _targetSelectionService == null) return;
        _closeAll();
        var ascension = Ascension;
        _targetSelectionService.EnterHexSelection("presenceofgod_select_hex", ascension.GetPresenceOfGodTargetHexes(),
            hex => ascension.ApplyPresenceOfGod(hex), TargetSelectionTheme.Friendly);
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
            case AutomationRenderer.PinKeyTownHall:      if (settings != null) settings.TownHallAutomationEnabled = !settings.TownHallAutomationEnabled;                   break;
            case AutomationRenderer.PinKeyGrandTemple:   if (settings != null) settings.TempleAutomationEnabled = !settings.TempleAutomationEnabled;                       break;
            case AutomationRenderer.PinKeyMithrilMine:   if (settings != null) settings.MithrilMineBuildingAutomationEnabled = !settings.MithrilMineBuildingAutomationEnabled;   break;
            case AutomationRenderer.PinKeyArcaneTower:   if (settings != null) settings.ArcaneTowerBuildingAutomationEnabled = !settings.ArcaneTowerBuildingAutomationEnabled;   break;
            case AutomationRenderer.PinKeyMonumentInvestment: if (settings != null) settings.MonumentInvestmentAutomationEnabled = !settings.MonumentInvestmentAutomationEnabled; break;
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

    /// <summary>
    /// Etat, libellé et infobulle d'une bascule épinglée. Source de vérité unique, partagée par
    /// le rendu Skia et l'instantané destiné à l'hôte : les deux affichages ne peuvent pas
    /// diverger sur ce qu'une bascule montre.
    /// </summary>
    private (bool? Value, string NameKey, string TooltipKey) ResolvePinnedToggle(
        string key, Civilization civ, SettlersOfIdlestan.Model.IslandMap.WorldState? worldState)
    {
        switch (key)
        {
            case AutomationRenderer.PinKeyBarracks:     return (AreAllActiveNullable<Barracks>(civ),     "building_barracks_name",     "tooltip_toggle_barracks");
            case AutomationRenderer.PinKeyArsenal:      return (AreAllActiveNullable<Arsenal>(civ),      "building_arsenal_name",      "tooltip_toggle_arsenal");
            case AutomationRenderer.PinKeyLaboratory:   return (AreAllActiveNullable<Laboratory>(civ),   "building_laboratory_name",   "tooltip_toggle_lab");
            case AutomationRenderer.PinKeySmelter:      return (AreAllActiveNullable<Smelter>(civ),      "building_smelter_name",      "tooltip_toggle_smelter");
            case AutomationRenderer.PinKeyWeaponSmith:  return (AreAllActiveNullable<WeaponSmith>(civ),  "building_weaponsmith_name",  "tooltip_toggle_weaponsmith");
            case AutomationRenderer.PinKeyArmorSmith:   return (AreAllActiveNullable<ArmorSmith>(civ),   "building_armorsmith_name",   "tooltip_toggle_armorsmith");
            case AutomationRenderer.PinKeyAlchimistHut: return (AreAllActiveNullable<AlchimistHut>(civ), "building_alchimisthut_name", "tooltip_toggle_alchimisthut");
        }

        var settings = worldState?.AutomationSettings;
        if (settings == null) return (false, key, GetAutomationPinDescKey(key));

        // Seule la valeur depend d'un switch : le libelle vient de la table des racines, pour
        // qu'un automatisme ne puisse pas etre bascule ici sans y etre nomme.
        bool value = key switch
        {
            AutomationRenderer.PinKeyTownHall     => settings.TownHallAutomationEnabled,
            AutomationRenderer.PinKeyGrandTemple  => settings.TempleAutomationEnabled,
            AutomationRenderer.PinKeyMithrilMine  => settings.MithrilMineBuildingAutomationEnabled,
            AutomationRenderer.PinKeyArcaneTower  => settings.ArcaneTowerBuildingAutomationEnabled,
            AutomationRenderer.PinKeyMonumentInvestment => settings.MonumentInvestmentAutomationEnabled,
            AutomationRenderer.PinKeyRoad         => settings.RoadAutomationEnabled,
            AutomationRenderer.PinKeyOutpost      => settings.OutpostAutomationEnabled,
            AutomationRenderer.PinKeyRoadUnderworld    => settings.RoadAutomationEnabledUnderworld,
            AutomationRenderer.PinKeyOutpostUnderworld => settings.OutpostAutomationEnabledUnderworld,
            AutomationRenderer.PinKeyProduction   => settings.ProductionBuildingAutomationEnabled,
            AutomationRenderer.PinKeyArtisan      => settings.ArtisanBuildingAutomationEnabled,
            AutomationRenderer.PinKeyLibrary      => settings.LibraryBuildingAutomationEnabled,
            AutomationRenderer.PinKeyMarket       => settings.MarketBuildingAutomationEnabled,
            AutomationRenderer.PinKeySeaport      => settings.SeaportBuildingAutomationEnabled,
            AutomationRenderer.PinKeyMilBuildings => settings.MilitaryBuildingAutomationEnabled,
            AutomationRenderer.PinKeyMilReinforce => settings.MilitaryReinforcementAutomationEnabled,
            AutomationRenderer.PinKeyMilVendetta  => settings.MilitaryVendettaAutomationEnabled,
            AutomationRenderer.PinKeyRestrictSoldierProduction =>
                IsRestrictSoldierProductionByLayer(settings, IslandMap.SurfaceLayer),
            AutomationRenderer.PinKeyRestrictSoldierProductionUnderworld =>
                IsRestrictSoldierProductionByLayer(settings, LayerState.UnderworldZ),
            AutomationRenderer.PinKeyRestrictSoldierProductionAbyss =>
                IsRestrictSoldierProductionByLayer(settings, LayerState.AbyssZ),
            _ => false,
        };

        string nameKey = AutomationPinLocalizationRoots.TryGetValue(key, out var root) ? $"{root}_name" : key;

        return (value, nameKey, GetAutomationPinDescKey(key));
    }

    /// <summary>
    /// Racine de clé de localisation de chaque automatisme épinglable : le libellé est
    /// <c>{racine}_name</c> et la description <c>{racine}_desc</c>.
    ///
    /// Une seule table plutôt que deux switch en miroir — ils divergeaient déjà, et c'est ainsi
    /// que cinq automatismes (hôtel de ville, grand temple, mine de mithril, tour des arcanes,
    /// investissement monument) se retrouvaient épinglables mais sans libellé ici.
    ///
    /// Toute case à cocher d'épinglage ajoutée dans <see cref="AutomationRenderer"/> doit y
    /// figurer, sinon le panneau afficherait la clé brute. Un test le vérifie.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> AutomationPinLocalizationRoots =
        new Dictionary<string, string>
        {
            [AutomationRenderer.PinKeyTownHall]           = "automation_townhall",
            [AutomationRenderer.PinKeyGrandTemple]        = "automation_grandtemple",
            [AutomationRenderer.PinKeyMithrilMine]        = "automation_mithrilmine",
            [AutomationRenderer.PinKeyArcaneTower]        = "automation_arcanetower",
            [AutomationRenderer.PinKeyMonumentInvestment] = "automation_monument_investment",
            [AutomationRenderer.PinKeyRoad]               = "automation_road",
            [AutomationRenderer.PinKeyOutpost]            = "automation_outpost",
            [AutomationRenderer.PinKeyRoadUnderworld]     = "automation_road_underworld",
            [AutomationRenderer.PinKeyOutpostUnderworld]  = "automation_outpost_underworld",
            [AutomationRenderer.PinKeyProduction]         = "automation_production",
            [AutomationRenderer.PinKeyArtisan]            = "automation_artisan",
            [AutomationRenderer.PinKeyLibrary]            = "automation_library",
            [AutomationRenderer.PinKeyMarket]             = "automation_market",
            [AutomationRenderer.PinKeySeaport]            = "automation_seaport",
            [AutomationRenderer.PinKeyMilBuildings]       = "automation_military_buildings",
            [AutomationRenderer.PinKeyMilReinforce]       = "automation_military_reinforcement",
            [AutomationRenderer.PinKeyMilVendetta]        = "automation_military_vendetta",
            [AutomationRenderer.PinKeyRestrictSoldierProduction]           = "automation_restrict_soldier_production",
            [AutomationRenderer.PinKeyRestrictSoldierProductionUnderworld] = "automation_restrict_soldier_production_underworld",
            [AutomationRenderer.PinKeyRestrictSoldierProductionAbyss]      = "automation_restrict_soldier_production_abyss",
        };

    /// <summary>
    /// Clé de description d'un automatisme épinglé — et non le générique
    /// "tooltip_pin_to_civ_panel", qui n'a de sens que sur la case à cocher de l'épinglage,
    /// pas une fois l'élément déjà épinglé.
    /// </summary>
    private static string GetAutomationPinDescKey(string key) =>
        AutomationPinLocalizationRoots.TryGetValue(key, out var root) ? $"{root}_desc" : "tooltip_pin_to_civ_panel";

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

    private bool CanPlaceObservatory()
    {
        var civ = _gameControllerService.PlayerCivilization;
        if (civ == null) return false;
        try { return _gameControllerService.MainGameController.ObservatoryController.CanPlaceObservatory(civ); }
        catch { return false; }
    }

    private bool CanPlaceNecropolis()
    {
        var civ = _gameControllerService.PlayerCivilization;
        if (civ == null) return false;
        try { return _gameControllerService.MainGameController.NecropolisController.CanPlaceNecropolis(civ); }
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

    // ── Pont vers l'hôte Avalonia ─────────────────────────────────────────────

    /// <summary>
    /// Instantané du panneau pour une vue portée par l'hôte. Reprend les règles de visibilité,
    /// de disponibilité, les libellés et les infobulles de <see cref="Render"/> — les mêmes
    /// prédicats, pas une réécriture — afin que les deux affichages ne divergent pas.
    ///
    /// Les infobulles sont ici du texte simple : la vue les rend en ToolTip Avalonia natif,
    /// contrairement à celle du panneau ville qui reste dessinée en Skia.
    /// </summary>
    public CivPanelSnapshot GetSnapshot()
    {
        var civ = _gameControllerService.PlayerCivilization;
        if (civ == null) return CivPanelSnapshot.Hidden;

        var worldState = _gameControllerService.CurrentWorldState;
        var pinned = _gameControllerService.CurrentGameState?.Settings.PinnedCivPanelKeys
                     ?? (IReadOnlySet<string>)new HashSet<string>();

        var iconActions = new List<CivActionSnapshot>();
        var actions     = new List<CivActionSnapshot>();

        // ── En-tête : boutons icône, dans l'ordre d'affichage de gauche à droite ──

        if (IsTradeVisible())
            iconActions.Add(new CivActionSnapshot(
                CivPanelSnapshot.KeyTrade, _localization.Get("trade_action"), true, false,
                IconName: null, Glyph: "💰",
                TooltipLines: [_localization.Get("trade_action"), _localization.Get("tooltip_trade")]));

        if (IsRaidVisible())
        {
            bool raidActive = IsRaidActive();
            var raidTooltip = raidActive
                ? new List<string>
                {
                    _localization.Get("raid_action_stop"),
                    _localization.Get("tooltip_raid_active"),
                    _localization.GetFormated("raid_upkeep_cost_current", worldState?.AutomationSettings.RaidCurrentUpkeep ?? 0),
                }
                : new List<string>
                {
                    _localization.Get("raid_action"),
                    _localization.Get("tooltip_raid"),
                    _localization.Get("raid_upkeep_cost"),
                };
            iconActions.Add(new CivActionSnapshot(
                CivPanelSnapshot.KeyRaid,
                _localization.Get(raidActive ? "raid_action_stop" : "raid_action"),
                IsEnabled: true, IsHighlighted: raidActive,
                IconName: "Resources.icons.military.attack.svg", Glyph: null,
                TooltipLines: raidTooltip));
        }

        if (IsWarHeraldVisible())
            iconActions.Add(new CivActionSnapshot(
                CivPanelSnapshot.KeyWarHerald, _localization.Get("warherald_action_short"), true, false,
                IconName: "Resources.icons.military.defense.svg", Glyph: null,
                TooltipLines: [_localization.Get("warherald_action_short"), _localization.Get("tooltip_warherald")]));

        if (GetAdventurersGuildCity(civ) != null)
            iconActions.Add(new CivActionSnapshot(
                CivPanelSnapshot.KeyLocateHero, _localization.Get("locate_hero_action"), true, false,
                IconName: "Resources.icons.military.hero-armor.svg", Glyph: null,
                TooltipLines: [_localization.Get("locate_hero_action"), _localization.Get("tooltip_locate_hero")]));

        // ── Grille d'actions, dans l'ordre du rendu Skia ──

        if (IsPrestigeVisible())
        {
            bool available = IsPrestigeAvailable();
            int points = GetPrestigePoints();

            var tooltip = new List<string>();
            if (!available)
            {
                if (!HasPrestigeImperialPort())
                    tooltip.Add(_localization.Get("tooltip_prestige_no_imperial_port"));
                if (points < PrestigeController.PrestigeRequiredPoints)
                    tooltip.Add(_localization.GetFormated("tooltip_prestige_not_enough_points",
                        SkiaTextUtils.FormatNumber(points), SkiaTextUtils.FormatNumber(PrestigeController.PrestigeRequiredPoints)));
            }
            tooltip.Add(_localization.Get("tooltip_prestige_next_island"));

            actions.Add(new CivActionSnapshot(
                CivPanelSnapshot.KeyPrestige,
                $"{_localization.Get("prestige_action")} (+{SkiaTextUtils.FormatNumber(points)})",
                available, false, null, null, tooltip));
        }

        if (WonderVisible)
            actions.Add(SimpleAction(CivPanelSnapshot.KeyWonder, "wonder_action_short",
                WonderEnabled, "tooltip_wonder", "tooltip_wonder_surface_only"));

        if (GreatLighthouseVisible)
            actions.Add(SimpleAction(CivPanelSnapshot.KeyGreatLighthouse, "great_lighthouse_action_short",
                GreatLighthouseEnabled, "tooltip_great_lighthouse", "tooltip_great_lighthouse_surface_only"));

        if (ObservatoryVisible)
            actions.Add(SimpleAction(CivPanelSnapshot.KeyObservatory, "observatory_action_short",
                ObservatoryEnabled, "tooltip_observatory", "tooltip_observatory_surface_only"));

        if (NecropolisVisible)
            actions.Add(SimpleAction(CivPanelSnapshot.KeyNecropolis, "necropolis_action_short",
                NecropolisEnabled, "tooltip_necropolis", "tooltip_necropolis_abyss_only"));

        if (DeepestMineVisible)
            actions.Add(SimpleAction(CivPanelSnapshot.KeyDeepestMine, "deepest_mine_action_short",
                DeepestMineEnabled, "tooltip_deepest_mine", "tooltip_deepest_mine_surface_only"));

        if (SpireVisible)
            actions.Add(SimpleAction(CivPanelSnapshot.KeySpire, "spire_action_short",
                SpireEnabled, "tooltip_spire", "tooltip_spire_underworld_only"));

        if (RelocationVisible)
            actions.Add(SimpleAction(CivPanelSnapshot.KeyRelocation, "relocation_action_short",
                RelocationEnabled, "tooltip_relocation", "tooltip_relocation_insufficient_resources"));

        if (WalkOfGodVisible)
        {
            int cost = Ascension.GetWalkOfGodCost();
            var tooltip = new List<string>
            {
                _localization.Get("tooltip_walkofgod"),
                _localization.GetFormated("tooltip_walkofgod_cost", cost),
            };
            if (!Ascension.CanUseWalkOfGod())
                tooltip.Add(_localization.Get("tooltip_walkofgod_insufficient_prestige"));
            if (Ascension.GetWalkOfGodTargetHexes().Count == 0)
                tooltip.Add(_localization.Get("tooltip_walkofgod_no_dominion"));

            actions.Add(new CivActionSnapshot(
                CivPanelSnapshot.KeyWalkOfGod,
                $"{_localization.Get("walkofgod_action_short")} ({cost})",
                WalkOfGodEnabled, false, null, null, tooltip));
        }

        if (PresenceOfGodVisible)
        {
            int cost = Ascension.GetPresenceOfGodCost();
            var tooltip = new List<string>
            {
                _localization.Get("tooltip_presenceofgod"),
                _localization.GetFormated("tooltip_presenceofgod_cost", cost),
            };
            if (!Ascension.CanUsePresenceOfGod())
                tooltip.Add(_localization.Get("tooltip_presenceofgod_insufficient_prestige"));

            actions.Add(new CivActionSnapshot(
                CivPanelSnapshot.KeyPresenceOfGod,
                $"{_localization.Get("presenceofgod_action_short")} ({cost})",
                PresenceOfGodEnabled, false, null, null, tooltip));
        }

        // ── Bascules épinglées ──

        bool hasBarracks      = HasBuilt<Barracks>(civ);
        bool hasArsenal       = HasBuilt<Arsenal>(civ);
        bool hasLabs          = HasBuilt<Laboratory>(civ);
        bool hasSmelters      = HasBuilt<Smelter>(civ);
        bool hasWeaponSmiths  = HasBuilt<WeaponSmith>(civ);
        bool hasArmorSmiths   = HasBuilt<ArmorSmith>(civ);
        bool hasAlchimistHuts = HasBuilt<AlchimistHut>(civ);

        var toggles = new List<CivToggleSnapshot>();
        foreach (var key in pinned)
        {
            if (!IsKeyShowable(key, civ, worldState, hasBarracks, hasArsenal, hasLabs, hasSmelters,
                    hasWeaponSmiths, hasArmorSmiths, hasAlchimistHuts))
                continue;

            var (value, nameKey, tooltipKey) = ResolvePinnedToggle(key, civ, worldState);
            toggles.Add(new CivToggleSnapshot(key, _localization.Get(nameKey), value, _localization.Get(tooltipKey)));
        }

        // Même règle que Render : sans action ni bascule, le panneau n'a rien à montrer.
        if (iconActions.Count == 0 && actions.Count == 0 && toggles.Count == 0)
            return CivPanelSnapshot.Hidden;

        return new CivPanelSnapshot(
            IsVisible: true,
            IsCollapsed: Collapsed,
            ActionsTitle: _localization.Get("panel_civ_actions"),
            ControlsTitle: _localization.Get("panel_civ_controls"),
            IconActions: iconActions,
            Actions: actions,
            Toggles: toggles);
    }

    /// Action de la grille dont l'infobulle se réduit à une raison unique selon sa disponibilité.
    private CivActionSnapshot SimpleAction(string key, string labelKey, bool enabled,
        string enabledTooltipKey, string disabledTooltipKey) =>
        new(key, _localization.Get(labelKey), enabled, false, null, null,
            [_localization.Get(enabled ? enabledTooltipKey : disabledTooltipKey)]);

    /// <summary>Déclenche une action du panneau depuis une vue portée par l'hôte.</summary>
    public void ExecuteActionFromHost(string key)
    {
        switch (key)
        {
            case CivPanelSnapshot.KeyTrade:           DoTrade();           break;
            case CivPanelSnapshot.KeyPrestige:        DoPrestige();        break;
            case CivPanelSnapshot.KeyWonder:          DoWonder();          break;
            case CivPanelSnapshot.KeyGreatLighthouse: DoGreatLighthouse(); break;
            case CivPanelSnapshot.KeyObservatory:     DoObservatory();     break;
            case CivPanelSnapshot.KeyNecropolis:      DoNecropolis();      break;
            case CivPanelSnapshot.KeyDeepestMine:     DoDeepestMine();     break;
            case CivPanelSnapshot.KeySpire:           DoSpire();           break;
            case CivPanelSnapshot.KeyRaid:            DoRaid();            break;
            case CivPanelSnapshot.KeyWarHerald:       DoWarHerald();       break;
            case CivPanelSnapshot.KeyLocateHero:      DoLocateHero();      break;
            case CivPanelSnapshot.KeyRelocation:      DoRelocation();      break;
            case CivPanelSnapshot.KeyWalkOfGod:       DoWalkOfGod();       break;
            case CivPanelSnapshot.KeyPresenceOfGod:   DoPresenceOfGod();   break;
        }
    }

    /// <summary>Bascule un élément épinglé depuis une vue portée par l'hôte.</summary>
    public void ToggleFromHost(string key) =>
        HandlePinnedToggle(key, _gameControllerService.PlayerCivilization, _gameControllerService.CurrentWorldState);

    /// <summary>
    /// Replie/déplie le panneau depuis une vue portée par l'hôte. Le repli reste stocké ici :
    /// en disposition mobile, <c>OverlayRenderer</c> replie ce panneau quand un panneau latéral
    /// droit s'ouvre, et cette règle doit continuer de s'imposer à la vue.
    /// </summary>
    public void SetCollapsedFromHost(bool collapsed)
    {
        bool wasCollapsed = Collapsed;
        Collapsed = collapsed;
        if (wasCollapsed && !collapsed) OnExpanded?.Invoke();
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
