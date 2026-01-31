using System;
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
        public GameClock Clock { get; set; }

        public MainGameState()
        {
            GodState = new GodState();
            Clock = new GameClock();
        }

        public MainGameState(GodState godState, GameClock clock)
        {
            GodState = godState;
            Clock = clock;
        }
    }
}
