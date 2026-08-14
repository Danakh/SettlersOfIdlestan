using System.Collections.Generic;
using System.Linq;

namespace SettlersOfIdlestan.Model.Ascension;

/// <summary>
/// Liste ordonnée des pouvoirs divins. Foi (colonne -1) est le pouvoir fondateur, toujours
/// disponible ; chaque colonne ne peut être débloquée qu'une fois Foi acquise, puis ses
/// pouvoirs se débloquent dans l'ordre de la liste au sein de cette colonne.
/// </summary>
public static class AscensionPowerDefinitions
{
    public static IReadOnlyList<AscensionPowerDefinition> All { get; } = new[]
    {
        new AscensionPowerDefinition(AscensionPowerId.Faith,
            "ascension_power_faith_name", "ascension_power_faith_desc", AscensionPowerDefinition.FoundationColumn, godPointCost: 1),

        new AscensionPowerDefinition(AscensionPowerId.HandOfGod,
            "ascension_power_handofgod_name", "ascension_power_handofgod_desc", column: 0, godPointCost: 3),

        new AscensionPowerDefinition(AscensionPowerId.DivineInventory,
            "ascension_power_divineinventory_name", "ascension_power_divineinventory_desc", column: 0, godPointCost: 5),

        new AscensionPowerDefinition(AscensionPowerId.EyeOfGod,
            "ascension_power_eyeofgod_name", "ascension_power_eyeofgod_desc", column: 1, godPointCost: 3),

        new AscensionPowerDefinition(AscensionPowerId.MemoryOfGod,
            "ascension_power_memoryofgod_name", "ascension_power_memoryofgod_desc", column: 1, godPointCost: 5),

        new AscensionPowerDefinition(AscensionPowerId.WalkOfGod,
            "ascension_power_walkofgod_name", "ascension_power_walkofgod_desc", column: 2, godPointCost: 3),

        new AscensionPowerDefinition(AscensionPowerId.PresenceOfGod,
            "ascension_power_presenceofgod_name", "ascension_power_presenceofgod_desc", column: 2, godPointCost: 5),

        new AscensionPowerDefinition(AscensionPowerId.ArmOfGod,
            "ascension_power_armofgod_name", "ascension_power_armofgod_desc", column: 3, godPointCost: 3),

        new AscensionPowerDefinition(AscensionPowerId.FistOfGod,
            "ascension_power_fistofgod_name", "ascension_power_fistofgod_desc", column: 3, godPointCost: 5),

        new AscensionPowerDefinition(AscensionPowerId.PrestigiousAscension,
            "ascension_power_prestigiousascension_name", "ascension_power_prestigiousascension_desc", column: 4, godPointCost: 3),

        new AscensionPowerDefinition(AscensionPowerId.GreaterPurification,
            "ascension_power_greaterpurification_name", "ascension_power_greaterpurification_desc", column: 4, godPointCost: 5),
    };

    /// <summary>Nombre de colonnes (hors Foi) : les colonnes vont de 0 à ColumnCount - 1.</summary>
    public static int ColumnCount { get; } = All.Max(d => d.Column) + 1;

    public static AscensionPowerDefinition? Get(AscensionPowerId id)
    {
        foreach (var def in All)
            if (def.Id == id) return def;
        return null;
    }

    /// <summary>Pouvoirs de la colonne donnée, dans leur ordre de déblocage au sein de la colonne.</summary>
    public static List<AscensionPowerDefinition> GetColumn(int column) =>
        All.Where(d => d.Column == column).ToList();
}
