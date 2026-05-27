using SettlersOfIdlestan.Model.GameplayModifier;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SettlersOfIdlestan.Model.Civilization;

public static class TechnologyDefinitions
{
    public static IReadOnlyList<Technology> All { get; } = new Technology[]
    {
        new(TechnologyId.HarvestEfficiency,
            "tech_harvest_efficiency_name", "tech_harvest_efficiency_desc",
            cost: 50,
            prerequisites: Array.Empty<TechnologyId>(),
            modifiers: new Modifier[] { new(ECategory.HARVEST_SPEED, EType.ADDITIVE, 0.1) }),

        new(TechnologyId.ImprovedHarvest,
            "tech_improved_harvest_name", "tech_improved_harvest_desc",
            cost: 120,
            prerequisites: new[] { TechnologyId.HarvestEfficiency },
            modifiers: new Modifier[] { new(ECategory.HARVEST_SPEED, EType.ADDITIVE, 0.2) }),

        new(TechnologyId.MasterHarvest,
            "tech_master_harvest_name", "tech_master_harvest_desc",
            cost: 250,
            prerequisites: new[] { TechnologyId.ImprovedHarvest },
            modifiers: new Modifier[] { new(ECategory.HARVEST_SPEED, EType.ADDITIVE, 0.3) }),

        new(TechnologyId.Architecture,
            "tech_architecture_name", "tech_architecture_desc",
            cost: 50,
            prerequisites: Array.Empty<TechnologyId>(),
            modifiers: new Modifier[] { new(ECategory.BUILDING_MAX_LEVEL, "TownHall", EType.ADDITIVE, 1) }),

        new(TechnologyId.AdvancedArchitecture,
            "tech_advanced_architecture_name", "tech_advanced_architecture_desc",
            cost: 120,
            prerequisites: new[] { TechnologyId.Architecture },
            modifiers: new Modifier[]
            {
                new(ECategory.BUILDING_MAX_LEVEL, "Sawmill",    EType.ADDITIVE, 1),
                new(ECategory.BUILDING_MAX_LEVEL, "Brickworks", EType.ADDITIVE, 1),
                new(ECategory.BUILDING_MAX_LEVEL, "Quarry",     EType.ADDITIVE, 1),
                new(ECategory.BUILDING_MAX_LEVEL, "Mill",       EType.ADDITIVE, 1),
            }),

        new(TechnologyId.StorageOptimization,
            "tech_storage_optimization_name", "tech_storage_optimization_desc",
            cost: 80,
            prerequisites: Array.Empty<TechnologyId>(),
            modifiers: new Modifier[] { new(ECategory.STORAGE_CAPACITY_BASIC, EType.ADDITIVE, 20) }),

        new(TechnologyId.Scholarship,
            "tech_scholarship_name", "tech_scholarship_desc",
            cost: 150,
            prerequisites: new[] { TechnologyId.StorageOptimization },
            modifiers: new Modifier[] { new(ECategory.RESEARCH_SPEED, EType.ADDITIVE, 0.2) }),

        new(TechnologyId.Artisanat,
            "tech_artisanat_name", "tech_artisanat_desc",
            cost: 100,
            prerequisites: Array.Empty<TechnologyId>(),
            modifiers: new Modifier[] { new(ECategory.FORGE_DOUBLE_PROD_BONUS, EType.ADDITIVE, 5) }),

        new(TechnologyId.Orpaillage,
            "tech_orpaillage_name", "tech_orpaillage_desc",
            cost: 100,
            prerequisites: new[] { TechnologyId.Artisanat },
            modifiers: new Modifier[] { new(ECategory.MINE_GOLD_CHANCE_PERCENT, EType.ADDITIVE, 10) }),

        new(TechnologyId.MilitaryDiscipline,
            "tech_military_discipline_name", "tech_military_discipline_desc",
            cost: 80,
            prerequisites: Array.Empty<TechnologyId>(),
            modifiers: new Modifier[] { new(ECategory.UNIT_PRODUCTION_SPEED, EType.ADDITIVE, 0.1) }),

        new(TechnologyId.MilitaryTactics,
            "tech_military_tactics_name", "tech_military_tactics_desc",
            cost: 180,
            prerequisites: new[] { TechnologyId.MilitaryDiscipline },
            modifiers: new Modifier[] { new(ECategory.UNIT_PRODUCTION_SPEED, EType.ADDITIVE, 0.2) }),

        new(TechnologyId.MilitaryMastery,
            "tech_military_mastery_name", "tech_military_mastery_desc",
            cost: 350,
            prerequisites: new[] { TechnologyId.MilitaryTactics },
            modifiers: new Modifier[] { new(ECategory.UNIT_PRODUCTION_SPEED, EType.ADDITIVE, 0.3) }),

        new(TechnologyId.ResearchEfficiency,
            "tech_research_efficiency_name", "tech_research_efficiency_desc",
            cost: 80,
            prerequisites: Array.Empty<TechnologyId>(),
            modifiers: new Modifier[] { new(ECategory.RESEARCH_SPEED, EType.ADDITIVE, 0.1) }),

        new(TechnologyId.ImprovedResearch,
            "tech_improved_research_name", "tech_improved_research_desc",
            cost: 180,
            prerequisites: new[] { TechnologyId.ResearchEfficiency },
            modifiers: new Modifier[] { new(ECategory.RESEARCH_SPEED, EType.ADDITIVE, 0.2) }),

        new(TechnologyId.MasterResearch,
            "tech_master_research_name", "tech_master_research_desc",
            cost: 350,
            prerequisites: new[] { TechnologyId.ImprovedResearch },
            modifiers: new Modifier[] { new(ECategory.RESEARCH_SPEED, EType.ADDITIVE, 0.3) }),
    };

    public static Technology? Get(TechnologyId id) => All.FirstOrDefault(t => t.Id == id);

    public static int GetDepth(TechnologyId id)
    {
        var tech = Get(id);
        if (tech == null || tech.Prerequisites.Count == 0) return 0;
        return tech.Prerequisites.Max(p => GetDepth(p)) + 1;
    }
}
