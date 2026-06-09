using System;
using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.IslandFeatures;
using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestan.Model.Tasks;

public static class TutorialTaskDefinitions
{
    private static readonly HashSet<BuildingType> ProductionBuildingTypes = new()
    {
        BuildingType.Sawmill,
        BuildingType.Brickworks,
        BuildingType.Mill,
        BuildingType.Quarry,
        BuildingType.Mine,
        BuildingType.Seaport,
        BuildingType.GlassWorks,
    };

    private static int CountBuilding(WorldState? island, BuildingType type)
        => island?.PlayerCivilization.Cities.Sum(c => c.Buildings.Count(b => b.Type == type)) ?? 0;

    private static int LiveMax(int recorded, int? live) => Math.Max(recorded, live ?? 0);

    private static int ComputePrestigePoints(WorldState? island)
        => island?.PlayerCivilization.Cities.SelectMany(c => c.Buildings).Sum(b => b.Type switch
        {
            BuildingType.Temple   => 1,
            BuildingType.TownHall => b.Level > 2 ? 2 : 1,
            _                     => 0,
        }) ?? 0;

    public static readonly IReadOnlyList<TutorialTask> All = new[]
    {
        new TutorialTask(TutorialTaskId.Harvest5Wood,
            "task_harvest_wood_name", "task_harvest_wood_desc",
            (g, _, _) => g.HarvestedResources.GetValueOrDefault("Wood") >= 5,
            (g, _, _) => (g.HarvestedResources.GetValueOrDefault("Wood"), 5)),

        new TutorialTask(TutorialTaskId.Harvest5Brick,
            "task_harvest_brick_name", "task_harvest_brick_desc",
            (g, _, _) => g.HarvestedResources.GetValueOrDefault("Brick") >= 5,
            (g, _, _) => (g.HarvestedResources.GetValueOrDefault("Brick"), 5)),

        new TutorialTask(TutorialTaskId.BuildSeaport,
            "task_build_seaport_name", "task_build_seaport_desc",
            (g, _, island) => g.BuildingCounts.GetValueOrDefault("Seaport") >= 1
                || CountBuilding(island, BuildingType.Seaport) >= 1),

        new TutorialTask(TutorialTaskId.BuildSawmill,
            "task_build_sawmill_name", "task_build_sawmill_desc",
            (g, _, island) => g.BuildingCounts.GetValueOrDefault("Sawmill") >= 1
                || CountBuilding(island, BuildingType.Sawmill) >= 1),

        new TutorialTask(TutorialTaskId.BuildBrickworks,
            "task_build_brickworks_name", "task_build_brickworks_desc",
            (g, _, island) => g.BuildingCounts.GetValueOrDefault("Brickworks") >= 1
                || CountBuilding(island, BuildingType.Brickworks) >= 1),

        new TutorialTask(TutorialTaskId.BuildFirstRoad,
            "task_build_first_road_name", "task_build_first_road_desc",
            (g, _, island) => g.TotalRoadsBuilt >= 3
                || island?.PlayerCivilization.Roads.Count >= 3,
            (g, _, island) => (LiveMax(g.TotalRoadsBuilt, island?.PlayerCivilization.Roads.Count), 3)),

        new TutorialTask(TutorialTaskId.Harvest15Food,
            "task_harvest_food_name", "task_harvest_food_desc",
            (g, _, _) => g.HarvestedResources.GetValueOrDefault("Food") >= 15,
            (g, _, _) => (g.HarvestedResources.GetValueOrDefault("Food"), 15)),

        new TutorialTask(TutorialTaskId.BuildSecondCity,
            "task_build_second_city_name", "task_build_second_city_desc",
            (g, _, island) => g.TotalCitiesBuilt >= 1
                || island?.PlayerCivilization.Cities.Count >= 2),

        new TutorialTask(TutorialTaskId.UpgradeProductionBuildingsLevel2,
            "task_upgrade_production_level2_name", "task_upgrade_production_level2_desc",
            (g, _, island) => g.ProductionBuildingsReachedLevel2 >= 2
                || island?.PlayerCivilization.Cities.SelectMany(c => c.Buildings)
                    .Count(b => ProductionBuildingTypes.Contains(b.Type) && b.Level >= 2) >= 2,
            (g, _, island) => (LiveMax(g.ProductionBuildingsReachedLevel2,
                island?.PlayerCivilization.Cities.SelectMany(c => c.Buildings)
                    .Count(b => ProductionBuildingTypes.Contains(b.Type) && b.Level >= 2)), 2)),

        new TutorialTask(TutorialTaskId.Build10Cities,
            "task_build_10_cities_name", "task_build_10_cities_desc",
            (g, _, island) => g.TotalCitiesBuilt >= 10
                || island?.PlayerCivilization.Cities.Count >= 10,
            (g, _, island) => (LiveMax(g.TotalCitiesBuilt, (island?.PlayerCivilization.Cities.Count ?? 1) - 1), 10)),

        new TutorialTask(TutorialTaskId.Build5Libraries,
            "task_build_5_libraries_name", "task_build_5_libraries_desc",
            (g, _, island) => g.BuildingCounts.GetValueOrDefault("Temple") >= 5
                || CountBuilding(island, BuildingType.Temple) >= 5,
            (g, _, island) => (LiveMax(g.BuildingCounts.GetValueOrDefault("Temple"), CountBuilding(island, BuildingType.Temple)), 5)),

        new TutorialTask(TutorialTaskId.SeaportAndTownHallLevel4SameCity,
            "task_seaport_townhall_level4_name", "task_seaport_townhall_level4_desc",
            (g, _, island) => g.HasSeaportAndTownHallLevel4SameCity
                || island?.PlayerCivilization.Cities.Any(c =>
                    c.Buildings.Any(b => b.Type == BuildingType.Seaport && b.Level >= 4) &&
                    c.Buildings.Any(b => b.Type == BuildingType.TownHall && b.Level >= 4)) == true),

        new TutorialTask(TutorialTaskId.SeaportLevel4,
            "task_seaport_level4_name", "task_seaport_level4_desc",
            (g, _, island) => g.HasSeaportLevel4
                || island?.PlayerCivilization.Cities.SelectMany(c => c.Buildings)
                    .Any(b => b.Type == BuildingType.Seaport && b.Level >= 4) == true),

        new TutorialTask(TutorialTaskId.TownHallLevel4,
            "task_townhall_level4_name", "task_townhall_level4_desc",
            (g, _, island) => g.HasTownHallLevel4
                || island?.PlayerCivilization.Cities.SelectMany(c => c.Buildings)
                    .Any(b => b.Type == BuildingType.TownHall && b.Level >= 4) == true),

        new TutorialTask(TutorialTaskId.Build5Palisades,
            "task_build_5_palisades_name", "task_build_5_palisades_desc",
            (g, _, island) => g.BuildingCounts.GetValueOrDefault("Palisade") >= 5
                || CountBuilding(island, BuildingType.Palisade) >= 5,
            (g, _, island) => (LiveMax(g.BuildingCounts.GetValueOrDefault("Palisade"), CountBuilding(island, BuildingType.Palisade)), 5)),

        new TutorialTask(TutorialTaskId.BuildImperialPort,
            "task_build_imperial_port_name", "task_build_imperial_port_desc",
            (g, _, island) => g.BuildingCounts.GetValueOrDefault("ImperialPort") >= 1
                || CountBuilding(island, BuildingType.ImperialPort) >= 1),

        new TutorialTask(TutorialTaskId.Build2Warehouses,
            "task_build_2_warehouses_name", "task_build_2_warehouses_desc",
            (g, _, island) => g.BuildingCounts.GetValueOrDefault("Warehouse") >= 2
                || CountBuilding(island, BuildingType.Warehouse) >= 2,
            (g, _, island) => (LiveMax(g.BuildingCounts.GetValueOrDefault("Warehouse"), CountBuilding(island, BuildingType.Warehouse)), 2)),

        new TutorialTask(TutorialTaskId.Build1Warehouse,
            "task_build_1_warehouse_name", "task_build_1_warehouse_desc",
            (g, _, island) => g.BuildingCounts.GetValueOrDefault("Warehouse") >= 1
                || CountBuilding(island, BuildingType.Warehouse) >= 1),

        new TutorialTask(TutorialTaskId.BuildMarket,
            "task_build_market_name", "task_build_market_desc",
            (g, _, island) => g.BuildingCounts.GetValueOrDefault("Market") >= 1
                || CountBuilding(island, BuildingType.Market) >= 1),

        new TutorialTask(TutorialTaskId.Trade10Gold,
            "task_trade_10_gold_name", "task_trade_10_gold_desc",
            (g, _, _) => g.TotalGoldObtainedFromTrade >= 10,
            (g, _, _) => (g.TotalGoldObtainedFromTrade, 10)),

        new TutorialTask(TutorialTaskId.Build3Warehouses,
            "task_build_3_warehouses_name", "task_build_3_warehouses_desc",
            (g, _, island) => g.BuildingCounts.GetValueOrDefault("Warehouse") >= 3
                || CountBuilding(island, BuildingType.Warehouse) >= 3,
            (g, _, island) => (LiveMax(g.BuildingCounts.GetValueOrDefault("Warehouse"), CountBuilding(island, BuildingType.Warehouse)), 3)),

        new TutorialTask(TutorialTaskId.Reach20VictoryPoints,
            "task_reach_20_victory_points_name", "task_reach_20_victory_points_desc",
            (g, _, island) => g.TotalPrestigesPerformed >= 1 || ComputePrestigePoints(island) >= 20,
            (g, _, island) => (g.TotalPrestigesPerformed >= 1 ? 20 : ComputePrestigePoints(island), 20)),

        new TutorialTask(TutorialTaskId.BuyResearchVertex,
            "task_buy_research_vertex_name", "task_buy_research_vertex_desc",
            (g, _, island) => g.TotalPrestigeVerticesPurchased >= 1
                || g.TotalResearchCompleted >= 1
                || island?.PlayerCivilization.TechnologyTree.ActiveResearch != null
                || (island?.PlayerCivilization.TechnologyTree.CompletedTechnologies.Count ?? 0) > 0),

        new TutorialTask(TutorialTaskId.CompleteFirstResearch,
            "task_complete_first_research_name", "task_complete_first_research_desc",
            (g, _, island) => g.TotalResearchCompleted >= 1
                || (island?.PlayerCivilization.TechnologyTree.CompletedTechnologies.Count ?? 0) >= 1),

        new TutorialTask(TutorialTaskId.BuildLibraryLevel2,
            "task_build_library_level_name", "task_build_library_level_desc",
            (g, _, island) => g.BuildingCounts.GetValueOrDefault("Library") >= 1
                || CountBuilding(island, BuildingType.Library) >= 1),

        new TutorialTask(TutorialTaskId.PerformPrestige,
            "task_perform_prestige_name", "task_perform_prestige_desc",
            (g, _, _) => g.TotalPrestigesPerformed >= 1),

        new TutorialTask(TutorialTaskId.Reach30VictoryPoints,
            "task_reach_enough_victory_points_name", "task_reach_enough_victory_points_desc",
            (g, _, island) => g.TotalPrestigesPerformed >= 1 || ComputePrestigePoints(island) >= 35,
            (g, _, island) => (g.TotalPrestigesPerformed >= 1 ? 35 : ComputePrestigePoints(island), 35)),

        new TutorialTask(TutorialTaskId.BuildBarracks,
            "task_build_barracks_name", "task_build_barracks_desc",
            (g, _, island) => g.BuildingCounts.GetValueOrDefault("Barracks") >= 1
                || CountBuilding(island, BuildingType.Barracks) >= 1),

        new TutorialTask(TutorialTaskId.BuyBarracksVertex,
            "task_buy_barracks_vertex_name", "task_buy_barracks_vertex_desc",
            (g, _, _) => g.HasPurchasedBarracksVertex),

        new TutorialTask(TutorialTaskId.CompleteMilitaryBuildingsResearch,
            "task_complete_military_buildings_research_name", "task_complete_military_buildings_research_desc",
            (g, _, island) => island?.PlayerCivilization.TechnologyTree.CompletedTechnologies.Contains(TechnologyId.MilitaryBuildings) == true),

        new TutorialTask(TutorialTaskId.Build2Barracks,
            "task_build_2_barracks_name", "task_build_2_barracks_desc",
            (g, _, island) => g.BuildingCounts.GetValueOrDefault("Barracks") >= 2
                || CountBuilding(island, BuildingType.Barracks) >= 2,
            (g, _, island) => (LiveMax(g.BuildingCounts.GetValueOrDefault("Barracks"), CountBuilding(island, BuildingType.Barracks)), 2)),

        new TutorialTask(TutorialTaskId.CreateReinforcementFlow,
            "task_create_reinforcement_flow_name", "task_create_reinforcement_flow_desc",
            (g, _, island) => g.HasCreatedReinforcementFlow
                || island?.PlayerCivilization.Cities.Any(c =>
                    c.FlowTarget != null &&
                    island.PlayerCivilization.Cities.Any(other =>
                        other != c && other.Position.Equals(c.FlowTarget))) == true),

        new TutorialTask(TutorialTaskId.DestroyEnemyCity,
            "task_destroy_enemy_city_name", "task_destroy_enemy_city_desc",
            (g, _, _) => g.TotalEnemyCitiesDestroyed >= 1),

        new TutorialTask(TutorialTaskId.KillBandit,
            "task_kill_bandit_name", "task_kill_bandit_desc",
            (g, _, _) => g.TotalBanditsDefeated >= 1),

        new TutorialTask(TutorialTaskId.PerformSecondPrestige,
            "task_perform_second_prestige_name", "task_perform_second_prestige_desc",
            (g, _, _) => g.TotalPrestigesPerformed >= 2),

        new TutorialTask(TutorialTaskId.DestroyBanditHideout,
            "task_destroy_bandit_hideout_name", "task_destroy_bandit_hideout_desc",
            (g, _, _) => g.TotalHideoutsDestroyed >= 1),

        new TutorialTask(TutorialTaskId.PlaceWonder,
            "task_place_wonder_name", "task_place_wonder_desc",
            (g, _, island) => g.HasPlacedWonder
                || island?.Features.OfType<Wonder>().Any() == true),

        new TutorialTask(TutorialTaskId.BuildWonder,
            "task_build_wonder_name", "task_build_wonder_desc",
            (g, _, island) => g.HasBuiltWonder
                || island?.Features.OfType<Wonder>().Any(w => w.Level >= 1) == true),
    };
}
