using System.Reflection;
using System.Text.Json;
using SettlersOfIdlestan.Model.Localization;

namespace SettlersOfIdlestanSkia.Services.Localization;

/// <summary>
/// Service de localisation chargé les traductions à partir de fichiers JSON.
/// </summary>
public class LocalizationService
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

    public string Get(string key)
    {
        if (_translations.TryGetValue(_currentLanguage, out var languageDict))
        {
            if (languageDict.TryGetValue(key, out var value))
            {
                return value;
            }
        }
        return key;
    }

    public string GetFormated(string key, params object[] args)
    {
        if (_translations.TryGetValue(_currentLanguage, out var languageDict))
        {
            if (languageDict.TryGetValue(key, out var value))
            {
                try
                {
                    return string.Format(value, args);
                }
                catch
                {
                    return value;
                }
            }
        }
        return key;
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
