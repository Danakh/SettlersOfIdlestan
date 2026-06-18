namespace SettlersOfIdlestan.Model.Achievements;

public static class AchievementDefinitions
{
    public static readonly IReadOnlyList<AchievementDefinition> All =
    [
        new(AchievementId.FirstPrestige,
            "achievement_first_prestige_name",
            g => g.TotalPrestigesPerformed >= 1),
        new(AchievementId.SlayDragon,
            "achievement_slay_dragon_name",
            g => g.TotalDragonsDefeated >= 1),
        new(AchievementId.WonderLevel1,
            "achievement_wonder_level_1_name",
            g => g.MaxWonderLevelReached >= 1),
        new(AchievementId.WonderLevel4,
            "achievement_wonder_level_4_name",
            g => g.MaxWonderLevelReached >= 4),
        new(AchievementId.FoundUnderworldCity,
            "achievement_found_underworld_city_name",
            g => g.HasFoundedUnderworldCity),
        new(AchievementId.BuildCorruptionSpire,
            "achievement_build_corruption_spire_name",
            g => g.HasBuiltCorruptionSpire),
        new(AchievementId.SlayTrollsAndOgres,
            "achievement_slay_trolls_and_ogres_name",
            g => g.TotalTrollsDefeated >= 10 && g.TotalOgresDefeated >= 10),
        new(AchievementId.FiveUniqueBuildingsOneIsland,
            "achievement_five_unique_buildings_one_island_name",
            g => g.MaxUniqueBuildingTypesOnIsland >= 5),
        new(AchievementId.TenUniqueBuildingsOneIsland,
            "achievement_ten_unique_buildings_one_island_name",
            g => g.MaxUniqueBuildingTypesOnIsland >= 10),
    ];
}
