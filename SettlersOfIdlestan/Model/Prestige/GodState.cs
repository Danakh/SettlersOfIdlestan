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
        /// Essences divines actuelles : chaque Purification d'Os Divins dans les Abysses en octroie
        /// directement 1 (voir DivineBonesController), converties en GodPoints à l'Ascension (qui les
        /// remet à zéro). Perdues au prestige, sauf jusqu'à Civilization.DivineEssenceKeptOnPrestige
        /// (Reliquaire Sacré/Renforcé — voir PrestigeController.PerformPrestige). Pilote aussi le coût
        /// de Purification des Os Divins suivants (voir DivineBones.EssenceAlreadyCollected).
        /// </summary>
        public int DivineEssence { get; set; }

        /// <summary>
        /// Total cumulé d'essences divines gagnées (cross-prestige, ne diminue jamais).
        /// </summary>
        public int TotalDivineEssenceEarned { get; set; }

        /// <summary>
        /// Essences divines garanties par le Reliquaire au début du run courant : figée à la valeur de
        /// <see cref="DivineEssence"/> juste après le clamp du Reliquaire dans
        /// PrestigeController.PerformPrestige (remise à zéro par l'Ascension, en même temps que
        /// DivineEssence). Sert de plancher lorsque le joueur perd sa dernière ville dans les Abysses
        /// (voir AbyssGateController.OnCityDestroyed) : les essences récoltées pendant le run sont
        /// perdues, mais jamais celles déjà garanties par le Reliquaire avant que le run ne commence.
        /// </summary>
        public int DivineEssenceReliquaryFloor { get; set; }

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
