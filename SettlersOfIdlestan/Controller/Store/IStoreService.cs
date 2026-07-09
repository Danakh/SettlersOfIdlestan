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

    /// <summary>
    /// Sauvegarde le contenu dans le stockage cloud du store (ex: Steam Cloud), sous le nom donné.
    /// No-op si le store ne supporte pas le cloud ou si celui-ci est indisponible/désactivé.
    /// </summary>
    void SaveCloudFile(string fileName, string content);

    /// <summary>
    /// Lit le contenu d'un fichier depuis le stockage cloud du store. Retourne null si absent,
    /// ou si le store ne supporte pas le cloud / est indisponible.
    /// </summary>
    string? LoadCloudFile(string fileName);

    /// <summary>
    /// Indique si un fichier existe dans le stockage cloud du store, sans en lire le contenu.
    /// Retourne false si le store ne supporte pas le cloud / est indisponible.
    /// </summary>
    bool HasCloudFile(string fileName);
}
