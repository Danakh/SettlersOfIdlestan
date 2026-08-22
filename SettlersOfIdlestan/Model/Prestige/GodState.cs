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
        /// Essences divines gagnées <b>pendant le run courant</b> : chaque Purification d'Os Divins
        /// dans les Abysses en octroie directement 1 (voir DivineBonesController), tant que
        /// <see cref="AscensionController.GetDivineEssenceCap"/> (lié à la corruption) n'est pas
        /// atteint. Remise à zéro à chaque prestige (voir PrestigeController.PerformPrestige) et à
        /// chaque Ascension. N'inclut jamais les essences du Reliquaire (<see cref="DivineEssenceReliquaryFloor"/>) :
        /// c'est ce qui les exclut du plafond de corruption et du coût de Purification (voir
        /// DivineBones.EssenceAlreadyCollected) — seul ce champ pilote l'un et l'autre. Pour
        /// l'Ascension (seuil, points divins gagnés), c'est la somme des deux qui compte — voir
        /// AscensionController.GetEffectiveDivineEssence.
        /// </summary>
        public int DivineEssence { get; set; }

        /// <summary>
        /// Total cumulé d'essences divines gagnées (cross-prestige, ne diminue jamais).
        /// </summary>
        public int TotalDivineEssenceEarned { get; set; }

        /// <summary>
        /// Essences divines conservées par le Reliquaire (Reliquaire Sacré/Renforcé), en dehors de
        /// <see cref="DivineEssence"/> : recalculée à chaque prestige comme le minimum entre
        /// Civilization.DivineEssenceKeptOnPrestige et le total détenu avant ce prestige
        /// (<see cref="DivineEssence"/> + cette réserve elle-même — voir PrestigeController.PerformPrestige),
        /// puis <see cref="DivineEssence"/> repart de zéro. Ne compte ni dans le plafond de corruption
        /// ni dans le coût de Purification des Os Divins suivants, mais compte comme essence effective
        /// pour l'Ascension (seuil et points divins gagnés — voir AscensionController.GetEffectiveDivineEssence).
        /// Perdue uniquement à l'Ascension (remise à zéro en même temps que DivineEssence) ; survit,
        /// elle, à la perte de la dernière ville dans les Abysses (voir AbyssGateController.OnCityDestroyed,
        /// qui ne fait perdre que <see cref="DivineEssence"/>).
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
