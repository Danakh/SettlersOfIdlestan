using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.IslandMap;
using SOITests.TestUtilities;
using System.Linq;
using Xunit;

namespace SOITests.ControllerTests;

public class TraderGuildTests
{
    private static (WorldState state, BuildingController controller, Civilization civ) CreateSetup()
    {
        var state = IslandTestFactory.CreateSevenHexIslandState();
        var civ = state.Civilizations[0];
        var city = civ.Cities[0];
        city.Buildings.Add(new TownHall { Level = 1 }); // city.Level = 1 → Market available

        var controller = new BuildingController(state);
        return (state, controller, civ);
    }

    // ── Max level ────────────────────────────────────────────────────────────

    [Fact]
    public void TraderGuild_IncreasesMarketMaxLevelByTwo()
    {
        var (state, controller, civ) = CreateSetup();
        var city = civ.Cities[0];

        var market = new Market { Level = 1 };
        city.Buildings.Add(market);

        Assert.Equal(1, controller.GetMaxLevel(market, 0));

        city.Buildings.Add(new TraderGuild { Level = 1 });
        civ.RebuildUniqueBuildingsModifiers();

        Assert.Equal(3, controller.GetMaxLevel(market, 0));
    }

    [Fact]
    public void TraderGuild_WithoutPrestige_HasMaxLevelZero()
    {
        var (state, controller, civ) = CreateSetup();
        var prototype = BuildingController.CreateBuilding(BuildingType.TraderGuild)!;

        // Without any modifier: default max level is 0 → can't be built
        Assert.Equal(0, controller.GetMaxLevel(prototype, 0));
    }

    // ── Automation: first build ───────────────────────────────────────────────

    [Fact]
    public void TraderGuild_AutomationBuildsMarketInCity()
    {
        var (state, controller, civ) = CreateSetup();
        var city = civ.Cities[0];

        city.Buildings.Add(new TraderGuild { Level = 1 });

        civ.AddResource(Resource.Food, 5);
        civ.AddResource(Resource.Wood, 5);
        civ.AddResource(Resource.Brick, 5);

        state.AutomationSettings.MarketBuildingAutomationEnabled = true;

        var clock = new GameClock();
        clock.Start();
        controller.Initialize(state, clock);

        // First advance: sets LastMarketBuildTick (first-fire guard)
        clock.SimulateAdvance(10);
        Assert.DoesNotContain(city.Buildings, b => b.Type == BuildingType.Market);

        // Past cooldown: automation builds the Market
        clock.SimulateAdvance(1000);
        Assert.Contains(city.Buildings, b => b.Type == BuildingType.Market && b.Level == 1);
    }

    [Fact]
    public void TraderGuild_AutomationDisabled_DoesNotBuildMarket()
    {
        var (state, controller, civ) = CreateSetup();
        var city = civ.Cities[0];

        city.Buildings.Add(new TraderGuild { Level = 1 });

        civ.AddResource(Resource.Food, 50);
        civ.AddResource(Resource.Wood, 50);
        civ.AddResource(Resource.Brick, 50);

        state.AutomationSettings.MarketBuildingAutomationEnabled = false;

        var clock = new GameClock();
        clock.Start();
        controller.Initialize(state, clock);

        clock.SimulateAdvance(10);
        clock.SimulateAdvance(2000);

        Assert.DoesNotContain(city.Buildings, b => b.Type == BuildingType.Market);
    }

    // ── Automation: upgrade ──────────────────────────────────────────────────

    [Fact]
    public void TraderGuild_AutomationUpgradesMarketToLevel2()
    {
        var (state, controller, civ) = CreateSetup();
        var city = civ.Cities[0];

        // Bump TownHall level so storage fits upgrade cost (Food 100, Wood 40, Brick 40, Gold 40):
        // Basic max = 10 (1 city) + 5×20 = 110
        city.Buildings.First(b => b.Type == BuildingType.TownHall).Level = 20;
        BuildingController.RecalculateStorageCapacity(civ);

        city.Buildings.Add(new TraderGuild { Level = 1 });
        civ.RebuildUniqueBuildingsModifiers();
        var market = new Market { Level = 1 };
        city.Buildings.Add(market);

        // Upgrade level 1→2 cost: Food 100, Wood 40, Brick 40, Gold 40 (GetUpgradeCost(2) = 50*2/20*2/20*2/20*2)
        civ.AddResource(Resource.Food, 100);
        civ.AddResource(Resource.Wood, 40);
        civ.AddResource(Resource.Brick, 40);
        civ.AddResource(Resource.Gold, 40);

        state.AutomationSettings.MarketBuildingAutomationEnabled = true;

        var clock = new GameClock();
        clock.Start();
        controller.Initialize(state, clock);

        clock.SimulateAdvance(10);    // guard
        clock.SimulateAdvance(1000);  // upgrade fires

        Assert.Equal(2, market.Level);
    }

    [Fact]
    public void TraderGuild_AutomationCannotUpgradeMarketBeyondLevel3()
    {
        var (state, controller, civ) = CreateSetup();
        var city = civ.Cities[0];

        city.Buildings.Add(new TraderGuild { Level = 1 });
        var market = new Market { Level = 3 }; // already at max
        city.Buildings.Add(market);

        civ.AddResource(Resource.Food, 500);
        civ.AddResource(Resource.Wood, 500);
        civ.AddResource(Resource.Gold, 500);

        state.AutomationSettings.MarketBuildingAutomationEnabled = true;

        var clock = new GameClock();
        clock.Start();
        controller.Initialize(state, clock);

        clock.SimulateAdvance(10);
        clock.SimulateAdvance(1000);

        // Still at 3: max level is 1 (default) + 2 (TraderGuild) = 3
        Assert.Equal(3, market.Level);
    }

    // ── Seaport random resource generation ───────────────────────────────────

    [Fact]
    public void Seaport_Level3_HasBaseCooldown()
    {
        var seaport = new Seaport { Level = 3 };

        long cooldown = HarvestController.GetEffectiveSeaportGenerationCooldown(seaport);

        Assert.Equal(HarvestController.SeaportGenerationCooldownTicks, cooldown);
    }

    [Fact]
    public void Seaport_Level4_ReducesCooldownBy20Percent()
    {
        var seaport = new Seaport { Level = 4 };

        long cooldown = HarvestController.GetEffectiveSeaportGenerationCooldown(seaport);

        // Level 4: multiplier = 0.8^(4-3) = 0.8 → 1000 * 0.8 = 800
        Assert.Equal(800L, cooldown);
    }
}
