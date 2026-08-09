using System.Collections.Generic;
using System.Linq;
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
    /// GameRecordUpdated est émis après chaque événement de jeu suivi (pas seulement au prestige),
    /// ce qui permet de valider et débloquer un achievement dès que sa condition est remplie.
    /// </summary>
    public void Connect(TaskRecordController taskRecordController)
    {
        taskRecordController.GameRecordUpdated += HandleGameRecordUpdated;
    }

    private void HandleGameRecordUpdated(object? sender, GameRecord record)
        => CheckAchievements(record);

    /// <summary>
    /// Clé persistée de chaque achievement, pré-calculée. Même raison que
    /// <c>TaskRecordController.TaskKeys</c> : <c>def.Id.ToString()</c> boxe l'enum et alloue une
    /// chaîne, et cette boucle tourne à chaque <c>GameRecordUpdated</c>, donc à chaque récolte.
    /// </summary>
    private static readonly string[] AchievementKeys =
        AchievementDefinitions.All.Select(a => a.Id.ToString()).ToArray();

    /// <summary>
    /// Indices des achievements pas encore débloqués, entretenus au fil des déverrouillages plutôt
    /// que redécouverts à chaque appel. Reconstruits quand le GameRecord change d'instance (nouvelle
    /// partie, chargement d'une sauvegarde).
    /// </summary>
    private readonly List<int> _pendingIndices = new();
    private GameRecord? _pendingIndicesRecord;

    /// <summary>
    /// Évalue tous les achievements contre le GameRecord courant.
    /// Émet OnAchievementUnlocked pour chaque achievement nouvellement débloqué.
    /// </summary>
    public void CheckAchievements(GameRecord record)
    {
        if (!ReferenceEquals(_pendingIndicesRecord, record))
        {
            _pendingIndicesRecord = record;
            _pendingIndices.Clear();
            for (int i = 0; i < AchievementDefinitions.All.Count; i++)
                if (!record.CompletedAchievements.Contains(AchievementKeys[i]))
                    _pendingIndices.Add(i);
        }

        // Compactage en place, dans l'ordre de définition — l'ordre d'émission de
        // OnAchievementUnlocked est celui de l'ancienne boucle.
        List<AchievementId>? unlocked = null;
        int kept = 0;
        for (int read = 0; read < _pendingIndices.Count; read++)
        {
            int index = _pendingIndices[read];
            var def = AchievementDefinitions.All[index];

            if (!def.IsCompleted(record))
            {
                _pendingIndices[kept++] = index;
                continue;
            }

            record.CompletedAchievements.Add(AchievementKeys[index]);
            (unlocked ??= new List<AchievementId>()).Add(def.Id);
        }
        _pendingIndices.RemoveRange(kept, _pendingIndices.Count - kept);

        if (unlocked == null) return;
        foreach (var id in unlocked)
            OnAchievementUnlocked?.Invoke(this, id);
    }
}
