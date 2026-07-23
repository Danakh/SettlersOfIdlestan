using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using SOITests.TestUtilities;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SOITests.ControllerTests;

/// <summary>
/// Tests for BuildersGuild automation: auto-road construction and auto-outpost construction.
/// </summary>
public class BuildersGuildAutomationTests
{
    // -------------------------------------------------------------------------
    // Shared hex coords for the 19-hex island (ring 0 + ring 1 + ring 2)
    // -------------------------------------------------------------------------

    // Ring 0
    private static readonly HexCoord C      = new(0, 0, IslandMap.SurfaceLayer);
    // Ring 1
    private static readonly HexCoord R1_E   = new(1, 0, IslandMap.SurfaceLayer);
    private static readonly HexCoord R1_NE  = new(0, 1, IslandMap.SurfaceLayer);
    private static readonly HexCoord R1_NW  = new(-1, 1, IslandMap.SurfaceLayer);
    private static readonly HexCoord R1_W   = new(-1, 0, IslandMap.SurfaceLayer);
    private static readonly HexCoord R1_SW  = new(0, -1, IslandMap.SurfaceLayer);
    private static readonly HexCoord R1_SE  = new(1, -1, IslandMap.SurfaceLayer);
    // Ring 2
    private static readonly HexCoord R2_E   = new(2, 0, IslandMap.SurfaceLayer);
    private static readonly HexCoord R2_NE  = new(1, 1, IslandMap.SurfaceLayer);
    private static readonly HexCoord R2_N   = new(0, 2, IslandMap.SurfaceLayer);
    private static readonly HexCoord R2_NW  = new(-1, 2, IslandMap.SurfaceLayer);
    private static readonly HexCoord R2_WNW = new(-2, 2, IslandMap.SurfaceLayer);
    private static readonly HexCoord R2_WW  = new(-2, 1, IslandMap.SurfaceLayer);
    private static readonly HexCoord R2_W   = new(-2, 0, IslandMap.SurfaceLayer);
    private static readonly HexCoord R2_SW  = new(-1, -1, IslandMap.SurfaceLayer);
    private static readonly HexCoord R2_S   = new(0, -2, IslandMap.SurfaceLayer);
    private static readonly HexCoord R2_SE  = new(1, -2, IslandMap.SurfaceLayer);
    private static readonly HexCoord R2_ESE = new(2, -2, IslandMap.SurfaceLayer);
    private static readonly HexCoord R2_SSE = new(2, -1, IslandMap.SurfaceLayer);

    /// <summary>
    /// 19-hex island (center + 2 rings), city at Vertex(C, R1_E, R1_NE).
    /// </summary>
    private static WorldState CreateNineteenHexIslandState()
    {
        var tiles = new List<HexTile>
        {
            new(C, TerrainType.Plain),
            new(R1_E,   TerrainType.Plain),
            new(R1_NE,  TerrainType.Plain),
            new(R1_NW,  TerrainType.Plain),
            new(R1_W,   TerrainType.Plain),
            new(R1_SW,  TerrainType.Plain),
            new(R1_SE,  TerrainType.Plain),
            new(R2_E,   TerrainType.Plain),
            new(R2_NE,  TerrainType.Plain),
            new(R2_N,   TerrainType.Plain),
            new(R2_NW,  TerrainType.Plain),
            new(R2_WNW, TerrainType.Plain),
            new(R2_WW,  TerrainType.Plain),
            new(R2_W,   TerrainType.Plain),
            new(R2_SW,  TerrainType.Plain),
            new(R2_S,   TerrainType.Plain),
            new(R2_SE,  TerrainType.Plain),
            new(R2_ESE, TerrainType.Plain),
            new(R2_SSE, TerrainType.Plain),
        };

        var map = new IslandMap(tiles);
        var civ = new Civilization { Index = 0 };

        // City at the vertex shared by center, east and north-east
        var cityVertex = Vertex.Create(C, R1_E, R1_NE);
        civ.AddCity(new City(cityVertex) { CivilizationIndex = 0 });

        return new WorldState(map, new List<Civilization> { civ }, AtlasController.InvalidIslandId);
    }

    // -------------------------------------------------------------------------
    // Helper: advance clock until no new roads appear for one full 500-tick cycle
    // or until maxCycles is reached. Returns the number of cycles consumed.
    // -------------------------------------------------------------------------
    private static void AdvanceUntilRoadSaturation(GameClock clock, IReadOnlyList<Road> civRoads, int maxCycles = 60)
    {
        int prev = civRoads.Count;
        for (int i = 0; i < maxCycles; i++)
        {
            clock.SimulateAdvance(500);
            if (civRoads.Count == prev) return; // no road built this cycle → saturated
            prev = civRoads.Count;
        }
    }

    // =========================================================================
    // Test 1 — Road saturation on the minimal 7-hex island
    // =========================================================================

    /// <summary>
    /// Guild level 1 only auto-builds roads at distance 1 from a city (3 edges on a 7-hex
    /// island). After those 3 edges are built the automation must stop adding new roads.
    /// </summary>
    [Fact]
    public void AutoRoad_SaturatesAllDistanceOneEdges_ThenStops()
    {
        var state = IslandTestFactory.CreateSevenHexIslandState();
        var civ   = state.Civilizations[0];
        var city  = civ.Cities[0];

        city.Buildings.Add(new BuildersGuild { Level = 1 });
        state.AutomationSettings.RoadAutomationEnabled = true;

        var clock = new GameClock();
        clock.Start();

        var roadController = new RoadController();
        roadController.Initialize(state, clock, new GamePRNG());

        // Pre-condition: no roads yet, 3 buildable at distance 1 from the city
        Assert.Empty(civ.Roads);
        Assert.Equal(3, roadController.GetBuildableRoadsAtDistance(0, 1).Count);

        // First SimulateAdvance just initialises the timer (first-fire guard)
        clock.SimulateAdvance(10);
        Assert.Empty(civ.Roads);

        // Each subsequent 500-tick window triggers exactly one auto-build
        clock.SimulateAdvance(500); // builds road 1 of 3
        clock.SimulateAdvance(500); // builds road 2 of 3
        clock.SimulateAdvance(500); // builds road 3 of 3

        Assert.Equal(3, civ.Roads.Count);
        Assert.Empty(roadController.GetBuildableRoadsAtDistance(0, 1));

        // Saturation: more time must not produce additional roads
        int countAtSaturation = civ.Roads.Count;
        clock.SimulateAdvance(500);
        clock.SimulateAdvance(500);
        clock.SimulateAdvance(1000);

        Assert.Equal(countAtSaturation, civ.Roads.Count);
    }

    // =========================================================================
    // Test 2 — Full scenario: road saturation → outpost auto-build → new roads
    // =========================================================================

    /// <summary>
    /// On a 19-hex island:
    /// 1. Guild level 1 auto-road saturates the distance-1 edges from the starting city.
    /// 2. Guild is upgraded to level 4; outpost automation (road disabled) places an outpost
    ///    at the one vertex at edge-distance ≥ 3 reachable from the manual road chain.
    /// 3. The new outpost exposes 2 brand-new distance-1 edges (at road-distance 4 from the
    ///    original city, so they were never touched by road automation).
    /// 4. Guild is set back to level 1 and road automation builds those 2 new edges.
    ///
    /// Manual road chain (pays cost at guild level 1, discount = 1):
    ///   Edge(C, R1_E)       distance 1  →  vertex (C,R1_E,R1_SE) at edge-dist 1
    ///   Edge(R1_E, R1_SE)   distance 2  →  vertex (R1_E,R1_SE,R2_SSE) at edge-dist 2
    ///   Edge(R1_SE, R2_SSE) distance 3  →  outpost vertex (R1_SE,R2_SSE,R2_ESE) at edge-dist 3
    /// </summary>
    [Fact]
    public void AutoOutpost_UnlocksNewDistanceOneRoads_ThatAutoRoadThenBuilds()
    {
        var state = CreateNineteenHexIslandState();
        var civ   = state.Civilizations[0];
        var city  = civ.Cities[0];

        // TownHall level 4 → city.Level = 4 → storage cap = 5×(2+4) = 30 per basic resource.
        // Guild starts at level 1 (auto-road dist ≤ 1 only) so road saturation stays deterministic.
        city.Buildings.Add(new TownHall { Level = 4 });
        var guild = new BuildersGuild { Level = 1 };
        city.Buildings.Add(guild);
        BuildingController.RecalculateStorageCapacity(civ);

        civ.AddResource(Resource.Wood,  30);
        civ.AddResource(Resource.Brick, 30);
        civ.AddResource(Resource.Food,  30);

        var clock = new GameClock();
        clock.Start();

        var roadController  = new RoadController();
        var cityController  = new CityBuilderController();
        roadController.Initialize(state, clock, new GamePRNG());
        cityController.Initialize(state, clock, new GamePRNG());

        // Build the manual chain to the future outpost vertex.
        roadController.BuildRoad(0, Edge.Create(C,     R1_E));   // dist 1  (cost 1W/1B at level-1 discount)
        roadController.BuildRoad(0, Edge.Create(R1_E,  R1_SE));  // dist 2
        roadController.BuildRoad(0, Edge.Create(R1_SE, R2_SSE)); // dist 3
        Assert.Equal(3, civ.Roads.Count);

        var expectedOutpostVertex = Vertex.Create(R1_SE, R2_SSE, R2_ESE);

        // ----------------------------------------------------------------
        // Phase 1 — Road automation (guild level 1, dist ≤ 1) saturates the
        //           two remaining distance-1 edges from the original city.
        // ----------------------------------------------------------------
        state.AutomationSettings.RoadAutomationEnabled    = true;
        state.AutomationSettings.OutpostAutomationEnabled = false;

        clock.SimulateAdvance(10);                        // initialise road timer
        AdvanceUntilRoadSaturation(clock, civ.Roads);     // builds Edge(C,R1_NE) and Edge(R1_E,R1_NE)

        int roadsAtSaturation = civ.Roads.Count;          // = 5

        // No dist-1 roads remain from the original city.
        Assert.Empty(roadController.GetBuildableRoadsAtDistance(0, 1));

        // ----------------------------------------------------------------
        // Phase 2 — Upgrade guild to level 4, disable road automation,
        //           enable outpost automation.
        //           With only 5 roads, Vertex(R1_SE,R2_SSE,R2_ESE) is the
        //           sole vertex at edge-distance ≥ 3 touched by a road.
        // ----------------------------------------------------------------
        guild.Level = 4;
        state.AutomationSettings.RoadAutomationEnabled    = false;
        state.AutomationSettings.OutpostAutomationEnabled = true;

        clock.SimulateAdvance(10);    // first-fire guard: LastOutpostBuildTick initialised
        clock.SimulateAdvance(1100);  // 1 100 ticks ≥ 1 000 cooldown → outpost fires

        Assert.Equal(2, civ.Cities.Count);
        Assert.Contains(civ.Cities, c => c.Position.Equals(expectedOutpostVertex));

        // ----------------------------------------------------------------
        // Phase 3 — New distance-1 edges near the outpost are now buildable.
        //           Edge(R1_SE,R2_SSE) was already built (road 3).
        //           The other two adjacent edges were at road-distance 4 from
        //           the original city so auto-road never reached them.
        // ----------------------------------------------------------------
        var newDist1 = roadController.GetBuildableRoadsAtDistance(0, 1);
        Assert.Equal(2, newDist1.Count);
        Assert.Contains(newDist1, r => r.Position.Equals(Edge.Create(R1_SE,  R2_ESE)));
        Assert.Contains(newDist1, r => r.Position.Equals(Edge.Create(R2_SSE, R2_ESE)));

        // ----------------------------------------------------------------
        // Phase 4 — Set guild back to level 1 (dist ≤ 1) so auto-road only
        //           picks the two new dist-1 edges and not dist-2/3 candidates.
        //           Disable outpost to avoid a second outpost interfering.
        // ----------------------------------------------------------------
        guild.Level = 1;
        state.AutomationSettings.OutpostAutomationEnabled = false;
        state.AutomationSettings.RoadAutomationEnabled    = true;

        clock.SimulateAdvance(500); // builds 1st new dist-1 road
        clock.SimulateAdvance(500); // builds 2nd new dist-1 road

        Assert.Empty(roadController.GetBuildableRoadsAtDistance(0, 1));
        Assert.Equal(roadsAtSaturation + 2, civ.Roads.Count);
    }

    // =========================================================================
    // Test 3 — BuildersGuild auto-upgrades the TownHall as soon as guild level 1
    // =========================================================================

    [Fact]
    public void AutoTownHall_UpgradesTownHall_AssoonAsGuildLevelOne()
    {
        var state = IslandTestFactory.CreateSevenHexIslandState();
        var civ   = state.Civilizations[0];
        var city  = civ.Cities[0];

        city.Buildings.Add(new TownHall { Level = 1 });
        city.Buildings.Add(new BuildersGuild { Level = 1 });
        BuildingController.RecalculateStorageCapacity(civ);

        civ.AddResource(Resource.Food,  10);
        civ.AddResource(Resource.Wood,  10);
        civ.AddResource(Resource.Brick, 10);
        civ.AddResource(Resource.Stone, 10);

        state.AutomationSettings.TownHallAutomationEnabled = true;

        var clock = new GameClock();
        clock.Start();

        var buildingController = new BuildingController();
        buildingController.Initialize(state, clock);

        // First tick just initialises the timer (first-fire guard)
        clock.SimulateAdvance(10);
        Assert.Equal(1, city.Level);

        // Cooldown (1000 ticks) elapsed → auto-upgrade fires
        clock.SimulateAdvance(1100);
        Assert.Equal(2, city.Level);
    }

    // =========================================================================
    // Test 4 — Guild automation must keep upgrading past level 5
    // (regression: TickGuildAutomation used to search only levels 1..5)
    // =========================================================================

    [Fact]
    public void AutoProduction_KeepsUpgrading_PastLevelFive()
    {
        // All-Forest map: Sawmill is the only HarvestersGuild target buildable here
        // (Brickworks needs Hill, Quarry needs Mountain, Mill needs Plain, MushroomFarm
        // needs MushroomCave), so the "new builds first" phase can never compete with
        // the upgrade phase once the Sawmill exists.
        var tiles = new List<HexTile>
        {
            new(C,     TerrainType.Forest),
            new(R1_E,  TerrainType.Forest),
            new(R1_NE, TerrainType.Forest),
        };
        var map = new IslandMap(tiles);
        var civ = new Civilization { Index = 0 };
        var city = new City(Vertex.Create(C, R1_E, R1_NE)) { CivilizationIndex = civ.Index };
        civ.AddCity(city);
        var state = new WorldState(map, new List<Civilization> { civ }, AtlasController.InvalidIslandId);

        city.Buildings.Add(new TownHall { Level = 1 });
        city.Buildings.Add(new HarvestersGuild { Level = 1 });

        // Sawmill's default max level is 4; push it to 8 so the test can exercise
        // levels above the old hard-coded search range of 1..5, and grant enough
        // storage that resources never bottleneck the automation.
        civ.AddCustomAggregator(new StaticModifierProvider(new[]
        {
            new Modifier(ECategory.BUILDING_MAX_LEVEL, nameof(BuildingType.Sawmill), EType.ADDITIVE, 4),
            new Modifier(ECategory.STORAGE_CAPACITY_BASIC, EType.ADDITIVE, 100_000),
        }));
        BuildingController.RecalculateStorageCapacity(civ);

        civ.AddResource(Resource.Wood,  100_000);
        civ.AddResource(Resource.Brick, 100_000);

        state.AutomationSettings.ProductionBuildingAutomationEnabled = true;

        var clock = new GameClock();
        clock.Start();

        var buildingController = new BuildingController();
        buildingController.Initialize(state, clock);

        clock.SimulateAdvance(10); // first-fire guard

        clock.SimulateAdvance(1100); // builds the new Sawmill (level 0 -> 1)
        var sawmill = city.Buildings.First(b => b.Type == BuildingType.Sawmill);
        Assert.Equal(1, sawmill.Level);

        for (int expectedLevel = 2; expectedLevel <= 8; expectedLevel++)
        {
            clock.SimulateAdvance(1100);
            Assert.Equal(expectedLevel, sawmill.Level);
        }
    }
}
