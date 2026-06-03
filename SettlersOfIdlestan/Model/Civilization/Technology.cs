using SettlersOfIdlestan.Model.GameplayModifier;

namespace SettlersOfIdlestan.Model.Civilization;

public enum TechnologyId
{
    // Tier 0
    HarvestEfficiency,
    Artisanat,
    Agriculture,
    Architecture,
    MilitaryDiscipline,
    // Tier 1
    ImprovedHarvest,
    StorageOptimization,
    Erudition,
    Orpaillage,
    MilitaryTactics,
    MilitaryBuildings,
    // Tier 2
    HarvestTools,
    AdvancedArchitecture,
    ResearchMethods,
    Metallurgy,
    MilitaryMastery,
    SpecializedMarket,
    // Tier 3
    MasterHarvest,
    GrandArchitecture,
    Scholarship,
    Masterwork,
    AdvancedTactics,
    EfficientTrading,
    // Tier 4
    EpicHarvest,
    TradeRoutes,
    ImprovedResearch,
    AdvancedStrategy,
    AutomaticMarket,
    // Tier 5
    MerchantGuild,
    MasterResearch,
    GloriousEmpire,
    // Tier 6
    IndustrialAge,
    Enlightenment,
    // Tier 7
    RenaissanceAge,
    // Tier 8
    GoldenEra,
    // Tier 9
    Utopia,
}

public enum TechnologyStatus
{
    Inactive,
    Available,
    InProgress,
    Completed,
}

public class Technology
{
    public TechnologyId Id { get; }
    public string NameKey { get; }
    public string DescKey { get; }
    public int Cost { get; }
    public IReadOnlyList<TechnologyId> Prerequisites { get; }
    public IReadOnlyList<Modifier> Modifiers { get; }
    public int Tier { get; }
    public int Line { get; }

    public Technology(
        TechnologyId id,
        string nameKey,
        string descKey,
        int cost,
        IReadOnlyList<TechnologyId> prerequisites,
        IReadOnlyList<Modifier> modifiers,
        int tier,
        int line)
    {
        Id = id;
        NameKey = nameKey;
        DescKey = descKey;
        Cost = cost;
        Prerequisites = prerequisites;
        Modifiers = modifiers;
        Tier = tier;
        Line = line;
    }
}
