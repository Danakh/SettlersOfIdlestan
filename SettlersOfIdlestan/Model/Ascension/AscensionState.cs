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
}
