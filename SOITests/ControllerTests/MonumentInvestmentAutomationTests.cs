using SettlersOfIdlestan.Controller.Expand;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandFeatures;
using SettlersOfIdlestan.Model.IslandMap;
using SOITests.TestUtilities;
using Xunit;

namespace SOITests.ControllerTests;

/// <summary>
/// Tests for the "Automatiser les Monuments" behavior (MonumentInvestment.TryAutoStartInvestment):
/// auto-enables investment on a Monument's resources only when the civilization can produce every
/// one of them, so a Monument never sits invested into a tier it can never complete.
/// </summary>
public class MonumentInvestmentAutomationTests
{
    private static HexCoord WonderHex => new(0, 0, IslandMap.SurfaceLayer);

    /// <summary>
    /// Seven-hex island (city vertex touches Plain/Plain/Forest) with a Sawmill (Wood), a Mill
    /// (Food) and a Market (Gold, level-independent of terrain) — but no GlassWorks/Desert, so
    /// Glass has no means of production.
    /// </summary>
    private static (WorldState state, Wonder wonder, HarvestController harvest) CreateSetup()
    {
        var state = IslandTestFactory.CreateSevenHexIslandState();
        var city = state.PlayerCivilization.Cities[0];
        city.AddBuilding(new Sawmill { Level = 1 });
        city.AddBuilding(new Mill { Level = 1 });
        city.AddBuilding(new Market { Level = 1 });

        var wonder = new Wonder(WonderHex) { Level = 0 };
        state.AddFeature(wonder);

        var clock = new GameClock();
        clock.Start();
        var harvest = new HarvestController();
        harvest.Initialize(state, clock);

        return (state, wonder, harvest);
    }

    [Fact]
    public void AutoStart_Disabled_EnablesNothing()
    {
        var (state, wonder, harvest) = CreateSetup();
        var civ = state.PlayerCivilization;
        state.AutomationSettings.MonumentInvestmentAutomationEnabled = false;

        var cost = new ResourceSet { { Resource.Food, 10 }, { Resource.Wood, 10 } };
        MonumentInvestment.TryAutoStartInvestment(wonder, cost, civ, harvest, state);

        Assert.Empty(wonder.InvestmentEnabled);
    }

    [Fact]
    public void AutoStart_AllResourcesProducible_EnablesEveryResourceInCost()
    {
        var (state, wonder, harvest) = CreateSetup();
        var civ = state.PlayerCivilization;
        state.AutomationSettings.MonumentInvestmentAutomationEnabled = true;

        var cost = new ResourceSet { { Resource.Food, 10 }, { Resource.Wood, 10 }, { Resource.Gold, 10 } };
        MonumentInvestment.TryAutoStartInvestment(wonder, cost, civ, harvest, state);

        Assert.Contains(Resource.Food, wonder.InvestmentEnabled);
        Assert.Contains(Resource.Wood, wonder.InvestmentEnabled);
        Assert.Contains(Resource.Gold, wonder.InvestmentEnabled);
    }

    [Fact]
    public void AutoStart_MissingProductionForOneResource_EnablesNothingForThatTier()
    {
        // No GlassWorks/Desert in this setup, so Glass can never be produced.
        var (state, wonder, harvest) = CreateSetup();
        var civ = state.PlayerCivilization;
        state.AutomationSettings.MonumentInvestmentAutomationEnabled = true;

        var cost = new ResourceSet { { Resource.Food, 10 }, { Resource.Wood, 10 }, { Resource.Glass, 10 } };
        MonumentInvestment.TryAutoStartInvestment(wonder, cost, civ, harvest, state);

        Assert.Empty(wonder.InvestmentEnabled);
    }

    [Fact]
    public void AutoStart_DoesNotOverrideExistingManualSelection()
    {
        var (state, wonder, harvest) = CreateSetup();
        var civ = state.PlayerCivilization;
        state.AutomationSettings.MonumentInvestmentAutomationEnabled = true;
        wonder.InvestmentEnabled.Add(Resource.Food);

        var cost = new ResourceSet { { Resource.Food, 10 }, { Resource.Wood, 10 }, { Resource.Gold, 10 } };
        MonumentInvestment.TryAutoStartInvestment(wonder, cost, civ, harvest, state);

        Assert.Single(wonder.InvestmentEnabled);
        Assert.Contains(Resource.Food, wonder.InvestmentEnabled);
    }

    /// <summary>
    /// Reproduces the DivineBones scenario: a resource already fully covered gets deselected by
    /// ProcessTick, then a cost hike (another Os Divin being purified, raising N) makes it
    /// insufficient again. With automation active, ResumeAutoInvestmentIfUnderfunded must re-enable
    /// it — otherwise it stays deselected forever and the tier can never complete.
    /// </summary>
    [Fact]
    public void ResumeIfUnderfunded_ResourceNoLongerCoveredAfterCostIncrease_ReEnablesIt()
    {
        var (state, wonder, harvest) = CreateSetup();
        var civ = state.PlayerCivilization;
        state.AutomationSettings.MonumentInvestmentAutomationEnabled = true;

        // Wood was fully funded and deselected under the old (lower) cost.
        wonder.InvestedResources[Resource.Wood] = 10;

        var raisedCost = new ResourceSet { { Resource.Wood, 20 } };
        MonumentInvestment.ResumeAutoInvestmentIfUnderfunded(wonder, raisedCost, civ, harvest, state);

        Assert.Contains(Resource.Wood, wonder.InvestmentEnabled);
    }

    [Fact]
    public void ResumeIfUnderfunded_AutomationDisabled_DoesNothing()
    {
        var (state, wonder, harvest) = CreateSetup();
        var civ = state.PlayerCivilization;
        state.AutomationSettings.MonumentInvestmentAutomationEnabled = false;
        wonder.InvestedResources[Resource.Wood] = 10;

        var raisedCost = new ResourceSet { { Resource.Wood, 20 } };
        MonumentInvestment.ResumeAutoInvestmentIfUnderfunded(wonder, raisedCost, civ, harvest, state);

        Assert.Empty(wonder.InvestmentEnabled);
    }

    [Fact]
    public void ResumeIfUnderfunded_StillFullyCovered_DoesNotReEnableIt()
    {
        var (state, wonder, harvest) = CreateSetup();
        var civ = state.PlayerCivilization;
        state.AutomationSettings.MonumentInvestmentAutomationEnabled = true;
        wonder.InvestedResources[Resource.Wood] = 10;

        var sameCost = new ResourceSet { { Resource.Wood, 10 } };
        MonumentInvestment.ResumeAutoInvestmentIfUnderfunded(wonder, sameCost, civ, harvest, state);

        Assert.Empty(wonder.InvestmentEnabled);
    }

    [Fact]
    public void ResumeIfUnderfunded_NoProductionMeans_DoesNotEnableIt()
    {
        var (state, wonder, harvest) = CreateSetup();
        var civ = state.PlayerCivilization;
        state.AutomationSettings.MonumentInvestmentAutomationEnabled = true;
        wonder.InvestedResources[Resource.Glass] = 5;

        var raisedCost = new ResourceSet { { Resource.Glass, 20 } };
        MonumentInvestment.ResumeAutoInvestmentIfUnderfunded(wonder, raisedCost, civ, harvest, state);

        Assert.Empty(wonder.InvestmentEnabled);
    }
}
