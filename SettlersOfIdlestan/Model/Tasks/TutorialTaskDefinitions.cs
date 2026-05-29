using System.Collections.Generic;

namespace SettlersOfIdlestan.Model.Tasks;

public static class TutorialTaskDefinitions
{
    public static readonly IReadOnlyList<TutorialTask> All = new[]
    {
        // --- Expansion routière ---
        new TutorialTask(TutorialTaskId.BuildFirstRoad,
            "task_build_first_road_name", "task_build_first_road_desc",
            (g, _) => g.TotalRoadsBuilt >= 1),

        new TutorialTask(TutorialTaskId.BuildFirst5Roads,
            "task_build_5_roads_name", "task_build_5_roads_desc",
            (g, _) => g.TotalRoadsBuilt >= 5),

        new TutorialTask(TutorialTaskId.BuildFirst10Roads,
            "task_build_10_roads_name", "task_build_10_roads_desc",
            (g, _) => g.TotalRoadsBuilt >= 10),

        // --- Villes ---
        new TutorialTask(TutorialTaskId.BuildFirstCity,
            "task_build_first_city_name", "task_build_first_city_desc",
            (g, _) => g.TotalCitiesBuilt >= 1),

        new TutorialTask(TutorialTaskId.BuildSecondCity,
            "task_build_second_city_name", "task_build_second_city_desc",
            (g, _) => g.TotalCitiesBuilt >= 2),

        new TutorialTask(TutorialTaskId.BuildThirdCity,
            "task_build_third_city_name", "task_build_third_city_desc",
            (g, _) => g.TotalCitiesBuilt >= 3),

        // --- Bâtiments de production ---
        new TutorialTask(TutorialTaskId.BuildFirstSawmill,
            "task_build_sawmill_name", "task_build_sawmill_desc",
            (g, _) => g.BuildingCounts.GetValueOrDefault("Sawmill") >= 1),

        new TutorialTask(TutorialTaskId.BuildFirstMarket,
            "task_build_market_name", "task_build_market_desc",
            (g, _) => g.BuildingCounts.GetValueOrDefault("Market") >= 1),

        new TutorialTask(TutorialTaskId.BuildFirstWarehouse,
            "task_build_warehouse_name", "task_build_warehouse_desc",
            (g, _) => g.BuildingCounts.GetValueOrDefault("Warehouse") >= 1),

        // --- Militaire ---
        new TutorialTask(TutorialTaskId.BuildFirstBarracks,
            "task_build_barracks_name", "task_build_barracks_desc",
            (g, _) => g.BuildingCounts.GetValueOrDefault("Barracks") >= 1),

        // --- Améliorations ---
        new TutorialTask(TutorialTaskId.Upgrade3Buildings,
            "task_upgrade_3_buildings_name", "task_upgrade_3_buildings_desc",
            (g, _) => g.TotalBuildingsUpgraded >= 3),

        // --- Recherche ---
        new TutorialTask(TutorialTaskId.CompleteFirstResearch,
            "task_first_research_name", "task_first_research_desc",
            (g, _) => g.TotalResearchCompleted >= 1),

        new TutorialTask(TutorialTaskId.Complete3Research,
            "task_3_research_name", "task_3_research_desc",
            (g, _) => g.TotalResearchCompleted >= 3),

        new TutorialTask(TutorialTaskId.Complete5Research,
            "task_5_research_name", "task_5_research_desc",
            (g, _) => g.TotalResearchCompleted >= 5),

        // --- Combat ---
        new TutorialTask(TutorialTaskId.DefeatFirstBandit,
            "task_first_bandit_name", "task_first_bandit_desc",
            (g, _) => g.TotalBanditsDefeated >= 1),

        new TutorialTask(TutorialTaskId.DestroyFirstHideout,
            "task_first_hideout_name", "task_first_hideout_desc",
            (g, _) => g.TotalHideoutsDestroyed >= 1),

        // --- Prestige ---
        new TutorialTask(TutorialTaskId.BuyFirstPrestigeVertex,
            "task_first_vertex_name", "task_first_vertex_desc",
            (g, _) => g.TotalPrestigeVerticesPurchased >= 1),

        new TutorialTask(TutorialTaskId.Buy3PrestigeVertices,
            "task_3_vertices_name", "task_3_vertices_desc",
            (g, _) => g.TotalPrestigeVerticesPurchased >= 3),

        new TutorialTask(TutorialTaskId.PerformFirstPrestige,
            "task_first_prestige_name", "task_first_prestige_desc",
            (g, _) => g.TotalPrestigesPerformed >= 1),

        new TutorialTask(TutorialTaskId.PerformThirdPrestige,
            "task_third_prestige_name", "task_third_prestige_desc",
            (g, _) => g.TotalPrestigesPerformed >= 3),
    };
}
