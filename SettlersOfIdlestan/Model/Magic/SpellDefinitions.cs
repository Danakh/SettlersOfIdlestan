namespace SettlersOfIdlestan.Model.Magic;

public static class SpellDefinitions
{
    public static IReadOnlyList<SpellDefinition> All { get; } = new SpellDefinition[]
    {
        new(SpellId.Abundance, crystalCost: 50, goldReward: 1000, costMultiplierPerCast: 2, cooldownTicks: 6000),
        new(SpellId.SummonTroops, crystalCost: 200, troopReward: 100, targetKind: SpellTargetKind.AllyCity, costMultiplierPerCast: 2, cooldownTicks: 30000),
        new(SpellId.ArcaneEdification, crystalCost: 2000, targetKind: SpellTargetKind.BuildableVertex, costMultiplierPerCast: 2, cooldownTicks: 720000),
        new(SpellId.VoidBridge, crystalCost: 2500, targetKind: SpellTargetKind.VoidVertex, costMultiplierPerCast: 2, cooldownTicks: 8640000),
    };

    public static SpellDefinition? Get(SpellId id) => All.FirstOrDefault(s => s.Id == id);
}
