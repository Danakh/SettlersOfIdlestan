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

    string Get(string key);
    string Get(string key, params object[] args);
}
