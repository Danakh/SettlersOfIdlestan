using SettlersOfIdlestan.Controller;
using SettlersOfIdlestanSkia.Services;

namespace SettlersOfIdlestanAvalonia.iOS.Services;

/// <summary>
/// Persistance iOS.
///
/// Ne PAS reprendre <c>DesktopFileSystemService</c> : il ecrit dans
/// <c>AppContext.BaseDirectory/saves</c>, c'est-a-dire dans le bundle de l'application, qui est
/// en lecture seule sur iOS. Toute sauvegarde y echouerait sur l'appareil (le head MAUI
/// declarait la cible iOS mais utilisait ce chemin, et n'a donc jamais pu y ecrire — il n'y a
/// par consequent aucune sauvegarde iOS existante a preserver).
///
/// Repartition retenue, conforme aux regles d'Apple :
/// <list type="bullet">
/// <item>partie, reglages et statistiques dans <c>Library/Application Support/saves</c> :
///       sauvegarde par iCloud/iTunes, jamais purge par le systeme, invisible du joueur ;</item>
/// <item>export/import dans <c>Documents</c>, expose dans l'app Fichiers par
///       <c>UIFileSharingEnabled</c> (cf. Info.plist).</item>
/// </list>
/// </summary>
public sealed class IosFileSystemService : IFileSystemService
{
    private static string SavesDirectory
    {
        get
        {
            // LocalApplicationData vaut Library/Application Support sur iOS ; contrairement au
            // dossier Documents, le systeme ne le cree pas d'office.
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "saves");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    private static string DocumentsDirectory =>
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

    private static string AutoSavePath => Path.Combine(SavesDirectory, "autosave.json");
    private static string SettingsPath => Path.Combine(SavesDirectory, "settings.json");
    private static string StatsPath    => Path.Combine(SavesDirectory, "playerstats.json");

    /// <summary>
    /// Export : ecrit dans Documents, ou le joueur le retrouve via l'app Fichiers. iOS n'offre
    /// pas de selecteur d'enregistrement sans UIDocumentPicker, dont le flux asynchrone ne
    /// correspond pas au contrat synchrone attendu ici.
    /// </summary>
    public Task SaveText(string fileName, string content)
    {
        File.WriteAllText(Path.Combine(DocumentsDirectory, fileName), content);
        return Task.CompletedTask;
    }

    /// <summary>Import : relit le meme dossier Documents, ou le joueur a depose son fichier.</summary>
    public Task<string?> LoadText(string fileName)
    {
        var path = Path.Combine(DocumentsDirectory, fileName);
        return Task.FromResult(File.Exists(path) ? File.ReadAllText(path) : null);
    }

    public Task SaveAuto(string content)
    {
        File.WriteAllText(AutoSavePath, content);
        return Task.CompletedTask;
    }

    public Task<string?> LoadAuto() => Task.FromResult(Read(AutoSavePath));

    public Task DeleteAuto()
    {
        if (File.Exists(AutoSavePath)) File.Delete(AutoSavePath);
        return Task.CompletedTask;
    }

    public Task SaveSettings(string content)
    {
        File.WriteAllText(SettingsPath, SaveController.Encrypt(content));
        return Task.CompletedTask;
    }

    public Task<string?> LoadSettings() => Task.FromResult(Decode(SettingsPath));

    public Task SaveStats(string content)
    {
        File.WriteAllText(StatsPath, SaveController.Encrypt(content));
        return Task.CompletedTask;
    }

    public Task<string?> LoadStats() => Task.FromResult(Decode(StatsPath));

    private static string? Read(string path) => File.Exists(path) ? File.ReadAllText(path) : null;

    private static string? Decode(string path)
    {
        var raw = Read(path);
        return raw != null ? SaveController.DecodeToJson(raw) : null;
    }
}
