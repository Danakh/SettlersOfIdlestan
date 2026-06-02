using SettlersOfIdlestan.Controller.Expand;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Controller.Military;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Prestige;
using System.Collections.Generic;
using Xunit;

namespace SOITests.ControllerTests;

/// <summary>
/// Vérifie que Civilization.LowStock est émis par MilitaryController (ore, food)
/// et ResearchController (gold) au bon moment, et silencieux quand le stock est suffisant.
///
/// Géométrie : 1 ville, 0 TownHall → city.Level = 0
///   baseCityResourceMax = 2 × 1 + 0 = 2  →  max ressource basique = 5 × 2 = 10
///   Seuil 10 % : qty × 10 ≤ 10, soit qty ≤ 1.
/// </summary>
public class CivilizationLowStockTests
{
    private static HexCoord H1 => new(0, 1, IslandMap.SurfaceLayer);
    private static HexCoord H2 => new(1, 0, IslandMap.SurfaceLayer);
    private static HexCoord H3 => new(1, 1, IslandMap.SurfaceLayer);
    private static Vertex CityVertex => Vertex.Create(H1, H2, H3);

    private static IslandMap MinimalMap() => new([
        new HexTile(H1, TerrainType.Plain),
        new HexTile(H2, TerrainType.Plain),
        new HexTile(H3, TerrainType.Plain),
    ]);

    // ── Ore / MilitaryController.ProduceSoldiers ────────────────────────────

    /// <summary>
    /// Crée un setup avec 1 caserne (prod soldats active) et une quantité d'ore précise.
    /// TownHall niv.4 → city.Level=4 → advancedCityResourceMax=2 → oreMax=10.
    /// Seuil 10 % : qty×10 ≤ 10, soit qty ≤ 1.
    /// Food élevé pour ne pas parasiter les assertions.
    /// </summary>
    private static (GameClock clock, Civilization civ) OreSetup(int initialOre)
    {
        var civ = new Civilization { Index = 0 };
        civ.Resources[Resource.Ore]  = initialOre;
        civ.Resources[Resource.Food] = 999;
        var city = new City(CityVertex) { CivilizationIndex = 0, Soldiers = 0 };
        city.Buildings.Add(new TownHall { Level = 4 }); // city.Level=4 → oreMax = 5*(4-2) = 10
        city.Buildings.Add(new Barracks { Level = 2 });
        civ.Cities.Add(city);

        var state = new IslandState(MinimalMap(), [civ], AtlasController.InvalidIslandId);
        var clock = new GameClock();
        clock.Start();
        new MilitaryController().Initialize(state, clock);
        return (clock, civ);
    }

    [Fact]
    public void LowStock_Ore_FiresWhenStockIsZero()
    {
        // ore=0 → production bloquée → LowStock immédiatement
        var (clock, civ) = OreSetup(initialOre: 0);

        Resource? received = null;
        civ.LowStock += (_, r) => received = r;

        clock.SimulateAdvance(MilitaryController.SoldierProductionIntervalTicks);

        Assert.Equal(Resource.Ore, received);
    }

    [Fact]
    public void LowStock_Ore_FiresWhenStockDropsToOrBelowTenPercent()
    {
        // ore=2 → produit 1 → ore=1 ; 1×10=10 ≤ max(10) → LowStock
        var (clock, civ) = OreSetup(initialOre: 2);

        Resource? received = null;
        civ.LowStock += (_, r) => received = r;

        clock.SimulateAdvance(MilitaryController.SoldierProductionIntervalTicks);

        Assert.Equal(Resource.Ore, received);
    }

    [Fact]
    public void LowStock_Ore_DoesNotFireWhenStockIsHigh()
    {
        // ore=6 → produit 1 → ore=5 ; 5×10=50 > max(10) → silence
        var (clock, civ) = OreSetup(initialOre: 6);

        bool fired = false;
        civ.LowStock += (_, r) => { if (r == Resource.Ore) fired = true; };

        clock.SimulateAdvance(MilitaryController.SoldierProductionIntervalTicks);

        Assert.False(fired);
    }

    // ── Food / MilitaryController.ResolveSoldierFeeding ────────────────────

    /// <summary>
    /// Crée un setup sans caserne (évite les consommations d'ore) avec des soldats pré-placés.
    /// </summary>
    private static (GameClock clock, Civilization civ) FoodSetup(int initialFood, int soldiers = 1)
    {
        var civ = new Civilization { Index = 0 };
        civ.Resources[Resource.Food] = initialFood;
        var city = new City(CityVertex) { CivilizationIndex = 0, Soldiers = soldiers };
        civ.Cities.Add(city);

        var state = new IslandState(MinimalMap(), [civ], AtlasController.InvalidIslandId);
        var clock = new GameClock();
        clock.Start();
        new MilitaryController().Initialize(state, clock);
        return (clock, civ);
    }

    [Fact]
    public void LowStock_Food_FiresWhenLastFoodConsumed()
    {
        // food=1, 1 soldat → consomme 1 → food=0 ; 0×10=0 ≤ max(10) → LowStock
        var (clock, civ) = FoodSetup(initialFood: 1, soldiers: 1);

        Resource? received = null;
        civ.LowStock += (_, r) => received = r;

        clock.SimulateAdvance(MilitaryController.SoldierFeedIntervalTicks);

        Assert.Equal(Resource.Food, received);
    }

    [Fact]
    public void LowStock_Food_FiresWhenStockDropsToOrBelowTenPercent()
    {
        // food=2, 1 soldat → consomme 1 → food=1 ; 1×10=10 ≤ max(10) → LowStock
        var (clock, civ) = FoodSetup(initialFood: 2, soldiers: 1);

        Resource? received = null;
        civ.LowStock += (_, r) => received = r;

        clock.SimulateAdvance(MilitaryController.SoldierFeedIntervalTicks);

        Assert.Equal(Resource.Food, received);
    }

    [Fact]
    public void LowStock_Food_DoesNotFireWhenStockIsHigh()
    {
        // food=6, 1 soldat → consomme 1 → food=5 ; 5×10=50 > max(10) → silence
        var (clock, civ) = FoodSetup(initialFood: 6, soldiers: 1);

        bool fired = false;
        civ.LowStock += (_, r) => { if (r == Resource.Food) fired = true; };

        clock.SimulateAdvance(MilitaryController.SoldierFeedIntervalTicks);

        Assert.False(fired);
    }

    // ── Gold / ResearchController (Laboratoire) ─────────────────────────────

    /// <summary>
    /// Cooldown Laboratoire niveau 1 = 1 000 ticks. Deux avancements nécessaires :
    /// le premier initialise LastResearchTick, le second déclenche la consommation.
    /// </summary>
    private const long LabCooldownTicks = 1_000L;

    private static (GameClock clock, Civilization civ) GoldSetup(int initialGold)
    {
        var civ = new Civilization { Index = 0 };
        civ.Resources[Resource.Gold] = initialGold;
        var lab = new Laboratory { Level = 1, ActivationStatus = ActivationStatus.ACTIVE };
        var city = new City(CityVertex) { CivilizationIndex = 0 };
        city.Buildings.Add(lab);
        civ.Cities.Add(city);

        var state = new IslandState(MinimalMap(), [civ], AtlasController.InvalidIslandId);
        var prestigeState = new PrestigeState(state);

        var clock = new GameClock();
        clock.Start();
        var ctrl = new ResearchController();
        ctrl.Initialize(state, clock, prestigeState);
        return (clock, civ);
    }

    [Fact]
    public void LowStock_Gold_FiresWhenStockIsZero()
    {
        // gold=0, labo prêt → ne peut pas consommer → LowStock
        var (clock, civ) = GoldSetup(initialGold: 0);

        Resource? received = null;
        civ.LowStock += (_, r) => received = r;

        clock.SimulateAdvance(1);                // initialise LastResearchTick
        clock.SimulateAdvance(LabCooldownTicks); // déclenche la tentative de consommation

        Assert.Equal(Resource.Gold, received);
    }

    [Fact]
    public void LowStock_Gold_FiresWhenStockDropsToOrBelowTenPercent()
    {
        // gold=2 → consomme 1 → gold=1 ; 1×10=10 ≤ max(10) → LowStock
        var (clock, civ) = GoldSetup(initialGold: 2);

        Resource? received = null;
        civ.LowStock += (_, r) => received = r;

        clock.SimulateAdvance(1);
        clock.SimulateAdvance(LabCooldownTicks);

        Assert.Equal(Resource.Gold, received);
    }

    [Fact]
    public void LowStock_Gold_DoesNotFireWhenStockIsHigh()
    {
        // gold=6 → consomme 1 → gold=5 ; 5×10=50 > max(10) → silence
        var (clock, civ) = GoldSetup(initialGold: 6);

        bool fired = false;
        civ.LowStock += (_, r) => { if (r == Resource.Gold) fired = true; };

        clock.SimulateAdvance(1);
        clock.SimulateAdvance(LabCooldownTicks);

        Assert.False(fired);
    }
}
