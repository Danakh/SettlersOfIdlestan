using Xunit;
using SettlersOfIdlestan.Controller;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.PrestigeMap;
using SettlersOfIdlestan.Model.GameplayModifier;
using SOITests.TestUtilities;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;
using SettlersOfIdlestan.Model.Prestige.PrestigeMap;

namespace SOITests.ControllerTests;

public class PrestigeMapControllerTests
{
    private static PrestigeMapController Controller() => new();
    private static PrestigeState EmptyPrestige() => new();

    // ─── CanPurchaseVertex ───────────────────────────────────────────────────

    [Fact]
    public void CanPurchase_Central_WhenEnoughPoints()
    {
        var state = EmptyPrestige();
        state.PrestigePoints = 3;
        Assert.True(Controller().CanPurchaseVertex(state, PrestigeVertexId.Central));
    }

    [Fact]
    public void CanPurchase_Central_FailsWhenNotEnoughPoints()
    {
        var state = EmptyPrestige();
        state.PrestigePoints = 2;
        Assert.False(Controller().CanPurchaseVertex(state, PrestigeVertexId.Central));
    }

    [Fact]
    public void CanPurchase_OuterVertex_FailsWithoutPrerequisite()
    {
        var state = EmptyPrestige();
        state.PrestigePoints = 100;
        Assert.False(Controller().CanPurchaseVertex(state, PrestigeVertexId.Laboratory));
        Assert.False(Controller().CanPurchaseVertex(state, PrestigeVertexId.Barracks));
        Assert.False(Controller().CanPurchaseVertex(state, PrestigeVertexId.SeaportMarket));
    }

    [Fact]
    public void CanPurchase_OuterVertex_SucceedsAfterCentralPurchased()
    {
        var state = EmptyPrestige();
        state.PrestigePoints = 100;
        state.PurchasedVertices.Add(PrestigeVertexId.Central);
        Assert.True(Controller().CanPurchaseVertex(state, PrestigeVertexId.Laboratory));
        Assert.True(Controller().CanPurchaseVertex(state, PrestigeVertexId.Barracks));
        Assert.True(Controller().CanPurchaseVertex(state, PrestigeVertexId.SeaportMarket));
    }

    [Fact]
    public void CanPurchase_AlreadyPurchasedVertex_ReturnsFalse()
    {
        var state = EmptyPrestige();
        state.PrestigePoints = 100;
        state.PurchasedVertices.Add(PrestigeVertexId.Central);
        Assert.False(Controller().CanPurchaseVertex(state, PrestigeVertexId.Central));
    }

    // ─── PurchaseVertex ──────────────────────────────────────────────────────

    [Fact]
    public void Purchase_DeductsPointsAndAddsToList()
    {
        var state = EmptyPrestige();
        state.PrestigePoints = 10;
        bool result = Controller().PurchaseVertex(state, PrestigeVertexId.Central);
        Assert.True(result);
        Assert.Contains(PrestigeVertexId.Central, state.PurchasedVertices);
        Assert.Equal(7, state.PrestigePoints); // 10 - 3 = 7
    }

    [Fact]
    public void Purchase_FailsWhenCannotPurchase()
    {
        var state = EmptyPrestige();
        state.PrestigePoints = 0;
        bool result = Controller().PurchaseVertex(state, PrestigeVertexId.Central);
        Assert.False(result);
        Assert.Empty(state.PurchasedVertices);
    }

    [Fact]
    public void Purchase_ChainCentralThenOuter()
    {
        var state = EmptyPrestige();
        state.PrestigePoints = 8;
        var c = Controller();
        Assert.True(c.PurchaseVertex(state, PrestigeVertexId.Central));   // costs 3
        Assert.True(c.PurchaseVertex(state, PrestigeVertexId.Laboratory)); // costs 5
        Assert.Equal(0, state.PrestigePoints);
        Assert.Contains(PrestigeVertexId.Central, state.PurchasedVertices);
        Assert.Contains(PrestigeVertexId.Laboratory, state.PurchasedVertices);
    }

    // ─── ApplyPrestigeToNewGame – vertex modifiers ───────────────────────────

    [Fact]
    public void Apply_NoPurchases_NoEffect()
    {
        var island = IslandTestFactory.CreateSevenHexIslandState();
        var prestige = new PrestigeState(island);
        Controller().ApplyPrestigeToNewGame(island, prestige);
        var civ = island.PlayerCivilization;
        Assert.Empty(civ.TechnologyTree.Modifiers);
        Assert.Equal(0, civ.GetResourceQuantity(Resource.Food));
    }

    [Fact]
    public void Apply_CentralPurchased_LibraryMaxLevelPlus3()
    {
        var island = IslandTestFactory.CreateSevenHexIslandState();
        var prestige = new PrestigeState(island);
        prestige.PurchasedVertices.Add(PrestigeVertexId.Central);
        Controller().ApplyPrestigeToNewGame(island, prestige);

        var civ = island.PlayerCivilization;
        int maxLevel = civ.TechnologyTree.ApplyModifiers(ECategory.BUILDING_MAX_LEVEL, "Library", new Library().GetDefaultMaxLevel());
        Assert.Equal(4, maxLevel); // 1 (default) + 3 (prestige) = 4
    }

    [Fact]
    public void Apply_LaboratoryPurchased_LaboratoryUnlocked()
    {
        var island = IslandTestFactory.CreateSevenHexIslandState();
        var prestige = new PrestigeState(island);
        prestige.PurchasedVertices.Add(PrestigeVertexId.Central);
        prestige.PurchasedVertices.Add(PrestigeVertexId.Laboratory);
        Controller().ApplyPrestigeToNewGame(island, prestige);

        var civ = island.PlayerCivilization;
        int maxLevel = civ.TechnologyTree.ApplyModifiers(ECategory.BUILDING_MAX_LEVEL, "Laboratory", new Laboratory().GetDefaultMaxLevel());
        Assert.Equal(2, maxLevel); // 0 (locked) + 2 (prestige) = 2
    }

    [Fact]
    public void Apply_BarracksPurchased_BarracksUnlocked()
    {
        var island = IslandTestFactory.CreateSevenHexIslandState();
        var prestige = new PrestigeState(island);
        prestige.PurchasedVertices.Add(PrestigeVertexId.Central);
        prestige.PurchasedVertices.Add(PrestigeVertexId.Barracks);
        Controller().ApplyPrestigeToNewGame(island, prestige);

        var civ = island.PlayerCivilization;
        int maxLevel = civ.TechnologyTree.ApplyModifiers(ECategory.BUILDING_MAX_LEVEL, "Barracks", new Barracks().GetDefaultMaxLevel());
        Assert.Equal(2, maxLevel); // 0 + 2 = 2
    }

    // ─── ApplyPrestigeToNewGame – starting buildings ─────────────────────────

    [Fact]
    public void Apply_SeaportMarketPurchased_AddsBuildingsToStartingCity()
    {
        var island = IslandTestFactory.CreateSevenHexIslandState();
        var prestige = new PrestigeState(island);
        prestige.PurchasedVertices.Add(PrestigeVertexId.Central);
        prestige.PurchasedVertices.Add(PrestigeVertexId.SeaportMarket);
        Controller().ApplyPrestigeToNewGame(island, prestige);

        var city = island.PlayerCivilization.Cities[0];
        Assert.Contains(city.Buildings, b => b.Type == BuildingType.Seaport && b.Level == 1);
        Assert.Contains(city.Buildings, b => b.Type == BuildingType.Market && b.Level == 1);
    }

    [Fact]
    public void Apply_SeaportMarketNotPurchased_NoBuildingsAdded()
    {
        var island = IslandTestFactory.CreateSevenHexIslandState();
        var prestige = new PrestigeState(island);
        prestige.PurchasedVertices.Add(PrestigeVertexId.Central);
        Controller().ApplyPrestigeToNewGame(island, prestige);

        var city = island.PlayerCivilization.Cities[0];
        Assert.DoesNotContain(city.Buildings, b => b.Type == BuildingType.Seaport);
        Assert.DoesNotContain(city.Buildings, b => b.Type == BuildingType.Market);
    }

    // ─── ApplyPrestigeToNewGame – hex passive bonuses ────────────────────────

    [Fact]
    public void Apply_CentralPurchased_StartingResourcesHex_GivesBonus()
    {
        var island = IslandTestFactory.CreateSevenHexIslandState();
        var prestige = new PrestigeState(island);
        prestige.PurchasedVertices.Add(PrestigeVertexId.Central); // adjacent to StartingResources
        Controller().ApplyPrestigeToNewGame(island, prestige);

        var civ = island.PlayerCivilization;
        // 1 adjacent purchased vertex × +2 per vertex = +2 of each basic resource
        Assert.Equal(2, civ.GetResourceQuantity(Resource.Food));
        Assert.Equal(2, civ.GetResourceQuantity(Resource.Wood));
        Assert.Equal(2, civ.GetResourceQuantity(Resource.Brick));
        Assert.Equal(2, civ.GetResourceQuantity(Resource.Stone));
    }

    [Fact]
    public void Apply_ThreeVerticesPurchased_StartingResourcesHexScales()
    {
        var island = IslandTestFactory.CreateSevenHexIslandState();
        var prestige = new PrestigeState(island);
        // All 3 vertices adjacent to StartingResources: Central, SeaportMarket, Barracks
        prestige.PurchasedVertices.Add(PrestigeVertexId.Central);
        prestige.PurchasedVertices.Add(PrestigeVertexId.SeaportMarket);
        prestige.PurchasedVertices.Add(PrestigeVertexId.Barracks);
        Controller().ApplyPrestigeToNewGame(island, prestige);

        var civ = island.PlayerCivilization;
        // 3 adjacent vertices × +2 = +6 of each basic resource
        Assert.Equal(6, civ.GetResourceQuantity(Resource.Food));
        Assert.Equal(6, civ.GetResourceQuantity(Resource.Wood));
    }

    [Fact]
    public void Apply_CentralPurchased_HarvestSpeedHex_AddsTechTreeModifier()
    {
        var island = IslandTestFactory.CreateSevenHexIslandState();
        var prestige = new PrestigeState(island);
        prestige.PurchasedVertices.Add(PrestigeVertexId.Central); // adjacent to HarvestSpeed
        Controller().ApplyPrestigeToNewGame(island, prestige);

        var civ = island.PlayerCivilization;
        double speed = civ.TechnologyTree.ApplyModifiers(ECategory.HARVEST_SPEED, "", 1.0);
        Assert.Equal(1.1, speed, 5); // 1.0 + 0.1 for 1 adjacent vertex
    }

    [Fact]
    public void Apply_TwoVerticesAdjacentToHarvestSpeed_SpeedScales()
    {
        var island = IslandTestFactory.CreateSevenHexIslandState();
        var prestige = new PrestigeState(island);
        // Central + SeaportMarket are both adjacent to HarvestSpeed
        prestige.PurchasedVertices.Add(PrestigeVertexId.Central);
        prestige.PurchasedVertices.Add(PrestigeVertexId.SeaportMarket);
        Controller().ApplyPrestigeToNewGame(island, prestige);

        var civ = island.PlayerCivilization;
        double speed = civ.TechnologyTree.ApplyModifiers(ECategory.HARVEST_SPEED, "", 1.0);
        Assert.Equal(1.2, speed, 5); // 1.0 + 0.1 + 0.1
    }

    [Fact]
    public void Apply_ResearchSpeedHex_AddsResearchSpeedModifier()
    {
        var island = IslandTestFactory.CreateSevenHexIslandState();
        var prestige = new PrestigeState(island);
        prestige.PurchasedVertices.Add(PrestigeVertexId.Central); // adjacent to ResearchSpeed
        Controller().ApplyPrestigeToNewGame(island, prestige);

        var civ = island.PlayerCivilization;
        Assert.Equal(1.1, civ.ResearchSpeed, 5);
    }
}
