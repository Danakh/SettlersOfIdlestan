using System.IO;
using System.Linq;
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
