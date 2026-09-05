using System.Collections.Generic;

namespace SettlersOfIdlestan.Model.Ascension;

/// <summary>
/// Définition d'un jalon d'Ascension : nombre de races différentes ayant déjà accompli une Ascension
/// (AscensionState.AscendedRaces, voir AscensionController.AscendedRaces) requis pour le débloquer —
/// voir <see cref="AscensionController.IsMilestoneUnlocked"/>. Contrairement aux pouvoirs divins
/// achetables (AscensionPowerDefinitions), aucun coût en points divins : le jalon se débloque tout
/// seul dès que le seuil est atteint, la première Ascension accomplie faisant en outre foi pour le
/// premier jalon (RequiredAscendedRaceCount 0).
/// </summary>
public sealed record AscensionMilestoneDefinition(
    AscensionMilestoneId Id,
    string NameKey,
    string DescKey,
    string RequirementKey,
    int RequiredAscendedRaceCount);

public static class AscensionMilestoneDefinitions
{
    public static IReadOnlyList<AscensionMilestoneDefinition> All { get; } = new[]
    {
        new AscensionMilestoneDefinition(AscensionMilestoneId.PermanentUniqueBuildings,
            "ascension_milestone_buildings_name", "ascension_milestone_buildings_desc", "ascension_milestone_buildings_requirement",
            RequiredAscendedRaceCount: 0),

        new AscensionMilestoneDefinition(AscensionMilestoneId.PrestigiousAscension,
            "ascension_milestone_prestige_name", "ascension_milestone_prestige_desc", "ascension_milestone_prestige_requirement",
            RequiredAscendedRaceCount: 1),

        new AscensionMilestoneDefinition(AscensionMilestoneId.ResearchProduction,
            "ascension_milestone_research_name", "ascension_milestone_research_desc", "ascension_milestone_research_requirement",
            RequiredAscendedRaceCount: 2),

        new AscensionMilestoneDefinition(AscensionMilestoneId.FreeRelocation,
            "ascension_milestone_relocation_name", "ascension_milestone_relocation_desc", "ascension_milestone_relocation_requirement",
            RequiredAscendedRaceCount: 3),
    };

    public static AscensionMilestoneDefinition? Get(AscensionMilestoneId id)
    {
        foreach (var def in All)
            if (def.Id == id) return def;
        return null;
    }
}
