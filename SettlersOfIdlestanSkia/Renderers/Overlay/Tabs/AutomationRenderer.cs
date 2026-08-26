using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.IslandMap;
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
    internal const string PinKeyGrandTemple   = "GrandTemple";
    internal const string PinKeyMithrilMine   = "MithrilMine";
    internal const string PinKeyMilReinforce  = "MilitaryReinforcement";
    internal const string PinKeyMilVendetta   = "MilitaryVendetta";
    internal const string PinKeyMonumentInvestment = "MonumentInvestment";
    internal const string PinKeyBarracks      = "Barracks";
    internal const string PinKeyArsenal       = "Arsenal";
    internal const string PinKeyLaboratory    = "Laboratory";
    internal const string PinKeySmelter       = "Smelter";
    internal const string PinKeyWeaponSmith   = "WeaponSmith";
    internal const string PinKeyArmorSmith    = "ArmorSmith";
    internal const string PinKeyAlchimistHut  = "AlchimistHut";
    internal const string PinKeyArcaneTower   = "ArcaneTower";
    internal const string PinKeyRestrictSoldierProduction           = "RestrictSoldierProduction";
    internal const string PinKeyRestrictSoldierProductionUnderworld = "RestrictSoldierProductionUnderworld";
    internal const string PinKeyRestrictSoldierProductionAbyss      = "RestrictSoldierProductionAbyss";
    internal const string PinKeyRestrictSoldierProductionPandemonium = "RestrictSoldierProductionPandemonium";

    /// <summary>
    /// Famille de chaque cle d'epinglage, pour styler differemment les bascules du panneau
    /// civilisation. Reprend exactement le classement des sections de <see cref="BuildColumns"/> :
    /// "buildings" -> Construction, "behaviors" -> Behavior, "controls" -> Activation. Table
    /// separee plutot que deduite de BuildColumns (comme PlayerCivilizationPanelRenderer le fait
    /// deja pour les libelles avec AutomationPinLocalizationRoots) car ce panneau ne connait une
    /// bascule que par sa cle, jamais par la RowModel qui l'a produite. Un test verrouille
    /// l'accord entre les deux.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, AutomationCategory> PinKeyCategories =
        new Dictionary<string, AutomationCategory>
        {
            [PinKeyRoad] = AutomationCategory.Construction,
            [PinKeyRoadUnderworld] = AutomationCategory.Construction,
            [PinKeyOutpost] = AutomationCategory.Construction,
            [PinKeyOutpostUnderworld] = AutomationCategory.Construction,
            [PinKeyTownHall] = AutomationCategory.Construction,
            [PinKeyProduction] = AutomationCategory.Construction,
            [PinKeyArtisan] = AutomationCategory.Construction,
            [PinKeyLibrary] = AutomationCategory.Construction,
            [PinKeyMarket] = AutomationCategory.Construction,
            [PinKeySeaport] = AutomationCategory.Construction,
            [PinKeyMilBuildings] = AutomationCategory.Construction,
            [PinKeyGrandTemple] = AutomationCategory.Construction,
            [PinKeyMithrilMine] = AutomationCategory.Construction,
            [PinKeyArcaneTower] = AutomationCategory.Construction,

            [PinKeyMilReinforce] = AutomationCategory.Behavior,
            [PinKeyMilVendetta] = AutomationCategory.Behavior,
            [PinKeyMonumentInvestment] = AutomationCategory.Behavior,

            [PinKeyBarracks] = AutomationCategory.Activation,
            [PinKeyArsenal] = AutomationCategory.Activation,
            [PinKeyLaboratory] = AutomationCategory.Activation,
            [PinKeySmelter] = AutomationCategory.Activation,
            [PinKeyWeaponSmith] = AutomationCategory.Activation,
            [PinKeyArmorSmith] = AutomationCategory.Activation,
            [PinKeyAlchimistHut] = AutomationCategory.Activation,
            [PinKeyRestrictSoldierProduction] = AutomationCategory.Activation,
            [PinKeyRestrictSoldierProductionUnderworld] = AutomationCategory.Activation,
            [PinKeyRestrictSoldierProductionAbyss] = AutomationCategory.Activation,
            [PinKeyRestrictSoldierProductionPandemonium] = AutomationCategory.Activation,
        };

    private readonly GameControllerService _gameControllerService;
    private readonly LocalizationService _localization;
    private readonly UILayoutService _uiLayout;

    private SKSize _canvasSize;
    private bool _disposed;

    /// <summary>
    /// Rectangles des bascules de la frame courante, avec la cle de leur ligne. Une seule
    /// collection remplace les vingt champs _xxxToggleRect qu'il fallait tenir a jour un par un
    /// — c'est precisement le genre de structure parallele au rendu que la migration supprime.
    /// </summary>
    private readonly List<(SKRect rect, string key)> _toggleRects = new();

    /// Cle de la bascule survolee, la ou vingt booleens se disputaient la meme information.
    private string? _hoveredToggleKey;

    private SKRect _globalAutomationsToggleRect = SKRect.Empty;
    private bool _hoveredGlobalAutomationsToggle;

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
    private static readonly BuildingType[] MilitaryTypes   = [BuildingType.Barracks, BuildingType.Garrison, BuildingType.Arsenal, BuildingType.WeaponSmith, BuildingType.ArmorSmith, BuildingType.Palisade];
    private static readonly BuildingType[] GrandTempleTypes = [BuildingType.Temple];
    private static readonly BuildingType[] MithrilMineTypes = [BuildingType.MithrilMine];
    private static readonly BuildingType[] ArcaneTowerTypes = [BuildingType.MageTower, BuildingType.AlchimistHut];

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
        _toggleRects.Clear();

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

        // Interrupteur global (voir GameSettings.AutomationsEnabled / AutomationSettings.Bind) —
        // les réglages individuels ci-dessous restent affichés tels que stockés même quand il est
        // coupé, ils reprennent effet dès sa réactivation sans perdre les préférences par ligne.
        bool automationsGloballyEnabled = _gameControllerService.CurrentGameState?.Settings.AutomationsEnabled ?? true;
        const float headerRowH = 32f;
        float globalToggleY = y + (headerRowH - ToggleHeight) / 2f;
        _globalAutomationsToggleRect = new SKRect(x + contentWidth - ToggleWidth, globalToggleY, x + contentWidth, globalToggleY + ToggleHeight);
        SkiaToggleUtils.Draw(canvas, _globalAutomationsToggleRect, automationsGloballyEnabled, _hoveredGlobalAutomationsToggle);
        _hoverableCards.Add((_globalAutomationsToggleRect, _localization.Get("settings_automations_enabled")));
        y += headerRowH;

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
        GrandTemple? grandTemple = null;
        VolcanicForge? volcanicForge = null;
        ArcaneTower? arcaneTower = null;
        foreach (var city in civ.Cities)
        {
            buildersGuild ??= city.Buildings.OfType<BuildersGuild>().FirstOrDefault();
            harvestersGuild ??= city.Buildings.OfType<HarvestersGuild>().FirstOrDefault();
            artisansGuild ??= city.Buildings.OfType<ArtisansGuild>().FirstOrDefault();
            academy ??= city.Buildings.OfType<Academy>().FirstOrDefault();
            traderGuild ??= city.Buildings.OfType<TraderGuild>().FirstOrDefault();
            imperialPort ??= city.Buildings.OfType<ImperialPort>().FirstOrDefault();
            warRoom ??= city.Buildings.OfType<WarRoom>().FirstOrDefault();
            grandTemple ??= city.Buildings.OfType<GrandTemple>().FirstOrDefault();
            volcanicForge ??= city.Buildings.OfType<VolcanicForge>().FirstOrDefault();
            arcaneTower ??= city.Buildings.OfType<ArcaneTower>().FirstOrDefault();
            if (buildersGuild != null && harvestersGuild != null && artisansGuild != null && academy != null && traderGuild != null && imperialPort != null && warRoom != null && grandTemple != null && volcanicForge != null && arcaneTower != null) break;
        }
        bool hasSeaportAutomation = civ.ModifierAggregator.HasModifier(Modifier.ECategory.UNLOCK_SEAPORT_AUTOMATION);
        bool hasBuildersGuildUnderworld = civ.ModifierAggregator.HasModifier(Modifier.ECategory.UNLOCK_BUILDERS_GUILD_UNDERWORLD);

        const float ColGap = 12f;
        float colWidth = (contentWidth - ColGap) / 2f;
        float startY = y;

        _pinnedKeys = _gameControllerService.CurrentGameState!.Settings.PinnedCivPanelKeys;
        var (leftSections, rightSections) = BuildColumns(civ, WorldState);

        float leftBottom  = DrawSections(canvas, leftSections,  x, startY, colWidth, civ);
        float rightBottom = DrawSections(canvas, rightSections, x + colWidth + ColGap, startY, colWidth, civ);

        canvas.Restore();

        _totalContentH = Math.Max(leftBottom, rightBottom) + Padding - topBar;

        if (needsScroll)
            DrawScrollbar(canvas, topBar, _viewportH);

        if (_hoveredPinKey != null)
            DrawFloatingTooltip(canvas, _localization.Get("tooltip_pin_to_civ_panel"), _mousePosition);
        else if (_hoveredNote != null)
            DrawFloatingTooltip(canvas, _hoveredNote, _mousePosition);
    }

    // ── Definition unique des lignes ──────────────────────────────────────────
    //
    // Le rendu Skia, son hit-testing et l'instantane destine a l'hote lisent tous cette liste.
    // Elle etait auparavant ecrite trois fois : une vingtaine d'appels de dessin, autant de
    // rectangles memorises, et autant de branches dans le gestionnaire de clic.

    /// <param name="Key">Cle d'epinglage de la ligne, qui lui sert aussi d'identifiant.</param>
    /// <param name="IsOn">Null pour un etat mixte (certains batiments du type actifs, d'autres non).</param>
    /// <param name="IsLocked">Ligne verrouillee : pas de bascule, Desc porte la raison du verrouillage.</param>
    private sealed record RowModel(
        string Key,
        string Name,
        string Desc,
        string? Note,
        bool? IsOn,
        bool IsLocked,
        bool CanPin,
        BuildingType[]? SummaryTypes,
        AutomationCategory Category);

    private sealed record SectionModel(string Header, List<RowModel> Rows);

    private IReadOnlySet<string> _pinnedKeys = new HashSet<string>();

    /// <summary>
    /// Deblocage de chaque bascule "structurelle" (bacs batiments/comportements) — tout sauf les
    /// controles bati-par-bati et les restrictions de production de soldats, qui se determinent
    /// par existence de batiment. Unique source de verite entre <see cref="BuildColumns"/> (onglet
    /// plein ecran) et PlayerCivilizationPanelRenderer (bascules epinglees) : sans elle, une
    /// bascule dont le deblocage a ete perdu (guilde retombee a un niveau insuffisant apres une
    /// Ascension, par exemple) continuait de s'afficher — et de rester actionnable — dans le
    /// panneau civilisation alors que l'onglet Automatisation la montre verrouillee.
    /// </summary>
    internal static Dictionary<string, bool> ComputeStructuralUnlocks(Civilization civ)
    {
        BuildersGuild? buildersGuild = null;
        HarvestersGuild? harvestersGuild = null;
        ArtisansGuild? artisansGuild = null;
        Academy? academy = null;
        TraderGuild? traderGuild = null;
        ImperialPort? imperialPort = null;
        WarRoom? warRoom = null;
        GrandTemple? grandTemple = null;
        VolcanicForge? volcanicForge = null;
        ArcaneTower? arcaneTower = null;
        foreach (var city in civ.Cities)
        {
            buildersGuild ??= city.Buildings.OfType<BuildersGuild>().FirstOrDefault();
            harvestersGuild ??= city.Buildings.OfType<HarvestersGuild>().FirstOrDefault();
            artisansGuild ??= city.Buildings.OfType<ArtisansGuild>().FirstOrDefault();
            academy ??= city.Buildings.OfType<Academy>().FirstOrDefault();
            traderGuild ??= city.Buildings.OfType<TraderGuild>().FirstOrDefault();
            imperialPort ??= city.Buildings.OfType<ImperialPort>().FirstOrDefault();
            warRoom ??= city.Buildings.OfType<WarRoom>().FirstOrDefault();
            grandTemple ??= city.Buildings.OfType<GrandTemple>().FirstOrDefault();
            volcanicForge ??= city.Buildings.OfType<VolcanicForge>().FirstOrDefault();
            arcaneTower ??= city.Buildings.OfType<ArcaneTower>().FirstOrDefault();
            if (buildersGuild != null && harvestersGuild != null && artisansGuild != null && academy != null
                && traderGuild != null && imperialPort != null && warRoom != null && grandTemple != null
                && volcanicForge != null && arcaneTower != null) break;
        }

        bool hasSeaportAutomation = civ.ModifierAggregator.HasModifier(Modifier.ECategory.UNLOCK_SEAPORT_AUTOMATION);
        bool hasBuildersGuildUnderworld = civ.ModifierAggregator.HasModifier(Modifier.ECategory.UNLOCK_BUILDERS_GUILD_UNDERWORLD);
        bool roadUnlocked = buildersGuild != null && buildersGuild.Level >= 1;
        bool outpostUnlocked = buildersGuild != null && buildersGuild.Level >= 4;

        return new Dictionary<string, bool>
        {
            [PinKeyRoad] = roadUnlocked,
            [PinKeyRoadUnderworld] = roadUnlocked && hasBuildersGuildUnderworld,
            [PinKeyOutpost] = outpostUnlocked,
            [PinKeyOutpostUnderworld] = outpostUnlocked && hasBuildersGuildUnderworld,
            [PinKeyTownHall] = roadUnlocked,
            [PinKeyProduction] = harvestersGuild is { Level: >= 1 },
            [PinKeyArtisan] = artisansGuild is { Level: >= 1 },
            [PinKeyLibrary] = academy is { Level: >= 1 },
            [PinKeyMarket] = traderGuild is { Level: >= 1 },
            [PinKeySeaport] = hasSeaportAutomation && imperialPort != null,
            [PinKeyMilBuildings] = warRoom is { Level: >= 1 },
            [PinKeyGrandTemple] = grandTemple is { Level: >= 1 },
            [PinKeyMithrilMine] = volcanicForge is { Level: >= 1 },
            [PinKeyArcaneTower] = arcaneTower is { Level: >= 1 },
            [PinKeyMilReinforce] = civ.TechnologyTree.CompletedTechnologies.Contains(TechId.AdvancedTactics),
            [PinKeyMilVendetta] = civ.TechnologyTree.CompletedTechnologies.Contains(TechId.Vendetta),
            [PinKeyMonumentInvestment] = true,
        };
    }

    private (List<SectionModel> Left, List<SectionModel> Right) BuildColumns(Civilization civ, WorldState worldState)
    {
        var settings = worldState.AutomationSettings;
        var unlocks = ComputeStructuralUnlocks(civ);

        // Categorie deduite de la cle plutot que passee en parametre : PinKeyCategories est deja
        // la table de reference (partagee avec le panneau civilisation), la dedoubler ici ne
        // pourrait que diverger.
        AutomationCategory CategoryOf(string key) =>
            PinKeyCategories.TryGetValue(key, out var category) ? category : AutomationCategory.Construction;

        RowModel Row(string key, string root, bool unlocked, bool isOn, BuildingType[]? summary = null, bool hasNote = true) =>
            unlocked
                ? new RowModel(key, _localization.Get(root + "_name"), _localization.Get(root + "_desc"),
                    hasNote ? _localization.Get(root + "_note") : null, isOn, IsLocked: false, CanPin: true, summary, CategoryOf(key))
                : new RowModel(key, _localization.Get(root + "_name"), _localization.Get(root + "_locked"),
                    null, null, IsLocked: true, CanPin: false, null, CategoryOf(key));

        var buildings = new List<RowModel>
        {
            Row(PinKeyRoad, "automation_road", unlocks[PinKeyRoad], settings.RoadAutomationEnabled),
            Row(PinKeyRoadUnderworld, "automation_road_underworld", unlocks[PinKeyRoadUnderworld], settings.RoadAutomationEnabledUnderworld),
            Row(PinKeyOutpost, "automation_outpost", unlocks[PinKeyOutpost], settings.OutpostAutomationEnabled),
            Row(PinKeyOutpostUnderworld, "automation_outpost_underworld", unlocks[PinKeyOutpostUnderworld], settings.OutpostAutomationEnabledUnderworld),
            Row(PinKeyTownHall, "automation_townhall", unlocks[PinKeyTownHall], settings.TownHallAutomationEnabled, TownHallTypes),
            Row(PinKeyProduction, "automation_production", unlocks[PinKeyProduction], settings.ProductionBuildingAutomationEnabled, ProductionTypes),
            Row(PinKeyArtisan, "automation_artisan", unlocks[PinKeyArtisan], settings.ArtisanBuildingAutomationEnabled, ArtisanTypes),
            Row(PinKeyLibrary, "automation_library", unlocks[PinKeyLibrary], settings.LibraryBuildingAutomationEnabled, LibraryTypes),
            Row(PinKeyMarket, "automation_market", unlocks[PinKeyMarket], settings.MarketBuildingAutomationEnabled, MarketTypes),
            // Seule ligne sans note explicative.
            Row(PinKeySeaport, "automation_seaport", unlocks[PinKeySeaport], settings.SeaportBuildingAutomationEnabled, SeaportTypes, hasNote: false),
            Row(PinKeyMilBuildings, "automation_military_buildings", unlocks[PinKeyMilBuildings], settings.MilitaryBuildingAutomationEnabled, MilitaryTypes),
            Row(PinKeyGrandTemple, "automation_grandtemple", unlocks[PinKeyGrandTemple], settings.TempleAutomationEnabled, GrandTempleTypes),
            Row(PinKeyMithrilMine, "automation_mithrilmine", unlocks[PinKeyMithrilMine], settings.MithrilMineBuildingAutomationEnabled, MithrilMineTypes),
            Row(PinKeyArcaneTower, "automation_arcanetower", unlocks[PinKeyArcaneTower], settings.ArcaneTowerBuildingAutomationEnabled, ArcaneTowerTypes),
        };

        var behaviors = new List<RowModel>
        {
            Row(PinKeyMilReinforce, "automation_military_reinforcement",
                unlocks[PinKeyMilReinforce], settings.MilitaryReinforcementAutomationEnabled),
            Row(PinKeyMilVendetta, "automation_military_vendetta",
                unlocks[PinKeyMilVendetta], settings.MilitaryVendettaAutomationEnabled),
            Row(PinKeyMonumentInvestment, "automation_monument_investment", unlocks[PinKeyMonumentInvestment], settings.MonumentInvestmentAutomationEnabled),
        };

        var right = new List<SectionModel> { new("automation_header_behaviors", behaviors) };

        // Controles batiments : une ligne par type effectivement bati.
        var controls = new List<RowModel>();
        void BuildingControl<T>(string key, string nameKey, string descKey) where T : Building
        {
            if (!BuildingExists<T>(civ)) return;
            controls.Add(new RowModel(key, _localization.Get(nameKey), _localization.Get(descKey),
                null, AreAllActiveNullable<T>(civ), IsLocked: false, CanPin: true, null, CategoryOf(key)));
        }

        BuildingControl<Barracks>(PinKeyBarracks, "building_barracks_name", "tooltip_toggle_barracks");
        BuildingControl<Arsenal>(PinKeyArsenal, "building_arsenal_name", "tooltip_toggle_arsenal");

        // La restriction s'applique aux Casernes ET aux Arsenaux du layer : une ligne par layer
        // connu, seulement si l'un des deux existe.
        // Le quota est celui lu par SoldierProductionEngine : le meme pour tous les layers,
        // applique ville par ville. A 0, la restriction n'a aucun effet (rien a limiter) :
        // le reglage est masque plutot que d'afficher une case sans consequence.
        int freePerCity = (int)civ.ModifierAggregator.ApplyModifiers(Modifier.ECategory.SOLDIER_FOOD_FREE_PER_CITY, "", 0.0);
        if (freePerCity > 0 && (BuildingExists<Barracks>(civ) || BuildingExists<Arsenal>(civ)))
        {
            string note = _localization.GetFormated("automation_restrict_soldier_production_note", freePerCity);

            foreach (var layerZ in worldState.Layers.Keys.OrderBy(z => z))
            {
                string root = RestrictSoldierProductionKeyPrefix(layerZ);
                string key = RestrictSoldierProductionPinKey(layerZ);
                bool isRestricted = settings.RestrictSoldierProductionToFreeSoldiersByLayer.TryGetValue(layerZ, out var v) && v;
                controls.Add(new RowModel(key, _localization.Get(root + "_name"), _localization.Get(root + "_desc"),
                    note, isRestricted, IsLocked: false, CanPin: true, null, CategoryOf(key)));
            }
        }

        BuildingControl<Laboratory>(PinKeyLaboratory, "building_laboratory_name", "tooltip_toggle_lab");
        BuildingControl<Smelter>(PinKeySmelter, "building_smelter_name", "tooltip_toggle_smelter");
        BuildingControl<WeaponSmith>(PinKeyWeaponSmith, "building_weaponsmith_name", "tooltip_toggle_weaponsmith");
        BuildingControl<ArmorSmith>(PinKeyArmorSmith, "building_armorsmith_name", "tooltip_toggle_armorsmith");
        BuildingControl<AlchimistHut>(PinKeyAlchimistHut, "building_alchimisthut_name", "tooltip_toggle_alchimisthut");

        if (controls.Count > 0)
            right.Add(new SectionModel("automation_header_controls", controls));

        return ([new SectionModel("automation_header_buildings", buildings)], right);
    }

    private float DrawSections(SKCanvas canvas, List<SectionModel> sections, float x, float y, float width, Civilization civ)
    {
        foreach (var section in sections)
        {
            SkiaTextUtils.DrawText(canvas, _localization.Get(section.Header), x, y + 12, _nameFont, _accentPaint);
            y += 20f;

            foreach (var row in section.Rows)
            {
                if (row.IsLocked)
                {
                    y += DrawLockedRow(canvas, x, y, width, row.Name, row.Desc) + RowSpacing;
                    continue;
                }

                var (toggleRect, height) = DrawAutomationRow(canvas, x, y, width, row.IsOn,
                    _hoveredToggleKey == row.Key, row.Name, row.Desc, row.Note,
                    row.SummaryTypes != null ? civ.Cities : null, row.SummaryTypes,
                    row.CanPin ? row.Key : null, _hoveredPinKey == row.Key, _pinnedKeys.Contains(row.Key));
                _toggleRects.Add((toggleRect, row.Key));
                y += height + RowSpacing;
            }

            y += 4f;
        }
        return y;
    }

    private (SKRect toggleRect, float height) DrawAutomationRow(
        SKCanvas canvas, float x, float y, float width,
        bool? isOn, bool isHovered, string name, string desc,
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

            var (text, isEmpty) = FormatSummaryEntry(cities, type);
            SkiaTextUtils.DrawText(canvas, text, curX, y, _summaryFont, isEmpty ? _summaryEmptyPaint : _summaryBuiltPaint);
            curX += _summaryFont.MeasureText(text);
        }
    }

    /// <summary>
    /// Etat de construction d'un type de batiment, tel qu'affiche sous une ligne d'automatisme :
    /// « Scierie: 3×Niv2 4×Niv1 », ou « Scierie: - » si aucun n'est bati. Partage entre le rendu
    /// Skia et l'instantane destine a l'hote.
    /// </summary>
    private (string Text, bool IsEmpty) FormatSummaryEntry(IEnumerable<City> cities, BuildingType type)
    {
        var buildings = cities.SelectMany(c => c.Buildings)
            .Where(b => b.Type == type && b.Level >= 1)
            .ToList();

        string bldName = _localization.Get($"building_{type.ToString().ToLower()}_name");
        if (buildings.Count == 0) return ($"{bldName}: -", true);

        string lv = _localization.Get("level_abbrev");
        var groups = buildings.GroupBy(b => b.Level).OrderBy(g => g.Key);
        return ($"{bldName}: {string.Join(" ", groups.Select(g => $"{g.Count()}×{lv}{g.Key}"))}", false);
    }

    // ── Pont vers l'hote Avalonia ─────────────────────────────────────────────

    /// <summary>
    /// Instantane de l'onglet pour une vue portee par l'hote. Projette <see cref="BuildColumns"/>,
    /// la meme liste que celle que dessine le rendu Skia : les conditions de deblocage, l'ordre
    /// des lignes et leur etat n'existent qu'a un seul endroit.
    /// </summary>
    /// <param name="isVisible">L'onglet Automatisation est-il actif ? La regle appartient a
    /// OverlayRenderer, qui detient l'onglet courant.</param>
    public AutomationSnapshot GetSnapshot(bool isVisible)
    {
        if (_disposed || !isVisible) return AutomationSnapshot.Hidden;

        var civ = _gameControllerService.PlayerCivilization;
        var worldState = _gameControllerService.CurrentWorldState;
        var gameState = _gameControllerService.CurrentGameState;
        if (civ == null || worldState == null || gameState == null) return AutomationSnapshot.Hidden;

        var pinned = gameState.Settings.PinnedCivPanelKeys;
        var (left, right) = BuildColumns(civ, worldState);
        bool presetsUnlocked = civ.TechnologyTree.CompletedTechnologies.Contains(TechId.AutomationPreset);

        IReadOnlyList<AutomationSectionSnapshot> Project(List<SectionModel> sections) =>
            sections.Select(section => new AutomationSectionSnapshot(
                _localization.Get(section.Header),
                section.Rows.Select(row => new AutomationRowSnapshot(
                    Key: row.Key,
                    Name: row.Name,
                    Description: row.Desc,
                    Note: row.Note,
                    IsOn: row.IsOn,
                    IsLocked: row.IsLocked,
                    CanPin: row.CanPin,
                    IsPinned: pinned.Contains(row.Key),
                    SummaryLines: row.SummaryTypes == null
                        ? []
                        : row.SummaryTypes.Select(t => FormatSummaryEntry(civ.Cities, t).Text).ToList(),
                    Category: row.Category))
                    .ToList()))
                .ToList();

        return new AutomationSnapshot(
            IsVisible: true,
            Title: _localization.Get("automation_title"),
            GlobalToggleLabel: _localization.Get("settings_automations_enabled"),
            GlobalToggleOn: gameState.Settings.AutomationsEnabled,
            PinTooltip: _localization.Get("tooltip_pin_to_civ_panel"),
            PresetBarVisible: presetsUnlocked,
            ActivePreset: gameState.GodState.AutomationPresets.ActivePreset,
            PresetChangeButtonLabel: _localization.Get("automation_preset_change_button"),
            LeftColumn: Project(left),
            RightColumn: Project(right));
    }

    /// <summary>Bascule le preset d'automatisation actif (1 a 3), depuis la vue de l'hote.</summary>
    public void SelectAutomationPresetFromHost(int preset) =>
        _gameControllerService.CurrentGameState?.GodState.AutomationPresets.SetActivePreset(preset);

    private bool _presetPopupOpen;

    /// <summary>Instantane du popup d'edition des presets pour une vue portee par l'hote.</summary>
    public AutomationPresetPopupSnapshot GetAutomationPresetPopupSnapshot()
    {
        var gameState = _gameControllerService.CurrentGameState;
        if (!_presetPopupOpen || _disposed || gameState == null) return AutomationPresetPopupSnapshot.Closed;

        var presets = gameState.GodState.AutomationPresets;
        var rows = BuildingController.PresetTableBuildingTypes
            .Select(type => new AutomationPresetRowSnapshot(
                Key: type.ToString(),
                Name: _localization.Get($"building_{type.ToString().ToLower()}_name"),
                MaxLevel: Math.Min(SettlersOfIdlestan.Model.Prestige.AutomationPresetSettings.MaxCap, BuildingController.CreateBuilding(type)!.GetAbsoluteMaxLevel()),
                Preset1: presets.GetCap(1, type),
                Preset2: presets.GetCap(2, type),
                Preset3: presets.GetCap(3, type)))
            .OrderBy(row => row.Name, StringComparer.CurrentCulture)
            .ToList();

        return new AutomationPresetPopupSnapshot(
            true,
            _localization.Get("automation_preset_popup_title"),
            _localization.Get("automation_preset_popup_building_header"),
            _localization.Get("automation_preset_column_zero_tooltip"),
            _localization.Get("automation_preset_column_max_tooltip"),
            presets.ActivePreset,
            rows);
    }

    public void OpenAutomationPresetPopupFromHost() => _presetPopupOpen = true;
    public void CloseAutomationPresetPopupFromHost() => _presetPopupOpen = false;

    /// <summary>Modifie le plafond d'un batiment pour un preset donne, depuis la vue de l'hote.</summary>
    public void SetAutomationPresetCapFromHost(string buildingKey, int preset, int value)
    {
        var gameState = _gameControllerService.CurrentGameState;
        if (gameState == null || !Enum.TryParse<BuildingType>(buildingKey, out var type)) return;
        gameState.GodState.AutomationPresets.SetCap(preset, type, value);
    }

    /// <summary>Epingle ou desepingle une ligne au panneau civilisation, depuis la vue de l'hote.</summary>
    public void TogglePinFromHost(string key)
    {
        var settings = _gameControllerService.CurrentGameState?.Settings;
        if (settings == null) return;
        if (!settings.PinnedCivPanelKeys.Remove(key)) settings.PinnedCivPanelKeys.Add(key);
    }

    /// <summary>
    /// Bascule l'interrupteur global. Les reglages par ligne restent stockes tels quels et
    /// reprennent effet des sa reactivation.
    /// </summary>
    public void ToggleGlobalFromHost()
    {
        var settings = _gameControllerService.CurrentGameState?.Settings;
        if (settings == null) return;
        settings.AutomationsEnabled = !settings.AutomationsEnabled;
    }

    /// <summary>
    /// Applique la bascule d'une ligne. Aiguillage unique, partage par le hit-testing Skia et
    /// par la vue de l'hote — une garde dupliquee finirait par diverger, comme l'ont fait les
    /// switch d'epinglage du panneau civilisation.
    /// </summary>
    public void ToggleByKey(string key)
    {
        var state = _gameControllerService.CurrentWorldState;
        var civ = _gameControllerService.PlayerCivilization;
        if (state == null || civ == null) return;
        var settings = state.AutomationSettings;

        switch (key)
        {
            case PinKeyRoad:               settings.RoadAutomationEnabled = !settings.RoadAutomationEnabled; return;
            case PinKeyRoadUnderworld:     settings.RoadAutomationEnabledUnderworld = !settings.RoadAutomationEnabledUnderworld; return;
            case PinKeyOutpost:            settings.OutpostAutomationEnabled = !settings.OutpostAutomationEnabled; return;
            case PinKeyOutpostUnderworld:  settings.OutpostAutomationEnabledUnderworld = !settings.OutpostAutomationEnabledUnderworld; return;
            case PinKeyTownHall:           settings.TownHallAutomationEnabled = !settings.TownHallAutomationEnabled; return;
            case PinKeyProduction:         settings.ProductionBuildingAutomationEnabled = !settings.ProductionBuildingAutomationEnabled; return;
            case PinKeyArtisan:            settings.ArtisanBuildingAutomationEnabled = !settings.ArtisanBuildingAutomationEnabled; return;
            case PinKeyLibrary:            settings.LibraryBuildingAutomationEnabled = !settings.LibraryBuildingAutomationEnabled; return;
            case PinKeyMarket:             settings.MarketBuildingAutomationEnabled = !settings.MarketBuildingAutomationEnabled; return;
            case PinKeySeaport:            settings.SeaportBuildingAutomationEnabled = !settings.SeaportBuildingAutomationEnabled; return;
            case PinKeyMilBuildings:       settings.MilitaryBuildingAutomationEnabled = !settings.MilitaryBuildingAutomationEnabled; return;
            case PinKeyGrandTemple:        settings.TempleAutomationEnabled = !settings.TempleAutomationEnabled; return;
            case PinKeyMithrilMine:        settings.MithrilMineBuildingAutomationEnabled = !settings.MithrilMineBuildingAutomationEnabled; return;
            case PinKeyArcaneTower:        settings.ArcaneTowerBuildingAutomationEnabled = !settings.ArcaneTowerBuildingAutomationEnabled; return;
            case PinKeyMonumentInvestment: settings.MonumentInvestmentAutomationEnabled = !settings.MonumentInvestmentAutomationEnabled; return;

            // Couper le renfort automatique doit aussi vider les flux deja etablis, sans quoi ils
            // continueraient de tourner apres l'arret.
            case PinKeyMilReinforce:
                settings.MilitaryReinforcementAutomationEnabled = !settings.MilitaryReinforcementAutomationEnabled;
                if (!settings.MilitaryReinforcementAutomationEnabled)
                    _gameControllerService.MainGameController.MilitaryController.ClearReinforcementFlows(civ);
                return;

            // La vendetta relance des pillages : basculer le reglage arrete celui en cours.
            case PinKeyMilVendetta:
                settings.MilitaryVendettaAutomationEnabled = !settings.MilitaryVendettaAutomationEnabled;
                _gameControllerService.MainGameController.MilitaryController.StopRaid(civ);
                return;

            case PinKeyBarracks:     ToggleAll<Barracks>(civ); return;
            case PinKeyArsenal:      ToggleAll<Arsenal>(civ); return;
            case PinKeyLaboratory:   ToggleAll<Laboratory>(civ); return;
            case PinKeySmelter:      ToggleAll<Smelter>(civ); return;
            case PinKeyWeaponSmith:  ToggleAll<WeaponSmith>(civ); return;
            case PinKeyArmorSmith:   ToggleAll<ArmorSmith>(civ); return;
            case PinKeyAlchimistHut: ToggleAll<AlchimistHut>(civ); return;
        }

        // Restriction de production de soldats : une cle par layer.
        foreach (var layerZ in state.Layers.Keys)
        {
            if (RestrictSoldierProductionPinKey(layerZ) != key) continue;
            var byLayer = settings.RestrictSoldierProductionToFreeSoldiersByLayer;
            byLayer[layerZ] = !(byLayer.TryGetValue(layerZ, out var current) && current);
            return;
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
        _hoveredGlobalAutomationsToggle = !_globalAutomationsToggleRect.IsEmpty && _globalAutomationsToggleRect.Contains(adj.X, adj.Y);

        _hoveredToggleKey = null;
        foreach (var (rect, key) in _toggleRects)
        {
            if (rect.Contains(adj.X, adj.Y)) { _hoveredToggleKey = key; break; }
        }

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

        // Interrupteur global (voir GameSettings.AutomationsEnabled)
        if (!_globalAutomationsToggleRect.IsEmpty && _globalAutomationsToggleRect.Contains(adj.X, adj.Y))
        {
            var gameSettings = _gameControllerService.CurrentGameState!.Settings;
            gameSettings.AutomationsEnabled = !gameSettings.AutomationsEnabled;
            return true;
        }

        // Bascules : une seule boucle sur les rectangles de la frame, puis un aiguillage par cle.
        foreach (var (rect, key) in _toggleRects)
        {
            if (!rect.Contains(adj.X, adj.Y)) continue;
            ToggleByKey(key);
            return true;
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

    /// <summary>Préfixe de clé de localisation pour la ligne "restreindre aux soldats gratuits" d'un layer donné.</summary>
    private static string RestrictSoldierProductionKeyPrefix(int layerZ) => layerZ switch
    {
        LayerState.UnderworldZ => "automation_restrict_soldier_production_underworld",
        LayerState.AbyssZ      => "automation_restrict_soldier_production_abyss",
        LayerState.PandemoniumZ => "automation_restrict_soldier_production_pandemonium",
        _                      => "automation_restrict_soldier_production",
    };

    /// <summary>Clé de pin (PinnedCivPanelKeys) pour la ligne "restreindre aux soldats gratuits" d'un layer donné.</summary>
    private static string RestrictSoldierProductionPinKey(int layerZ) => layerZ switch
    {
        LayerState.UnderworldZ => PinKeyRestrictSoldierProductionUnderworld,
        LayerState.AbyssZ      => PinKeyRestrictSoldierProductionAbyss,
        LayerState.PandemoniumZ => PinKeyRestrictSoldierProductionPandemonium,
        _                      => PinKeyRestrictSoldierProduction,
    };

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
