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

    // Même progression que les autres bâtiments uniques à automatisation (Académie, Guilde des
    // Bâtisseurs) : ×2 le coût de construction au niveau 2, puis ×3,5, ×5,5 et ×8.
    public override ResourceSet GetUpgradeCost(int level) => level switch
    {
        2 => new ResourceSet { { Resource.Wood, 400 }, { Resource.Brick, 200 }, { Resource.Stone, 200 }, { Resource.Gold, 60 } },
        3 => new ResourceSet { { Resource.Wood, 700 }, { Resource.Brick, 350 }, { Resource.Stone, 350 }, { Resource.Gold, 100 } },
        4 => new ResourceSet { { Resource.Wood, 1100 }, { Resource.Brick, 550 }, { Resource.Stone, 550 }, { Resource.Gold, 160 } },
        5 => new ResourceSet { { Resource.Wood, 1600 }, { Resource.Brick, 800 }, { Resource.Stone, 800 }, { Resource.Gold, 240 } },
        _ => new ResourceSet()
    };

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
