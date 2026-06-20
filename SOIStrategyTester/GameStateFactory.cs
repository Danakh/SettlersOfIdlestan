using System;
using System.IO;
using SettlersOfIdlestan.Controller;

namespace SOIStrategyTester;

/// <summary>
/// Builds a fresh MainGameController for a strategy run, either from a save file (same encrypted/JSON
/// format produced by MainGameController.ExportMainState, as found under the repo's saves/ folder) or
/// from a brand new game. Strategies are compared by re-running each one from an identical starting
/// state, so a new controller must be built for every run rather than reusing a mutated one.
/// </summary>
public static class GameStateFactory
{
    public static MainGameController FromSaveFile(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Save file not found: {path}", path);

        var controller = new MainGameController();
        controller.ImportMainState(File.ReadAllText(path));
        return controller;
    }

    public static MainGameController NewGame(int? worldId, int? prngSeed)
    {
        var controller = new MainGameController();
        var atlas = controller.AtlasController;
        var resolvedWorldId = worldId ?? atlas.GetFirstWorldId();
        var parameters = atlas.GetIslandParameters(resolvedWorldId);

        if (controller.CreateNewGame(parameters, prngSeed) == null)
            throw new InvalidOperationException($"Failed to generate a new game for world id {resolvedWorldId}.");

        return controller;
    }
}
