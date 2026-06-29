using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Controller;
using Xunit;

namespace SOITests.TestUtilities;

/// <summary>
/// Test helper to save a MainGameController's state to the "saves" folder,
/// reload it into a new MainGameController and assert the saved state
/// is identical after a round-trip.
/// </summary>
public static class SaveUtils
{
    // StepIslandSaveGeneratorTests regenerates "current" saves and we can't force xUnit to run it
    // before the tests that read them — so reads retry briefly to ride out the (now minimal) window
    // where a save is mid-(re)write.
    private const int ReadRetryMaxAttempts = 10;
    private static readonly TimeSpan ReadRetryDelay = TimeSpan.FromMilliseconds(500);

    // ── Folder-aware API ─────────────────────────────────────────────────────

    /// <summary>
    /// Saves controller state to saves/{folder}/{name}.json, then reloads and
    /// asserts round-trip equality. Writing to release-* folders is forbidden.
    /// </summary>
    public static void SaveAndReloadAndAssertEqual(MainGameController controller, string folder, string name)
    {
        if (controller == null) throw new ArgumentNullException(nameof(controller));
        if (string.IsNullOrWhiteSpace(folder)) throw new ArgumentException("folder cannot be empty", nameof(folder));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("name cannot be empty", nameof(name));
        if (folder.StartsWith("release-", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Cannot write to release folder '{folder}' — release saves are immutable.");

        var filePath = ResolvePath(folder, name);
        WriteAndAssertEqual(controller, filePath);
    }

    /// <summary>Returns true when saves/{folder}/{name}.json exists (retries briefly — see TryReadWithRetry).</summary>
    public static bool SaveExists(string folder, string name)
        => TryReadWithRetry(ResolvePath(folder, name), out _);

    /// <summary>Loads saves/{folder}/{name}.json into a fresh controller (retries briefly — see TryReadWithRetry).</summary>
    public static MainGameController LoadSave(string folder, string name)
    {
        var filePath = ResolvePath(folder, name);
        Assert.True(TryReadWithRetry(filePath, out var content), $"Expected save file at {filePath}");

        var controller = new MainGameController();
        controller.ImportMainState(content);
        return controller;
    }

    // ── Legacy flat-saves API (used by AutoplayerTests) ──────────────────────

    /// <summary>Saves controller state to saves/{name}.json and asserts round-trip equality.</summary>
    public static void SaveAndReloadAndAssertEqual(MainGameController controller, string name)
    {
        if (controller == null) throw new ArgumentNullException(nameof(controller));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("name cannot be empty", nameof(name));

        var solutionRoot = GetSolutionRootDirectory(Directory.GetCurrentDirectory());
        var savesDir = Path.Combine(solutionRoot, "saves");
        if (!Directory.Exists(savesDir)) Directory.CreateDirectory(savesDir);

        WriteAndAssertEqual(controller, Path.Combine(savesDir, name + ".json"));
    }

    /// <summary>Loads saves/{name}.json into a fresh controller.</summary>
    public static MainGameController LoadSave(string name)
    {
        var solutionRoot = GetSolutionRootDirectory(Directory.GetCurrentDirectory());
        var filePath = Path.Combine(solutionRoot, "saves", name + ".json");
        Assert.True(File.Exists(filePath), $"Expected save file at {filePath}");

        var controller = new MainGameController();
        controller.ImportMainState(File.ReadAllText(filePath));
        return controller;
    }

    // ── Internals ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Écrit l'état du contrôleur dans saves/{folder}/{name}.json sans vérification de round-trip.
    /// Utile pour sauvegarder un état en cas d'échec de test, où l'assertion elle-même a déjà échoué.
    /// </summary>
    public static void SaveOnly(MainGameController controller, string folder, string name)
    {
        if (controller == null) throw new ArgumentNullException(nameof(controller));
        if (string.IsNullOrWhiteSpace(folder)) throw new ArgumentException("folder cannot be empty", nameof(folder));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("name cannot be empty", nameof(name));
        if (folder.StartsWith("release-", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Cannot write to release folder '{folder}' — release saves are immutable.");

        var filePath = ResolvePath(folder, name);
        var exported = controller.ExportMainState();
        var tempPath = filePath + ".tmp";
        File.WriteAllText(tempPath, exported);
        File.Move(tempPath, filePath, overwrite: true);
    }

    /// <summary>
    /// Supprime, dans saves/{folder}, les fichiers .json dont le nom (sans extension) n'est pas
    /// dans keepNames. A appeler après la régénération (pas avant) : les saves régénérées sont
    /// remplacées de façon atomique par WriteAndAssertEqual, donc seuls les fichiers vraiment
    /// obsolètes (ex: ancien format JSON non-chiffré) doivent être nettoyés ici.
    /// </summary>
    public static void PruneFolder(string folder, IEnumerable<string> keepNames)
    {
        var solutionRoot = GetSolutionRootDirectory(Directory.GetCurrentDirectory());
        var dir = Path.Combine(solutionRoot, "saves", folder);
        if (!Directory.Exists(dir)) return;
        var keep = new HashSet<string>(keepNames, StringComparer.OrdinalIgnoreCase);
        foreach (var file in Directory.GetFiles(dir, "*.json"))
        {
            if (!keep.Contains(Path.GetFileNameWithoutExtension(file)))
                File.Delete(file);
        }
    }

    private static string ResolvePath(string folder, string name)
    {
        var solutionRoot = GetSolutionRootDirectory(Directory.GetCurrentDirectory());
        var dir = Path.Combine(solutionRoot, "saves", folder);
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        return Path.Combine(dir, name + ".json");
    }

    private static void WriteAndAssertEqual(MainGameController controller, string filePath)
    {
        var exported = controller.ExportMainState();

        // Ecrit dans un fichier temporaire puis remplace par un rename atomique : le fichier
        // final n'est jamais vide/partiel, et la fenêtre où il est manquant se limite à la
        // durée du rename (quasi instantané), pas à celle de l'écriture du contenu.
        var tempPath = filePath + ".tmp";
        File.WriteAllText(tempPath, exported);
        File.Move(tempPath, filePath, overwrite: true);

        var reloadedController = new MainGameController();
        reloadedController.ImportMainState(File.ReadAllText(filePath));

        var originalIsland = controller.CurrentMainState?.CurrentWorldState
                             ?? throw new InvalidOperationException("Controller does not have a current island state");
        var reloadedIsland = reloadedController.CurrentMainState?.CurrentWorldState
                             ?? throw new InvalidOperationException("Reloaded controller does not have a current island state");

        Assert.Equal(originalIsland.GetMapForZ(IslandMap.SurfaceLayer)!.Tiles.Count, reloadedIsland.GetMapForZ(IslandMap.SurfaceLayer)!.Tiles.Count);
        Assert.Equal(originalIsland.Civilizations.Count, reloadedIsland.Civilizations.Count);
        Assert.Equal(
            originalIsland.Civilizations.Sum(c => c.Roads.Count),
            reloadedIsland.Civilizations.Sum(c => c.Roads.Count));
        Assert.Equal(
            originalIsland.Civilizations.Sum(c => c.Cities.Count),
            reloadedIsland.Civilizations.Sum(c => c.Cities.Count));
        Assert.Equal(
            originalIsland.Civilizations.Sum(c => c.Cities.Sum(city => city.Buildings.Count)),
            reloadedIsland.Civilizations.Sum(c => c.Cities.Sum(city => city.Buildings.Count)));

        foreach (var originalCiv in originalIsland.Civilizations)
        {
            var reloadedCiv = reloadedIsland.Civilizations.FirstOrDefault(c => c.Index == originalCiv.Index)
                              ?? throw new InvalidOperationException($"Missing civilization with index {originalCiv.Index} in reloaded state");

            foreach (Resource res in Enum.GetValues(typeof(Resource)))
            {
                Assert.Equal(
                    originalCiv.GetResourceQuantity((Resource)res),
                    reloadedCiv.GetResourceQuantity((Resource)res));
            }
        }
    }

    /// <summary>
    /// Tente de lire filePath, en retentant toutes les 0.5s (jusqu'à 10 fois) si le fichier est
    /// absent ou verrouillé en écriture (StepIslandSaveGeneratorTests peut être en train de le
    /// (re)générer dans une autre passe — l'ordre entre classes de test n'est pas garanti).
    /// </summary>
    private static bool TryReadWithRetry(string filePath, out string content)
    {
        for (int attempt = 1; attempt <= ReadRetryMaxAttempts; attempt++)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    content = File.ReadAllText(filePath);
                    return true;
                }
            }
            catch (IOException)
            {
                // Fichier verrouillé pendant son écriture — on retente.
            }

            if (attempt < ReadRetryMaxAttempts)
                Thread.Sleep(ReadRetryDelay);
        }

        content = "";
        return false;
    }

    internal static string GetSolutionRootDirectory(string startDirectory)
    {
        var dir = new DirectoryInfo(startDirectory);
        while (dir != null)
        {
            if (dir.GetFiles("*.csproj").Any())
                return dir.FullName;
            dir = dir.Parent;
        }
        return startDirectory;
    }
}
