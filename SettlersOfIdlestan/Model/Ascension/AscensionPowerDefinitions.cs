using System.Collections.Generic;
using System.Linq;

namespace SettlersOfIdlestan.Model.Ascension;

/// <summary>
/// Liste ordonnée des pouvoirs divins. Foi (colonne -1) est le pouvoir fondateur, toujours
/// disponible ; chaque colonne 0-3 ne peut être débloquée qu'une fois Foi acquise, puis ses
/// pouvoirs se débloquent dans l'ordre de la liste au sein de cette colonne.
/// </summary>
public static class AscensionPowerDefinitions
{
    public static IReadOnlyList<AscensionPowerDefinition> All { get; } = new[]
    {
        new AscensionPowerDefinition(AscensionPowerId.Faith,
            "ascension_power_faith_name", "ascension_power_faith_desc", AscensionPowerDefinition.FoundationColumn),

        new AscensionPowerDefinition(AscensionPowerId.HandOfGod,
            "ascension_power_handofgod_name", "ascension_power_handofgod_desc", column: 0),

        new AscensionPowerDefinition(AscensionPowerId.EyeOfGod,
            "ascension_power_eyeofgod_name", "ascension_power_eyeofgod_desc", column: 1),

        new AscensionPowerDefinition(AscensionPowerId.WalkOfGod,
            "ascension_power_walkofgod_name", "ascension_power_walkofgod_desc", column: 2),

        new AscensionPowerDefinition(AscensionPowerId.ArmOfGod,
            "ascension_power_armofgod_name", "ascension_power_armofgod_desc", column: 3),
    };

    public static AscensionPowerDefinition? Get(AscensionPowerId id)
    {
        foreach (var def in All)
            if (def.Id == id) return def;
        return null;
    }

    /// <summary>Pouvoirs de la colonne donnée (0-3), dans leur ordre de déblocage au sein de la colonne.</summary>
    public static List<AscensionPowerDefinition> GetColumn(int column) =>
        All.Where(d => d.Column == column).ToList();
}
