using System;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Prestige;
using SettlersOfIdlestan.Model.Tasks;

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

        public GameSettings Settings { get; set; } = new();

        /// <summary>
        /// Index du step tutoriel courant. null = tutoriel terminé (ou ancienne sauvegarde).
        /// </summary>
        public int TutorialStepIndex { get; set; } = 0;

        /// <summary>
        /// Statistiques cumulatives all-time (achievements, tâches tutoriel).
        /// </summary>
        public GameRecord GameRecord { get; set; } = new();

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
