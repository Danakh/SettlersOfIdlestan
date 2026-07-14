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
        /// Essences divines actuelles (cross-prestige), gagnées en purifiant des Os Divins dans les
        /// Abysses. Se transformeront en GodPoints via une mécanique à venir.
        /// </summary>
        public int DivineEssence { get; set; }

        /// <summary>
        /// Total cumulé d'essences divines gagnées (cross-prestige, ne diminue jamais). Pilote
        /// aussi le coût de Purification des Os Divins suivants (voir DivineBones.EssenceAlreadyCollected).
        /// </summary>
        public int TotalDivineEssenceEarned { get; set; }

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
