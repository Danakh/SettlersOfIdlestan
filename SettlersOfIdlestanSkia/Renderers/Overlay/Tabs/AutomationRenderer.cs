using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestanSkia.Services.Localization;
using SettlersOfIdlestanSkia.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using TechId = SettlersOfIdlestan.Model.Civilization.TechnologyId;

namespace SettlersOfIdlestanSkia.Renderers.Overlay.Tabs;

public sealed class AutomationRenderer : IDisposable
{
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
    internal const string PinKeyAbundanceAutoCast = "AbundanceAutoCast";
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
            [PinKeyAbundanceAutoCast] = AutomationCategory.Behavior,

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

    private bool _disposed;

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

    public AutomationRenderer(GameControllerService gameControllerService, LocalizationService localization)
    {
        _gameControllerService = gameControllerService;
        _localization = localization;
    }

    // ── Definition unique des lignes ──────────────────────────────────────────
    //
    // Le rendu Skia, son hit-testing et l'instantane destine a l'hote lisent tous cette liste.
    // Elle etait auparavant ecrite trois fois : une vingtaine d'appels de dessin, autant de
    // rectangles memorises, et autant de branches dans le gestionnaire de clic.

    /// <param name="Key">Cle d'epinglage de la ligne, qui lui sert aussi d'identifiant.</param>
    /// <param name="IsOn">Null pour un etat mixte (certains batiments du type actifs, d'autres non).</param>
    /// <param name="IsLocked">Ligne verrouillee : pas de bascule, Desc porte la raison du verrouillage.</param>
    /// <param name="CanDemobilize">Ligne de restriction de production de soldats : affiche un
    /// bouton "Demobiliser" qui ramene les soldats du layer au quota nourri gratuitement.</param>
    private sealed record RowModel(
        string Key,
        string Name,
        string Desc,
        string? Note,
        bool? IsOn,
        bool IsLocked,
        bool CanPin,
        BuildingType[]? SummaryTypes,
        AutomationCategory Category,
        bool CanDemobilize = false);

    private sealed record SectionModel(string Header, List<RowModel> Rows);

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

        // Bâtiment unique permanent accordé par l'Ascension (voir
        // AscensionController.ApplyPermanentUniqueBuildingToCivilization) : ne vit dans aucune ville,
        // donc invisible à la boucle ci-dessus — seul Civilization.GetUniqueBuilding l'expose.
        buildersGuild ??= civ.GetUniqueBuilding(BuildingType.BuildersGuild) as BuildersGuild;
        harvestersGuild ??= civ.GetUniqueBuilding(BuildingType.HarvestersGuild) as HarvestersGuild;
        artisansGuild ??= civ.GetUniqueBuilding(BuildingType.ArtisansGuild) as ArtisansGuild;
        academy ??= civ.GetUniqueBuilding(BuildingType.Academy) as Academy;
        traderGuild ??= civ.GetUniqueBuilding(BuildingType.TraderGuild) as TraderGuild;
        imperialPort ??= civ.GetUniqueBuilding(BuildingType.ImperialPort) as ImperialPort;
        warRoom ??= civ.GetUniqueBuilding(BuildingType.WarRoom) as WarRoom;
        grandTemple ??= civ.GetUniqueBuilding(BuildingType.GrandTemple) as GrandTemple;
        volcanicForge ??= civ.GetUniqueBuilding(BuildingType.VolcanicForge) as VolcanicForge;
        arcaneTower ??= civ.GetUniqueBuilding(BuildingType.ArcaneTower) as ArcaneTower;

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
            [PinKeySeaport] = imperialPort is { Level: >= 2 },
            [PinKeyMilBuildings] = warRoom is { Level: >= 1 },
            [PinKeyGrandTemple] = grandTemple is { Level: >= 1 },
            [PinKeyMithrilMine] = volcanicForge is { Level: >= 1 },
            [PinKeyArcaneTower] = arcaneTower is { Level: >= 1 },
            [PinKeyMilReinforce] = civ.TechnologyTree.CompletedTechnologies.Contains(TechId.AdvancedTactics),
            [PinKeyMilVendetta] = civ.TechnologyTree.CompletedTechnologies.Contains(TechId.Vendetta),
            [PinKeyMonumentInvestment] = true,
            [PinKeyAbundanceAutoCast] = civ.ModifierAggregator.HasModifier(Modifier.ECategory.UNLOCK_SPELL, nameof(SettlersOfIdlestan.Model.Magic.SpellId.Abundance)),
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
            Row(PinKeyAbundanceAutoCast, "automation_abundance_autocast", unlocks[PinKeyAbundanceAutoCast], settings.AbundanceAutoCastEnabled),
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
                    note, isRestricted, IsLocked: false, CanPin: true, null, CategoryOf(key), CanDemobilize: true));
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

    /// <summary>
    /// Etat de construction d'un type de batiment, tel qu'affiche sous une ligne d'automatisme :
    /// « Scierie: 3×Niv2 4×Niv1 », ou « Scierie: - » si aucun n'est bati. Partage entre le rendu
    /// Skia et l'instantane destine a l'hote.
    /// </summary>
    /// <param name="settings">Non-null pour ajouter, a droite de la ligne, le plafond du preset
    /// actif pour ce type (« (preset 5) ») — uniquement une fois TechnologyId.AutomationPreset
    /// debloquee (voir GetSnapshot), pour ne pas afficher un plafond que le joueur ne peut pas
    /// encore configurer (AutomationSettings.GetActivePresetCap retombe de toute facon sur le
    /// plafond illimite tant que ce n'est pas le cas).</param>
    private (string Text, bool IsEmpty) FormatSummaryEntry(IEnumerable<City> cities, BuildingType type, Civilization? civ = null, AutomationSettings? settings = null)
    {
        var buildings = cities.SelectMany(c => c.Buildings)
            .Where(b => b.Type == type && b.Level >= 1)
            .ToList();

        string bldName = _localization.Get($"building_{type.ToString().ToLower()}_name");
        string text = buildings.Count == 0
            ? $"{bldName}: -"
            : $"{bldName}: {string.Join(" ", buildings.GroupBy(b => b.Level).OrderBy(g => g.Key)
                .Select(g => $"{g.Count()}×{_localization.Get("level_abbrev")}{g.Key}"))}";

        if (settings != null && civ != null)
            text += $" {_localization.GetFormated("automation_preset_cap_suffix", settings.GetActivePresetCap(type, civ))}";

        return (text, buildings.Count == 0);
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
                        : row.SummaryTypes.Select(t => FormatSummaryEntry(civ.Cities, t,
                            presetsUnlocked ? civ : null, presetsUnlocked ? worldState.AutomationSettings : null).Text).ToList(),
                    Category: row.Category,
                    CanDemobilize: row.CanDemobilize))
                    .ToList()))
                .ToList();

        return new AutomationSnapshot(
            IsVisible: true,
            Title: _localization.Get("automation_title"),
            GlobalToggleLabel: _localization.Get("settings_automations_enabled"),
            GlobalToggleOn: gameState.Settings.AutomationsEnabled,
            PinTooltip: _localization.Get("tooltip_pin_to_civ_panel"),
            DemobilizeButtonLabel: _localization.Get("automation_demobilize_button"),
            DemobilizeButtonTooltip: _localization.Get("tooltip_demobilize"),
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
                MaxLevel: Math.Min(SettlersOfIdlestan.Model.Prestige.AutomationPresetSettings.MaxCap, BuildingFactory.Create(type)!.GetAbsoluteMaxLevel()),
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
            case PinKeyAbundanceAutoCast:  settings.AbundanceAutoCastEnabled = !settings.AbundanceAutoCastEnabled; return;

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

    /// <summary>
    /// Bouton "Demobiliser" d'une ligne de restriction de production de soldats : ramene les
    /// soldats du layer correspondant au quota nourri gratuitement. Cle de layer resolue de la
    /// meme facon que dans <see cref="ToggleByKey"/> — un layer inexistant (cle perimee) ne fait
    /// simplement rien.
    /// </summary>
    public void DemobilizeFromHost(string key)
    {
        var state = _gameControllerService.CurrentWorldState;
        var civ = _gameControllerService.PlayerCivilization;
        if (state == null || civ == null) return;

        foreach (var layerZ in state.Layers.Keys)
        {
            if (RestrictSoldierProductionPinKey(layerZ) != key) continue;
            _gameControllerService.MainGameController.MilitaryController.DemobilizeSoldiersAboveFreeLimit(civ, layerZ);
            return;
        }
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

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
    }
}
