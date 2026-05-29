using System.Collections.Generic;

namespace SettlersOfIdlestan.Model.Tasks;

/// <summary>
/// Regroupe des tâches tutoriel en une étape cohérente avec un titre et une description.
/// Les tâches principales sont requises pour progresser ; les secondaires sont optionnelles.
/// </summary>
public class TutorialStep
{
    public string TitleKey { get; }
    public string DescKey { get; }
    public IReadOnlyList<TutorialTask> PrimaryTasks { get; }
    public IReadOnlyList<TutorialTask> SecondaryTasks { get; }

    public TutorialStep(
        string titleKey,
        string descKey,
        IReadOnlyList<TutorialTask> primaryTasks,
        IReadOnlyList<TutorialTask> secondaryTasks)
    {
        TitleKey = titleKey;
        DescKey = descKey;
        PrimaryTasks = primaryTasks;
        SecondaryTasks = secondaryTasks;
    }
}
