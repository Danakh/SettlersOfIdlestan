using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestanSkia.Services.Localization;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Services;
using SkiaSharp;
using Svg.Skia;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Controller.Military;
using SettlersOfIdlestan.Controller.Island;

namespace SettlersOfIdlestanSkia.Renderers.Overlay.Panels;

public class SelectedCityPanelRenderer : PanelRendererBase
{
    private readonly LocalizationService _localization;
    private readonly CityBuildingService _cityBuildingService;
    private readonly InputHandlingService _inputService;
    private readonly ResourceManager _resourceManager;
    private readonly Dictionary<Resource, SKSvg?> _resourceIcons = new();

    private SKPoint _lastPointerPosition = SKPoint.Empty;
    private BuildingType? _hoveredBuildingType = null;

    private bool _showUniqueBuildings = false;
    private BuildingType? _hoveredActivationCheckbox = null;

    public Action<int, float, float>? CenterCameraOnMapPosition { get; set; }

    public SelectedCityPanelRenderer(CityBuildingService cityBuildingService, LocalizationService localization, InputHandlingService inputService, ResourceManager resourceManager)
    {
        _cityBuildingService = cityBuildingService;
        _inputService = inputService;
        _localization = localization;
        _resourceManager = resourceManager;
        _cityBuildingService.SelectionChanged += (_, _) => Collapsed = false;
    }

    public override void Initialize(SKSize canvasSize)
    {
        base.Initialize(canvasSize);

        foreach (Resource resource in Enum.GetValues(typeof(Resource)))
        {
            string name = resource.ToString().ToLower();
            _resourceIcons[resource] = _resourceManager.LoadImage($"Resources.icons.resources.{name}.svg");
        }
    }

    public void Close()
    {
        _cityBuildingService.ClearSelectedCity();
        _hoveredBuildingType = null;
        ScrollOffset = 0;
    }

    /// <summary>
    /// Dessine le tooltip de survol (bâtiment ou case d'activation). S'affiche par-dessus la
    /// carte, hors du panneau, et reste donc produit en Skia pendant que la vue du panneau
    /// lui-même est portée par l'hôte Avalonia.
    /// </summary>
    private void DrawHoverTooltip(SKCanvas canvas, List<Building> visibleBuildings)
    {
        if (_hoveredBuildingType.HasValue)
        {
            var hoveredBuilding = visibleBuildings.FirstOrDefault(b => b.Type == _hoveredBuildingType.Value);
            if (hoveredBuilding != null)
            {
                var buildingName = hoveredBuilding.Level == 0
                    ? _localization.GetFormated("tooltip_build_building", _localization.Get(hoveredBuilding.NameKey))
                    : _localization.Get(hoveredBuilding.NameKey);
                var levelDescriptionKey = hoveredBuilding.DescriptionKey + "_" + hoveredBuilding.Level;
                var description = _localization.Get(levelDescriptionKey);
                if (levelDescriptionKey == description)
                    description = _localization.Get(hoveredBuilding.DescriptionKey);

                if (hoveredBuilding is Market)
                {
                    int maxLevel = _cityBuildingService.GetMaxLevel(hoveredBuilding);
                    description = maxLevel >= 4
                        ? _localization.Get("building_market_desc_maxlvl4")
                        : _localization.Get("building_market_desc");
                }

                var cost = hoveredBuilding.Level == 0 ? hoveredBuilding.GetBuildCost() : hoveredBuilding.GetUpgradeCost(hoveredBuilding.Level + 1);

                var tooltipLines = new List<string> { buildingName, "", description, "" };

                bool tooltipIsOtherCity = hoveredBuilding.Level > 0 && !_cityBuildingService.IsBuiltInSelectedCity(hoveredBuilding);
                if (tooltipIsOtherCity)
                {
                    tooltipLines.Add(_localization.Get("tooltip_unique_other_city"));
                    tooltipLines.Add("");
                }
                else if (hoveredBuilding.Level == 0 && _cityBuildingService.SelectedCity != null)
                {
                    if (hoveredBuilding.IsUnique && _cityBuildingService.SelectedCityHasAnyUniqueBuilding())
                    {
                        tooltipLines.Add(_localization.Get("tooltip_unique_city_limit"));
                        tooltipLines.Add("");
                    }
                    else
                    {
                        var missingKey = _cityBuildingService.GetMissingPrerequisiteKey(hoveredBuilding);
                        if (missingKey != null)
                        {
                            tooltipLines.Add(_localization.Get(missingKey));
                            tooltipLines.Add("");
                        }
                    }
                }

                if (!tooltipIsOtherCity)
                {
                    var buildWarningKey = _cityBuildingService.GetBuildWarningKey(hoveredBuilding);
                    if (buildWarningKey != null)
                    {
                        tooltipLines.Add(_localization.Get(buildWarningKey));
                        tooltipLines.Add("");
                    }
                }

                var manualHarvestRes = hoveredBuilding.ManualHarvestResource;
                var autoHarvestRes   = hoveredBuilding.AutomaticHarvestResource;
                if (hoveredBuilding.Level > 0 && (manualHarvestRes.HasValue || autoHarvestRes.HasValue))
                {
                    bool isAtMaxForHarvest = _cityBuildingService.IsAtMaxLevel(hoveredBuilding);
                    int displayLvl = hoveredBuilding.Level;

                    tooltipLines.Add(_localization.Get("tooltip_harvest_header"));

                    if (manualHarvestRes.HasValue)
                    {
                        var manualName = _localization.Get("resource_" + manualHarvestRes.Value.ToString().ToLower());
                        tooltipLines.Add(_localization.Get("tooltip_harvest_manual") + " " + manualName);
                    }

                    if (autoHarvestRes.HasValue)
                    {
                        bool autoActive = displayLvl >= hoveredBuilding.AutomaticHarvestUnlockLevel;
                        var autoName = _localization.Get("resource_" + autoHarvestRes.Value.ToString().ToLower());
                        tooltipLines.Add(_localization.Get("tooltip_harvest_auto") + " " + (autoActive ? autoName : "—"));
                        if (!autoActive && !isAtMaxForHarvest && displayLvl + 1 >= hoveredBuilding.AutomaticHarvestUnlockLevel)
                            tooltipLines.Add(_localization.Get("tooltip_harvest_auto_next") + " " + autoName);

                        double speedMult = _cityBuildingService.GetHarvestSpeedMultiplier(hoveredBuilding);
                        if (autoActive)
                        {
                            long rawCooldown = hoveredBuilding.GetAutomaticHarvestCooldown(HarvestController.AutomaticHarvestCooldownTicks, displayLvl);
                            long effective = Math.Max(1L, (long)(rawCooldown / speedMult));
                            tooltipLines.Add(_localization.Get("tooltip_harvest_rate") + $" {effective / 100.0:0.0}s");
                            if (!isAtMaxForHarvest)
                            {
                                long nextRaw = hoveredBuilding.GetAutomaticHarvestCooldown(HarvestController.AutomaticHarvestCooldownTicks, displayLvl + 1);
                                long nextEffective = Math.Max(1L, (long)(nextRaw / speedMult));
                                if (nextEffective != effective)
                                    tooltipLines.Add(_localization.Get("tooltip_harvest_auto_next") + $" {nextEffective / 100.0:0.0}s");
                            }
                        }
                        else if (!isAtMaxForHarvest && displayLvl + 1 >= hoveredBuilding.AutomaticHarvestUnlockLevel)
                        {
                            int activateLevel = hoveredBuilding.AutomaticHarvestUnlockLevel;
                            long previewRaw = hoveredBuilding.GetAutomaticHarvestCooldown(HarvestController.AutomaticHarvestCooldownTicks, activateLevel);
                            long previewEffective = Math.Max(1L, (long)(previewRaw / speedMult));
                            tooltipLines.Add(_localization.Get("tooltip_harvest_rate") + $" {previewEffective / 100.0:0.0}s");
                        }
                    }

                    tooltipLines.Add("");
                }

                if (hoveredBuilding is VolcanicForge volcanicForge && volcanicForge.Level > 0)
                {
                    int glassBonus   = VolcanicForge.GlassGenerationPerLevel * volcanicForge.Level;
                    int oreBonus     = VolcanicForge.OreHarvestBonusPerLevel * volcanicForge.Level;
                    int steelBonus   = VolcanicForge.SteelBonusPerLevel * volcanicForge.Level;
                    int mithrilBonus = VolcanicForge.MithrilHarvestBonusPerLevel * volcanicForge.Level;
                    tooltipLines.Add(_localization.GetFormated("volcanicforge_glass_bonus", glassBonus));
                    tooltipLines.Add(_localization.GetFormated("volcanicforge_ore_bonus", oreBonus));
                    tooltipLines.Add(_localization.GetFormated("volcanicforge_steel_bonus", steelBonus));
                    tooltipLines.Add(_localization.GetFormated("volcanicforge_mithril_bonus", mithrilBonus));
                    if (!_cityBuildingService.IsAtMaxLevel(volcanicForge))
                    {
                        int nextLevel = volcanicForge.Level + 1;
                        tooltipLines.Add(_localization.Get("tooltip_harvest_auto_next")
                            + $" +{VolcanicForge.GlassGenerationPerLevel * nextLevel} " + _localization.Get("resource_glass")
                            + $", +{VolcanicForge.OreHarvestBonusPerLevel * nextLevel}% " + _localization.Get("resource_ore")
                            + $", +{VolcanicForge.SteelBonusPerLevel * nextLevel} " + _localization.Get("resource_steel")
                            + $", +{VolcanicForge.MithrilHarvestBonusPerLevel * nextLevel}% " + _localization.Get("resource_mithril"));
                    }
                    tooltipLines.Add("");
                }

                if (hoveredBuilding is Forge forge && forge.Level > 0)
                {
                    int forgeBonus  = _cityBuildingService.GetSelectedCivilizationForgeBonus(forge);
                    int totalChance = forge.DoubleProdChancePercent + forgeBonus;
                    string forgeText = _localization.Get("forge_double_prod") + $" {totalChance}%";
                    if (forgeBonus > 0)
                        forgeText += $" ({forge.DoubleProdChancePercent}% + {forgeBonus}% {_localization.Get("forge_double_prod_research_bonus")})";
                    tooltipLines.Add(forgeText);
                    if (!_cityBuildingService.IsAtMaxLevel(forge))
                    {
                        int civBonusPerLevel = _cityBuildingService.GetCivilizationForgeHarvestBonusPerLevel();
                        int nextChance = (forge.Level + 1) * (10 + civBonusPerLevel);
                        tooltipLines.Add(_localization.Get("tooltip_harvest_auto_next") + $" {nextChance}%");
                    }
                    tooltipLines.Add("");
                }

                if (hoveredBuilding is AdventurersGuild adventurersGuild && adventurersGuild.Level > 0)
                {
                    var activeAdventurer = _cityBuildingService.GetActiveAdventurer();
                    if (activeAdventurer != null)
                    {
                        tooltipLines.Add(_localization.GetFormated("adventurersguild_current_hp",
                            activeAdventurer.Hp, activeAdventurer.MaxHp));
                    }
                    else
                    {
                        long currentTick = _cityBuildingService.GetCurrentTick();
                        long elapsed = adventurersGuild.LastAdventurerDeathTick == 0 ? long.MaxValue : currentTick - adventurersGuild.LastAdventurerDeathTick;
                        long remaining = Math.Max(0, AdventurersGuild.AdventurerRespawnCooldownTicks - elapsed);
                        tooltipLines.Add(_localization.Get("adventurersguild_respawn_cooldown") + $" {remaining / 100.0:0.0}s");
                    }
                    tooltipLines.Add("");
                }

                if (hoveredBuilding is Mine && hoveredBuilding.Level > 0)
                {
                    int goldChance = _cityBuildingService.GetSelectedCivilizationMineGoldChancePercent();
                    if (goldChance > 0)
                    {
                        tooltipLines.Add(_localization.Get("mine_gold_bonus") + $" {goldChance}%");
                        tooltipLines.Add("");
                    }
                }

                if (hoveredBuilding is Palisade palisade && palisade.Level > 0)
                {
                    int defenseBonus = palisade.GetDefenseBonusAtLevel(palisade.Level);
                    double regenBonus = palisade.GetDefenseRegenBonusAtLevel(palisade.Level);
                    tooltipLines.Add(_localization.GetFormated("palisade_defense_bonus", defenseBonus));
                    if (regenBonus > 0)
                        tooltipLines.Add(_localization.GetFormated("palisade_regen_bonus", (int)Math.Round(regenBonus * 100)));
                    if (!_cityBuildingService.IsAtMaxLevel(palisade))
                    {
                        int nextDefense = palisade.GetDefenseBonusAtLevel(palisade.Level + 1);
                        double nextRegen = palisade.GetDefenseRegenBonusAtLevel(palisade.Level + 1);
                        if (nextDefense != defenseBonus)
                            tooltipLines.Add(_localization.Get("tooltip_harvest_auto_next") + " " + _localization.GetFormated("palisade_defense_bonus", nextDefense));
                        if (nextRegen != regenBonus)
                            tooltipLines.Add(_localization.Get("tooltip_harvest_auto_next") + " " + _localization.GetFormated("palisade_regen_bonus", (int)Math.Round(nextRegen * 100)));
                    }
                    tooltipLines.Add("");
                }

                if (hoveredBuilding is Seaport seaportBuilding && seaportBuilding.Level >= 3)
                {
                    long currentTick = _cityBuildingService.GetCurrentTick();
                    long elapsed = seaportBuilding.LastGenerationTick == 0 ? 0 : currentTick - seaportBuilding.LastGenerationTick;
                    long effectiveCooldown = _cityBuildingService.GetEffectiveSeaportGenerationCooldown(seaportBuilding);
                    long remaining = Math.Max(0, effectiveCooldown - elapsed);
                    tooltipLines.Add(_localization.Get("seaport_generation_cooldown") + $" {remaining/100.0:0.0}s/{effectiveCooldown/100.0:0.0}s");
                    tooltipLines.Add("");
                }

                if (hoveredBuilding is Market market && market.Level > 0)
                {
                    long currentTick = _cityBuildingService.GetCurrentTick();
                    long elapsed   = market.LastGoldGenerationTick == 0 ? 0 : currentTick - market.LastGoldGenerationTick;
                    long cooldown  = _cityBuildingService.GetMarketGoldGenerationCooldown(market);
                    long remaining = Math.Max(0, cooldown - elapsed);
                    tooltipLines.Add(_localization.Get("market_gold_cooldown") + $" {remaining / 100.0:0.0}s/{cooldown / 100.0:0.0}s");
                    if (!_cityBuildingService.IsAtMaxLevel(market))
                    {
                        long nextCooldown = _cityBuildingService.GetMarketGoldGenerationCooldown(market, market.Level + 1);
                        if (nextCooldown != cooldown)
                            tooltipLines.Add(_localization.Get("tooltip_building_prestige_next") + $" {nextCooldown / 100.0:0.0}s");
                    }
                    tooltipLines.Add("");
                }

                if (hoveredBuilding is Barracks barracks && barracks.Level > 0)
                {
                    tooltipLines.Add(_localization.GetFormated("barracks_max_soldiers_bonus", barracks.GetMaxSoldiersBonus()));
                    if (!_cityBuildingService.IsAtMaxLevel(barracks))
                    {
                        int nextBonus = Barracks.MaxSoldiersPerLevel * (barracks.Level + 1);
                        tooltipLines.Add(_localization.Get("tooltip_harvest_auto_next") + " " + _localization.GetFormated("barracks_max_soldiers_bonus", nextBonus));
                    }

                    long prodInterval = _cityBuildingService.GetBarracksProductionIntervalTicks();
                    double intervalSecs = prodInterval / 100.0;
                    double orePerSec = 1.0 / intervalSecs;
                    tooltipLines.Add(_localization.GetFormated("barracks_production_cost",
                        orePerSec.ToString("G3"),
                        intervalSecs.ToString("F1")));
                    double feedSecs = MilitaryController.SoldierFeedIntervalTicks / 100.0;
                    tooltipLines.Add(_localization.GetFormated("barracks_food_upkeep", feedSecs.ToString("F0")));

                    tooltipLines.Add("");
                }

                if (hoveredBuilding is BuildersGuild buildersGuild && buildersGuild.Level > 0)
                {
                    long currentTick = _cityBuildingService.GetCurrentTick();

                    long roadElapsed   = buildersGuild.LastRoadBuildTick == 0 ? 0 : currentTick - buildersGuild.LastRoadBuildTick;
                    long roadRemaining = Math.Max(0, RoadController.AutoRoadBuildCooldownTicks - roadElapsed);
                    tooltipLines.Add(_localization.Get("buildersguild_next_road") + $" {roadRemaining / 100.0:0.0}s/{RoadController.AutoRoadBuildCooldownTicks / 100.0:0.0}s");

                    if (buildersGuild.Level >= 4)
                    {
                        long outpostElapsed   = buildersGuild.LastOutpostBuildTick == 0 ? 0 : currentTick - buildersGuild.LastOutpostBuildTick;
                        long outpostRemaining = Math.Max(0, CityBuilderController.AutoOutpostBuildCooldownTicks - outpostElapsed);
                        tooltipLines.Add(_localization.Get("buildersguild_next_outpost") + $" {outpostRemaining / 100.0:0.0}s/{CityBuilderController.AutoOutpostBuildCooldownTicks / 100.0:0.0}s");
                    }

                    tooltipLines.Add("");
                }

                if (hoveredBuilding is Library library && library.Level > 0)
                {
                    bool isLibraryAtMax = _cityBuildingService.IsAtMaxLevel(library);
                    if (library.CanProduceResearch)
                    {
                        long currentTick = _cityBuildingService.GetCurrentTick();
                        long elapsed   = library.LastResearchTick == 0 ? 0 : currentTick - library.LastResearchTick;
                        long cooldown  = library.GetResearchCooldownTicks();
                        long remaining = Math.Max(0, cooldown - elapsed);
                        tooltipLines.Add(_localization.Get("library_research_cooldown") + $" {remaining / 100.0:0.0}s/{cooldown / 100.0:0.0}s");
                        if (!isLibraryAtMax)
                        {
                            long nextCooldown = library.GetResearchCooldownTicks(library.Level + 1);
                            if (nextCooldown != cooldown)
                                tooltipLines.Add(_localization.Get("tooltip_building_prestige_next") + $" {nextCooldown / 100.0:0.0}s");
                        }
                    }
                    else if (!isLibraryAtMax)
                    {
                        long nextCooldown = library.GetResearchCooldownTicks(library.Level + 1);
                        if (nextCooldown < long.MaxValue)
                            tooltipLines.Add(_localization.GetFormated("library_research_unlocks_next", $"{nextCooldown / 100.0:0.0}"));
                    }
                    tooltipLines.Add("");
                }

                if (hoveredBuilding is Smelter smelter && smelter.Level > 0)
                {
                    long currentTick = _cityBuildingService.GetCurrentTick();
                    long cooldown  = _cityBuildingService.GetSmelterEffectiveCooldown();
                    long elapsed   = smelter.LastProductionTick == 0 ? 0 : currentTick - smelter.LastProductionTick;
                    long remaining = Math.Max(0, cooldown - elapsed);
                    tooltipLines.Add(_localization.Get("smelter_production_cooldown") + $" {remaining / 100.0:0.0}s/{cooldown / 100.0:0.0}s");
                    tooltipLines.Add(_localization.GetFormated("smelter_production_costs",
                        _cityBuildingService.GetSmelterOreInput(), _cityBuildingService.GetSmelterSteelOutput()));
                    tooltipLines.Add("");
                }

                if (hoveredBuilding is Arsenal arsenal && arsenal.Level > 0)
                {
                    tooltipLines.Add(_localization.GetFormated("arsenal_max_soldiers_bonus", arsenal.GetMaxSoldiersBonus()));
                    if (_cityBuildingService.IsSteelArmorUnlocked())
                        tooltipLines.Add(_localization.GetFormated("arsenal_armor_save_chance", arsenal.ArmorSavePercent));
                    else
                        tooltipLines.Add(_localization.Get("arsenal_armor_save_locked"));
                    tooltipLines.Add("");
                }

                if (hoveredBuilding is WeaponSmith weaponSmith && weaponSmith.Level > 0)
                {
                    long currentTick = _cityBuildingService.GetCurrentTick();
                    long cooldown  = _cityBuildingService.GetWeaponSmithEffectiveCooldown();
                    long elapsed   = weaponSmith.LastProductionTick == 0 ? 0 : currentTick - weaponSmith.LastProductionTick;
                    long remaining = Math.Max(0, cooldown - elapsed);
                    tooltipLines.Add(_localization.Get("weaponsmith_production_cooldown") + $" {remaining / 100.0:0.0}s/{cooldown / 100.0:0.0}s");
                    tooltipLines.Add(_localization.GetFormated("weaponsmith_production_costs", WeaponSmith.SteelInputPerWeapon));
                    if (!_cityBuildingService.IsAtMaxLevel(weaponSmith))
                    {
                        long nextCooldown = _cityBuildingService.GetWeaponSmithEffectiveCooldown(weaponSmith.Level + 1);
                        if (nextCooldown != cooldown)
                            tooltipLines.Add(_localization.Get("tooltip_building_prestige_next") + $" {nextCooldown / 100.0:0.0}s");
                    }
                    tooltipLines.Add("");
                }

                if (hoveredBuilding is ArmorSmith armorSmith && armorSmith.Level > 0)
                {
                    long currentTick = _cityBuildingService.GetCurrentTick();
                    long cooldown  = _cityBuildingService.GetArmorSmithEffectiveCooldown();
                    long elapsed   = armorSmith.LastProductionTick == 0 ? 0 : currentTick - armorSmith.LastProductionTick;
                    long remaining = Math.Max(0, cooldown - elapsed);
                    tooltipLines.Add(_localization.Get("armorsmith_production_cooldown") + $" {remaining / 100.0:0.0}s/{cooldown / 100.0:0.0}s");
                    tooltipLines.Add(_localization.GetFormated("armorsmith_production_costs", ArmorSmith.SteelInputPerArmor));
                    if (!_cityBuildingService.IsAtMaxLevel(armorSmith))
                    {
                        long nextCooldown = _cityBuildingService.GetArmorSmithEffectiveCooldown(armorSmith.Level + 1);
                        if (nextCooldown != cooldown)
                            tooltipLines.Add(_localization.Get("tooltip_building_prestige_next") + $" {nextCooldown / 100.0:0.0}s");
                    }
                    tooltipLines.Add("");
                }

                if (hoveredBuilding is AlchimistHut alchimistHut && alchimistHut.Level > 0)
                {
                    long currentTick = _cityBuildingService.GetCurrentTick();
                    long cooldown  = _cityBuildingService.GetAlchimistHutPotionCooldown();
                    long elapsed   = alchimistHut.LastPotionProductionTick == 0 ? 0 : currentTick - alchimistHut.LastPotionProductionTick;
                    long remaining = Math.Max(0, cooldown - elapsed);
                    tooltipLines.Add(_localization.Get("alchimisthut_potion_cooldown") + $" {remaining / 100.0:0.0}s/{cooldown / 100.0:0.0}s");
                    tooltipLines.Add(_localization.GetFormated("alchimisthut_potion_costs", AlchimistHut.GlassInputPerPotion, AlchimistHut.CrystalInputPerPotion));
                    if (!_cityBuildingService.IsAtMaxLevel(alchimistHut))
                    {
                        long nextCooldown = _cityBuildingService.GetAlchimistHutPotionCooldown(alchimistHut.Level + 1);
                        if (nextCooldown != cooldown)
                            tooltipLines.Add(_localization.Get("tooltip_building_prestige_next") + $" {nextCooldown / 100.0:0.0}s");
                    }
                    tooltipLines.Add("");
                }

                if (hoveredBuilding is Warehouse warehouse)
                {
                    if (warehouse.Level > 0)
                    {
                        tooltipLines.Add(_localization.GetFormated("warehouse_storage_bonus",
                            warehouse.GetStorageCapacityBonusBasic(), warehouse.GetStorageCapacityBonusAdvanced()));
                    }
                    if (!_cityBuildingService.IsAtMaxLevel(warehouse))
                    {
                        int nextBasic = warehouse.GetStorageCapacityBonusBasicAtLevel(warehouse.Level + 1);
                        int nextAdvanced = warehouse.GetStorageCapacityBonusAdvancedAtLevel(warehouse.Level + 1);
                        tooltipLines.Add(_localization.Get("tooltip_harvest_auto_next") + " " +
                            _localization.GetFormated("warehouse_storage_bonus", nextBasic, nextAdvanced));
                    }
                    tooltipLines.Add("");
                }

                if (hoveredBuilding is TownHall townHall && townHall.Level > 0)
                {
                    int basicBonus = townHall.GetStorageCapacityBonusBasic();
                    int advancedBonus = townHall.GetStorageCapacityBonusAdvanced();
                    tooltipLines.Add(_localization.GetFormated("townhall_storage_bonus_basic", basicBonus));
                    if (advancedBonus > 0)
                        tooltipLines.Add(_localization.GetFormated("townhall_storage_bonus_advanced", advancedBonus));
                    if (!_cityBuildingService.IsAtMaxLevel(townHall))
                    {
                        int nextBasic = townHall.GetStorageCapacityBonusBasicAtLevel(townHall.Level + 1);
                        int nextAdvanced = townHall.GetStorageCapacityBonusAdvancedAtLevel(townHall.Level + 1);
                        if (nextBasic != basicBonus)
                            tooltipLines.Add(_localization.Get("tooltip_harvest_auto_next") + " " +
                                _localization.GetFormated("townhall_storage_bonus_basic", nextBasic));
                        if (nextAdvanced != advancedBonus)
                            tooltipLines.Add(_localization.Get("tooltip_harvest_auto_next") + " " +
                                _localization.GetFormated("townhall_storage_bonus_advanced", nextAdvanced));
                    }
                    tooltipLines.Add("");
                }

                if (hoveredBuilding is Garrison garrison && garrison.Level > 0)
                {
                    int soldierBonus = garrison.GetMaxSoldiersBonus();
                    int prodBonus = (int)Math.Round(garrison.Level * 25.0);
                    tooltipLines.Add(_localization.GetFormated("garrison_stats", soldierBonus, prodBonus));
                    if (!_cityBuildingService.IsAtMaxLevel(garrison))
                    {
                        int nextSoldier = (garrison.Level + 1) * Garrison.MaxSoldiersPerLevel;
                        int nextProd = (garrison.Level + 1) * 25;
                        tooltipLines.Add(_localization.Get("tooltip_harvest_auto_next") + " " + _localization.GetFormated("garrison_stats", nextSoldier, nextProd));
                    }
                    tooltipLines.Add("");
                }

                if (hoveredBuilding is Laboratory laboratory && laboratory.Level > 0)
                {
                    bool isLabAtMax = _cityBuildingService.IsAtMaxLevel(laboratory);
                    long currentTick = _cityBuildingService.GetCurrentTick();
                    long cooldown  = laboratory.GetResearchCooldownTicks();
                    long elapsed   = laboratory.LastResearchTick == 0 ? 0 : currentTick - laboratory.LastResearchTick;
                    long remaining = Math.Max(0, cooldown - elapsed);
                    tooltipLines.Add(_localization.Get("laboratory_research_cooldown") + $" {remaining / 100.0:0.0}s/{cooldown / 100.0:0.0}s");
                    tooltipLines.Add(_localization.Get("laboratory_research_gold_cost"));
                    if (!isLabAtMax)
                    {
                        long nextCooldown = laboratory.GetResearchCooldownTicks(laboratory.Level + 1);
                        tooltipLines.Add(_localization.Get("tooltip_building_prestige_next") + $" {nextCooldown / 100.0:0.0}s");
                    }
                    tooltipLines.Add("");
                }

                var prestigeController = _cityBuildingService.PrestigeController;
                if (hoveredBuilding.Level > 0)
                {
                    var currentPrestige = prestigeController.GetBuildingPrestigePoints(hoveredBuilding);
                    if (currentPrestige > 0)
                    {
                        tooltipLines.Add(_localization.Get("tooltip_building_prestige") + " " + currentPrestige);
                        var isAtMax = _cityBuildingService.IsAtMaxLevel(hoveredBuilding);
                        if (!isAtMax)
                        {
                            var nextPrestige = prestigeController.GetBuildingPrestigePointsAtNextLevel(hoveredBuilding);
                            if (nextPrestige != currentPrestige)
                                tooltipLines.Add(_localization.Get("tooltip_building_prestige_next") + " " + nextPrestige);
                        }
                        tooltipLines.Add("");
                    }
                }
                else
                {
                    var buildPrestige = prestigeController.GetBuildingPrestigePointsAtNextLevel(hoveredBuilding);
                    if (buildPrestige > 0)
                    {
                        tooltipLines.Add(_localization.Get("tooltip_building_prestige") + " " + buildPrestige);
                        tooltipLines.Add("");
                    }
                }

                TooltipRenderUtils.DrawTooltip(canvas, CanvasSize, _lastPointerPosition, tooltipLines.ToArray(), Font10!, cost, _resourceIcons, LastUiScale,
                    _cityBuildingService.GetSelectedCivilizationResourceQuantity);
            }
        }
        else if (_hoveredActivationCheckbox.HasValue)
        {
            var lines = new[] { _localization.Get("tooltip_activate_building") };
            TooltipRenderUtils.DrawTooltip(canvas, CanvasSize, _lastPointerPosition, lines, Font10!, new ResourceSet(), _resourceIcons, LastUiScale);
        }
    }

    /// <summary>
    /// Instantané du panneau pour une vue portée par l'hôte : bâti ici / ailleurs, niveau max,
    /// solvabilité, visibilité du bouton pour les uniques.
    /// </summary>
    public CityPanelSnapshot GetSnapshot()
    {
        if (_cityBuildingService.SelectedCity == null) return CityPanelSnapshot.Hidden;

        bool hasUnique = _cityBuildingService.HasUniqueBuildingsUnlocked();
        bool showUnique = _showUniqueBuildings && hasUnique;

        var buildings = (showUnique
            ? _cityBuildingService.SelectedCityUniqueBuildingsAndBuildables()
            : _cityBuildingService.SelectedCityBuildingsAndBuildables()).ToList();

        var rows = new List<CityBuildingRowSnapshot>(buildings.Count);
        foreach (var building in buildings)
        {
            bool isBuilt = building.Level > 0;
            bool builtHere = isBuilt && _cityBuildingService.IsBuiltInSelectedCity(building);
            bool builtElsewhere = isBuilt && !builtHere;
            bool canBuildOrUpgrade = !builtElsewhere && _cityBuildingService.CanBuildOrUpgrade(building);
            bool atMaxLevel = builtHere && _cityBuildingService.IsAtMaxLevel(building);

            var activation = builtHere && building.ActivationStatus != ActivationStatus.NON_ACTIVABLE
                ? (building.ActivationStatus == ActivationStatus.ACTIVE
                    ? BuildingActivationState.Active
                    : BuildingActivationState.Inactive)
                : BuildingActivationState.None;

            var cost = new List<CostItemSnapshot>();
            if (!builtElsewhere && !atMaxLevel)
            {
                var costSet = builtHere ? building.GetUpgradeCost(building.Level + 1) : building.GetBuildCost();
                foreach (var kvp in costSet)
                {
                    cost.Add(new CostItemSnapshot(
                        IconName: kvp.Key.ToString(),
                        Amount: SkiaTextUtils.FormatNumber(kvp.Value),
                        CanAfford: _cityBuildingService.GetSelectedCivilizationResourceQuantity(kvp.Key) >= kvp.Value));
                }
            }

            CityBuildingAction action;
            string actionLabel;
            bool actionEnabled;

            if (builtElsewhere)
            {
                action = CityBuildingAction.GoToOtherCity;
                actionLabel = "➤";
                actionEnabled = true;
            }
            // Même condition que le rendu : un bâtiment unique non constructible ici et jamais
            // bâti n'affiche aucun bouton.
            else if (builtHere || !building.IsUnique || canBuildOrUpgrade
                     || _cityBuildingService.CanBuildOrUpgradeIgnoringResources(building))
            {
                action = atMaxLevel ? CityBuildingAction.MaxLevel
                       : builtHere ? CityBuildingAction.Upgrade
                       : CityBuildingAction.Build;
                actionLabel = _localization.Get(action switch
                {
                    CityBuildingAction.MaxLevel => "action_maxlevel",
                    CityBuildingAction.Upgrade  => "action_upgrade",
                    _                           => "action_build",
                });
                actionEnabled = !atMaxLevel && canBuildOrUpgrade;
            }
            else
            {
                action = CityBuildingAction.None;
                actionLabel = "";
                actionEnabled = false;
            }

            rows.Add(new CityBuildingRowSnapshot(
                Key: building.Type.ToString(),
                Label: _localization.Get(building.NameKey) + (isBuilt ? $" (Niv {building.Level})" : ""),
                Activation: activation,
                Cost: cost,
                Action: action,
                ActionLabel: actionLabel,
                IsActionEnabled: actionEnabled,
                IsBuiltElsewhere: builtElsewhere));
        }

        var (soldiers, maxSoldiers) = _cityBuildingService.GetSelectedCitySoldiers();
        var (defense, maxDefense) = _cityBuildingService.GetSelectedCityDefense();

        return new CityPanelSnapshot(
            IsVisible: true,
            HasUniqueTab: hasUnique,
            ShowingUnique: showUnique,
            RegularTabLabel: _localization.Get("tab_buildings_classic"),
            UniqueTabLabel: _localization.Get("tab_buildings_unique"),
            Rows: rows,
            MilitaryFooter: $"{_localization.Get("footer_soldiers")}: {soldiers}/{maxSoldiers}    "
                          + $"{_localization.Get("footer_defense")}: {defense}/{maxDefense}");
    }

    /// <summary>
    /// Dessine uniquement le tooltip de survol, sans le panneau. Utilisé quand le panneau est
    /// rendu par l'hôte : le tooltip s'affiche par-dessus la carte, hors du panneau, et reste
    /// donc produit en Skia — ce qui évite de réécrire ses centaines de lignes de règles.
    /// </summary>
    public void RenderHostTooltipOnly(SKCanvas canvas, GameRenderContext context)
    {
        if (_cityBuildingService.SelectedCity == null) return;
        if (_hoveredBuildingType == null && _hoveredActivationCheckbox == null) return;

        UpdateScale(context.UiScale);

        // La vue de l'hôte affiche toutes les lignes (défilement natif) : le survol peut donc
        // porter sur n'importe quel bâtiment, pas seulement sur une fenêtre défilée.
        bool showUnique = _showUniqueBuildings && _cityBuildingService.HasUniqueBuildingsUnlocked();
        var buildings = (showUnique
            ? _cityBuildingService.SelectedCityUniqueBuildingsAndBuildables()
            : _cityBuildingService.SelectedCityBuildingsAndBuildables()).ToList();

        DrawHoverTooltip(canvas, buildings);
    }

    /// <summary>Bascule l'onglet Bâtiments / Uniques depuis une vue portée par l'hôte.</summary>
    public void SetShowUniqueFromHost(bool showUnique)
    {
        _showUniqueBuildings = showUnique;
        _hoveredBuildingType = null;
        ScrollOffset = 0;
    }

    /// <summary>Bascule l'activation d'un bâtiment depuis une vue portée par l'hôte.</summary>
    public void ToggleActivationFromHost(string buildingKey)
    {
        if (Enum.TryParse<BuildingType>(buildingKey, out var type))
            _cityBuildingService.ToggleBuildingActivation(type);
    }

    /// <summary>Exécute l'action (construire/améliorer) depuis une vue portée par l'hôte.</summary>
    public void ExecuteActionFromHost(string buildingKey)
    {
        if (Enum.TryParse<BuildingType>(buildingKey, out var type))
            _cityBuildingService.TryExecuteSelectedCityBuildingAction(type);
    }

    /// <summary>Recentre sur l'autre ville possédant ce bâtiment, depuis une vue portée par l'hôte.</summary>
    public void GoToOtherCityFromHost(string buildingKey)
    {
        if (Enum.TryParse<BuildingType>(buildingKey, out var type))
            GoToOtherCityWithBuilding(type);
    }

    /// <summary>
    /// Renseigne le bâtiment survolé depuis une vue portée par l'hôte. Le tooltip continue
    /// d'être dessiné en Skia par-dessus la carte (cf. <see cref="DrawHoverTooltip"/>).
    /// </summary>
    /// <param name="pointerX">Position du pointeur, en unités logiques. Indispensable : une
    /// fois le panneau porté par l'hôte, Avalonia consomme le survol et
    /// <c>_lastPointerPosition</c> n'est plus alimenté par le service d'entrée Skia — le
    /// tooltip s'afficherait alors à la dernière position connue sur la carte.</param>
    public void SetHoveredBuildingFromHost(string? buildingKey, float pointerX, float pointerY)
    {
        _hoveredBuildingType = buildingKey != null && Enum.TryParse<BuildingType>(buildingKey, out var type)
            ? type
            : null;
        _lastPointerPosition = new SKPoint(pointerX, pointerY);
    }

    private void GoToOtherCityWithBuilding(BuildingType type)
    {
        var city = _cityBuildingService.FindOtherCityWithBuilding(type);
        if (city == null) return;

        var (wx, wy) = VertexToWorld(city.Position);
        CenterCameraOnMapPosition?.Invoke(city.Position.Z, wx, wy);
        _cityBuildingService.SetSelectedCity(city.Position);
    }

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

    public override void Dispose()
    {
        base.Dispose();
    }
}
