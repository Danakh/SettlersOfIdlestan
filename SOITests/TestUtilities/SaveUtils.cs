using System;
using System.IO;
using System.Linq;
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

    /// <summary>Returns true when saves/{folder}/{name}.json exists.</summary>
    public static bool SaveExists(string folder, string name)
        => File.Exists(ResolvePath(folder, name));

    /// <summary>Loads saves/{folder}/{name}.json into a fresh controller.</summary>
    public static MainGameController LoadSave(string folder, string name)
    {
        var filePath = ResolvePath(folder, name);
        Assert.True(File.Exists(filePath), $"Expected save file at {filePath}");

        var controller = new MainGameController();
        controller.ImportMainState(File.ReadAllText(filePath));
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
        File.WriteAllText(filePath, exported);

        var reloadedController = new MainGameController();
        reloadedController.ImportMainState(File.ReadAllText(filePath));

        var originalIsland = controller.CurrentMainState?.CurrentWorldState
                             ?? throw new InvalidOperationException("Controller does not have a current island state");
        var reloadedIsland = reloadedController.CurrentMainState?.CurrentWorldState
                             ?? throw new InvalidOperationException("Reloaded controller does not have a current island state");

        Assert.Equal(originalIsland.GetMapForZ(IslandMap.SurfaceLayer).Tiles.Count, reloadedIsland.GetMapForZ(IslandMap.SurfaceLayer).Tiles.Count);
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

    private static string GetSolutionRootDirectory(string startDirectory)
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
