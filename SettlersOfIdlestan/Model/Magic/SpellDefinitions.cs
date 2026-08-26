namespace SettlersOfIdlestan.Model.Magic;

public static class SpellDefinitions
{
    public static IReadOnlyList<SpellDefinition> All { get; } = new SpellDefinition[]
    {
        new(SpellId.Abundance, crystalCost: 50, goldReward: 1000),
        new(SpellId.SummonTroops, crystalCost: 200, troopReward: 100, targetKind: SpellTargetKind.AllyCity),
        new(SpellId.ArcaneEdification, crystalCost: 2000, targetKind: SpellTargetKind.BuildableVertex),
        new(SpellId.VoidBridge, crystalCost: 2500, targetKind: SpellTargetKind.VoidVertex, costMultiplierPerCast: 6),
    };

    public static SpellDefinition? Get(SpellId id) => All.FirstOrDefault(s => s.Id == id);
}
