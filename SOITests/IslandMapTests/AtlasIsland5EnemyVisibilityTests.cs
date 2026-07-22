using System.Linq;
using SettlersOfIdlestan.Controller;
using SettlersOfIdlestan.Model.Monsters;
using SOITests.IslandMapTests.StepIslandTest;
using SOITests.TestUtilities;
using Xunit;

namespace SOITests.IslandMapTests;

/// <summary>
/// Sanity check on the Atlas generator (AtlasController.BuildHighEndIsland, WorldId 5+): right after
/// loading/(re)generating Island5, letting 6000 ticks (60 simulated seconds) pass with no player action
/// must not bring any hostile monster, nor any other civilization, into the player's current view.
/// Regenerates the island (MainGameController.RestartIsland, same WorldId, fresh random Atlas
/// parameters) and repeats, for a total of 10 independently generated Island5 variants.
/// </summary>
[Collection(StepIslandTestCollection.Name)]
public class AtlasIsland5EnemyVisibilityTests
{
    private const long TicksToSimulateForMonsters = 6000;
    private const long TicksToSimulateForOtherCivilizations = 30000;
    private const int Iterations = 10;

    /// <summary>
    /// GameClock.Advanced fires once per SimulateAdvance call, and every periodic monster behavior
    /// (movement, spawns, attacks — see MonsterFeatureController.Update) is a single "has the interval
    /// elapsed since last time?" check per firing, not a catch-up loop. A single
    /// SimulateAdvance(300_000) therefore gives a movable monster exactly one chance to take one step,
    /// identical to SimulateAdvance(6_000) — the size of the jump stops mattering past the first
    /// interval. Real gameplay instead calls GameClock.Advance(now) many times per second, so it
    /// fires that check repeatedly. Chunking here reproduces that: each chunk is small enough that a
    /// monster crosses at most one interval per chunk, so N ticks give it roughly N / interval chances
    /// to act, the same as it would get across N real ticks of play.
    /// </summary>
    private const long ChunkTicks = 100;

    [Fact]
    public void Island5_NoEnemiesInPlayerViewAfterTicksAdvance_AcrossRegenerations()
    {
        var controller = SaveUtils.LoadSave("current", "Island5_Prestige");

        for (int i = 0; i < Iterations; i++)
        {
            if (i > 0)
                controller.RestartIsland();

            controller.Clock!.SimulateAdvance(TicksToSimulateForMonsters, ChunkTicks);

            var visibleMaps = GetPlayerVisibleMaps(controller);
            AssertNoEnemiesInPlayerView(controller, visibleMaps, i);
            AssertNoOtherCivilizationInPlayerView(controller, visibleMaps, i);

            var additionalTicks = TicksToSimulateForOtherCivilizations - TicksToSimulateForMonsters;
            controller.Clock!.SimulateAdvance(additionalTicks, ChunkTicks);

            visibleMaps = GetPlayerVisibleMaps(controller);
            AssertNoOtherCivilizationInPlayerView(controller, visibleMaps, i);
        }
    }

    private static System.Collections.Generic.List<SettlersOfIdlestan.Model.IslandMap.VisibleIslandMap> GetPlayerVisibleMaps(MainGameController controller)
    {
        var worldState = controller.CurrentMainState!.CurrentWorldState!;
        var playerIndex = worldState.PlayerCivilization.Index;

        return worldState.GetMapsByZ()
            .Select(kvp => worldState.Visibility.GetForZ(kvp.Key))
            .Where(maps => maps.ContainsKey(playerIndex))
            .Select(maps => maps[playerIndex])
            .ToList();
    }

    private static void AssertNoEnemiesInPlayerView(
        MainGameController controller,
        System.Collections.Generic.List<SettlersOfIdlestan.Model.IslandMap.VisibleIslandMap> visibleMaps,
        int iteration)
    {
        var worldState = controller.CurrentMainState!.CurrentWorldState!;

        var visibleEnemies = worldState.Features
            .OfType<MonsterFeature>()
            .Where(m => !m.AttacksOtherMonsters) // exclut les monstres "amis" (ex: Aventurier)
            .Where(m => visibleMaps.Any(vm => vm.IsOnSameLayer(m.Position) && vm.HasTile(m.Position)))
            .ToList();

        Assert.True(visibleEnemies.Count == 0,
            $"[iteration {iteration}] Expected no enemies in player view, found {visibleEnemies.Count}: " +
            string.Join(", ", visibleEnemies.Select(m => $"{m.GetType().Name}@{m.Position}")));
    }

    /// <summary>
    /// Mirrors FeatureController.DiscoverCivilizations' own visibility check: an NPC civilization is
    /// "in view" when one of its cities touches a currently player-visible tile (roads alone don't
    /// count — the discovery message must only fire once an actual city is visible).
    /// </summary>
    private static void AssertNoOtherCivilizationInPlayerView(
        MainGameController controller,
        System.Collections.Generic.List<SettlersOfIdlestan.Model.IslandMap.VisibleIslandMap> visibleMaps,
        int iteration)
    {
        var worldState = controller.CurrentMainState!.CurrentWorldState!;
        var playerIndex = worldState.PlayerCivilization.Index;

        var visibleCivs = worldState.Civilizations
            .Where(civ => civ.Index != playerIndex)
            .Where(civ =>
                civ.Cities.Any(city =>
                    visibleMaps.Any(vm => vm.IsVertexVisible(city.Position))))
            .ToList();

        Assert.True(visibleCivs.Count == 0,
            $"[iteration {iteration}] Expected no other civilization in player view, found {visibleCivs.Count}: " +
            string.Join(", ", visibleCivs.Select(c => $"civ {c.Index}")));
    }
}
