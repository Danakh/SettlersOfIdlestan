using System;
using SettlersOfIdlestan.Model.Localization;
using SettlersOfIdlestan.Model.Tasks;

namespace SettlersOfIdlestan.Controller.Store;

public interface IStoreService : IDisposable
{
    bool IsAvailable { get; }

    /// <summary>
    /// Retourne la langue préférée du joueur selon le store, ou null si inconnue.
    /// </summary>
    Language? GetPreferredLanguage();

    /// <summary>
    /// Envoie les statistiques cumulatives au store.
    /// </summary>
    void SendStats(GameRecord gameRecord);

    /// <summary>
    /// Déverrouille un achievement dans le store.
    /// </summary>
    void UnlockAchievement(string achievementId);
}
