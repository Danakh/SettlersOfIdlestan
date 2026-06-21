using SettlersOfIdlestan.Model.Ascension;
using System;

namespace SettlersOfIdlestan.Model.Prestige
{
    /// <summary>
    /// Représente l'état du 'Dieu' qui contient l'état de prestige.
    /// Sérialisable pour la persistance ou le transport.
    /// </summary>
    [Serializable]
    public class GodState
    {
        /// <summary>
        /// L'état de prestige associé au dieu.
        /// </summary>
        public PrestigeState? PrestigeState { get; set; }

        /// <summary>
        /// Pouvoirs divins débloqués (cross-prestige).
        /// </summary>
        public AscensionState AscensionState { get; set; } = new();

        /// <summary>
        /// Points divins actuels (cross-prestige).
        /// </summary>
        public int GodPoints { get; set; }

        /// <summary>
        /// Total cumulé de points divins gagnés (cross-prestige, ne diminue jamais).
        /// </summary>
        public int TotalGodPointsEarned { get; set; }

        /// <summary>
        /// Constructeur parameterless requis par certains sérialiseurs.
        /// </summary>
        public GodState() { }

        public GodState(PrestigeState prestigeState)
        {
            PrestigeState = prestigeState;
        }
    }
}
