using Xunit;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Prestige;
using SettlersOfIdlestan.Model.GameplayModifier;
using SOITests.TestUtilities;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;
using SettlersOfIdlestan.Model.Prestige.PrestigeMap;
using SettlersOfIdlestan.Controller.Expand;
using System;

namespace SOITests.ControllerTests;

public class PrestigeMapControllerTests
{
    private static PrestigeMapController Controller() => new();
    private static PrestigeState EmptyPrestige() => new();

    private static void WireAggregator(WorldState island, PrestigeState prestige)
    {
        var civ = island.PlayerCivilization;
        civ.TechnologyTree = prestige.TechnologyTree;
        civ.AddCustomAggregator(new PrestigeModifierProvider(prestige, PrestigeMapController.DefaultMap));
    }

    // ─── CanPurchaseVertex ───────────────────────────────────────────────────

    [Fact]
    public void CanPurchase_Central_WhenEnoughPoints()
    {
        var state = EmptyPrestige();
        state.PrestigePoints = 10;
        Assert.True(Controller().CanPurchaseVertex(state, PrestigeMap.CentralVertex));
    }

    [Fact]
    public void CanPurchase_Central_FailsWhenNotEnoughPoints()
    {
        var state = EmptyPrestige();
        state.PrestigePoints = 9;
        Assert.False(Controller().CanPurchaseVertex(state, PrestigeMap.CentralVertex));
    }

    [Fact]
    public void CanPurchase_OuterVertex_FailsWithoutPurchasedNeighbor()
    {
        var state = EmptyPrestige();
        state.PrestigePoints = 100;
        Assert.False(Controller().CanPurchaseVertex(state, PrestigeMap.LaboratoryVertex));
        Assert.False(Controller().CanPurchaseVertex(state, PrestigeMap.BarracksVertex));
        Assert.False(Controller().CanPurchaseVertex(state, PrestigeMap.SeaportMarketVertex));
    }

    [Fact]
    public void CanPurchase_OuterVertex_SucceedsAfterNeighborPurchased()
    {
        var state = EmptyPrestige();
        state.PrestigePoints = 100;
        state.PurchasedVertices.Add(PrestigeMap.CentralVertex);
        Assert.True(Controller().CanPurchaseVertex(state, PrestigeMap.LaboratoryVertex));
        Assert.True(Controller().CanPurchaseVertex(state, PrestigeMap.BarracksVertex));
        Assert.True(Controller().CanPurchaseVertex(state, PrestigeMap.SeaportMarketVertex));
    }

    [Fact]
    public void CanPurchase_AlreadyPurchasedVertex_ReturnsFalse()
    {
        var state = EmptyPrestige();
        state.PrestigePoints = 100;
        state.PurchasedVertices.Add(PrestigeMap.CentralVertex);
        Assert.False(Controller().CanPurchaseVertex(state, PrestigeMap.CentralVertex));
    }

    [Fact]
    public void CanPurchase_SecondRingVertex_SucceedsAfterFirstRingNeighborPurchased()
    {
        var state = EmptyPrestige();
        state.PrestigePoints = 100;
        state.PurchasedVertices.Add(PrestigeMap.SeaportMarketVertex);
        Assert.True(Controller().CanPurchaseVertex(state, PrestigeMap.HarvestGuildVertex));
    }

    // ─── PurchaseVertex ──────────────────────────────────────────────────────

    [Fact]
    public void Purchase_DeductsPointsAndAddsToList()
    {
        var state = EmptyPrestige();
        state.PrestigePoints = 10;
        bool result = Controller().PurchaseVertex(state, PrestigeMap.CentralVertex);
        Assert.True(result);
        Assert.Contains(PrestigeMap.CentralVertex, state.PurchasedVertices);
        Assert.Equal(0, state.PrestigePoints); // 10 - 10 = 0
    }

    [Fact]
    public void Purchase_FailsWhenCannotPurchase()
    {
        var state = EmptyPrestige();
        state.PrestigePoints = 0;
        bool result = Controller().PurchaseVertex(state, PrestigeMap.CentralVertex);
        Assert.False(result);
        Assert.Empty(state.PurchasedVertices);
    }

    [Fact]
    public void Purchase_ChainCentralThenOuter()
    {
        var state = EmptyPrestige();
        state.PrestigePoints = 35; // Central=10, Laboratory=25
        var c = Controller();
        Assert.True(c.PurchaseVertex(state, PrestigeMap.CentralVertex));    // costs 10
        Assert.True(c.PurchaseVertex(state, PrestigeMap.LaboratoryVertex)); // costs 25
        Assert.Equal(0, state.PrestigePoints);
        Assert.Contains(PrestigeMap.CentralVertex, state.PurchasedVertices);
        Assert.Contains(PrestigeMap.LaboratoryVertex, state.PurchasedVertices);
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
        prestige.PurchasedVertices.Add(PrestigeMap.CentralVertex);
        Controller().ApplyPrestigeToNewGame(island, prestige);
        WireAggregator(island, prestige);

        var civ = island.PlayerCivilization;
        int maxLevel = civ.ModifierAggregator.ApplyModifiers(ECategory.BUILDING_MAX_LEVEL, "Library", new Library().GetDefaultMaxLevel());
        Assert.Equal(3, maxLevel); // 0 (default) + 3 (prestige) = 3
    }

    [Fact]
    public void Apply_LaboratoryPurchased_LaboratoryUnlocked()
    {
        var island = IslandTestFactory.CreateSevenHexIslandState();
        var prestige = new PrestigeState(island);
        prestige.PurchasedVertices.Add(PrestigeMap.CentralVertex);
        prestige.PurchasedVertices.Add(PrestigeMap.LaboratoryVertex);
        Controller().ApplyPrestigeToNewGame(island, prestige);
        WireAggregator(island, prestige);

        var civ = island.PlayerCivilization;
        int maxLevel = civ.ModifierAggregator.ApplyModifiers(ECategory.BUILDING_MAX_LEVEL, "Laboratory", new Laboratory().GetDefaultMaxLevel());
        Assert.Equal(2, maxLevel); // 0 (locked) + 2 (prestige) = 2
    }

    [Fact]
    public void Apply_BarracksPurchased_BarracksUnlocked()
    {
        var island = IslandTestFactory.CreateSevenHexIslandState();
        var prestige = new PrestigeState(island);
        prestige.PurchasedVertices.Add(PrestigeMap.CentralVertex);
        prestige.PurchasedVertices.Add(PrestigeMap.BarracksVertex);
        Controller().ApplyPrestigeToNewGame(island, prestige);
        WireAggregator(island, prestige);

        var civ = island.PlayerCivilization;
        int maxLevel = civ.ModifierAggregator.ApplyModifiers(ECategory.BUILDING_MAX_LEVEL, "Barracks", new Barracks().GetDefaultMaxLevel());
        Assert.Equal(2, maxLevel); // 0 + 2 = 2
    }

    // ─── ApplyPrestigeToNewGame – starting buildings ─────────────────────────

    [Fact]
    public void Apply_SeaportMarketPurchased_AddsBuildingsToStartingCity()
    {
        var island = IslandTestFactory.CreateSevenHexIslandState();
        var prestige = new PrestigeState(island);
        prestige.PurchasedVertices.Add(PrestigeMap.CentralVertex);
        prestige.PurchasedVertices.Add(PrestigeMap.SeaportMarketVertex);
        WireAggregator(island, prestige);
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
        prestige.PurchasedVertices.Add(PrestigeMap.CentralVertex);
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
        prestige.PurchasedVertices.Add(PrestigeMap.CentralVertex); // adjacent to StartingResources
        Controller().ApplyPrestigeToNewGame(island, prestige);

        var civ = island.PlayerCivilization;
        Assert.Equal(5, civ.GetResourceQuantity(Resource.Food));
        Assert.Equal(5, civ.GetResourceQuantity(Resource.Wood));
        Assert.Equal(5, civ.GetResourceQuantity(Resource.Brick));
        Assert.Equal(5, civ.GetResourceQuantity(Resource.Stone));
    }

    [Fact]
    public void Apply_ThreeVerticesPurchased_StartingResourcesHexScales()
    {
        var island = IslandTestFactory.CreateSevenHexIslandState();
        var prestige = new PrestigeState(island);
        // All 3 vertices adjacent to StartingResources: Central, SeaportMarket, Barracks
        prestige.PurchasedVertices.Add(PrestigeMap.CentralVertex);
        prestige.PurchasedVertices.Add(PrestigeMap.SeaportMarketVertex);
        prestige.PurchasedVertices.Add(PrestigeMap.BarracksVertex);
        Controller().ApplyPrestigeToNewGame(island, prestige);

        var civ = island.PlayerCivilization;
        int targetValue = Math.Min(15, civ.GetResourceMaxQuantity(Resource.Food));
        Assert.Equal(targetValue, civ.GetResourceQuantity(Resource.Food));
        Assert.Equal(targetValue, civ.GetResourceQuantity(Resource.Wood));
    }

    [Fact]
    public void Apply_CentralPurchased_HarvestSpeedHex_AddsHarvestSpeedModifier()
    {
        var island = IslandTestFactory.CreateSevenHexIslandState();
        var prestige = new PrestigeState(island);
        prestige.PurchasedVertices.Add(PrestigeMap.CentralVertex); // adjacent to HarvestSpeed
        Controller().ApplyPrestigeToNewGame(island, prestige);
        WireAggregator(island, prestige);

        var civ = island.PlayerCivilization;
        double speed = civ.ModifierAggregator.ApplyModifiers(ECategory.HARVEST_SPEED, "", 1.0);
        Assert.Equal(1.1, speed, 5);
    }

    [Fact]
    public void Apply_TwoVerticesAdjacentToHarvestSpeed_SpeedScales()
    {
        var island = IslandTestFactory.CreateSevenHexIslandState();
        var prestige = new PrestigeState(island);
        // Central + SeaportMarket are both adjacent to HarvestSpeed
        prestige.PurchasedVertices.Add(PrestigeMap.CentralVertex);
        prestige.PurchasedVertices.Add(PrestigeMap.SeaportMarketVertex);
        Controller().ApplyPrestigeToNewGame(island, prestige);
        WireAggregator(island, prestige);

        var civ = island.PlayerCivilization;
        double speed = civ.ModifierAggregator.ApplyModifiers(ECategory.HARVEST_SPEED, "", 1.0);
        Assert.Equal(1.2, speed, 5);
    }

    [Fact]
    public void Apply_ResearchSpeedHex_AddsResearchProductionSpeedModifier()
    {
        var island = IslandTestFactory.CreateSevenHexIslandState();
        var prestige = new PrestigeState(island);
        prestige.PurchasedVertices.Add(PrestigeMap.CentralVertex); // adjacent to ResearchSpeed
        Controller().ApplyPrestigeToNewGame(island, prestige);
        WireAggregator(island, prestige);

        var civ = island.PlayerCivilization;
        Assert.Equal(1.1, civ.ResearchProductionSpeed, 5);
    }

    // ─── MaritimeRoutesVertex ────────────────────────────────────────────────

    [Fact]
    public void MaritimeRoutesVertex_ExistsInDefaultMap()
    {
        Assert.NotNull(PrestigeMapController.DefaultMap.GetVertex(PrestigeMap.MaritimeRoutesVertex));
    }

    [Fact]
    public void MaritimeRoutesVertex_HasUnlockMaritimeRoutesModifier()
    {
        var vertex = PrestigeMapController.DefaultMap.GetVertex(PrestigeMap.MaritimeRoutesVertex)!;
        Assert.Contains(vertex.Modifiers, m => m.Category == ECategory.UNLOCK_MARITIME_ROUTES);
    }

    [Fact]
    public void MaritimeRoutesVertex_GrantsModifierWhenPurchased()
    {
        var island   = IslandTestFactory.CreateSevenHexIslandState();
        var prestige = EmptyPrestige();
        prestige.PurchasedVertices.Add(PrestigeMap.MaritimeRoutesVertex);
        WireAggregator(island, prestige);

        Assert.True(island.PlayerCivilization.ModifierAggregator.HasModifier(ECategory.UNLOCK_MARITIME_ROUTES));
    }

    [Fact]
    public void MaritimeRoutesVertex_NotGrantedWhenNotPurchased()
    {
        var island   = IslandTestFactory.CreateSevenHexIslandState();
        var prestige = EmptyPrestige();
        WireAggregator(island, prestige);

        Assert.False(island.PlayerCivilization.ModifierAggregator.HasModifier(ECategory.UNLOCK_MARITIME_ROUTES));
    }
}
