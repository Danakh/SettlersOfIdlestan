using System;
using SettlersOfIdlestan.Model.Localization;

namespace SettlersOfIdlestan.Controller.Store;

public interface IStoreService : IDisposable
{
    string Name { get; }
    bool IsAvailable { get; }
    StoreConnectionStatus ConnectionStatus { get; }

    /// <summary>
    /// Retourne la langue préférée du joueur selon le store, ou null si inconnue.
    /// </summary>
    Language? GetPreferredLanguage();

    /// <summary>
    /// Déverrouille un achievement dans le store.
    /// </summary>
    void UnlockAchievement(string achievementId);
}
