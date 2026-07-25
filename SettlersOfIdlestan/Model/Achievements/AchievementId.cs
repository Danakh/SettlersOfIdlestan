using System.Text.Json.Serialization;

namespace SettlersOfIdlestan.Model.Achievements;

[JsonConverter(typeof(JsonStringEnumConverter<AchievementId>))]
public enum AchievementId
{
    FirstPrestige,
    SlayDragon,
    WonderLevel1,
    WonderLevel4,
    FoundUnderworldCity,
    BuildCorruptionSpire,
    SlayTrollsAndOgres,
    FiveUniqueBuildingsOneIsland,
    TenUniqueBuildingsOneIsland,
}
