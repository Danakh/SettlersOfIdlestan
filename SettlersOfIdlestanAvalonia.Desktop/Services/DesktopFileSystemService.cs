using SettlersOfIdlestan.Controller;
using SettlersOfIdlestanSkia.Services;

namespace SettlersOfIdlestanAvalonia.Desktop.Services;

/// <summary>
/// Persistance de bureau : tout vit dans un dossier <c>saves</c> a cote de l'executable.
///
/// Ce chemin est un contrat avec les joueurs Steam existants — le head OpenTK ecrivait au meme
/// endroit, et l'executable porte le meme nom pour cette raison. Le deplacer ferait perdre
/// leur partie a l'installation suivante.
/// </summary>
public class DesktopFileSystemService : IFileSystemService
{
    private static string GetSavesDirectory()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "saves");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string AutoSavePath()  => Path.Combine(GetSavesDirectory(), "autosave.json");
    private static string SettingsPath()  => Path.Combine(GetSavesDirectory(), "settings.json");
    private static string StatsPath()     => Path.Combine(GetSavesDirectory(), "playerstats.json");

    public Task SaveText(string fileName, string content)
    {
        File.WriteAllText(Path.Combine(GetSavesDirectory(), fileName), content);
        return Task.CompletedTask;
    }

    public Task<string?> LoadText(string fileName)
    {
        var path = Path.Combine(GetSavesDirectory(), fileName);
        return Task.FromResult<string?>(File.Exists(path) ? File.ReadAllText(path) : null);
    }

    public Task SaveAuto(string content)
    {
        File.WriteAllText(AutoSavePath(), content);
        return Task.CompletedTask;
    }

    public Task<string?> LoadAuto()
    {
        var p = AutoSavePath();
        return Task.FromResult<string?>(File.Exists(p) ? File.ReadAllText(p) : null);
    }

    public Task DeleteAuto()
    {
        var p = AutoSavePath();
        if (File.Exists(p)) File.Delete(p);
        return Task.CompletedTask;
    }

    public Task SaveSettings(string content)
    {
        File.WriteAllText(SettingsPath(), SaveController.Encrypt(content));
        return Task.CompletedTask;
    }

    public Task<string?> LoadSettings()
    {
        var p = SettingsPath();
        return Task.FromResult<string?>(File.Exists(p) ? SaveController.DecodeToJson(File.ReadAllText(p)) : null);
    }

    public Task SaveStats(string content)
    {
        File.WriteAllText(StatsPath(), SaveController.Encrypt(content));
        return Task.CompletedTask;
    }

    public Task<string?> LoadStats()
    {
        var p = StatsPath();
        return Task.FromResult<string?>(File.Exists(p) ? SaveController.DecodeToJson(File.ReadAllText(p)) : null);
    }
}
