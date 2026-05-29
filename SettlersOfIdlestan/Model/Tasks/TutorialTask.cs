using System;

namespace SettlersOfIdlestan.Model.Tasks;

public class TutorialTask
{
    public TutorialTaskId Id { get; }
    public string NameKey { get; }
    public string DescKey { get; }

    /// <summary>Condition de complétion évaluée contre le GameRecord (all-time), le RunRecord (run courant) et l'état live de l'île.</summary>
    public Func<GameRecord, RunRecord?, IslandMap.IslandState?, bool> IsCompleted { get; }

    public TutorialTask(TutorialTaskId id, string nameKey, string descKey, Func<GameRecord, RunRecord?, IslandMap.IslandState?, bool> isCompleted)
    {
        Id = id;
        NameKey = nameKey;
        DescKey = descKey;
        IsCompleted = isCompleted;
    }
}
