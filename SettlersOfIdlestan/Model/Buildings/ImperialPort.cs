using SettlersOfIdlestan.Model.IslandMap;
using System.Linq;

namespace SettlersOfIdlestan.Model.Buildings;

public class ImperialPort : Building
{
    public ImperialPort() : base(BuildingType.ImperialPort)
    {
        AvailableAtLevel = 4;
    }

    public override bool IsUnique => true;

    public override ResourceSet GetBuildCost() => new ResourceSet
    {
        { Resource.Wood, 200 },
        { Resource.Brick, 100 },
        { Resource.Stone, 100 },
        { Resource.Gold, 30 },
    };

    public override ResourceSet GetUpgradeCost(int level) => new();

    public override bool IsBuildingAvailableForCity(IslandMap.IslandMap map, IBuildingContext city)
    {
        return city.Level >= 4 && map.VertexHasTerrainType(city.Position, TerrainType.Water);
    }

    public override bool HasBuildPrerequisites(IBuildingContext city)
    {
        return city.Buildings.Any(b => b.Type == BuildingType.Seaport && b.Level >= 4);
    }

    public override string? GetMissingPrerequisiteKey(IBuildingContext city)
    {
        if (!HasBuildPrerequisites(city))
            return "tooltip_requires_seaport_4";
        return null;
    }
}
