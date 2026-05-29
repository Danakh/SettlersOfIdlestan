using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Model.Buildings;
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

    private static int CountBuilding(IslandState? island, BuildingType type)
        => island?.PlayerCivilization.Cities.Sum(c => c.Buildings.Count(b => b.Type == type)) ?? 0;

    public static readonly IReadOnlyList<TutorialTask> All = new[]
    {
        new TutorialTask(TutorialTaskId.Harvest5Wood,
            "task_harvest_wood_name", "task_harvest_wood_desc",
            (g, _, _) => g.HarvestedResources.GetValueOrDefault("Wood") >= 5),

        new TutorialTask(TutorialTaskId.Harvest5Brick,
            "task_harvest_brick_name", "task_harvest_brick_desc",
            (g, _, _) => g.HarvestedResources.GetValueOrDefault("Brick") >= 5),

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
                || island?.PlayerCivilization.Roads.Count >= 3),

        new TutorialTask(TutorialTaskId.Harvest15Food,
            "task_harvest_food_name", "task_harvest_food_desc",
            (g, _, _) => g.HarvestedResources.GetValueOrDefault("Food") >= 15),

        new TutorialTask(TutorialTaskId.BuildSecondCity,
            "task_build_second_city_name", "task_build_second_city_desc",
            (g, _, island) => g.TotalCitiesBuilt >= 1
                || island?.PlayerCivilization.Cities.Count >= 2),

        new TutorialTask(TutorialTaskId.UpgradeProductionBuildingsLevel2,
            "task_upgrade_production_level2_name", "task_upgrade_production_level2_desc",
            (g, _, island) => g.ProductionBuildingsReachedLevel2 >= 2
                || island?.PlayerCivilization.Cities.SelectMany(c => c.Buildings)
                    .Count(b => ProductionBuildingTypes.Contains(b.Type) && b.Level >= 2) >= 2),

        new TutorialTask(TutorialTaskId.Build10Cities,
            "task_build_10_cities_name", "task_build_10_cities_desc",
            (g, _, island) => g.TotalCitiesBuilt >= 10
                || island?.PlayerCivilization.Cities.Count >= 10),

        new TutorialTask(TutorialTaskId.Build5Libraries,
            "task_build_5_libraries_name", "task_build_5_libraries_desc",
            (g, _, island) => g.BuildingCounts.GetValueOrDefault("Library") >= 5
                || CountBuilding(island, BuildingType.Library) >= 5),

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
                || CountBuilding(island, BuildingType.Palisade) >= 5),

        new TutorialTask(TutorialTaskId.BuildImperialPort,
            "task_build_imperial_port_name", "task_build_imperial_port_desc",
            (g, _, island) => g.BuildingCounts.GetValueOrDefault("ImperialPort") >= 1
                || CountBuilding(island, BuildingType.ImperialPort) >= 1),

        new TutorialTask(TutorialTaskId.PerformPrestige,
            "task_perform_prestige_name", "task_perform_prestige_desc",
            (g, _, _) => g.TotalPrestigesPerformed >= 1),
    };
}
