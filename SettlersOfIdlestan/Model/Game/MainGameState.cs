using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Prestige;
using SettlersOfIdlestan.Model.Tasks;
using System;
using System.Net.NetworkInformation;

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
        public WorldState? CurrentWorldState => PrestigeState?.WorldState;
        public GameClock Clock { get; set; }
        public GamePRNG PRNG { get; set; }

        public GameSettings Settings { get; set; } = new();

        /// <summary>
        /// Index du step tutoriel courant. null = tutoriel terminé (ou ancienne sauvegarde).
        /// </summary>
        public int TutorialStepIndex { get; set; } = 0;

        /// <summary>
        /// Statistiques cumulatives all-time (achievements, tâches tutoriel).
        /// </summary>
        public GameRecord GameRecord { get; set; } = new();

        public MainGameState(int? prngSeed = null)
        {
            GodState = new GodState();
            Clock = new GameClock();
            PRNG = (prngSeed.HasValue) ? new GamePRNG(prngSeed.Value) : new GamePRNG();
        }

        public MainGameState(WorldState worldState, GameClock clock, GamePRNG prng)
        {
            var prestigeState = new PrestigeState(worldState);
            GodState = new GodState(prestigeState);
            Clock = clock;
            PRNG = prng;
        }
    }
}
