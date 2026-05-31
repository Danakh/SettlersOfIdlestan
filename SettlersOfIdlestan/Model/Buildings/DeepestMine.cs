using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.IslandMap;
using System.Linq;

namespace SettlersOfIdlestan.Model.Buildings;

public class DeepestMine : Building
{
    public DeepestMine() : base(BuildingType.DeepestMine)
    {
        AvailableAtLevel = 3;
    }

    public override bool IsUnique => true;

    public override ResourceSet GetBuildCost() => new ResourceSet
    {
        { Resource.Stone, 1000 },
        { Resource.Ore, 300 },
        { Resource.Gold, 1000 },
    };

    public override ResourceSet GetUpgradeCost(int level) => new();

    public override int GetDefaultMaxLevel() => 1;

    public override bool IsBuildingAvailableForCity(IslandMap.IslandMap map, City city)
    {
        return city.Level >= 4 && map.VertexHasTerrainType(city.Position, TerrainType.Mountain);
    }

    public override bool HasBuildPrerequisites(City city)
    {
        return city.Buildings.Any(b => b.Type == BuildingType.Mine && b.Level >= 4);
    }

    public override string? GetMissingPrerequisiteKey(City city)
    {
        if (!HasBuildPrerequisites(city))
            return "tooltip_requires_mine_3";
        return null;
    }
}
