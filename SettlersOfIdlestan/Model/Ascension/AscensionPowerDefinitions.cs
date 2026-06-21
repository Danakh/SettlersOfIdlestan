using System.Collections.Generic;

namespace SettlersOfIdlestan.Model.Ascension;

/// <summary>
/// Liste ordonnée des pouvoirs divins (ordre de la colonne = ordre de déblocage).
/// </summary>
public static class AscensionPowerDefinitions
{
    public static IReadOnlyList<AscensionPowerDefinition> All { get; } = new[]
    {
        new AscensionPowerDefinition(AscensionPowerId.HandOfGod,
            "ascension_power_handofgod_name", "ascension_power_handofgod_desc"),

        new AscensionPowerDefinition(AscensionPowerId.EyeOfGod,
            "ascension_power_eyeofgod_name", "ascension_power_eyeofgod_desc"),

        new AscensionPowerDefinition(AscensionPowerId.WalkOfGod,
            "ascension_power_walkofgod_name", "ascension_power_walkofgod_desc"),

        new AscensionPowerDefinition(AscensionPowerId.ArmOfGod,
            "ascension_power_armofgod_name", "ascension_power_armofgod_desc"),
    };

    public static AscensionPowerDefinition? Get(AscensionPowerId id)
    {
        foreach (var def in All)
            if (def.Id == id) return def;
        return null;
    }

    public static int IndexOf(AscensionPowerId id)
    {
        for (int i = 0; i < All.Count; i++)
            if (All[i].Id == id) return i;
        return -1;
    }
}
