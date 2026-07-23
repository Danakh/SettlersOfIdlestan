using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestanSkia.Services.Localization;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Services;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using TechId = SettlersOfIdlestan.Model.Civilization.TechnologyId;

namespace SettlersOfIdlestanSkia.Renderers.Overlay.Tabs;

public sealed class AutomationRenderer : IDisposable
{
    private const float Padding = 20f;
    private const float ToggleWidth = 60f;
    private const float ToggleHeight = 28f;
    private const float RowMinHeight = 54f;
    private const float SummaryHeight = 20f;
    private const float SummaryLineHeight = 16f;
    private const float RowSpacing = 8f;
    private const float TextOffsetX = ToggleWidth + 14f;
    private const float DescRightPad = 12f;
    private const float PinCheckboxSize   = 14f;
    private const float PinCheckboxMargin = 7f;

    // Clés de pin pour PinnedToCivPanel
    internal const string PinKeyRoad          = "Road";
    internal const string PinKeyOutpost       = "Outpost";
    internal const string PinKeyRoadUnderworld    = "RoadUnderworld";
    internal const string PinKeyOutpostUnderworld = "OutpostUnderworld";
    internal const string PinKeyTownHall      = "TownHall";
    internal const string PinKeyProduction    = "Production";
    internal const string PinKeyArtisan       = "Artisan";
    internal const string PinKeyLibrary       = "Library";
    internal const string PinKeyMarket        = "Market";
    internal const string PinKeySeaport        = "Seaport";
    internal const string PinKeyMilBuildings  = "MilitaryBuildings";
    internal const string PinKeyMilReinforce  = "MilitaryReinforcement";
    internal const string PinKeyMilPatrol     = "MilitaryPatrol";
    internal const string PinKeyMilVendetta   = "MilitaryVendetta";
    internal const string PinKeyMonumentInvestment = "MonumentInvestment";
    internal const string PinKeyBarracks      = "Barracks";
    internal const string PinKeyLaboratory    = "Laboratory";
    internal const string PinKeySmelter       = "Smelter";
    internal const string PinKeyArsenal       = "Arsenal";
    internal const string PinKeyWeaponSmith   = "WeaponSmith";
    internal const string PinKeyArmorSmith    = "ArmorSmith";
    internal const string PinKeyAlchimistHut  = "AlchimistHut";

    private readonly GameControllerService _gameControllerService;
    private readonly LocalizationService _localization;
    private readonly UILayoutService _uiLayout;

    private SKSize _canvasSize;
    private bool _disposed;

    private SKRect _roadToggleRect = SKRect.Empty;
    private SKRect _outpostToggleRect = SKRect.Empty;
    private SKRect _roadUnderworldToggleRect = SKRect.Empty;
    private SKRect _outpostUnderworldToggleRect = SKRect.Empty;
    private SKRect _townHallToggleRect = SKRect.Empty;
    private SKRect _productionToggleRect = SKRect.Empty;
    private SKRect _artisanToggleRect = SKRect.Empty;
    private SKRect _libraryToggleRect = SKRect.Empty;
    private SKRect _marketToggleRect = SKRect.Empty;
    private SKRect _seaportToggleRect = SKRect.Empty;
    private SKRect _militaryBuildingsToggleRect = SKRect.Empty;
    private SKRect _militaryReinforcementToggleRect = SKRect.Empty;
    private SKRect _militaryPatrolToggleRect = SKRect.Empty;
    private SKRect _militaryVendettaToggleRect = SKRect.Empty;
    private SKRect _monumentInvestmentToggleRect = SKRect.Empty;
    private SKRect _barracksToggleRect     = SKRect.Empty;
    private SKRect _labToggleRect          = SKRect.Empty;
    private SKRect _smelterToggleRect      = SKRect.Empty;
    private SKRect _arsenalToggleRect      = SKRect.Empty;
    private SKRect _weaponSmithToggleRect  = SKRect.Empty;
    private SKRect _armorSmithToggleRect   = SKRect.Empty;
    private SKRect _alchimistHutToggleRect = SKRect.Empty;
    private bool _hoveredRoadToggle;
    private bool _hoveredOutpostToggle;
    private bool _hoveredRoadUnderworldToggle;
    private bool _hoveredOutpostUnderworldToggle;
    private bool _hoveredTownHallToggle;
    private bool _hoveredProductionToggle;
    private bool _hoveredArtisanToggle;
    private bool _hoveredLibraryToggle;
    private bool _hoveredMarketToggle;
    private bool _hoveredSeaportToggle;
    private bool _hoveredMilitaryBuildingsToggle;
    private bool _hoveredMilitaryReinforcementToggle;
    private bool _hoveredMilitaryPatrolToggle;
    private bool _hoveredMilitaryVendettaToggle;
    private bool _hoveredMonumentInvestmentToggle;
    private bool _hoveredBarracksToggle;
    private bool _hoveredLabToggle;
    private bool _hoveredSmelterToggle;
    private bool _hoveredArsenalToggle;
    private bool _hoveredWeaponSmithToggle;
    private bool _hoveredArmorSmithToggle;
    private bool _hoveredAlchimistHutToggle;

    private readonly List<(SKRect rect, string note)> _hoverableCards = new();
    private string? _hoveredNote;
    private SKPoint _mousePosition;

    private float _scrollOffsetPx        = 0f;
    private float _totalContentH         = 0f;
    private float _viewportH             = 0f;
    private bool  _isDraggingScrollbar   = false;
    private float _scrollDragStartY      = 0f;
    private float _scrollDragStartOffset = 0f;
    private SKRect _scrollTrackRect      = SKRect.Empty;
    private SKRect _scrollThumbRect      = SKRect.Empty;

    private readonly List<(SKRect rect, string key)> _pinCheckboxes = new();
    private string? _hoveredPinKey;

    private readonly SKPaint _bgPaint              = new() { Color = new SKColor(18, 18, 24, 240), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _cardPaint            = new() { Color = new SKColor(30, 30, 40, 220), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _cardBorderPaint      = new() { Color = new SKColor(60, 60, 80), StrokeWidth = 1f, Style = SKPaintStyle.Stroke, IsAntialias = true };
    private readonly SKPaint _namePaint            = new() { Color = new SKColor(230, 230, 240), IsAntialias = true };
    private readonly SKPaint _descPaint            = new() { Color = new SKColor(150, 150, 165), IsAntialias = true };
    private readonly SKPaint _notePaint            = new() { Color = new SKColor(100, 160, 130), IsAntialias = true };
    private readonly SKPaint _tooltipBgPaint       = new() { Color = new SKColor(22, 22, 32, 245), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _tooltipBorderPaint   = new() { Color = new SKColor(80, 120, 100), StrokeWidth = 1f, Style = SKPaintStyle.Stroke, IsAntialias = true };
    private readonly SKPaint _mutedPaint           = new() { Color = new SKColor(110, 110, 125), IsAntialias = true };
    private readonly SKPaint _accentPaint          = new() { Color = new SKColor(255, 215, 0), IsAntialias = true };
    private readonly SKPaint _summaryBuiltPaint    = new() { Color = new SKColor(120, 175, 120), IsAntialias = true };
    private readonly SKPaint _summaryEmptyPaint    = new() { Color = new SKColor(95, 95, 108), IsAntialias = true };
    private readonly SKPaint _summaryDividerPaint  = new() { Color = new SKColor(70, 70, 88), StrokeWidth = 1f, Style = SKPaintStyle.Stroke, IsAntialias = true };
    private readonly SKPaint _scrollTrackPaint     = new() { Color = new SKColor(50, 50, 65, 200), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _scrollThumbPaint     = new() { Color = new SKColor(130, 130, 165, 210), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _pinBorderPaint       = new() { Color = new SKColor(100, 100, 120), StrokeWidth = 1f, Style = SKPaintStyle.Stroke, IsAntialias = true };
    private readonly SKPaint _pinHoverPaint        = new() { Color = new SKColor(180, 180, 220), StrokeWidth = 1f, Style = SKPaintStyle.Stroke, IsAntialias = true };
    private readonly SKPaint _pinCheckedPaint      = new() { Color = new SKColor(255, 215, 0, 210), Style = SKPaintStyle.Fill, IsAntialias = true };

    private readonly SKFont _headerFont  = new() { Size = 17, Typeface = SkiaFonts.Bold };
    private readonly SKFont _nameFont    = new() { Size = 13, Typeface = SkiaFonts.Bold };
    private readonly SKFont _descFont    = new() { Size = 11, Typeface = SkiaFonts.Regular };
    private readonly SKFont _summaryFont = new() { Size = 10, Typeface = SkiaFonts.Regular };

    private static readonly BuildingType[] TownHallTypes   = [BuildingType.TownHall];
    private static readonly BuildingType[] ProductionTypes = [BuildingType.Sawmill, BuildingType.Brickworks, BuildingType.Quarry, BuildingType.Mill, BuildingType.MushroomFarm];
    private static readonly BuildingType[] ArtisanTypes    = [BuildingType.Forge, BuildingType.Warehouse, BuildingType.GlassWorks, BuildingType.Smelter];
    private static readonly BuildingType[] LibraryTypes    = [BuildingType.Library, BuildingType.Laboratory];
    private static readonly BuildingType[] MarketTypes     = [BuildingType.Market];
    private static readonly BuildingType[] SeaportTypes    = [BuildingType.Seaport];
    private static readonly BuildingType[] MilitaryTypes   = [BuildingType.Barracks, BuildingType.Garrison, BuildingType.Arsenal, BuildingType.WeaponSmith, BuildingType.ArmorSmith];

    public AutomationRenderer(GameControllerService gameControllerService, LocalizationService localization, UILayoutService uiLayout)
    {
        _gameControllerService = gameControllerService;
        _localization = localization;
        _uiLayout = uiLayout;
    }

    public void Initialize(SKSize canvasSize) => _canvasSize = canvasSize;

    public void RenderAutomationPage(SKCanvas canvas, GameRenderContext context)
    {
        if (_disposed) return;
        if (context.GameState is not MainGameState) return;

        _hoverableCards.Clear();
        _pinCheckboxes.Clear();

        float topBar = _uiLayout.SecondRowBottom;
        canvas.DrawRect(new SKRect(0, topBar, _canvasSize.Width, _canvasSize.Height), _bgPaint);

        _viewportH = _canvasSize.Height - topBar;
        float maxScroll = Math.Max(0, _totalContentH - _viewportH);
        _scrollOffsetPx = Math.Clamp(_scrollOffsetPx, 0, maxScroll);
        bool needsScroll = _totalContentH > _viewportH + 1f;

        canvas.Save();
        canvas.ClipRect(new SKRect(0, topBar, _canvasSize.Width, _canvasSize.Height));
        canvas.Translate(0, -_scrollOffsetPx);

        float contentWidth = Math.Min(640f, _canvasSize.Width - Padding * 2);
        float x = (_canvasSize.Width - contentWidth) / 2;
        float y = topBar + Padding;

        SkiaTextUtils.DrawText(canvas, _localization.Get("automation_title"), x, y + 14, _headerFont, _accentPaint);
        y += 32f;

        var civ = _gameControllerService.PlayerCivilization;
        var WorldState = _gameControllerService.CurrentWorldState;
        if (civ == null || WorldState == null)
        {
            canvas.Restore();
            return;
        }

        BuildersGuild? buildersGuild = null;
        HarvestersGuild? harvestersGuild = null;
        ArtisansGuild? artisansGuild = null;
        Academy? academy = null;
        TraderGuild? traderGuild = null;
        ImperialPort? imperialPort = null;
        WarRoom? warRoom = null;
        foreach (var city in civ.Cities)
        {
            buildersGuild ??= city.Buildings.OfType<BuildersGuild>().FirstOrDefault();
            harvestersGuild ??= city.Buildings.OfType<HarvestersGuild>().FirstOrDefault();
            artisansGuild ??= city.Buildings.OfType<ArtisansGuild>().FirstOrDefault();
            academy ??= city.Buildings.OfType<Academy>().FirstOrDefault();
            traderGuild ??= city.Buildings.OfType<TraderGuild>().FirstOrDefault();
            imperialPort ??= city.Buildings.OfType<ImperialPort>().FirstOrDefault();
            warRoom ??= city.Buildings.OfType<WarRoom>().FirstOrDefault();
            if (buildersGuild != null && harvestersGuild != null && artisansGuild != null && academy != null && traderGuild != null && imperialPort != null && warRoom != null) break;
        }
        bool hasSeaportAutomation = civ.ModifierAggregator.HasModifier(Modifier.ECategory.UNLOCK_SEAPORT_AUTOMATION);
        bool hasBuildersGuildUnderworld = civ.ModifierAggregator.HasModifier(Modifier.ECategory.UNLOCK_BUILDERS_GUILD_UNDERWORLD);

        const float ColGap = 12f;
        float colWidth = (contentWidth - ColGap) / 2f;
        float leftX = x;
        float rightX = x + colWidth + ColGap;
        float startY = y;

        float rowH;
        var pinned = _gameControllerService.CurrentGameState!.Settings.PinnedCivPanelKeys;

        // === Colonne gauche : constructions automatiques ===
        float leftY = startY;
        SkiaTextUtils.DrawText(canvas, _localization.Get("automation_header_buildings"), leftX, leftY + 12, _nameFont, _accentPaint);
        leftY += 20f;

        bool roadUnlocked = buildersGuild != null && buildersGuild.Level >= 1;
        if (roadUnlocked)
            (_roadToggleRect, rowH) = DrawAutomationRow(canvas, leftX, leftY, colWidth, WorldState.AutomationSettings.RoadAutomationEnabled, _hoveredRoadToggle, _localization.Get("automation_road_name"), _localization.Get("automation_road_desc"), _localization.Get("automation_road_note"), pinKey: PinKeyRoad, isPinHovered: _hoveredPinKey == PinKeyRoad, isPinned: pinned.Contains(PinKeyRoad));
        else
        {
            _roadToggleRect = SKRect.Empty;
            rowH = DrawLockedRow(canvas, leftX, leftY, colWidth, _localization.Get("automation_road_name"), _localization.Get("automation_road_locked"));
        }
        leftY += rowH + RowSpacing;

        bool roadUnderworldUnlocked = roadUnlocked && hasBuildersGuildUnderworld;
        if (roadUnderworldUnlocked)
            (_roadUnderworldToggleRect, rowH) = DrawAutomationRow(canvas, leftX, leftY, colWidth, WorldState.AutomationSettings.RoadAutomationEnabledUnderworld, _hoveredRoadUnderworldToggle, _localization.Get("automation_road_underworld_name"), _localization.Get("automation_road_underworld_desc"), _localization.Get("automation_road_underworld_note"), pinKey: PinKeyRoadUnderworld, isPinHovered: _hoveredPinKey == PinKeyRoadUnderworld, isPinned: pinned.Contains(PinKeyRoadUnderworld));
        else
        {
            _roadUnderworldToggleRect = SKRect.Empty;
            rowH = DrawLockedRow(canvas, leftX, leftY, colWidth, _localization.Get("automation_road_underworld_name"), _localization.Get("automation_road_underworld_locked"));
        }
        leftY += rowH + RowSpacing;

        bool outpostUnlocked = buildersGuild != null && buildersGuild.Level >= 4;
        if (outpostUnlocked)
            (_outpostToggleRect, rowH) = DrawAutomationRow(canvas, leftX, leftY, colWidth, WorldState.AutomationSettings.OutpostAutomationEnabled, _hoveredOutpostToggle, _localization.Get("automation_outpost_name"), _localization.Get("automation_outpost_desc"), _localization.Get("automation_outpost_note"), pinKey: PinKeyOutpost, isPinHovered: _hoveredPinKey == PinKeyOutpost, isPinned: pinned.Contains(PinKeyOutpost));
        else
        {
            _outpostToggleRect = SKRect.Empty;
            rowH = DrawLockedRow(canvas, leftX, leftY, colWidth, _localization.Get("automation_outpost_name"), _localization.Get("automation_outpost_locked"));
        }
        leftY += rowH + RowSpacing;

        bool outpostUnderworldUnlocked = outpostUnlocked && hasBuildersGuildUnderworld;
        if (outpostUnderworldUnlocked)
            (_outpostUnderworldToggleRect, rowH) = DrawAutomationRow(canvas, leftX, leftY, colWidth, WorldState.AutomationSettings.OutpostAutomationEnabledUnderworld, _hoveredOutpostUnderworldToggle, _localization.Get("automation_outpost_underworld_name"), _localization.Get("automation_outpost_underworld_desc"), _localization.Get("automation_outpost_underworld_note"), pinKey: PinKeyOutpostUnderworld, isPinHovered: _hoveredPinKey == PinKeyOutpostUnderworld, isPinned: pinned.Contains(PinKeyOutpostUnderworld));
        else
        {
            _outpostUnderworldToggleRect = SKRect.Empty;
            rowH = DrawLockedRow(canvas, leftX, leftY, colWidth, _localization.Get("automation_outpost_underworld_name"), _localization.Get("automation_outpost_underworld_locked"));
        }
        leftY += rowH + RowSpacing;

        bool townHallUnlocked = buildersGuild != null && buildersGuild.Level >= 1;
        if (townHallUnlocked)
            (_townHallToggleRect, rowH) = DrawAutomationRow(canvas, leftX, leftY, colWidth, WorldState.AutomationSettings.TownHallAutomationEnabled, _hoveredTownHallToggle, _localization.Get("automation_townhall_name"), _localization.Get("automation_townhall_desc"), _localization.Get("automation_townhall_note"), civ.Cities, TownHallTypes, PinKeyTownHall, _hoveredPinKey == PinKeyTownHall, pinned.Contains(PinKeyTownHall));
        else
        {
            _townHallToggleRect = SKRect.Empty;
            rowH = DrawLockedRow(canvas, leftX, leftY, colWidth, _localization.Get("automation_townhall_name"), _localization.Get("automation_townhall_locked"));
        }
        leftY += rowH + RowSpacing;

        bool productionUnlocked = harvestersGuild != null && harvestersGuild.Level >= 1;
        if (productionUnlocked)
            (_productionToggleRect, rowH) = DrawAutomationRow(canvas, leftX, leftY, colWidth, WorldState.AutomationSettings.ProductionBuildingAutomationEnabled, _hoveredProductionToggle, _localization.Get("automation_production_name"), _localization.Get("automation_production_desc"), _localization.Get("automation_production_note"), civ.Cities, ProductionTypes, PinKeyProduction, _hoveredPinKey == PinKeyProduction, pinned.Contains(PinKeyProduction));
        else
        {
            _productionToggleRect = SKRect.Empty;
            rowH = DrawLockedRow(canvas, leftX, leftY, colWidth, _localization.Get("automation_production_name"), _localization.Get("automation_production_locked"));
        }
        leftY += rowH + RowSpacing;

        bool artisanUnlocked = artisansGuild != null && artisansGuild.Level >= 1;
        if (artisanUnlocked)
            (_artisanToggleRect, rowH) = DrawAutomationRow(canvas, leftX, leftY, colWidth, WorldState.AutomationSettings.ArtisanBuildingAutomationEnabled, _hoveredArtisanToggle, _localization.Get("automation_artisan_name"), _localization.Get("automation_artisan_desc"), _localization.Get("automation_artisan_note"), civ.Cities, ArtisanTypes, PinKeyArtisan, _hoveredPinKey == PinKeyArtisan, pinned.Contains(PinKeyArtisan));
        else
        {
            _artisanToggleRect = SKRect.Empty;
            rowH = DrawLockedRow(canvas, leftX, leftY, colWidth, _localization.Get("automation_artisan_name"), _localization.Get("automation_artisan_locked"));
        }
        leftY += rowH + RowSpacing;

        bool libraryUnlocked = academy != null && academy.Level >= 1;
        if (libraryUnlocked)
            (_libraryToggleRect, rowH) = DrawAutomationRow(canvas, leftX, leftY, colWidth, WorldState.AutomationSettings.LibraryBuildingAutomationEnabled, _hoveredLibraryToggle, _localization.Get("automation_library_name"), _localization.Get("automation_library_desc"), _localization.Get("automation_library_note"), civ.Cities, LibraryTypes, PinKeyLibrary, _hoveredPinKey == PinKeyLibrary, pinned.Contains(PinKeyLibrary));
        else
        {
            _libraryToggleRect = SKRect.Empty;
            rowH = DrawLockedRow(canvas, leftX, leftY, colWidth, _localization.Get("automation_library_name"), _localization.Get("automation_library_locked"));
        }
        leftY += rowH + RowSpacing;

        bool marketUnlocked = traderGuild != null && traderGuild.Level >= 1;
        if (marketUnlocked)
            (_marketToggleRect, rowH) = DrawAutomationRow(canvas, leftX, leftY, colWidth, WorldState.AutomationSettings.MarketBuildingAutomationEnabled, _hoveredMarketToggle, _localization.Get("automation_market_name"), _localization.Get("automation_market_desc"), _localization.Get("automation_market_note"), civ.Cities, MarketTypes, PinKeyMarket, _hoveredPinKey == PinKeyMarket, pinned.Contains(PinKeyMarket));
        else
        {
            _marketToggleRect = SKRect.Empty;
            rowH = DrawLockedRow(canvas, leftX, leftY, colWidth, _localization.Get("automation_market_name"), _localization.Get("automation_market_locked"));
        }
        leftY += rowH + RowSpacing;

        if (hasSeaportAutomation && imperialPort != null)
            (_seaportToggleRect, rowH) = DrawAutomationRow(canvas, leftX, leftY, colWidth, WorldState.AutomationSettings.SeaportBuildingAutomationEnabled, _hoveredSeaportToggle, _localization.Get("automation_seaport_name"), _localization.Get("automation_seaport_desc"), null, civ.Cities, SeaportTypes, PinKeySeaport, _hoveredPinKey == PinKeySeaport, pinned.Contains(PinKeySeaport));
        else
        {
            _seaportToggleRect = SKRect.Empty;
            rowH = DrawLockedRow(canvas, leftX, leftY, colWidth, _localization.Get("automation_seaport_name"), _localization.Get("automation_seaport_locked"));
        }
        leftY += rowH + RowSpacing;

        bool militaryBuildingsUnlocked = warRoom != null && warRoom.Level >= 1;
        if (militaryBuildingsUnlocked)
            (_militaryBuildingsToggleRect, rowH) = DrawAutomationRow(canvas, leftX, leftY, colWidth, WorldState.AutomationSettings.MilitaryBuildingAutomationEnabled, _hoveredMilitaryBuildingsToggle, _localization.Get("automation_military_buildings_name"), _localization.Get("automation_military_buildings_desc"), _localization.Get("automation_military_buildings_note"), civ.Cities, MilitaryTypes, PinKeyMilBuildings, _hoveredPinKey == PinKeyMilBuildings, pinned.Contains(PinKeyMilBuildings));
        else
        {
            _militaryBuildingsToggleRect = SKRect.Empty;
            rowH = DrawLockedRow(canvas, leftX, leftY, colWidth, _localization.Get("automation_military_buildings_name"), _localization.Get("automation_military_buildings_locked"));
        }

        float leftBottom = leftY + rowH;

        // === Colonne droite : comportements + contrôles bâtiments ===
        float rightY = startY;
        SkiaTextUtils.DrawText(canvas, _localization.Get("automation_header_behaviors"), rightX, rightY + 12, _nameFont, _accentPaint);
        rightY += 20f;

        bool hasAdvancedTactics = civ.TechnologyTree.CompletedTechnologies.Contains(TechId.AdvancedTactics);
        if (hasAdvancedTactics)
            (_militaryReinforcementToggleRect, rowH) = DrawAutomationRow(canvas, rightX, rightY, colWidth, WorldState.AutomationSettings.MilitaryReinforcementAutomationEnabled, _hoveredMilitaryReinforcementToggle, _localization.Get("automation_military_reinforcement_name"), _localization.Get("automation_military_reinforcement_desc"), _localization.Get("automation_military_reinforcement_note"), pinKey: PinKeyMilReinforce, isPinHovered: _hoveredPinKey == PinKeyMilReinforce, isPinned: pinned.Contains(PinKeyMilReinforce));
        else
        {
            _militaryReinforcementToggleRect = SKRect.Empty;
            rowH = DrawLockedRow(canvas, rightX, rightY, colWidth, _localization.Get("automation_military_reinforcement_name"), _localization.Get("automation_military_reinforcement_locked"));
        }
        rightY += rowH + RowSpacing;

        bool hasPatrol = civ.TechnologyTree.CompletedTechnologies.Contains(TechId.Patrol);
        if (hasPatrol)
            (_militaryPatrolToggleRect, rowH) = DrawAutomationRow(canvas, rightX, rightY, colWidth, WorldState.AutomationSettings.MilitaryPatrolAutomationEnabled, _hoveredMilitaryPatrolToggle, _localization.Get("automation_military_patrol_name"), _localization.Get("automation_military_patrol_desc"), _localization.Get("automation_military_patrol_note"), pinKey: PinKeyMilPatrol, isPinHovered: _hoveredPinKey == PinKeyMilPatrol, isPinned: pinned.Contains(PinKeyMilPatrol));
        else
        {
            _militaryPatrolToggleRect = SKRect.Empty;
            rowH = DrawLockedRow(canvas, rightX, rightY, colWidth, _localization.Get("automation_military_patrol_name"), _localization.Get("automation_military_patrol_locked"));
        }
        rightY += rowH + RowSpacing;

        bool hasVendetta = civ.TechnologyTree.CompletedTechnologies.Contains(TechId.Vendetta);
        if (hasVendetta)
            (_militaryVendettaToggleRect, rowH) = DrawAutomationRow(canvas, rightX, rightY, colWidth, WorldState.AutomationSettings.MilitaryVendettaAutomationEnabled, _hoveredMilitaryVendettaToggle, _localization.Get("automation_military_vendetta_name"), _localization.Get("automation_military_vendetta_desc"), _localization.Get("automation_military_vendetta_note"), pinKey: PinKeyMilVendetta, isPinHovered: _hoveredPinKey == PinKeyMilVendetta, isPinned: pinned.Contains(PinKeyMilVendetta));
        else
        {
            _militaryVendettaToggleRect = SKRect.Empty;
            rowH = DrawLockedRow(canvas, rightX, rightY, colWidth, _localization.Get("automation_military_vendetta_name"), _localization.Get("automation_military_vendetta_locked"));
        }
        rightY += rowH + RowSpacing;

        (_monumentInvestmentToggleRect, rowH) = DrawAutomationRow(canvas, rightX, rightY, colWidth, WorldState.AutomationSettings.MonumentInvestmentAutomationEnabled, _hoveredMonumentInvestmentToggle, _localization.Get("automation_monument_investment_name"), _localization.Get("automation_monument_investment_desc"), _localization.Get("automation_monument_investment_note"), pinKey: PinKeyMonumentInvestment, isPinHovered: _hoveredPinKey == PinKeyMonumentInvestment, isPinned: pinned.Contains(PinKeyMonumentInvestment));
        rightY += rowH + RowSpacing;

        // --- Contrôles bâtiments ---
        bool hasBarracks      = BuildingExists<Barracks>(civ);
        bool hasLabs          = BuildingExists<Laboratory>(civ);
        bool hasSmelters      = BuildingExists<Smelter>(civ);
        bool hasArsenals      = BuildingExists<Arsenal>(civ);
        bool hasWeaponSmiths  = BuildingExists<WeaponSmith>(civ);
        bool hasArmorSmiths   = BuildingExists<ArmorSmith>(civ);
        bool hasAlchimistHuts = BuildingExists<AlchimistHut>(civ);
        bool anyBuildingControls = hasBarracks || hasLabs || hasSmelters || hasArsenals
            || hasWeaponSmiths || hasArmorSmiths || hasAlchimistHuts;

        if (anyBuildingControls)
        {
            rightY += 4f;
            SkiaTextUtils.DrawText(canvas, _localization.Get("automation_header_controls"), rightX, rightY + 12, _nameFont, _accentPaint);
            rightY += 20f;

            if (hasBarracks)
            {
                bool? allBarracksOn = AreAllActiveNullable<Barracks>(civ);
                (_barracksToggleRect, rowH) = DrawBuildingControlRow(canvas, rightX, rightY, colWidth, allBarracksOn, _hoveredBarracksToggle, _localization.Get("building_barracks_name"), _localization.Get("tooltip_toggle_barracks"), PinKeyBarracks, _hoveredPinKey == PinKeyBarracks, pinned.Contains(PinKeyBarracks));
                rightY += rowH + RowSpacing;
            }
            else _barracksToggleRect = SKRect.Empty;

            if (hasLabs)
            {
                bool? allLabsOn = AreAllActiveNullable<Laboratory>(civ);
                (_labToggleRect, rowH) = DrawBuildingControlRow(canvas, rightX, rightY, colWidth, allLabsOn, _hoveredLabToggle, _localization.Get("building_laboratory_name"), _localization.Get("tooltip_toggle_lab"), PinKeyLaboratory, _hoveredPinKey == PinKeyLaboratory, pinned.Contains(PinKeyLaboratory));
                rightY += rowH + RowSpacing;
            }
            else _labToggleRect = SKRect.Empty;

            if (hasSmelters)
            {
                bool? allSmeltersOn = AreAllActiveNullable<Smelter>(civ);
                (_smelterToggleRect, rowH) = DrawBuildingControlRow(canvas, rightX, rightY, colWidth, allSmeltersOn, _hoveredSmelterToggle, _localization.Get("building_smelter_name"), _localization.Get("tooltip_toggle_smelter"), PinKeySmelter, _hoveredPinKey == PinKeySmelter, pinned.Contains(PinKeySmelter));
                rightY += rowH + RowSpacing;
            }
            else _smelterToggleRect = SKRect.Empty;

            if (hasArsenals)
            {
                bool? allArsenalsOn = AreAllActiveNullable<Arsenal>(civ);
                (_arsenalToggleRect, rowH) = DrawBuildingControlRow(canvas, rightX, rightY, colWidth, allArsenalsOn, _hoveredArsenalToggle, _localization.Get("building_arsenal_name"), _localization.Get("tooltip_toggle_arsenal"), PinKeyArsenal, _hoveredPinKey == PinKeyArsenal, pinned.Contains(PinKeyArsenal));
                rightY += rowH + RowSpacing;
            }
            else _arsenalToggleRect = SKRect.Empty;

            if (hasWeaponSmiths)
            {
                bool? allWeaponSmithsOn = AreAllActiveNullable<WeaponSmith>(civ);
                (_weaponSmithToggleRect, rowH) = DrawBuildingControlRow(canvas, rightX, rightY, colWidth, allWeaponSmithsOn, _hoveredWeaponSmithToggle, _localization.Get("building_weaponsmith_name"), _localization.Get("tooltip_toggle_weaponsmith"), PinKeyWeaponSmith, _hoveredPinKey == PinKeyWeaponSmith, pinned.Contains(PinKeyWeaponSmith));
                rightY += rowH + RowSpacing;
            }
            else _weaponSmithToggleRect = SKRect.Empty;

            if (hasArmorSmiths)
            {
                bool? allArmorSmithsOn = AreAllActiveNullable<ArmorSmith>(civ);
                (_armorSmithToggleRect, rowH) = DrawBuildingControlRow(canvas, rightX, rightY, colWidth, allArmorSmithsOn, _hoveredArmorSmithToggle, _localization.Get("building_armorsmith_name"), _localization.Get("tooltip_toggle_armorsmith"), PinKeyArmorSmith, _hoveredPinKey == PinKeyArmorSmith, pinned.Contains(PinKeyArmorSmith));
                rightY += rowH + RowSpacing;
            }
            else _armorSmithToggleRect = SKRect.Empty;

            if (hasAlchimistHuts)
            {
                bool? allAlchimistHutsOn = AreAllActiveNullable<AlchimistHut>(civ);
                (_alchimistHutToggleRect, rowH) = DrawBuildingControlRow(canvas, rightX, rightY, colWidth, allAlchimistHutsOn, _hoveredAlchimistHutToggle, _localization.Get("building_alchimisthut_name"), _localization.Get("tooltip_toggle_alchimisthut"), PinKeyAlchimistHut, _hoveredPinKey == PinKeyAlchimistHut, pinned.Contains(PinKeyAlchimistHut));
                rightY += rowH + RowSpacing;
            }
            else _alchimistHutToggleRect = SKRect.Empty;
        }
        else
        {
            _barracksToggleRect = _labToggleRect = _smelterToggleRect = _arsenalToggleRect = SKRect.Empty;
            _weaponSmithToggleRect = _armorSmithToggleRect = _alchimistHutToggleRect = SKRect.Empty;
        }

        float rightBottom = rightY;

        canvas.Restore();

        _totalContentH = Math.Max(leftBottom, rightBottom) + Padding - topBar;

        if (needsScroll)
            DrawScrollbar(canvas, topBar, _viewportH);

        if (_hoveredPinKey != null)
            DrawFloatingTooltip(canvas, _localization.Get("tooltip_pin_to_civ_panel"), _mousePosition);
        else if (_hoveredNote != null)
            DrawFloatingTooltip(canvas, _hoveredNote, _mousePosition);
    }

    private (SKRect toggleRect, float height) DrawAutomationRow(
        SKCanvas canvas, float x, float y, float width,
        bool isOn, bool isHovered, string name, string desc,
        string? note = null,
        IEnumerable<City>? cities = null, BuildingType[]? summaryTypes = null,
        string? pinKey = null, bool isPinHovered = false, bool isPinned = false)
    {
        bool hasSummary = cities != null && summaryTypes != null;
        int summaryLines = !hasSummary ? 0 : summaryTypes!.Length;

        float textX = x + 12f + TextOffsetX;
        float rightReserve = pinKey != null ? PinCheckboxSize + PinCheckboxMargin * 2 : DescRightPad;
        float descMaxWidth = width - 12f - TextOffsetX - rightReserve;
        var descLayout = SkiaTextUtils.MeasureWrappedText(desc, descMaxWidth, _descFont);

        float contentHeight = Math.Max(RowMinHeight, 18f + _nameFont.Spacing + 2f + descLayout.Size.Height + 10f);
        float summaryExtra = summaryLines == 0 ? 0f : SummaryHeight + (summaryLines - 1) * SummaryLineHeight;
        float cardHeight = contentHeight + summaryExtra;

        var cardRect = new SKRect(x, y, x + width, y + cardHeight);
        canvas.DrawRoundRect(cardRect, 6, 6, _cardPaint);
        canvas.DrawRoundRect(cardRect, 6, 6, _cardBorderPaint);

        float toggleY = y + (contentHeight - ToggleHeight) / 2f;
        var toggleRect = new SKRect(x + 12f, toggleY, x + 12f + ToggleWidth, toggleY + ToggleHeight);
        SkiaToggleUtils.Draw(canvas, toggleRect, isOn, isHovered);

        SkiaTextUtils.DrawText(canvas, name, textX, y + 18, _nameFont, _namePaint);
        SkiaTextUtils.DrawWrappedText(canvas, desc, textX, y + 18f + _nameFont.Spacing + 2f, descMaxWidth, _descFont, _descPaint);

        if (hasSummary)
        {
            canvas.DrawLine(x + 12f, y + contentHeight, x + width - 12f, y + contentHeight, _summaryDividerPaint);
            DrawBuildingSummary(canvas, x + 12f, y + contentHeight + 14f, cities!, summaryTypes!);
        }

        if (note != null)
            _hoverableCards.Add((cardRect, note));

        if (pinKey != null)
        {
            var cbRect = new SKRect(x + width - PinCheckboxMargin - PinCheckboxSize, y + PinCheckboxMargin,
                                    x + width - PinCheckboxMargin, y + PinCheckboxMargin + PinCheckboxSize);
            DrawPinCheckbox(canvas, cbRect, isPinned, isPinHovered);
            _pinCheckboxes.Add((cbRect, pinKey));
        }

        return (toggleRect, cardHeight);
    }

    private (SKRect toggleRect, float height) DrawBuildingControlRow(
        SKCanvas canvas, float x, float y, float width,
        bool? isOn, bool isHovered, string name, string desc,
        string? pinKey = null, bool isPinHovered = false, bool isPinned = false,
        bool isDimmed = false)
    {
        float textX = x + 12f + TextOffsetX;
        float rightReserve = pinKey != null ? PinCheckboxSize + PinCheckboxMargin * 2 : DescRightPad;
        float descMaxWidth = width - 12f - TextOffsetX - rightReserve;
        var descLayout = SkiaTextUtils.MeasureWrappedText(desc, descMaxWidth, _descFont);

        float contentHeight = Math.Max(RowMinHeight, 18f + _nameFont.Spacing + 2f + descLayout.Size.Height + 10f);

        var cardRect = new SKRect(x, y, x + width, y + contentHeight);
        canvas.DrawRoundRect(cardRect, 6, 6, _cardPaint);
        canvas.DrawRoundRect(cardRect, 6, 6, _cardBorderPaint);

        float toggleY = y + (contentHeight - ToggleHeight) / 2f;
        var toggleRect = new SKRect(x + 12f, toggleY, x + 12f + ToggleWidth, toggleY + ToggleHeight);
        SkiaToggleUtils.Draw(canvas, toggleRect, isOn, isHovered, isDimmed);

        SkiaTextUtils.DrawText(canvas, name, textX, y + 18, _nameFont, isDimmed ? _mutedPaint : _namePaint);
        SkiaTextUtils.DrawWrappedText(canvas, desc, textX, y + 18f + _nameFont.Spacing + 2f, descMaxWidth, _descFont, isDimmed ? _mutedPaint : _descPaint);

        if (pinKey != null)
        {
            var cbRect = new SKRect(x + width - PinCheckboxMargin - PinCheckboxSize, y + PinCheckboxMargin,
                                    x + width - PinCheckboxMargin, y + PinCheckboxMargin + PinCheckboxSize);
            DrawPinCheckbox(canvas, cbRect, isPinned, isPinHovered);
            _pinCheckboxes.Add((cbRect, pinKey));
        }

        return (toggleRect, contentHeight);
    }

    private void DrawPinCheckbox(SKCanvas canvas, SKRect rect, bool isPinned, bool isHovered)
    {
        canvas.DrawRoundRect(rect, 2, 2, isHovered ? _pinHoverPaint : _pinBorderPaint);
        if (isPinned)
        {
            var inner = new SKRect(rect.Left + 3, rect.Top + 3, rect.Right - 3, rect.Bottom - 3);
            canvas.DrawRoundRect(inner, 1, 1, _pinCheckedPaint);
        }
    }

    private void DrawFloatingTooltip(SKCanvas canvas, string text, SKPoint pos)
    {
        const float MaxW = 240f;
        const float PadX = 10f;
        const float PadY = 8f;

        var layout = SkiaTextUtils.MeasureWrappedText(text, MaxW, _descFont);
        float w = layout.Size.Width + PadX * 2;
        float h = layout.Size.Height + PadY * 2;

        float tx = pos.X + 14f;
        float ty = pos.Y - h - 6f;
        if (tx + w > _canvasSize.Width - 6) tx = _canvasSize.Width - 6 - w;
        if (ty < 0) ty = pos.Y + 18f;

        var rect = new SKRect(tx, ty, tx + w, ty + h);
        canvas.DrawRoundRect(rect, 5, 5, _tooltipBgPaint);
        canvas.DrawRoundRect(rect, 5, 5, _tooltipBorderPaint);
        SkiaTextUtils.DrawWrappedText(canvas, text, tx + PadX, ty + PadY + _descFont.Size, MaxW, _descFont, _notePaint);
    }

    private float DrawLockedRow(SKCanvas canvas, float x, float y, float width, string name, string lockDesc)
    {
        float textX = x + 12f;
        float descMaxWidth = width - 24f;
        var descLayout = SkiaTextUtils.MeasureWrappedText(lockDesc, descMaxWidth, _descFont);
        float contentHeight = Math.Max(RowMinHeight, 18f + _nameFont.Spacing + 2f + descLayout.Size.Height + 10f);

        var cardRect = new SKRect(x, y, x + width, y + contentHeight);
        canvas.DrawRoundRect(cardRect, 6, 6, _cardPaint);
        canvas.DrawRoundRect(cardRect, 6, 6, _cardBorderPaint);

        SkiaTextUtils.DrawText(canvas, name, textX, y + 18, _nameFont, _mutedPaint);
        SkiaTextUtils.DrawWrappedText(canvas, lockDesc, textX, y + 18f + _nameFont.Spacing + 2f, descMaxWidth, _descFont, _mutedPaint);

        return contentHeight;
    }

    private void DrawBuildingSummary(SKCanvas canvas, float x, float y, IEnumerable<City> cities, BuildingType[] types)
    {
        for (int i = 0; i < types.Length; i++)
            DrawSummaryLine(canvas, x, y + i * SummaryLineHeight, cities, [types[i]]);
    }

    private void DrawSummaryLine(SKCanvas canvas, float x, float y, IEnumerable<City> cities, BuildingType[] types)
    {
        float curX = x;
        bool first = true;
        foreach (var type in types)
        {
            if (!first) curX += 14f;
            first = false;

            var buildings = cities.SelectMany(c => c.Buildings)
                .Where(b => b.Type == type && b.Level >= 1)
                .ToList();

            string bldName = _localization.Get($"building_{type.ToString().ToLower()}_name");

            string text;
            SKPaint paint;
            if (buildings.Count == 0)
            {
                text = $"{bldName}: -";
                paint = _summaryEmptyPaint;
            }
            else
            {
                var groups = buildings.GroupBy(b => b.Level).OrderBy(g => g.Key);
                string lv = _localization.Get("level_abbrev");
                string levels = string.Join(" ", groups.Select(g => $"{g.Count()}×{lv}{g.Key}"));
                text = $"{bldName}: {levels}";
                paint = _summaryBuiltPaint;
            }

            SkiaTextUtils.DrawText(canvas, text, curX, y, _summaryFont, paint);
            curX += _summaryFont.MeasureText(text);
        }
    }

    public void HandlePointerMoved(SKPoint position)
    {
        _mousePosition = position;

        if (_isDraggingScrollbar)
        {
            float dy         = position.Y - _scrollDragStartY;
            float thumbRange = _scrollTrackRect.Height - _scrollThumbRect.Height;
            float maxScroll  = Math.Max(0, _totalContentH - _viewportH);
            float scrollPerPx = thumbRange > 0 ? maxScroll / thumbRange : 0;
            _scrollOffsetPx  = Math.Clamp(_scrollDragStartOffset + dy * scrollPerPx, 0, maxScroll);
            return;
        }

        var adj = new SKPoint(position.X, position.Y + _scrollOffsetPx);
        _hoveredRoadToggle                   = !_roadToggleRect.IsEmpty                   && _roadToggleRect.Contains(adj.X, adj.Y);
        _hoveredOutpostToggle                = !_outpostToggleRect.IsEmpty                && _outpostToggleRect.Contains(adj.X, adj.Y);
        _hoveredRoadUnderworldToggle         = !_roadUnderworldToggleRect.IsEmpty         && _roadUnderworldToggleRect.Contains(adj.X, adj.Y);
        _hoveredOutpostUnderworldToggle      = !_outpostUnderworldToggleRect.IsEmpty      && _outpostUnderworldToggleRect.Contains(adj.X, adj.Y);
        _hoveredTownHallToggle               = !_townHallToggleRect.IsEmpty               && _townHallToggleRect.Contains(adj.X, adj.Y);
        _hoveredProductionToggle             = !_productionToggleRect.IsEmpty             && _productionToggleRect.Contains(adj.X, adj.Y);
        _hoveredArtisanToggle                = !_artisanToggleRect.IsEmpty                && _artisanToggleRect.Contains(adj.X, adj.Y);
        _hoveredLibraryToggle                = !_libraryToggleRect.IsEmpty                && _libraryToggleRect.Contains(adj.X, adj.Y);
        _hoveredMarketToggle                 = !_marketToggleRect.IsEmpty                 && _marketToggleRect.Contains(adj.X, adj.Y);
        _hoveredSeaportToggle                = !_seaportToggleRect.IsEmpty                && _seaportToggleRect.Contains(adj.X, adj.Y);
        _hoveredMilitaryBuildingsToggle      = !_militaryBuildingsToggleRect.IsEmpty      && _militaryBuildingsToggleRect.Contains(adj.X, adj.Y);
        _hoveredMilitaryReinforcementToggle  = !_militaryReinforcementToggleRect.IsEmpty  && _militaryReinforcementToggleRect.Contains(adj.X, adj.Y);
        _hoveredMilitaryPatrolToggle         = !_militaryPatrolToggleRect.IsEmpty         && _militaryPatrolToggleRect.Contains(adj.X, adj.Y);
        _hoveredMilitaryVendettaToggle       = !_militaryVendettaToggleRect.IsEmpty       && _militaryVendettaToggleRect.Contains(adj.X, adj.Y);
        _hoveredMonumentInvestmentToggle     = !_monumentInvestmentToggleRect.IsEmpty     && _monumentInvestmentToggleRect.Contains(adj.X, adj.Y);
        _hoveredBarracksToggle      = !_barracksToggleRect.IsEmpty      && _barracksToggleRect.Contains(adj.X, adj.Y);
        _hoveredLabToggle           = !_labToggleRect.IsEmpty           && _labToggleRect.Contains(adj.X, adj.Y);
        _hoveredSmelterToggle       = !_smelterToggleRect.IsEmpty       && _smelterToggleRect.Contains(adj.X, adj.Y);
        _hoveredArsenalToggle       = !_arsenalToggleRect.IsEmpty       && _arsenalToggleRect.Contains(adj.X, adj.Y);
        _hoveredWeaponSmithToggle   = !_weaponSmithToggleRect.IsEmpty   && _weaponSmithToggleRect.Contains(adj.X, adj.Y);
        _hoveredArmorSmithToggle    = !_armorSmithToggleRect.IsEmpty    && _armorSmithToggleRect.Contains(adj.X, adj.Y);
        _hoveredAlchimistHutToggle  = !_alchimistHutToggleRect.IsEmpty  && _alchimistHutToggleRect.Contains(adj.X, adj.Y);

        _hoveredNote = null;
        foreach (var (rect, note) in _hoverableCards)
        {
            if (rect.Contains(adj.X, adj.Y)) { _hoveredNote = note; break; }
        }

        _hoveredPinKey = null;
        foreach (var (rect, key) in _pinCheckboxes)
        {
            if (rect.Contains(adj.X, adj.Y)) { _hoveredPinKey = key; break; }
        }
    }

    public bool HandlePointerPressed(SKPoint position)
    {
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

        var state = _gameControllerService.CurrentWorldState;
        if (state == null) return false;

        var adj = new SKPoint(position.X, position.Y + _scrollOffsetPx);

        // Checkboxes de pin
        foreach (var (rect, key) in _pinCheckboxes)
        {
            if (rect.Contains(adj.X, adj.Y))
            {
                var ps = _gameControllerService.CurrentGameState!.Settings.PinnedCivPanelKeys;
                if (!ps.Remove(key)) ps.Add(key);
                return true;
            }
        }

        // Toggles d'automatisme
        if (!_roadToggleRect.IsEmpty && _roadToggleRect.Contains(adj.X, adj.Y))
        {
            state.AutomationSettings.RoadAutomationEnabled = !state.AutomationSettings.RoadAutomationEnabled;
            return true;
        }
        if (!_outpostToggleRect.IsEmpty && _outpostToggleRect.Contains(adj.X, adj.Y))
        {
            state.AutomationSettings.OutpostAutomationEnabled = !state.AutomationSettings.OutpostAutomationEnabled;
            return true;
        }
        if (!_roadUnderworldToggleRect.IsEmpty && _roadUnderworldToggleRect.Contains(adj.X, adj.Y))
        {
            state.AutomationSettings.RoadAutomationEnabledUnderworld = !state.AutomationSettings.RoadAutomationEnabledUnderworld;
            return true;
        }
        if (!_outpostUnderworldToggleRect.IsEmpty && _outpostUnderworldToggleRect.Contains(adj.X, adj.Y))
        {
            state.AutomationSettings.OutpostAutomationEnabledUnderworld = !state.AutomationSettings.OutpostAutomationEnabledUnderworld;
            return true;
        }
        if (!_townHallToggleRect.IsEmpty && _townHallToggleRect.Contains(adj.X, adj.Y))
        {
            state.AutomationSettings.TownHallAutomationEnabled = !state.AutomationSettings.TownHallAutomationEnabled;
            return true;
        }
        if (!_productionToggleRect.IsEmpty && _productionToggleRect.Contains(adj.X, adj.Y))
        {
            state.AutomationSettings.ProductionBuildingAutomationEnabled = !state.AutomationSettings.ProductionBuildingAutomationEnabled;
            return true;
        }
        if (!_artisanToggleRect.IsEmpty && _artisanToggleRect.Contains(adj.X, adj.Y))
        {
            state.AutomationSettings.ArtisanBuildingAutomationEnabled = !state.AutomationSettings.ArtisanBuildingAutomationEnabled;
            return true;
        }
        if (!_libraryToggleRect.IsEmpty && _libraryToggleRect.Contains(adj.X, adj.Y))
        {
            state.AutomationSettings.LibraryBuildingAutomationEnabled = !state.AutomationSettings.LibraryBuildingAutomationEnabled;
            return true;
        }
        if (!_marketToggleRect.IsEmpty && _marketToggleRect.Contains(adj.X, adj.Y))
        {
            state.AutomationSettings.MarketBuildingAutomationEnabled = !state.AutomationSettings.MarketBuildingAutomationEnabled;
            return true;
        }
        if (!_seaportToggleRect.IsEmpty && _seaportToggleRect.Contains(adj.X, adj.Y))
        {
            state.AutomationSettings.SeaportBuildingAutomationEnabled = !state.AutomationSettings.SeaportBuildingAutomationEnabled;
            return true;
        }
        if (!_militaryBuildingsToggleRect.IsEmpty && _militaryBuildingsToggleRect.Contains(adj.X, adj.Y))
        {
            state.AutomationSettings.MilitaryBuildingAutomationEnabled = !state.AutomationSettings.MilitaryBuildingAutomationEnabled;
            return true;
        }
        if (!_militaryReinforcementToggleRect.IsEmpty && _militaryReinforcementToggleRect.Contains(adj.X, adj.Y))
        {
            state.AutomationSettings.MilitaryReinforcementAutomationEnabled = !state.AutomationSettings.MilitaryReinforcementAutomationEnabled;
            if (!state.AutomationSettings.MilitaryReinforcementAutomationEnabled)
            {
                var civR = _gameControllerService.PlayerCivilization;
                if (civR != null) _gameControllerService.MainGameController.MilitaryController.ClearReinforcementFlows(civR);
            }
            return true;
        }
        if (!_militaryPatrolToggleRect.IsEmpty && _militaryPatrolToggleRect.Contains(adj.X, adj.Y))
        {
            state.AutomationSettings.MilitaryPatrolAutomationEnabled = !state.AutomationSettings.MilitaryPatrolAutomationEnabled;
            return true;
        }
        if (!_militaryVendettaToggleRect.IsEmpty && _militaryVendettaToggleRect.Contains(adj.X, adj.Y))
        {
            state.AutomationSettings.MilitaryVendettaAutomationEnabled = !state.AutomationSettings.MilitaryVendettaAutomationEnabled;
            if (!state.AutomationSettings.MilitaryVendettaAutomationEnabled)
            {
                var civV = _gameControllerService.PlayerCivilization;
                if (civV != null) _gameControllerService.MainGameController.MilitaryController.ClearAttackFlows(civV);
            }
            return true;
        }
        if (!_monumentInvestmentToggleRect.IsEmpty && _monumentInvestmentToggleRect.Contains(adj.X, adj.Y))
        {
            state.AutomationSettings.MonumentInvestmentAutomationEnabled = !state.AutomationSettings.MonumentInvestmentAutomationEnabled;
            return true;
        }

        // Toggles de contrôle bâtiments
        var civ = _gameControllerService.PlayerCivilization;
        if (civ != null)
        {
            if (!_barracksToggleRect.IsEmpty && _barracksToggleRect.Contains(adj.X, adj.Y))
            {
                ToggleAll<Barracks>(civ); return true;
            }
            if (!_labToggleRect.IsEmpty && _labToggleRect.Contains(adj.X, adj.Y))
            {
                ToggleAll<Laboratory>(civ); return true;
            }
            if (!_smelterToggleRect.IsEmpty && _smelterToggleRect.Contains(adj.X, adj.Y))
            {
                ToggleAll<Smelter>(civ); return true;
            }
            if (!_arsenalToggleRect.IsEmpty && _arsenalToggleRect.Contains(adj.X, adj.Y))
            {
                ToggleAll<Arsenal>(civ); return true;
            }
            if (!_weaponSmithToggleRect.IsEmpty && _weaponSmithToggleRect.Contains(adj.X, adj.Y))
            {
                ToggleAll<WeaponSmith>(civ); return true;
            }
            if (!_armorSmithToggleRect.IsEmpty && _armorSmithToggleRect.Contains(adj.X, adj.Y))
            {
                ToggleAll<ArmorSmith>(civ); return true;
            }
            if (!_alchimistHutToggleRect.IsEmpty && _alchimistHutToggleRect.Contains(adj.X, adj.Y))
            {
                ToggleAll<AlchimistHut>(civ); return true;
            }
        }

        return false;
    }

    public void HandlePointerReleased(SKPoint position)
    {
        _isDraggingScrollbar = false;
    }

    public void HandleScroll(float delta)
    {
        const float step = 60f;
        float dir = delta > 0 ? -1f : 1f;
        float maxScroll = Math.Max(0, _totalContentH - _viewportH);
        _scrollOffsetPx = Math.Clamp(_scrollOffsetPx + dir * step, 0, maxScroll);
    }

    private static bool BuildingExists<T>(Civilization civ) where T : Building
        => civ.Cities.Any(c => c.Buildings.OfType<T>().Any(b => b.Level >= 1));

    private static bool? AreAllActiveNullable<T>(Civilization civ) where T : Building
    {
        var list = civ.Cities.SelectMany(c => c.Buildings.OfType<T>()).Where(b => b.Level >= 1).ToList();
        if (list.Count == 0) return false;
        if (list.All(b => b.ActivationStatus == ActivationStatus.ACTIVE)) return true;
        return list.Any(b => b.ActivationStatus == ActivationStatus.ACTIVE) ? null : false;
    }

    private static void ToggleAll<T>(Civilization civ) where T : Building
    {
        var list = civ.Cities.SelectMany(c => c.Buildings.OfType<T>()).Where(b => b.Level >= 1).ToList();
        bool allActive = list.All(b => b.ActivationStatus == ActivationStatus.ACTIVE);
        var next = allActive ? ActivationStatus.INACTIVE : ActivationStatus.ACTIVE;
        foreach (var b in list) b.ActivationStatus = next;
    }

    private void DrawScrollbar(SKCanvas canvas, float trackTop, float trackH)
    {
        const float scrollW      = 6f;
        const float scrollMargin = 4f;
        float trackX = _canvasSize.Width - scrollW - scrollMargin;

        _scrollTrackRect = new SKRect(trackX, trackTop, trackX + scrollW, trackTop + trackH);
        canvas.DrawRoundRect(_scrollTrackRect, 3, 3, _scrollTrackPaint);

        float thumbRatio = _viewportH / _totalContentH;
        float thumbH     = Math.Max(24f, thumbRatio * trackH);
        float maxScroll  = Math.Max(1, _totalContentH - _viewportH);
        float thumbTop   = trackTop + (_scrollOffsetPx / maxScroll) * (trackH - thumbH);
        _scrollThumbRect = new SKRect(trackX, thumbTop, trackX + scrollW, thumbTop + thumbH);
        canvas.DrawRoundRect(_scrollThumbRect, 3, 3, _scrollThumbPaint);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _bgPaint.Dispose();
        _cardPaint.Dispose();
        _cardBorderPaint.Dispose();
        _namePaint.Dispose();
        _descPaint.Dispose();
        _notePaint.Dispose();
        _tooltipBgPaint.Dispose();
        _tooltipBorderPaint.Dispose();
        _mutedPaint.Dispose();
        _accentPaint.Dispose();
        _summaryBuiltPaint.Dispose();
        _summaryEmptyPaint.Dispose();
        _summaryDividerPaint.Dispose();
        _scrollTrackPaint.Dispose();
        _scrollThumbPaint.Dispose();
        _pinBorderPaint.Dispose();
        _pinHoverPaint.Dispose();
        _pinCheckedPaint.Dispose();
        _headerFont.Dispose();
        _nameFont.Dispose();
        _descFont.Dispose();
        _summaryFont.Dispose();
        _disposed = true;
    }
}
