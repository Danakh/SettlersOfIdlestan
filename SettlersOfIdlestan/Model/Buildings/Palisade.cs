using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestan.Model.Buildings;

public class Palisade : Building
{
    public Palisade() : base(BuildingType.Palisade)
    {
        AvailableAtLevel = 1;
    }

    public override int GetDefaultMaxLevel() => 1;

    public override ResourceSet GetBuildCost() => new ResourceSet
    {
        { Resource.Stone, 5 },
        { Resource.Wood, 10 },
    };
}
