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
            modifiers: new Modifier[] { new(ECategory.BUILDING_MAX_LEVEL, EType.ADDITIVE, 1) }),

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
    };

    public static Technology? Get(TechnologyId id) => All.FirstOrDefault(t => t.Id == id);

    public static int GetDepth(TechnologyId id)
    {
        var tech = Get(id);
        if (tech == null || tech.Prerequisites.Count == 0) return 0;
        return tech.Prerequisites.Max(p => GetDepth(p)) + 1;
    }
}
