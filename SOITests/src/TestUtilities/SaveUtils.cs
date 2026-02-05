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
    public static void SaveAndReloadAndAssertEqual(MainGameController controller, string name)
    {
        if (controller == null) throw new System.ArgumentNullException(nameof(controller));
        if (string.IsNullOrWhiteSpace(name)) throw new System.ArgumentException("name cannot be empty", nameof(name));

        var solutionRoot = GetSolutionRootDirectory(Directory.GetCurrentDirectory());
        var savesDir = Path.Combine(solutionRoot, "saves");
        if (!Directory.Exists(savesDir)) Directory.CreateDirectory(savesDir);

        var filePath = Path.Combine(savesDir, name + ".json");

        // Export current main state to JSON and save to file
        var exported = controller.ExportMainState();
        File.WriteAllText(filePath, exported);

        // Load the saved file into a fresh controller and export again
        var reloadedController = new MainGameController();
        var fileJson = File.ReadAllText(filePath);
        reloadedController.ImportMainState(fileJson);
        var roundtrip = reloadedController.ExportMainState();

        // Assert the two JSON representations match exactly
        Assert.Equal(exported, roundtrip);

        // Additionally compare some important IslandState metrics to give clearer test failures
        var originalIsland = controller.CurrentMainState?.CurrentIslandState
                             ?? throw new System.InvalidOperationException("Controller does not have a current island state");
        var reloadedIsland = reloadedController.CurrentMainState?.CurrentIslandState
                             ?? throw new System.InvalidOperationException("Reloaded controller does not have a current island state");

        // number of hex tiles
        var originalHexCount = originalIsland.Map.Tiles.Count;
        var reloadedHexCount = reloadedIsland.Map.Tiles.Count;
        Assert.Equal(originalHexCount, reloadedHexCount);

        // number of civilizations
        var originalCivCount = originalIsland.Civilizations.Count;
        var reloadedCivCount = reloadedIsland.Civilizations.Count;
        Assert.Equal(originalCivCount, reloadedCivCount);

        // total numbers across all civilizations: roads and cities
        var originalRoads = originalIsland.Civilizations.Sum(c => c.Roads.Count);
        var reloadedRoads = reloadedIsland.Civilizations.Sum(c => c.Roads.Count);
        Assert.Equal(originalRoads, reloadedRoads);

        var originalCities = originalIsland.Civilizations.Sum(c => c.Cities.Count);
        var reloadedCities = reloadedIsland.Civilizations.Sum(c => c.Cities.Count);
        Assert.Equal(originalCities, reloadedCities);

        // total buildings across all cities
        var originalBuildings = originalIsland.Civilizations.Sum(c => c.Cities.Sum(city => city.Buildings.Count));
        var reloadedBuildings = reloadedIsland.Civilizations.Sum(c => c.Cities.Sum(city => city.Buildings.Count));
        Assert.Equal(originalBuildings, reloadedBuildings);

        // Compare resources for each civilization for every Resource type
        foreach (var civPair in originalIsland.Civilizations.Select((c, idx) => (c, idx)))
        {
            var idx = civPair.idx;
            var originalCiv = civPair.c;
            var reloadedCiv = reloadedIsland.Civilizations.FirstOrDefault(c => c.Index == originalCiv.Index)
                              ?? throw new InvalidOperationException($"Missing civilization with index {originalCiv.Index} in reloaded state");

            foreach (Resource res in Enum.GetValues(typeof(Resource)))
            {
                var origQty = originalCiv.GetResourceQuantity((SettlersOfIdlestan.Model.IslandMap.Resource)res);
                var relQty = reloadedCiv.GetResourceQuantity((SettlersOfIdlestan.Model.IslandMap.Resource)res);
                Assert.Equal(origQty, relQty);
            }
        }
    }

    public static MainGameController LoadSave(string name)
    {
        // Locate the saved start file produced by the previous test
        var solutionRoot = GetSolutionRootDirectory(Directory.GetCurrentDirectory());
        var savesDir = Path.Combine(solutionRoot, "saves");
        var startPath = Path.Combine(savesDir, name);
        var filePath = Path.Combine(savesDir, name + ".json");
        Assert.True(File.Exists(filePath), $"Expected save file at {filePath}");

        var controller = new MainGameController();
        var json = File.ReadAllText(filePath);
        controller.ImportMainState(json);

        return controller;
    }

    private static string GetSolutionRootDirectory(string startDirectory)
    {
        var dir = new DirectoryInfo(startDirectory);
        while (dir != null)
        {
            // Consider this the solution root if we find a solution file or a .git folder
            if (dir.GetFiles("*.sln").Any() || Directory.Exists(Path.Combine(dir.FullName, ".git")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        // Fallback to the starting directory if nothing found
        return startDirectory;
    }
}
