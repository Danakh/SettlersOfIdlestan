using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Controller.Military;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using System.Linq;
using Xunit;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SOITests.ControllerTests;

/// <summary>
/// Vérifie ResolveSoldierFeeding : mort des soldats par famine et effet de SOLDIER_FOOD_FREE_PER_CITY.
///
/// Règle : chaque cycle d'alimentation, chaque soldat consomme 1 nourriture.
/// Les soldats "free" (via SOLDIER_FOOD_FREE_PER_CITY × nb villes) ne consomment rien.
/// Les soldats non nourris meurent. L'événement SoldierStarved est ajouté au log
/// uniquement quand la civ est la civilisation joueur (index 0).
/// </summary>
public class SoldierFeedingTests
{
    private static HexCoord H1 => new(0, 1, IslandMap.SurfaceLayer);
    private static HexCoord H2 => new(1, 0, IslandMap.SurfaceLayer);
    private static HexCoord H3 => new(1, 1, IslandMap.SurfaceLayer);
    private static Vertex City1Vertex => Vertex.Create(H1, H2, H3);

    private static HexCoord H4 => new(3, 0, IslandMap.SurfaceLayer);
    private static HexCoord H5 => new(4, 0, IslandMap.SurfaceLayer);
    private static HexCoord H6 => new(3, 1, IslandMap.SurfaceLayer);
    private static Vertex City2Vertex => Vertex.Create(H4, H5, H6);

    private static IslandMap SingleCityMap() => new([
        new HexTile(H1, TerrainType.Plain),
        new HexTile(H2, TerrainType.Plain),
        new HexTile(H3, TerrainType.Plain),
    ]);

    private static IslandMap TwoCityMap() => new([
        new HexTile(H1, TerrainType.Plain),
        new HexTile(H2, TerrainType.Plain),
        new HexTile(H3, TerrainType.Plain),
        new HexTile(H4, TerrainType.Plain),
        new HexTile(H5, TerrainType.Plain),
        new HexTile(H6, TerrainType.Plain),
    ]);

    /// <summary>
    /// Civ joueur (index 0) avec une seule ville et des soldats pré-placés.
    /// Pas de caserne pour éviter les consommations d'ore parasites.
    /// </summary>
    private static (WorldState state, GameClock clock, Civilization civ)
        SingleCitySetup(int initialFood, int soldiers, int freePerCity = 0)
    {
        var civ = new Civilization { Index = 0 };
        civ.Resources[Resource.Food] = initialFood;
        var city = new City(City1Vertex) { CivilizationIndex = 0, Soldiers = soldiers };
        civ.AddCity(city);

        if (freePerCity > 0)
            civ.AddCustomAggregator(new StaticModifierProvider([
                new Modifier(ECategory.SOLDIER_FOOD_FREE_PER_CITY, EType.ADDITIVE, freePerCity)
            ]));

        var state = new WorldState(SingleCityMap(), [civ], AtlasController.InvalidIslandId);
        var clock = new GameClock();
        clock.Start();
        new MilitaryController().Initialize(state, clock);
        return (state, clock, civ);
    }

    /// <summary>
    /// Civ joueur (index 0) avec deux villes et des soldats pré-placés.
    /// </summary>
    private static (WorldState state, GameClock clock, Civilization civ, City city1, City city2)
        TwoCitySetup(int soldiers1, int soldiers2, int initialFood, int freePerCity = 0)
    {
        var civ = new Civilization { Index = 0 };
        civ.Resources[Resource.Food] = initialFood;
        var city1 = new City(City1Vertex) { CivilizationIndex = 0, Soldiers = soldiers1 };
        var city2 = new City(City2Vertex) { CivilizationIndex = 0, Soldiers = soldiers2 };
        civ.AddCity(city1);
        civ.AddCity(city2);

        if (freePerCity > 0)
            civ.AddCustomAggregator(new StaticModifierProvider([
                new Modifier(ECategory.SOLDIER_FOOD_FREE_PER_CITY, EType.ADDITIVE, freePerCity)
            ]));

        var state = new WorldState(TwoCityMap(), [civ], AtlasController.InvalidIslandId);
        var clock = new GameClock();
        clock.Start();
        new MilitaryController().Initialize(state, clock);
        return (state, clock, civ, city1, city2);
    }

    // ── Mort par famine — cas de base ─────────────────────────────────────────

    [Fact]
    public void Starvation_NoFood_AllSoldiersStarve()
    {
        // 5 soldats, 0 nourriture → 0 free → 5 meurent → 0 restants
        var (_, clock, civ) = SingleCitySetup(initialFood: 0, soldiers: 5);

        clock.SimulateAdvance(MilitaryController.SoldierFeedIntervalTicks);

        Assert.Equal(0, civ.Cities[0].Soldiers);
    }

    [Fact]
    public void Starvation_InsufficientFood_PartialDeath()
    {
        // 5 soldats, 3 nourriture → 3 nourris, 2 meurent → 3 restants
        var (_, clock, civ) = SingleCitySetup(initialFood: 3, soldiers: 5);

        clock.SimulateAdvance(MilitaryController.SoldierFeedIntervalTicks);

        Assert.Equal(3, civ.Cities[0].Soldiers);
    }

    [Fact]
    public void Starvation_EnoughFood_NoDeaths()
    {
        // 5 soldats, 5 nourriture → tous nourris → 5 restants
        var (_, clock, civ) = SingleCitySetup(initialFood: 5, soldiers: 5);

        clock.SimulateAdvance(MilitaryController.SoldierFeedIntervalTicks);

        Assert.Equal(5, civ.Cities[0].Soldiers);
    }

    [Fact]
    public void Starvation_NoFood_SoldierStarvedEventFired()
    {
        // Des soldats meurent → SoldierStarved doit être logué
        var (state, clock, _) = SingleCitySetup(initialFood: 0, soldiers: 5);

        clock.SimulateAdvance(MilitaryController.SoldierFeedIntervalTicks);

        Assert.Contains(state.EventLog.Entries, e => e.Type == GameEventType.SoldierStarved);
    }

    [Fact]
    public void Starvation_EnoughFood_NoStarvedEvent()
    {
        // Tous nourris → aucun événement SoldierStarved
        var (state, clock, _) = SingleCitySetup(initialFood: 5, soldiers: 5);

        clock.SimulateAdvance(MilitaryController.SoldierFeedIntervalTicks);

        Assert.DoesNotContain(state.EventLog.Entries, e => e.Type == GameEventType.SoldierStarved);
    }

    // ── SOLDIER_FOOD_FREE_PER_CITY ────────────────────────────────────────────

    [Fact]
    public void FreePerCity_ExactlyFree_ZeroFoodNoDeaths()
    {
        // freePerCity=10, 1 ville, 10 soldats, 0 nourriture
        // → totalFree = min(10, 10×1) = 10 ; soldiersNeedingFood = 0 → 0 mort
        var (state, clock, civ) = SingleCitySetup(initialFood: 0, soldiers: 10, freePerCity: 10);

        clock.SimulateAdvance(MilitaryController.SoldierFeedIntervalTicks);

        Assert.Equal(10, civ.Cities[0].Soldiers);
        Assert.DoesNotContain(state.EventLog.Entries, e => e.Type == GameEventType.SoldierStarved);
    }

    [Fact]
    public void FreePerCity_ExcessSoldiers_ExcessDie()
    {
        // freePerCity=10, 1 ville, 15 soldats, 0 nourriture
        // → totalFree = 10 ; soldiersNeedingFood = 5 → 5 meurent → 10 restants
        var (state, clock, civ) = SingleCitySetup(initialFood: 0, soldiers: 15, freePerCity: 10);

        clock.SimulateAdvance(MilitaryController.SoldierFeedIntervalTicks);

        Assert.Equal(10, civ.Cities[0].Soldiers);
        Assert.Contains(state.EventLog.Entries, e => e.Type == GameEventType.SoldierStarved);
    }

    [Fact]
    public void FreePerCity_TwoCities_ZeroFood_TwentyFreeSlots()
    {
        // freePerCity=10, 2 villes → totalFree = min(25, 10×2) = 20
        // 25 soldats au total (15 en ville1, 10 en ville2), 0 nourriture
        // → soldiersNeedingFood = 5 → 5 meurent → 20 restants
        var (state, clock, civ, _, _) = TwoCitySetup(
            soldiers1: 15, soldiers2: 10, initialFood: 0, freePerCity: 10);

        clock.SimulateAdvance(MilitaryController.SoldierFeedIntervalTicks);

        Assert.Equal(20, civ.Cities.Sum(c => c.Soldiers));
        Assert.Contains(state.EventLog.Entries, e => e.Type == GameEventType.SoldierStarved);
    }
}
