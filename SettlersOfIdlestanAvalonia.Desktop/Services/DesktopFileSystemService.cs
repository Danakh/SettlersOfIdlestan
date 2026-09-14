using Avalonia.Controls;
using Avalonia.Platform.Storage;
using SettlersOfIdlestan.Controller;
using SettlersOfIdlestan.Model.Game;
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
    private readonly Func<string?>? _getLastDirectory;
    private readonly Action<string?>? _setLastDirectory;

    /// <param name="topLevel">
    /// Fenetre hote dont on tire le <see cref="IStorageProvider"/>. Elle n'est pas encore ouverte
    /// quand ce service est construit : le provider n'est resolu qu'au moment du clic. Null
    /// (tests, outillage) fait retomber l'export/import sur le dossier <c>saves</c>.
    /// </param>
    /// <param name="getLastDirectory">
    /// Lecture du dernier dossier d'export/import memorise dans les reglages
    /// (<c>GameSettings.LastSaveDirectory</c>). Des delegues plutot que le runtime lui-meme :
    /// le service n'a besoin que de cette valeur, et elle est lue au clic — donc bien apres
    /// l'initialisation du runtime, qui recoit justement ce service en parametre.
    /// </param>
    /// <param name="setLastDirectory">Ecriture du dossier retenu apres un choix du joueur.</param>
    public DesktopFileSystemService(
        TopLevel? topLevel = null,
        Func<string?>? getLastDirectory = null,
        Action<string?>? setLastDirectory = null)
    {
        _topLevel         = topLevel;
        _getLastDirectory = getLastDirectory;
        _setLastDirectory = setLastDirectory;
    }

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
            RememberDirectory(file);

            await using var stream = await file.OpenWriteAsync();
            await using var writer = new StreamWriter(stream);
            await writer.WriteAsync(content);
        }
        catch (Exception ex)
        {
            // Disque plein, dossier protege, chemin reseau tombe : l'appelant est un async void
            // (SettingsMenu.SaveGame), une exception qui remonte tuerait le processus. Le joueur,
            // lui, a demande un export et n'obtient aucun fichier : Debug.WriteLine etant supprime
            // en Release, il n'avait jusqu'ici strictement rien pour comprendre.
            GameLog.Error(nameof(DesktopFileSystemService), nameof(SaveText), ex);
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
            RememberDirectory(files[0]);

            await using var stream = await files[0].OpenReadAsync();
            using var reader = new StreamReader(stream);
            return await reader.ReadToEndAsync();
        }
        catch (Exception ex)
        {
            // Meme raison qu'a l'export : renvoyer null se lit « le joueur a annule », alors qu'ici
            // c'est une lecture qui a echoue. Sans trace, les deux cas sont indiscernables.
            GameLog.Error(nameof(DesktopFileSystemService), nameof(LoadText), ex);
            return null;
        }
    }

    /// <summary>
    /// Ouvre la boite sur le dernier dossier ou le joueur a exporte ou relu une sauvegarde, export
    /// et import partageant la meme memoire. A defaut — premiere partie, ou dossier disparu depuis
    /// (support amovible retire, dossier supprime, reglages recopies d'une autre machine) —, on
    /// retombe sur le dossier <c>saves</c> : c'est la que vit la sauvegarde automatique et celles
    /// des versions precedentes.
    /// </summary>
    private async Task<IStorageFolder?> StartLocation(IStorageProvider storage)
    {
        var last = _getLastDirectory?.Invoke();
        var path = !string.IsNullOrEmpty(last) && Directory.Exists(last) ? last : GetSavesDirectory();

        // Pas de journalisation ici : renvoyer null laisse simplement la boite de dialogue s'ouvrir
        // sur son dossier par defaut. C'est un confort, pas une operation qui a echoue.
        try { return await storage.TryGetFolderFromPathAsync(path); }
        catch { return null; }
    }

    /// <summary>
    /// Retient le dossier du fichier que le joueur vient de designer, pour rouvrir la boite au
    /// meme endroit la fois suivante. Silencieux si le chemin n'est pas un chemin local : le
    /// selecteur n'a alors rien qu'on puisse redonner a <c>TryGetFolderFromPathAsync</c>.
    /// </summary>
    private void RememberDirectory(IStorageFile file)
    {
        if (_setLastDirectory is null) return;

        try
        {
            if (!file.Path.IsAbsoluteUri || !file.Path.IsFile) return;
            var directory = Path.GetDirectoryName(file.Path.LocalPath);
            if (!string.IsNullOrEmpty(directory)) _setLastDirectory(directory);
        }
        catch (Exception ex)
        {
            // Memoriser le dossier est un confort : son echec ne doit pas faire echouer l'export ou
            // l'import lui-meme, qui n'a pas encore eu lieu a ce point.
            GameLog.Error(nameof(DesktopFileSystemService), nameof(RememberDirectory), ex);
        }
    }

    public Task SaveAuto(string content)
    {
        WriteAtomic(AutoSavePath(), File.WriteAllText, content);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Chemin emprunte par la sauvegarde automatique : le contenu arrive deja en UTF-8 (l'ASCII du
    /// Base64), il n'y a donc rien a transcoder avant de l'ecrire.
    /// </summary>
    public Task SaveAuto(ReadOnlyMemory<byte> utf8Content)
    {
        WriteAtomic(AutoSavePath(), static (path, bytes) =>
        {
            using var stream = File.Create(path);
            stream.Write(bytes.Span);
        }, utf8Content);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Ecrit a cote puis remplace, plutot que d'ecrire par-dessus. La sauvegarde automatique fait
    /// ~2 Mo toutes les 5 secondes : une coupure de courant ou un arret force pendant l'ecriture
    /// laissait un autosave.json tronque, donc une partie perdue. File.Move remplace en une seule
    /// operation du systeme de fichiers — soit l'ancienne version, soit la nouvelle, jamais un
    /// melange des deux.
    /// </summary>
    private static void WriteAtomic<T>(string path, Action<string, T> write, T content)
    {
        var temporary = path + ".tmp";
        write(temporary, content);
        File.Move(temporary, path, overwrite: true);
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
