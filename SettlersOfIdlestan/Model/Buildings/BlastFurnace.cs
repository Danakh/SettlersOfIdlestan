using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.IslandMap;
using System.Linq;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SettlersOfIdlestan.Model.Buildings;

public class BlastFurnace : Building, IUniqueBuilding
{
    /// <summary>Acier supplémentaire produit par cycle par chaque Fonderie de la civilisation.</summary>
    public const int BonusSteelPerSmelterCycle = 1;

    public BlastFurnace() : base(BuildingType.BlastFurnace)
    {
        AvailableAtLevel = 4;
    }

    public override bool IsUnique => true;

    // Verrouillé par défaut ; débloqué par le vertex de prestige Hauts-Fourneaux (+1 niveau max)
    public override int GetDefaultMaxLevel() => 0;

    public override ResourceSet GetBuildCost() => new ResourceSet
    {
        { Resource.Stone, 800 },
        { Resource.Brick, 400 },
        { Resource.Ore,   200 },
        { Resource.Gold,   50 },
    };

    public override ResourceSet GetUpgradeCost(int level) => new();

    public override bool IsBuildingAvailableForCity(IslandMap.IslandMap map, IBuildingContext city)
    {
        return city.Level >= 4 && map.VertexHasTerrainType(city.Position, TerrainType.Mountain);
    }

    public override bool HasBuildPrerequisites(IBuildingContext city)
    {
        return city.Buildings.Any(b => b.Type == BuildingType.Smelter && b.Level >= 5);
    }

    public override string? GetMissingPrerequisiteKey(IBuildingContext city)
    {
        if (!HasBuildPrerequisites(city))
            return "tooltip_requires_smelter_5";
        return null;
    }

    public IEnumerable<Modifier> GetUniqueBuildingModifiers()
    {
        if (Level <= 0) yield break;
        yield return new Modifier(ECategory.BUILDING_PRODUCTION, "Smelter", EType.ADDITIVE, BonusSteelPerSmelterCycle);
    }
}
