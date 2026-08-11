using SettlersOfIdlestan.Controller;
using SettlersOfIdlestanSkia.Services;

namespace SettlersOfIdlestanAvalonia.Browser.Services;

/// <summary>
/// Persistance web : sauvegarde automatique, reglages et statistiques dans le localStorage,
/// import/export par telechargement et selecteur de fichier.
/// </summary>
public sealed class BrowserFileSystemService : IFileSystemService
{
    // Ces cles designent les memes entrees de localStorage que le head Blazor : les changer
    // ferait perdre sa partie a tout joueur web deja installe.
    private const string AutoSaveKey = "settlers_autosave";
    private const string SettingsKey = "settlers_settings";
    private const string StatsKey    = "settlers_stats";

    public Task SaveText(string fileName, string content)
    {
        BrowserInterop.DownloadFile(fileName, content);
        return Task.CompletedTask;
    }

    // Le navigateur ne donne pas acces au disque : le nom de fichier est ignore, c'est le
    // joueur qui designe la source via le selecteur.
    public Task<string?> LoadText(string fileName) => BrowserInterop.OpenFilePicker();

    public Task SaveAuto(string content)
    {
        BrowserInterop.SetStorageItem(AutoSaveKey, content);
        return Task.CompletedTask;
    }

    public Task<string?> LoadAuto() => Task.FromResult(BrowserInterop.GetStorageItem(AutoSaveKey));

    public Task DeleteAuto()
    {
        BrowserInterop.RemoveStorageItem(AutoSaveKey);
        return Task.CompletedTask;
    }

    public Task SaveSettings(string content)
    {
        BrowserInterop.SetStorageItem(SettingsKey, SaveController.Encrypt(content));
        return Task.CompletedTask;
    }

    public Task<string?> LoadSettings() => Task.FromResult(Decode(SettingsKey));

    public Task SaveStats(string content)
    {
        BrowserInterop.SetStorageItem(StatsKey, SaveController.Encrypt(content));
        return Task.CompletedTask;
    }

    public Task<string?> LoadStats() => Task.FromResult(Decode(StatsKey));

    private static string? Decode(string key)
    {
        var raw = BrowserInterop.GetStorageItem(key);
        return raw != null ? SaveController.DecodeToJson(raw) : null;
    }
}
