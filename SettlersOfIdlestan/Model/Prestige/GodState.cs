using SettlersOfIdlestan.Model.Ascension;
using SettlersOfIdlestan.Model.IslandMap;
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
        /// Plafonds de niveau par bâtiment pour l'automatisation de construction, en 3 préréglages
        /// (cross-prestige, cross-ascension — voir TechnologyId.AutomationPreset).
        /// </summary>
        public AutomationPresetSettings AutomationPresets { get; set; } = new();

        /// <summary>
        /// Interrupteurs, seuils de vente/achat auto et restriction de production de soldats de
        /// l'automatisation (cross-prestige ET cross-ascension, comme <see cref="AutomationPresets"/>) :
        /// une seule instance, jamais recréée par PrestigeController.PerformPrestige ni AscensionController.
        /// Câblée sur WorldState.AutomationSettings (property qui garde son propre stockage pour l'état
        /// éphémère lié à l'île en cours — cible de raid, Héraut de Guerre, Vendetta) à chaque
        /// île/prestige/ascension/chargement par MainGameController.InitializeControllersForCurrentIsland,
        /// qui écrase la valeur fraîchement générée/désérialisée de WorldState.AutomationSettings par
        /// cette même instance. Voir AutomationSettings.ResetIslandEphemeralState (réinitialisation de
        /// l'état éphémère à chaque nouvelle île) et <see cref="AutomationSettingsMigrated"/> (migration
        /// ponctuelle depuis l'ancien emplacement par île).
        /// </summary>
        public AutomationSettings AutomationSettings { get; set; } = new();

        /// <summary>
        /// [Migration héritée v0.20.1] Vrai une fois que <see cref="AutomationSettings"/> a récupéré les
        /// interrupteurs/seuils de l'île en cours depuis son ancien emplacement (WorldState.AutomationSettings,
        /// propre à chaque île) — voir MainGameController.InitializeControllersForCurrentIsland. Faux par
        /// défaut : une sauvegarde antérieure à cette migration ne contient pas ce champ et charge donc
        /// faux, ce qui déclenche la migration une seule fois. Sans ce garde-fou, le prestige ou
        /// l'ascension suivant — qui régénère un WorldState.AutomationSettings vierge — écraserait les
        /// réglages migrés avec des valeurs par défaut.
        /// </summary>
        public bool AutomationSettingsMigrated { get; set; }

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
        /// <see cref="AscensionController.GetDivineEssenceCap"/> (lié à la corruption et au nombre de
        /// pouvoirs divins débloqués) n'est pas atteint. Remise à zéro à chaque prestige (voir
        /// PrestigeController.PerformPrestige) et à
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
