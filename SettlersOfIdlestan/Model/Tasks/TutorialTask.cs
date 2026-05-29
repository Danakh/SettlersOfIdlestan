using System;

namespace SettlersOfIdlestan.Model.Tasks;

public class TutorialTask
{
    public TutorialTaskId Id { get; }
    public string NameKey { get; }
    public string DescKey { get; }

    /// <summary>Condition de complétion évaluée contre le GameRecord (all-time) et le RunRecord (run courant).</summary>
    public Func<GameRecord, RunRecord?, bool> IsCompleted { get; }

    public TutorialTask(TutorialTaskId id, string nameKey, string descKey, Func<GameRecord, RunRecord?, bool> isCompleted)
    {
        Id = id;
        NameKey = nameKey;
        DescKey = descKey;
        IsCompleted = isCompleted;
    }
}
