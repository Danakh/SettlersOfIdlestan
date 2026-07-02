using System;
using System.Globalization;
using System.IO;
using System.Linq;
using SettlersOfIdlestan.Controller;

namespace SOITests.TestUtilities;

/// <summary>
/// Appends one CSV row per prestige to saves/run_current.csv (or run_release.csv), summarizing
/// progression: ticks elapsed during the run, cities built, total building levels, unique
/// buildings built, prestige points generated, prestige vertices purchased, completed research
/// and achievements unlocked.
/// </summary>
public static class RunSummaryReporter
{
    private const string Header = "Prestige,Ticks,CityCount,TotalBuildingLevels,UniqueBuildings,PrestigePointsGenerated,PrestigeVerticesPurchased,ResearchCompleted,AchievementsUnlocked";

    /// <summary>Deletes any existing recap file for the given folder, ready for a fresh run.</summary>
    public static void Reset(string loadFolder)
    {
        var path = ResolvePath(loadFolder);
        if (File.Exists(path)) File.Delete(path);
    }

    /// <summary>Appends one row summarizing the prestige that was just performed by the given controller.</summary>
    public static void AppendRow(string loadFolder, MainGameController controller)
    {
        var mainState = controller.CurrentMainState
            ?? throw new InvalidOperationException("Controller has no CurrentMainState after a prestige step");
        var prestigeState = mainState.PrestigeState
            ?? throw new InvalidOperationException("Controller has no PrestigeState after a prestige step");
        var lastRun = prestigeState.RunHistory.LastOrDefault()
            ?? throw new InvalidOperationException("PrestigeState.RunHistory is empty after a prestige step");

        var path = ResolvePath(loadFolder);
        var isNewFile = !File.Exists(path);

        using var writer = new StreamWriter(path, append: true);
        if (isNewFile) writer.WriteLine(Header);

        writer.WriteLine(string.Join(",",
            prestigeState.RunHistory.Count.ToString(CultureInfo.InvariantCulture),
            lastRun.TickDuration.ToString(CultureInfo.InvariantCulture),
            lastRun.CityCount.ToString(CultureInfo.InvariantCulture),
            lastRun.TotalBuildingLevels.ToString(CultureInfo.InvariantCulture),
            lastRun.UniqueBuildings.ToString(CultureInfo.InvariantCulture),
            lastRun.PrestigePoints.ToString(CultureInfo.InvariantCulture),
            prestigeState.PurchasedVertices.Count.ToString(CultureInfo.InvariantCulture),
            prestigeState.TechnologyTree.CompletedTechnologies.Count.ToString(CultureInfo.InvariantCulture),
            mainState.GameRecord.CompletedAchievements.Count.ToString(CultureInfo.InvariantCulture)));
    }

    private static string ResolvePath(string loadFolder)
    {
        var tag = loadFolder.StartsWith("release", StringComparison.OrdinalIgnoreCase) ? "release" : "current";
        var solutionRoot = SaveUtils.GetSolutionRootDirectory(Directory.GetCurrentDirectory());
        var savesDir = Path.Combine(solutionRoot, "saves");
        if (!Directory.Exists(savesDir)) Directory.CreateDirectory(savesDir);
        return Path.Combine(savesDir, $"run_{tag}.csv");
    }
}
