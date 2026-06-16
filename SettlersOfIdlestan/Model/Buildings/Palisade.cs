using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestan.Model.Buildings;

public class Palisade : Building
{
    public Palisade() : base(BuildingType.Palisade)
    {
        AvailableAtLevel = 1;
    }

    public override int GetDefaultMaxLevel() => 1;

    public override int GetDefenseBonus() => GetDefenseBonusAtLevel(Level);

    public int GetDefenseBonusAtLevel(int level) => level switch
    {
        >= 3 => 30,
        2    => 20,
        _    => 10,
    };

    public override double GetDefenseRegenBonus() => GetDefenseRegenBonusAtLevel(Level);

    public double GetDefenseRegenBonusAtLevel(int level) => level switch
    {
        >= 3 => 0.4,
        2    => 0.2,
        _    => 0.0,
    };

    public override ResourceSet GetBuildCost() => new ResourceSet
    {
        { Resource.Stone, 5 },
        { Resource.Wood, 10 },
    };

    public override ResourceSet GetUpgradeCost(int level) => level switch
    {
        2 => new ResourceSet { { Resource.Brick, 50 } },
        3 => new ResourceSet { { Resource.Stone, 200 } },
        _ => new ResourceSet(),
    };
}
