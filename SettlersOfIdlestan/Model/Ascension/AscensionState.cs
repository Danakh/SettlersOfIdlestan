using System;
using System.Collections.Generic;
using SettlersOfIdlestan.Model.Buildings;

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
    /// Bâtiment unique permanent choisi (droit acquis dès la première Ascension) — voir
    /// AscensionController.PermanentUniqueBuildingChoices. Appliqué à chaque début d'île
    /// (AscensionController.ApplyPermanentUniqueBuildingToCivilization), sans jamais occuper
    /// d'emplacement dans une ville.
    /// </summary>
    public BuildingType? PermanentUniqueBuilding { get; set; }
}
