using System;
using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Controller.Tasks;
using SettlersOfIdlestan.Model.Localization;
using SettlersOfIdlestan.Model.Tasks;

namespace SettlersOfIdlestan.Controller.Store;

/// <summary>
/// Agrège les IStoreService disponibles et dispatche les appels vers chacun.
/// </summary>
public class StoreController : IDisposable
{
    private readonly List<IStoreService> _activeServices;
    private TaskRecordController? _connectedController;

    public IReadOnlyList<IStoreService> ActiveServices => _activeServices;

    public StoreController(IEnumerable<IStoreService>? services = null)
    {
        _activeServices = services?.Where(s => s.IsAvailable).ToList() ?? [];
    }

    /// <summary>
    /// Souscrit aux événements du TaskRecordController pour synchroniser achievements et stats.
    /// Peut être appelé plusieurs fois — remplace la connexion précédente.
    /// </summary>
    public void Connect(TaskRecordController taskRecordController)
    {
        if (_connectedController != null)
        {
            _connectedController.OnTaskCompleted  -= HandleTaskCompleted;
            _connectedController.PrestigeRecorded -= HandlePrestigeRecorded;
        }

        _connectedController = taskRecordController;
        taskRecordController.OnTaskCompleted  += HandleTaskCompleted;
        taskRecordController.PrestigeRecorded += HandlePrestigeRecorded;
    }

    private void HandlePrestigeRecorded(object? sender, GameRecord record)
        => SendStats(record);

    private void HandleTaskCompleted(object? sender, TutorialTaskId taskId)
    {
        var achievementId = GetAchievementId(taskId);
        if (achievementId != null) UnlockAchievement(achievementId);
    }

    /// <summary>
    /// Mapping des tâches tutoriel vers les IDs d'achievements du store.
    /// Les IDs correspondent aux noms définis dans le portail Steam (ou équivalent).
    /// </summary>
    private static string? GetAchievementId(TutorialTaskId taskId) => taskId switch
    {
        TutorialTaskId.PerformPrestige       => "ACH_FIRST_PRESTIGE",
        TutorialTaskId.PerformSecondPrestige => "ACH_SECOND_PRESTIGE",
        _ => null,
    };

    // ── API publique ──────────────────────────────────────────────────────────

    /// <summary>
    /// Retourne la langue préférée du premier store disponible qui en fournit une, ou null.
    /// </summary>
    public Language? GetPreferredLanguage()
    {
        foreach (var service in _activeServices)
        {
            var lang = service.GetPreferredLanguage();
            if (lang.HasValue) return lang;
        }
        return null;
    }

    public void SendStats(GameRecord gameRecord)
    {
        foreach (var service in _activeServices)
            service.SendStats(gameRecord);
    }

    public void UnlockAchievement(string achievementId)
    {
        foreach (var service in _activeServices)
            service.UnlockAchievement(achievementId);
    }

    // ── Dispose ───────────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_connectedController != null)
        {
            _connectedController.OnTaskCompleted  -= HandleTaskCompleted;
            _connectedController.PrestigeRecorded -= HandlePrestigeRecorded;
            _connectedController = null;
        }
        foreach (var service in _activeServices)
            service.Dispose();
        _activeServices.Clear();
    }
}
