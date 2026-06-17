using System.Collections.Generic;
using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.IslandMap;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SettlersOfIdlestan.Model.Buildings;

public class Market : Building
{
    public long LastGoldGenerationTick { get; set; } = 0;

    public Market() : base(BuildingType.Market)
    {
        AvailableAtLevel = 1;
    }

    public override ResourceSet GetBuildCost() => new ResourceSet
    {
        { Resource.Food, 5 },
        { Resource.Wood, 5 },
        { Resource.Brick, 5 }
    };

    public override ResourceSet GetUpgradeCost(int level) => new ResourceSet
    {
        { Resource.Food, 50 * level },
        { Resource.Wood, 20 * level },
        { Resource.Brick, 20 * level },
        { Resource.Gold, 20 * level }
    };
}