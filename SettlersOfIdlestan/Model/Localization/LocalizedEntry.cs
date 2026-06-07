namespace SettlersOfIdlestan.Model.Localization;

/// <summary>
/// Capture une clé de localisation et ses arguments, résolvable via ILocalizationService.
/// </summary>
public record LocalizedEntry(string Key, object[] Args)
{
    public LocalizedEntry(string key) : this(key, []) { }
}
