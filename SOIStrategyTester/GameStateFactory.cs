using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SettlersOfIdlestan.Controller;
using SettlersOfIdlestan.Controller.Ascension;
using SettlersOfIdlestan.Model.Ascension;
using SettlersOfIdlestan.Model.Races;

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

    /// <summary>
    /// A brand-new game played by <paramref name="race"/>, on the state a player would actually be in
    /// the first time they can pick it: the divine powers its tier requires are unlocked, and the game
    /// starts on the first island of a fresh Ascension cycle.
    ///
    /// <para>Getting there through the real <see cref="AscensionController.PerformAscension(SettlersOfIdlestan.Model.Game.MainGameState, SettlersOfIdlestan.Controller.Generator.IslandParameters, RaceId)"/>
    /// rather than by poking <c>AscensionState.SelectedRace</c> is deliberate — it is the only path
    /// that regenerates island 1 <i>for that race</i> (start terrain, Underworld start for the Dark
    /// Elves) and grants the free prestige vertices that come with Faith and with race selection being
    /// unlocked (central vertex + its 3 neighbours, plus RaceDefinition.FreePrestigeVertices). Skipping
    /// it would measure a race on a map and a prestige map it never actually starts from.</para>
    ///
    /// <para>The powers are granted rather than purchased: god points buy nothing else, so the
    /// bookkeeping would change no gameplay. The essence spent to trigger the Ascension is the
    /// controller's own minimum.</para>
    /// </summary>
    public static MainGameController NewGameForRace(RaceId race, int? prngSeed)
    {
        var controller = NewGame(worldId: null, prngSeed);
        var godState = controller.CurrentMainState!.GodState;

        foreach (var power in RequiredPowersFor(RaceDefinitions.Get(race).Tier))
            godState.AscensionState.UnlockedPowers.Add(power);

        godState.DivineEssence = AscensionController.MinDivineEssenceForAscension;
        controller.PerformAscension(race);
        return controller;
    }

    /// <summary>
    /// The divine powers that make <paramref name="tier"/> selectable, derived the same way
    /// AscensionController.IsRaceSelectionUnlocked / AreAdvancedRacesUnlocked check for it: Faith, then
    /// the first power of every column (Base), plus the second power of every column that has one
    /// (Advanced). Derived rather than hard-coded so adding a column or a power keeps this honest.
    /// </summary>
    public static IReadOnlyList<AscensionPowerId> RequiredPowersFor(RaceTier tier)
    {
        var powers = new List<AscensionPowerId> { AscensionPowerId.Faith };
        int rows = tier == RaceTier.Advanced ? 2 : 1;

        var columns = AscensionPowerDefinitions.All
            .Where(d => d.Column != AscensionPowerDefinition.FoundationColumn)
            .Select(d => d.Column)
            .Distinct()
            .OrderBy(c => c);

        foreach (var column in columns)
        {
            var powersInColumn = AscensionPowerDefinitions.GetColumn(column);
            for (int row = 0; row < rows && row < powersInColumn.Count; row++)
                powers.Add(powersInColumn[row].Id);
        }

        return powers;
    }
}
