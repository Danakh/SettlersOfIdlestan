namespace SettlersOfIdlestan.Services.Localization;

/// <summary>
/// Interface de service de localisation pour supporter plusieurs langues.
/// </summary>
public interface ILocalizationService
{
    /// <summary>
    /// Obtient la langue actuellement active.
    /// </summary>
    Language CurrentLanguage { get; }

    /// <summary>
    /// Définit la langue actuellement active.
    /// </summary>
    void SetLanguage(Language language);

    /// <summary>
    /// Obtient la traduction pour une clé donnée.
    /// </summary>
    /// <param name="key">La clé de localisation.</param>
    /// <returns>Le texte traduit, ou la clé si non trouvée.</returns>
    string Get(LocalizationKey key);

    /// <summary>
    /// Obtient la traduction pour une clé donnée avec formatage.
    /// </summary>
    /// <param name="key">La clé de localisation.</param>
    /// <param name="args">Les arguments de formatage.</param>
    /// <returns>Le texte traduit et formaté.</returns>
    string Get(LocalizationKey key, params object[] args);
}
