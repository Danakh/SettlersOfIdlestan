using System.Reflection;
using System.Text.Json;

namespace SettlersOfIdlestan.Services.Localization;

/// <summary>
/// Service de localisation chargé les traductions à partir de fichiers JSON.
/// </summary>
public class LocalizationService : ILocalizationService
{
    private Language _currentLanguage = Language.French;
    private readonly Dictionary<Language, Dictionary<string, string>> _translations = new();

    public Language CurrentLanguage => _currentLanguage;

    public LocalizationService()
    {
        LoadAllTranslations();
    }

    private void LoadAllTranslations()
    {
        foreach (var language in Enum.GetValues(typeof(Language)).Cast<Language>())
        {
            LoadTranslations(language);
        }
    }

    private void LoadTranslations(Language language)
    {
        string languageCode = GetLanguageCode(language);
        string resourceName = $"SettlersOfIdlestan.Resources.Localization.{languageCode}.json";

        var assembly = Assembly.GetExecutingAssembly();
        var translationDict = new Dictionary<string, string>();

        using (var stream = assembly.GetManifestResourceStream(resourceName))
        {
            if (stream == null)
            {
                Console.WriteLine($"Ressource de localisation non trouvée: {resourceName}");
                _translations[language] = translationDict;
                return;
            }

            using (var reader = new StreamReader(stream))
            {
                string json = reader.ReadToEnd();
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(json, options);

                if (parsed != null)
                {
                    translationDict = parsed;
                }
            }
        }

        _translations[language] = translationDict;
    }

    public void SetLanguage(Language language)
    {
        if (!_translations.ContainsKey(language))
        {
            LoadTranslations(language);
        }
        _currentLanguage = language;
    }

    public string Get(LocalizationKey key)
    {
        string keyString = ConvertKeyToString(key);
        return GetTranslation(keyString) ?? keyString;
    }

    public string Get(LocalizationKey key, params object[] args)
    {
        string text = Get(key);
        try
        {
            return string.Format(text, args);
        }
        catch
        {
            return text;
        }
    }

    private string? GetTranslation(string key)
    {
        if (_translations.TryGetValue(_currentLanguage, out var languageDict))
        {
            if (languageDict.TryGetValue(key, out var value))
            {
                return value;
            }
        }
        return null;
    }

    private static string ConvertKeyToString(LocalizationKey key)
    {
        // Convertit MenuToggleDebugMode en menu.toggle_debug_mode
        string keyName = key.ToString();
        var result = new System.Text.StringBuilder();

        for (int i = 0; i < keyName.Length; i++)
        {
            char c = keyName[i];
            if (char.IsUpper(c) && i > 0)
            {
                result.Append('_');
                result.Append(char.ToLower(c));
            }
            else
            {
                result.Append(char.ToLower(c));
            }
        }

        // Remplace les underscores initiaux par des points (menu_toggle -> menu.toggle)
        return result.ToString();
    }

    private static string GetLanguageCode(Language language)
    {
        return language switch
        {
            Language.French => "fr",
            Language.English => "en",
            _ => "fr"
        };
    }
}
