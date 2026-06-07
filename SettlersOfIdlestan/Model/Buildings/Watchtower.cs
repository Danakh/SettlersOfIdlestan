using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestan.Model.Buildings;

public class Watchtower : Building
{
    public Watchtower() : base(BuildingType.Watchtower)
    {
        AvailableAtLevel = 1;
    }

    public override int GetDefaultMaxLevel() => 0;

    public override ResourceSet GetBuildCost() => new ResourceSet
    {
        { Resource.Stone, 20 },
        { Resource.Wood, 10 },
    };

    public override bool IsBuildingAvailableForCity(IslandMap.IslandMap map, IBuildingContext city)
    {
        return (map.Z == IslandMap.IslandMap.SurfaceLayer) && base.IsBuildingAvailableForCity(map, city);
    }
}
