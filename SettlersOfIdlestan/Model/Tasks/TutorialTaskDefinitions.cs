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
    };
}
