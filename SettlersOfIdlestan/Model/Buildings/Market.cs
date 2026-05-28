using System.Collections.Generic;
using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.IslandMap;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SettlersOfIdlestan.Model.Buildings;

public class Market : Building
{
    public long LastGenerationTick { get; set; } = 0;

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
        4 => new ResourceSet { { Resource.Food, 200 }, { Resource.Wood, 120 }, { Resource.Gold, 80 } },
        _ => new ResourceSet()
    };

    // Chaque niveau réduit le temps de génération de 20% (multiplicatif).
    public double BaseProductionCooldownMutiplier()
    {
        return (Level > 1) ? Math.Pow(0.8, Level - 1) : 1.0;
    }
}