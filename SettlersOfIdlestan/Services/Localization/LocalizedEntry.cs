namespace SettlersOfIdlestan.Services.Localization;

/// <summary>
/// Capture une clé de localisation et ses arguments, résolvable via ILocalizationService.
/// Permet à n'importe quelle couche du modèle d'exprimer du texte traduit avec des données locales.
/// </summary>
public record LocalizedEntry(string Key, object[] Args)
{
    public LocalizedEntry(string key) : this(key, []) { }
}

public static class LocalizationServiceExtensions
{
    public static string Resolve(this ILocalizationService loc, LocalizedEntry entry)
        => entry.Args.Length == 0 ? loc.Get(entry.Key) : loc.GetFormated(entry.Key, entry.Args);
}
