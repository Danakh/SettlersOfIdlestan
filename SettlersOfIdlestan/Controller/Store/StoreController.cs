using System;
using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Controller.Achievements;
using SettlersOfIdlestan.Model.Achievements;
using SettlersOfIdlestan.Model.Localization;
using SettlersOfIdlestan.Model.Tasks;

namespace SettlersOfIdlestan.Controller.Store;

/// <summary>
/// Agrège les IStoreService disponibles et dispatche les appels vers chacun.
/// </summary>
public class StoreController : IDisposable
{
    private readonly List<IStoreService> _allServices;
    private readonly List<IStoreService> _activeServices;
    private AchievementController? _connectedAchievementController;

    public IReadOnlyList<IStoreService> ActiveServices => _activeServices;

    public StoreController(IEnumerable<IStoreService>? services = null)
    {
        _allServices    = services?.ToList() ?? [];
        _activeServices = _allServices.Where(s => s.IsAvailable).ToList();
    }

    /// <summary>
    /// Retourne le statut de connexion de chaque service détecté (exclut NotDetected).
    /// </summary>
    public IReadOnlyList<(string Name, StoreConnectionStatus Status)> GetConnectionStatuses()
    {
        var result = new List<(string, StoreConnectionStatus)>();
        foreach (var svc in _allServices)
        {
            if (svc.ConnectionStatus != StoreConnectionStatus.NotDetected)
                result.Add((svc.Name, svc.ConnectionStatus));
        }
        return result;
    }

    /// <summary>
    /// Souscrit aux événements de l'AchievementController pour synchroniser les achievements avec les stores.
    /// Peut être appelé plusieurs fois — remplace la connexion précédente.
    /// </summary>
    public void Connect(AchievementController achievementController)
    {
        if (_connectedAchievementController != null)
            _connectedAchievementController.OnAchievementUnlocked -= HandleAchievementUnlocked;

        _connectedAchievementController = achievementController;
        achievementController.OnAchievementUnlocked += HandleAchievementUnlocked;
    }

    private void HandleAchievementUnlocked(object? sender, AchievementId id)
    {
        var storeId = GetStoreAchievementId(id);
        if (storeId != null) UnlockAchievement(storeId);
    }

    /// <summary>
    /// Mapping des AchievementId vers les identifiants définis dans le portail Steam (ou équivalent).
    /// </summary>
    private static string? GetStoreAchievementId(AchievementId id) => id switch
    {
        AchievementId.FirstPrestige => "ACH_FIRST_PRESTIGE",
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

    public void UnlockAchievement(string achievementId)
    {
        foreach (var service in _activeServices)
            service.UnlockAchievement(achievementId);
    }

    // ── Dispose ───────────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_connectedAchievementController != null)
        {
            _connectedAchievementController.OnAchievementUnlocked -= HandleAchievementUnlocked;
            _connectedAchievementController = null;
        }
        foreach (var service in _allServices)
            service.Dispose();
        _allServices.Clear();
        _activeServices.Clear();
    }
}
