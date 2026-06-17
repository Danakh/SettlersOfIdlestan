namespace SettlersOfIdlestan.Model.Magic;

public static class SpellDefinitions
{
    public static IReadOnlyList<SpellDefinition> All { get; } = new SpellDefinition[]
    {
        new(SpellId.Abundance, crystalCost: 10, goldReward: 1000),
        new(SpellId.SummonTroops, crystalCost: 100, troopReward: 100, targetKind: SpellTargetKind.AllyCity),
        new(SpellId.ArcaneEdification, crystalCost: 500, targetKind: SpellTargetKind.BuildableVertex),
    };

    public static SpellDefinition? Get(SpellId id) => All.FirstOrDefault(s => s.Id == id);
}
