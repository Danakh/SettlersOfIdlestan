using System;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.PrestigeMap;

namespace SettlersOfIdlestan.Model.Game
{
    /// <summary>
    /// Root state of the game, contains the god state and the game clock.
    /// Serializable for persistence/transport.
    /// </summary>
    [Serializable]
    public class MainGameState
    {
        public GodState GodState { get; set; }
        public PrestigeState? PrestigeState => GodState.PrestigeState;
        public IslandState? CurrentIslandState => PrestigeState?.IslandState;
        public GameClock Clock { get; set; }
        public GamePRNG PRNG { get; set; }

        public MainGameState()
        {
            GodState = new GodState();
            Clock = new GameClock();
            PRNG = new GamePRNG();
        }

        public MainGameState(IslandState islandState, GameClock clock)
        {
            var prestigeState = new PrestigeState(islandState);
            GodState = new GodState(prestigeState);
            Clock = clock;
            PRNG = new GamePRNG();
        }
        public MainGameState(GodState godState, GameClock clock)
        {
            GodState = godState;
            Clock = clock;
            PRNG = new GamePRNG();
        }
    }
}
