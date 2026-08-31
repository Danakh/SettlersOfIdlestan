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

    public override bool ProvidesAutomation => true;

    public long LastSeaportBuildTick { get; set; }
    public long GetAutoSeaportCooldownTicks() => 1000L;

    public override ResourceSet GetUpgradeCost(int level) => new();

    public override bool IsBuildingAvailableForCity(IslandMap.IslandMap map, IBuildingContext city, Model.Civilization.Civilization? civ)
    {
        return city.Level >= 4 && map.VertexHasTerrainType(city.Position, TerrainType.Water);
    }

    public override bool HasBuildPrerequisites(IBuildingContext city, WorldState? state)
    {
        return city.HasBuildingAtLevel(BuildingType.Seaport, 4);
    }

    public override string? GetMissingPrerequisiteKey(IBuildingContext city, WorldState? state)
    {
        if (!HasBuildPrerequisites(city, state))
            return "tooltip_requires_seaport_4";
        return null;
    }
}
