using System.Collections.Generic;
using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.IslandMap;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SettlersOfIdlestan.Model.Buildings;

public class Market : Building
{
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

    public override ResourceSet GetUpgradeCost(int level) => level switch
    {
        2 => new ResourceSet { { Resource.Food, 50 }, { Resource.Wood, 30 }, { Resource.Gold, 20 } },
        3 => new ResourceSet { { Resource.Food, 100 }, { Resource.Wood, 60 }, { Resource.Gold, 40 } },
        _ => new ResourceSet()
    };
}