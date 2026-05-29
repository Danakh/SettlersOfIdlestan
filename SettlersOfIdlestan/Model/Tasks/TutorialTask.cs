using System;

namespace SettlersOfIdlestan.Model.Tasks;

public class TutorialTask
{
    public TutorialTaskId Id { get; }
    public string NameKey { get; }
    public string DescKey { get; }

    public Func<GameRecord, RunRecord?, IslandMap.IslandState?, bool> IsCompleted { get; }

    /// <summary>Progression (courant, max). Null si binaire (max = 1). Ne pas afficher si max ≤ 1.</summary>
    public Func<GameRecord, RunRecord?, IslandMap.IslandState?, (int Current, int Max)>? GetProgress { get; }

    public TutorialTask(
        TutorialTaskId id,
        string nameKey,
        string descKey,
        Func<GameRecord, RunRecord?, IslandMap.IslandState?, bool> isCompleted,
        Func<GameRecord, RunRecord?, IslandMap.IslandState?, (int Current, int Max)>? getProgress = null)
    {
        Id = id;
        NameKey = nameKey;
        DescKey = descKey;
        IsCompleted = isCompleted;
        GetProgress = getProgress;
    }
}
