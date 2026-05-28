using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.IslandMap;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SettlersOfIdlestan.Model.Buildings;

public class TraderGuild : Building
{
    public long LastMarketBuildTick { get; set; }

    public TraderGuild() : base(BuildingType.TraderGuild)
    {
        AvailableAtLevel = 4;
    }

    public override bool IsUnique => true;
    public override bool ProvidesAutomation => true;
    public override int GetDefaultMaxLevel() => 0;

    public long GetAutoMarketCooldownTicks() => 1000L;

    public override bool HasBuildPrerequisites(City city)
    {
        bool hasMarket = city.Buildings.Any(b => b.Type == BuildingType.Market && b.Level >= 1);
        bool hasSeaport4 = city.Buildings.Any(b => b.Type == BuildingType.Seaport && b.Level >= 4);
        return hasMarket && hasSeaport4;
    }

    public override string? GetMissingPrerequisiteKey(City city)
    {
        if (!city.Buildings.Any(b => b.Type == BuildingType.Market && b.Level >= 1))
            return "tooltip_requires_market";
        if (!city.Buildings.Any(b => b.Type == BuildingType.Seaport && b.Level >= 4))
            return "tooltip_requires_seaport_4";
        return null;
    }

    public override ResourceSet GetBuildCost() => new ResourceSet
    {
        { Resource.Wood, 150 },
        { Resource.Brick, 100 },
        { Resource.Stone, 75 },
        { Resource.Gold, 50 }
    };

    public override ResourceSet GetUpgradeCost(int level) => new ResourceSet();

    public IEnumerable<Modifier> GetModifiers()
    {
        if (Level <= 0) yield break;
        yield return new Modifier(ECategory.BUILDING_MAX_LEVEL, "Market", EType.ADDITIVE, 3);
    }
}
