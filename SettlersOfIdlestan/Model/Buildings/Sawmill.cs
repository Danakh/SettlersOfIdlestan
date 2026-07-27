using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestan.Model.Buildings;

/// <summary>
/// Represents a Sawmill building.
/// </summary>
public class Sawmill : Building
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Sawmill"/> class.
    /// </summary>
    public Sawmill() : base(BuildingType.Sawmill)
    {
        AvailableAtLevel = 1;
    }

    public override int GetDefaultMaxLevel()
    {
        return 4;
    }

    public override Resource? ManualHarvestResource => null;
    public override Resource? AutomaticHarvestResource => Resource.Wood;

    public override int AutomaticHarvestUnlockLevel => 1;

    public override Resource? AutomaticHarvestCapability(TerrainType terrain)
    {
        if (terrain == TerrainType.Forest)
            return Resource.Wood;
        return null;
    }

    /// <summary>
    /// Sur les Cavernes aux Champignons, la récolte de Bois n'est possible qu'une fois la recherche
    /// Bois de Champignon complétée (voir Modifier.ECategory.UNLOCK_SAWMILL_MUSHROOM_HARVEST).
    /// </summary>
    public override Resource? AutomaticHarvestCapability(TerrainType terrain, SettlersOfIdlestan.Model.Civilization.Civilization civ)
    {
        if (terrain == TerrainType.MushroomCave && civ.ModifierAggregator.HasModifier(Modifier.ECategory.UNLOCK_SAWMILL_MUSHROOM_HARVEST))
            return Resource.Wood;
        return AutomaticHarvestCapability(terrain);
    }

    /// <summary>À moitié vitesse sur les Cavernes aux Champignons par rapport aux forêts (voir tech Bois de Champignon).</summary>
    public override double GetAutomaticHarvestTerrainSpeedMultiplier(TerrainType terrain)
        => terrain == TerrainType.MushroomCave ? 0.5 : 1.0;

    public override ResourceSet GetBuildCost() => new ResourceSet
    {
        { Resource.Wood, 10 },
        { Resource.Brick, 5 }
    };

    public override ResourceSet GetUpgradeCost(int level) => new ResourceSet
    {
        { Resource.Wood, 10 * (level + 1) },
        { Resource.Brick, 5 * (level + 1) }
    };

    public override bool IsBuildingAvailableForCity(IslandMap.IslandMap map, IBuildingContext city)
    {
        if (!base.IsBuildingAvailableForCity(map, city))
            return false;
        return map.VertexHasTerrainType(city.Position, TerrainType.Forest);
    }

    /// <summary>
    /// Aussi constructible à côté d'une Caverne aux Champignons une fois la recherche Bois de Champignon
    /// complétée (voir Modifier.ECategory.UNLOCK_SAWMILL_MUSHROOM_HARVEST).
    /// </summary>
    public override bool IsBuildingAvailableForCity(IslandMap.IslandMap map, IBuildingContext city, SettlersOfIdlestan.Model.Civilization.Civilization civ)
    {
        if (IsBuildingAvailableForCity(map, city))
            return true;
        if (city.Level < AvailableAtLevel)
            return false;
        return civ.ModifierAggregator.HasModifier(Modifier.ECategory.UNLOCK_SAWMILL_MUSHROOM_HARVEST)
            && map.VertexHasTerrainType(city.Position, TerrainType.MushroomCave);
    }
}