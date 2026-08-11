using Avalonia.Controls;
using Avalonia.Platform.Storage;
using SettlersOfIdlestan.Controller;
using SettlersOfIdlestanSkia.Services;

namespace SettlersOfIdlestanAvalonia.Desktop.Services;

/// <summary>
/// Persistance de bureau : sauvegarde automatique, reglages et statistiques vivent dans un
/// dossier <c>saves</c> a cote de l'executable ; l'export et l'import explicites passent par le
/// selecteur de fichier natif, comme le head navigateur.
///
/// Le dossier <c>saves</c> est un contrat avec les joueurs Steam existants — le head OpenTK
/// ecrivait au meme endroit, et l'executable porte le meme nom pour cette raison. Le deplacer
/// ferait perdre leur partie a l'installation suivante. Le selecteur ne s'applique donc qu'a
/// SaveText/LoadText, jamais aux trois fichiers geres par le jeu lui-meme.
/// </summary>
public class DesktopFileSystemService : IFileSystemService
{
    private static readonly FilePickerFileType SaveFileType =
        new("Settlers of Idlestan") { Patterns = ["*.json"], MimeTypes = ["application/json"] };

    private readonly TopLevel? _topLevel;

    /// <param name="topLevel">
    /// Fenetre hote dont on tire le <see cref="IStorageProvider"/>. Elle n'est pas encore ouverte
    /// quand ce service est construit : le provider n'est resolu qu'au moment du clic. Null
    /// (tests, outillage) fait retomber l'export/import sur le dossier <c>saves</c>.
    /// </param>
    public DesktopFileSystemService(TopLevel? topLevel = null) => _topLevel = topLevel;

    private static string GetSavesDirectory()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "saves");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string AutoSavePath()  => Path.Combine(GetSavesDirectory(), "autosave.json");
    private static string SettingsPath()  => Path.Combine(GetSavesDirectory(), "settings.json");
    private static string StatsPath()     => Path.Combine(GetSavesDirectory(), "playerstats.json");

    /// <summary>
    /// Export : le joueur choisit ou ecrire. Une boite annulee n'ecrit rien — c'est un abandon,
    /// pas une erreur.
    /// </summary>
    public async Task SaveText(string fileName, string content)
    {
        var storage = _topLevel?.StorageProvider;
        if (storage is null || !storage.CanSave)
        {
            File.WriteAllText(Path.Combine(GetSavesDirectory(), fileName), content);
            return;
        }

        try
        {
            var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                SuggestedFileName      = fileName,
                DefaultExtension       = "json",
                FileTypeChoices        = [SaveFileType],
                SuggestedStartLocation = await StartLocation(storage),
                ShowOverwritePrompt    = true,
            });
            if (file is null) return;

            await using var stream = await file.OpenWriteAsync();
            await using var writer = new StreamWriter(stream);
            await writer.WriteAsync(content);
        }
        catch (Exception ex)
        {
            // Disque plein, dossier protege, chemin reseau tombe : l'appelant est un async void
            // (SettingsMenu.SaveGame), une exception qui remonte tuerait le processus.
            System.Diagnostics.Debug.WriteLine($"Export impossible : {ex.Message}");
        }
    }

    /// <summary>
    /// Import : le joueur designe la sauvegarde a relire. Renvoie null s'il annule, ce que
    /// l'appelant traite deja comme « ne rien charger ».
    /// </summary>
    public async Task<string?> LoadText(string fileName)
    {
        var storage = _topLevel?.StorageProvider;
        if (storage is null || !storage.CanOpen)
        {
            var path = Path.Combine(GetSavesDirectory(), fileName);
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }

        try
        {
            var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                AllowMultiple          = false,
                FileTypeFilter         = [SaveFileType],
                SuggestedStartLocation = await StartLocation(storage),
            });
            if (files.Count == 0) return null;

            await using var stream = await files[0].OpenReadAsync();
            using var reader = new StreamReader(stream);
            return await reader.ReadToEndAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Import impossible : {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Ouvre la boite sur le dossier <c>saves</c> : c'est la que se trouvent les sauvegardes des
    /// versions precedentes, et c'est l'endroit ou le joueur s'attend a ecrire par defaut.
    /// </summary>
    private static async Task<IStorageFolder?> StartLocation(IStorageProvider storage)
    {
        try { return await storage.TryGetFolderFromPathAsync(GetSavesDirectory()); }
        catch { return null; }
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
