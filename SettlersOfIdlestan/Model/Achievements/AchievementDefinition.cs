using SettlersOfIdlestan.Model.Tasks;

namespace SettlersOfIdlestan.Model.Achievements;

public class AchievementDefinition
{
    public AchievementId Id { get; }
    public string NameKey { get; }
    public Func<GameRecord, bool> IsCompleted { get; }

    public AchievementDefinition(AchievementId id, string nameKey, Func<GameRecord, bool> isCompleted)
    {
        Id = id;
        NameKey = nameKey;
        IsCompleted = isCompleted;
    }
}
