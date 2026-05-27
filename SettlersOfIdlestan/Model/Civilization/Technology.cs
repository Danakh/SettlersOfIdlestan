using SettlersOfIdlestan.Model.GameplayModifier;

namespace SettlersOfIdlestan.Model.Civilization;

public enum TechnologyId
{
    HarvestEfficiency,
    ImprovedHarvest,
    MasterHarvest,
    Architecture,
    AdvancedArchitecture,
    StorageOptimization,
    Scholarship,
    Artisanat,
    Orpaillage,
    MilitaryDiscipline,
    MilitaryTactics,
    MilitaryMastery,
    ResearchEfficiency,
    ImprovedResearch,
    MasterResearch,
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

    public Technology(
        TechnologyId id,
        string nameKey,
        string descKey,
        int cost,
        IReadOnlyList<TechnologyId> prerequisites,
        IReadOnlyList<Modifier> modifiers)
    {
        Id = id;
        NameKey = nameKey;
        DescKey = descKey;
        Cost = cost;
        Prerequisites = prerequisites;
        Modifiers = modifiers;
    }
}
