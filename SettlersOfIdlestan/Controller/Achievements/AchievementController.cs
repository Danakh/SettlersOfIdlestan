using SettlersOfIdlestan.Controller.Tasks;
using SettlersOfIdlestan.Model.Achievements;
using SettlersOfIdlestan.Model.Tasks;

namespace SettlersOfIdlestan.Controller.Achievements;

/// <summary>
/// Évalue les achievements du joueur et émet OnAchievementUnlocked lors d'un nouveau déverrouillage.
/// Les achievements sont distincts des tâches tutoriel : ils ont leur propre définition et leur
/// propre état persisté dans GameRecord.CompletedAchievements.
/// </summary>
public class AchievementController
{
    public event EventHandler<AchievementId>? OnAchievementUnlocked;

    /// <summary>
    /// Souscrit aux événements du TaskRecordController pour déclencher les vérifications.
    /// </summary>
    public void Connect(TaskRecordController taskRecordController)
    {
        taskRecordController.PrestigeRecorded += HandlePrestigeRecorded;
    }

    private void HandlePrestigeRecorded(object? sender, GameRecord record)
        => CheckAchievements(record);

    /// <summary>
    /// Évalue tous les achievements contre le GameRecord courant.
    /// Émet OnAchievementUnlocked pour chaque achievement nouvellement débloqué.
    /// </summary>
    public void CheckAchievements(GameRecord record)
    {
        foreach (var def in AchievementDefinitions.All)
        {
            var key = def.Id.ToString();
            if (!record.CompletedAchievements.Contains(key) && def.IsCompleted(record))
            {
                record.CompletedAchievements.Add(key);
                OnAchievementUnlocked?.Invoke(this, def.Id);
            }
        }
    }
}
