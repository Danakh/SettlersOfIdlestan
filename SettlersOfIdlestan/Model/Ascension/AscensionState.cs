using System;
using System.Collections.Generic;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Races;

namespace SettlersOfIdlestan.Model.Ascension;

/// <summary>
/// État des pouvoirs divins débloqués par le joueur. Persiste cross-prestige (porté par GodState).
/// </summary>
[Serializable]
public class AscensionState
{
    public HashSet<AscensionPowerId> UnlockedPowers { get; set; } = new();

    public bool IsEyeOfGodActive => UnlockedPowers.Contains(AscensionPowerId.EyeOfGod);

    /// <summary>
    /// Nombre total d'Ascensions effectuées (cross-prestige, ne diminue jamais). Pilote le nombre
    /// d'emplacements de bâtiments uniques permanents (voir AscensionController.
    /// PermanentUniqueBuildingSlots) : 1 emplacement supplémentaire gratuit par Ascension.
    /// </summary>
    public int AscensionsPerformed { get; set; }

    /// <summary>
    /// Bâtiments uniques permanents choisis (jusqu'à <see cref="AscensionsPerformed"/> emplacements)
    /// — voir AscensionController.PermanentUniqueBuildingChoices. Appliqués à chaque début d'île
    /// (AscensionController.ApplyPermanentUniqueBuildingToCivilization), sans jamais occuper
    /// d'emplacement dans une ville.
    /// </summary>
    public HashSet<BuildingType> PermanentUniqueBuildings { get; set; } = new();

    /// <summary>
    /// Race jouée pendant le cycle d'Ascension en cours (toutes les îles jusqu'à la prochaine
    /// Ascension). Choisie au moment de l'Ascension une fois la première rangée de pouvoirs divins
    /// complète (voir AscensionController.IsRaceSelectionUnlocked) ; Humains par défaut, y compris
    /// pour les anciennes sauvegardes.
    /// </summary>
    public RaceId SelectedRace { get; set; } = RaceId.Human;

    /// <summary>
    /// Races avec lesquelles au moins une Ascension a été effectuée. Chacune ajoute définitivement
    /// son bâtiment racial aux choix de bâtiment permanent (voir
    /// AscensionController.PermanentUniqueBuildingChoices), quelle que soit la race jouée ensuite.
    /// </summary>
    public HashSet<RaceId> AscendedRaces { get; set; } = new();
}
