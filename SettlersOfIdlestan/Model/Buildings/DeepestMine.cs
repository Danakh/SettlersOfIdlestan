using SettlersOfIdlestan.Model.IslandMap;
using System.Linq;

namespace SettlersOfIdlestan.Model.Buildings;

/// <summary>
/// [Legacy] Conservé uniquement pour la désérialisation des anciennes sauvegardes.
/// La Mine Profonde est désormais une IslandFeature (voir Model/IslandFeatures/DeepestMine.cs)
/// placée comme une Merveille et creusée par investissement progressif.
/// </summary>
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

    // Plus jamais constructible — remplacé par la feature DeepestMine.
    public override bool IsBuildingAvailableForCity(IslandMap.IslandMap map, IBuildingContext city)
    {
        return false;
    }

    public override bool HasBuildPrerequisites(IBuildingContext city)
    {
        return city.Buildings.Any(b => b.Type == BuildingType.Mine && b.Level >= 4);
    }

    public override string? GetMissingPrerequisiteKey(IBuildingContext city)
    {
        if (!HasBuildPrerequisites(city))
            return "tooltip_requires_mine_3";
        return null;
    }
}
