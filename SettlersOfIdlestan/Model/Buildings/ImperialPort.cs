using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.IslandMap;

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

    public override bool IsBuildingAvailableForCity(IslandMap.IslandMap map, City city)
    {
        return city.Level >= 4;
    }
}
