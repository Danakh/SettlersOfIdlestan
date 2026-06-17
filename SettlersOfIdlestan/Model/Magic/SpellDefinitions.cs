namespace SettlersOfIdlestan.Model.Magic;

public static class SpellDefinitions
{
    public static IReadOnlyList<SpellDefinition> All { get; } = new SpellDefinition[]
    {
        new(SpellId.Abundance, crystalCost: 10, goldReward: 1000),
    };

    public static SpellDefinition? Get(SpellId id) => All.FirstOrDefault(s => s.Id == id);
}
