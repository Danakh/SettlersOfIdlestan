using System.Collections.Generic;

namespace SettlersOfIdlestan.Model.Tasks;

public static class TutorialTaskDefinitions
{
    public static readonly IReadOnlyList<TutorialTask> All = new[]
    {
        new TutorialTask(TutorialTaskId.Harvest5Wood,
            "task_harvest_wood_name", "task_harvest_wood_desc",
            (g, _) => g.HarvestedResources.GetValueOrDefault("Wood") >= 5),

        new TutorialTask(TutorialTaskId.Harvest5Brick,
            "task_harvest_brick_name", "task_harvest_brick_desc",
            (g, _) => g.HarvestedResources.GetValueOrDefault("Brick") >= 5),

        new TutorialTask(TutorialTaskId.BuildSeaport,
            "task_build_seaport_name", "task_build_seaport_desc",
            (g, _) => g.BuildingCounts.GetValueOrDefault("Seaport") >= 1),

        new TutorialTask(TutorialTaskId.BuildSawmill,
            "task_build_sawmill_name", "task_build_sawmill_desc",
            (g, _) => g.BuildingCounts.GetValueOrDefault("Sawmill") >= 1),

        new TutorialTask(TutorialTaskId.BuildBrickworks,
            "task_build_brickworks_name", "task_build_brickworks_desc",
            (g, _) => g.BuildingCounts.GetValueOrDefault("Brickworks") >= 1),

        new TutorialTask(TutorialTaskId.BuildFirstRoad,
            "task_build_first_road_name", "task_build_first_road_desc",
            (g, _) => g.TotalRoadsBuilt >= 1),

        new TutorialTask(TutorialTaskId.BuildSecondCity,
            "task_build_second_city_name", "task_build_second_city_desc",
            (g, _) => g.TotalCitiesBuilt >= 1),

        new TutorialTask(TutorialTaskId.UpgradeProductionBuildingsLevel2,
            "task_upgrade_production_level2_name", "task_upgrade_production_level2_desc",
            (g, _) => g.ProductionBuildingsReachedLevel2 >= 4),

        new TutorialTask(TutorialTaskId.Build10Cities,
            "task_build_10_cities_name", "task_build_10_cities_desc",
            (g, _) => g.TotalCitiesBuilt >= 10),

        new TutorialTask(TutorialTaskId.Build5Libraries,
            "task_build_5_libraries_name", "task_build_5_libraries_desc",
            (g, _) => g.BuildingCounts.GetValueOrDefault("Library") >= 5),

        new TutorialTask(TutorialTaskId.SeaportAndTownHallLevel4SameCity,
            "task_seaport_townhall_level4_name", "task_seaport_townhall_level4_desc",
            (g, _) => g.HasSeaportAndTownHallLevel4SameCity),

        new TutorialTask(TutorialTaskId.BuildImperialPort,
            "task_build_imperial_port_name", "task_build_imperial_port_desc",
            (g, _) => g.BuildingCounts.GetValueOrDefault("ImperialPort") >= 1),

        new TutorialTask(TutorialTaskId.PerformPrestige,
            "task_perform_prestige_name", "task_perform_prestige_desc",
            (g, _) => g.TotalPrestigesPerformed >= 1),
    };
}
