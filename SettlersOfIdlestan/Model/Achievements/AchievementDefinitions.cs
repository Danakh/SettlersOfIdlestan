namespace SettlersOfIdlestan.Model.Achievements;

public static class AchievementDefinitions
{
    public static readonly IReadOnlyList<AchievementDefinition> All =
    [
        new(AchievementId.FirstPrestige,
            "achievement_first_prestige_name",
            g => g.TotalPrestigesPerformed >= 1),
    ];
}
