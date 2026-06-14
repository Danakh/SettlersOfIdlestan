using SettlersOfIdlestanSkia.Services;

namespace SettlersOfIdlestanOpenTK.Services;

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
        File.WriteAllText(SettingsPath(), content);
        return Task.CompletedTask;
    }

    public Task<string?> LoadSettings()
    {
        var p = SettingsPath();
        return Task.FromResult<string?>(File.Exists(p) ? File.ReadAllText(p) : null);
    }
}
