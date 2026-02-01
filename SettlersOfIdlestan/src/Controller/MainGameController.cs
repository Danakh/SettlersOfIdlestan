using System;
using System.Collections.Generic;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.PrestigeMap;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Civilization;

namespace SettlersOfIdlestan.Controller
{
    /// <summary>
    /// Controls creation and management of the main game state.
    /// </summary>
    public class MainGameController
    {
        private readonly IslandMapGenerator _islandGenerator;

        public MainGameController(IslandMapGenerator? islandGenerator = null)
        {
            _islandGenerator = islandGenerator ?? new IslandMapGenerator();
        }

        /// <summary>
        /// Creates a new MainGameState by generating a new island using the island generator.
        /// Returns null if island generation fails.
        /// </summary>
        /// <param name="tileData">Tile data used by the island generator (terrain type and counts).</param>
        /// <param name="civilizationCount">Number of civilizations to create (player is index 0).</param>
        public MainGameState? CreateNewGame(IEnumerable<(TerrainType terrainType, int tileCount)> tileData, int civilizationCount)
        {
            if (civilizationCount <= 0) throw new ArgumentException("civilizationCount must be >= 1", nameof(civilizationCount));

            var civs = new List<Civilization>();
            for (int i = 0; i < civilizationCount; i++)
            {
                var civ = new Civilization { Index = i };
                civs.Add(civ);
            }
            // Create a main state early so we can use its PRNG for deterministic generation
            var mainState = new MainGameState();

            // Use a generator wired with the main state's PRNG to ensure reproducible maps
            var generator = new IslandMapGenerator(mainState.PRNG);
            var map = generator.GenerateIsland(tileData, civs);
            if (map is null) return null;

            var islandState = new IslandState(map, civs);
            var prestigeState = new PrestigeState(islandState);
            var godState = new GodState(prestigeState);
            var clock = mainState.Clock;

            // populate the main state with the created sub-states
            mainState.GodState = godState;
            mainState.Clock = clock;

            return mainState;
        }
    }
}
