using SettlersOfIdlestanSkia.Services;

namespace SettlersOfIdlestanDesktop.Services;

public class DesktopFileSystemService : IFileSystemService
{
    public void SaveText(string fileName, string content)
    {
        var path = Path.Combine(FileSystem.Current.AppDataDirectory, fileName);
        File.WriteAllText(path, content);
    }

    public string? LoadText(string fileName)
    {
        var path = Path.Combine(FileSystem.Current.AppDataDirectory, fileName);
        if (!File.Exists(path))
            return null;
        return File.ReadAllText(path);
    }
}
