using SettlersOfIdlestan.Controller.Generator;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.HexGrid;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace SOITests.ControllerTests;

/// <summary>
/// Camp Mobile — voir aussi WarFleetControllerTests (structure similaire, mais terrestre) et
/// CityBuilderControllerTests (partage le même layout "ruban" de hex en ligne) :
///   h1(0,0) — h2(1,0) — h3(0,1) — h4(1,1) — h5(0,2)
///   V1      = Vertex(h1,h2,h3)
///   VMiddle = Vertex(h2,h3,h4) — adjacent à V1 (arête h2-h3)
///   V2      = Vertex(h3,h4,h5) — adjacent à VMiddle (arête h3-h4), à distance 2 de V1
/// </summary>
public class MobileCampControllerTests
{
    private static HexCoord H(int q, int r) => new(q, r, IslandMap.SurfaceLayer);

    private static (WorldState state, Civilization civ, Vertex v1, Vertex vMiddle, Vertex v2) RibbonIsland()
    {
        var h1 = H(0, 0);
        var h2 = H(1, 0);
        var h3 = H(0, 1);
        var h4 = H(1, 1);
        var h5 = H(0, 2);

        var map = new IslandMap(new HexTile[]
        {
            new(h1, TerrainType.Plain),
            new(h2, TerrainType.Plain),
            new(h3, TerrainType.Plain),
            new(h4, TerrainType.Plain),
            new(h5, TerrainType.Plain),
        });

        var civ = new Civilization { Index = 0 };
        var state = new WorldState(map, new List<Civilization> { civ }, AtlasController.InvalidIslandId);

        var v1 = Vertex.Create(h1, h2, h3);
        var vMiddle = Vertex.Create(h2, h3, h4);
        var v2 = Vertex.Create(h3, h4, h5);

        civ.AddRoad(new Road(Edge.Create(h2, h3)) { CivilizationIndex = 0 });
        civ.AddRoad(new Road(Edge.Create(h3, h4)) { CivilizationIndex = 0 });

        return (state, civ, v1, vMiddle, v2);
    }

    private static (CityBuilderController city, MobileCampController camp) Controllers(WorldState state, GameClock? clock = null)
    {
        var cityController = new CityBuilderController();
        cityController.Initialize(state);
        var campController = new MobileCampController();
        campController.Initialize(state, cityController, clock);
        return (cityController, campController);
    }

    private static void GrantTech(Civilization civ) => civ.TechnologyTree.CompletedTechnologies.Add(TechnologyId.MobileCampConstruction);

    [Fact]
    public void IsMobileCampUnlocked_FalseWithoutTech()
    {
        var (_, civ, _, _, _) = RibbonIsland();
        Assert.False(new MobileCampController().IsMobileCampUnlocked(civ));
    }

    [Fact]
    public void IsMobileCampUnlocked_TrueWithTech()
    {
        var (_, civ, _, _, _) = RibbonIsland();
        GrantTech(civ);
        Assert.True(new MobileCampController().IsMobileCampUnlocked(civ));
    }

    [Fact]
    public void GetPotentialVertices_EmptyWhenCityBuildableEverywhere()
    {
        // No city yet: every road-touching vertex is buildable as a regular outpost, so no Mobile
        // Camp should be proposed anywhere (see MobileCampController.GetPotentialVertices doc).
        var (state, _, _, _, _) = RibbonIsland();
        var (_, campController) = Controllers(state);

        Assert.Empty(campController.GetPotentialVertices(0));
    }

    [Fact]
    public void GetPotentialVertices_IncludesVertexTooCloseForOutpostButFarEnoughFromMilitary()
    {
        var (state, civ, v1, vMiddle, v2) = RibbonIsland();
        civ.AddCity(new City(v1) { CivilizationIndex = 0 });
        var (_, campController) = Controllers(state);

        var vertices = campController.GetPotentialVertices(0);

        // v2 is at distance 2 from the own city: too close for a new outpost (MinDistanceBetweenCivilizationCities = 3)
        // but far enough for a Mobile Camp (MinDistanceBetweenMilitaryVertices = 2).
        Assert.Contains(vertices, v => v.Equals(v2));
        // vMiddle is at distance 1: too close for both an outpost and a Mobile Camp.
        Assert.DoesNotContain(vertices, v => v.Equals(vMiddle));
    }

    [Fact]
    public void GetPotentialVertices_NotBlockedByEnemyCityProximity()
    {
        var (state, civ, v1, vMiddle, v2) = RibbonIsland();
        var enemyCiv = new Civilization { Index = 1 };
        state.AddCivilization(enemyCiv);
        enemyCiv.AddCity(new City(vMiddle) { CivilizationIndex = 1 });
        var (_, campController) = Controllers(state);

        var vertices = campController.GetPotentialVertices(0);

        // v1 and v2 are both at distance 1 from the enemy city — too close for a regular outpost, but
        // a Mobile Camp has no restriction whatsoever against other civilizations' military vertices.
        Assert.Contains(vertices, v => v.Equals(v1));
        Assert.Contains(vertices, v => v.Equals(v2));
    }

    [Fact]
    public void GetPotentialVertices_ExcludesVertexAlreadyOccupiedByOwnCamp()
    {
        var (state, civ, v1, vMiddle, v2) = RibbonIsland();
        civ.AddCity(new City(v1) { CivilizationIndex = 0 });
        civ.AddMobileCamp(new MobileCamp(v2) { CivilizationIndex = 0 });
        var (_, campController) = Controllers(state);

        Assert.DoesNotContain(campController.GetPotentialVertices(0), v => v.Equals(v2));
    }

    [Fact]
    public void GetBuildableVertices_EmptyWithoutTech()
    {
        var (state, civ, v1, _, _) = RibbonIsland();
        civ.AddCity(new City(v1) { CivilizationIndex = 0 });
        var (_, campController) = Controllers(state);

        Assert.Empty(campController.GetBuildableVertices(0));
    }

    [Fact]
    public void GetBuildableVertices_IncludesPotentialVertex_WithTech()
    {
        var (state, civ, v1, _, v2) = RibbonIsland();
        civ.AddCity(new City(v1) { CivilizationIndex = 0 });
        GrantTech(civ);
        var (_, campController) = Controllers(state);

        Assert.Contains(campController.GetBuildableVertices(0), v => v.Equals(v2));
    }

    [Fact]
    public void BuildMobileCamp_WithoutTech_ReturnsNull()
    {
        var (state, civ, v1, _, v2) = RibbonIsland();
        civ.AddCity(new City(v1) { CivilizationIndex = 0 });
        var (_, campController) = Controllers(state);

        var camp = campController.BuildMobileCamp(0, v2);

        Assert.Null(camp);
        Assert.Empty(civ.MobileCamps);
    }

    [Fact]
    public void BuildMobileCamp_VertexNotPotential_Throws()
    {
        var (state, civ, v1, vMiddle, _) = RibbonIsland();
        civ.AddCity(new City(v1) { CivilizationIndex = 0 });
        GrantTech(civ);
        var (_, campController) = Controllers(state);

        Assert.Throws<System.InvalidOperationException>(() => campController.BuildMobileCamp(0, vMiddle));
    }

    [Fact]
    public void BuildMobileCamp_PaysCostAndAddsCampWithFixedStats()
    {
        var (state, civ, v1, _, v2) = RibbonIsland();
        civ.AddCity(new City(v1) { CivilizationIndex = 0 });
        GrantTech(civ);
        civ.SetStorageCapacityCache(1000, 1000);
        civ.AddResource(Resource.Stone, 100);
        civ.AddResource(Resource.Brick, 100);
        civ.AddResource(Resource.Ore, 100);
        civ.AddResource(Resource.Food, 200);
        civ.AddResource(Resource.Gold, 200);
        var (_, campController) = Controllers(state);

        var camp = campController.BuildMobileCamp(0, v2);

        Assert.NotNull(camp);
        Assert.Contains(civ.MobileCamps, c => c == camp);
        Assert.Equal(20, camp!.MaxSoldiers);
        Assert.Equal(20, camp.MaxDefense);
        Assert.Equal(0, civ.GetResourceQuantity(Resource.Stone));
        Assert.Equal(0, civ.GetResourceQuantity(Resource.Brick));
        Assert.Equal(0, civ.GetResourceQuantity(Resource.Ore));
        Assert.Equal(0, civ.GetResourceQuantity(Resource.Food));
        Assert.Equal(0, civ.GetResourceQuantity(Resource.Gold));
    }

    [Fact]
    public void DestroyMobileCamp_RemovesCampFromCivilization()
    {
        var (state, civ, v1, _, v2) = RibbonIsland();
        civ.AddCity(new City(v1) { CivilizationIndex = 0 });
        GrantTech(civ);
        civ.SetStorageCapacityCache(1000, 1000);
        civ.AddResource(Resource.Stone, 100);
        civ.AddResource(Resource.Brick, 100);
        civ.AddResource(Resource.Ore, 100);
        civ.AddResource(Resource.Food, 200);
        civ.AddResource(Resource.Gold, 200);
        var (_, campController) = Controllers(state);
        var camp = campController.BuildMobileCamp(0, v2);

        campController.DestroyMobileCamp(camp!);

        Assert.DoesNotContain(civ.MobileCamps, c => c == camp);
    }

    [Fact]
    public void DestroyCampsNear_DestroysOwnCampWithinDistanceOne_ButNotFartherOwnCamp()
    {
        var (state, civ, v1, vMiddle, v2) = RibbonIsland();
        // vMiddle (distance 1 from v1) belongs to the player — must be destroyed.
        civ.AddMobileCamp(new MobileCamp(vMiddle) { CivilizationIndex = 0 });
        var (_, campController) = Controllers(state);

        campController.DestroyCampsNear(v1, civilizationIndex: 0);

        Assert.Empty(civ.MobileCamps);
    }

    [Fact]
    public void DestroyCampsNear_DoesNotDestroyEnemyCamp()
    {
        var (state, civ, v1, vMiddle, _) = RibbonIsland();
        var enemyCiv = new Civilization { Index = 1 };
        state.AddCivilization(enemyCiv);
        // vMiddle (distance 1 from v1) belongs to the enemy — an allied city being built must not
        // affect enemy Mobile Camps, only the building civilization's own camps.
        enemyCiv.AddMobileCamp(new MobileCamp(vMiddle) { CivilizationIndex = 1 });
        var (_, campController) = Controllers(state);

        campController.DestroyCampsNear(v1, civilizationIndex: 0);

        Assert.Single(enemyCiv.MobileCamps);
    }

    [Fact]
    public void CityBuilt_DestroysNearbyOwnMobileCamp_ButNotEnemyCamp_ViaOnCityBuiltEvent()
    {
        // Mirrors the wiring done in MainGameController: CityBuilderController.OnCityBuilt triggers
        // MobileCampController.DestroyCampsNear for the building civilization's own camps only.
        var (state, civ, v1, vMiddle, _) = RibbonIsland();
        var enemyCiv = new Civilization { Index = 1 };
        state.AddCivilization(enemyCiv);
        civ.AddMobileCamp(new MobileCamp(vMiddle) { CivilizationIndex = 0 });
        enemyCiv.AddMobileCamp(new MobileCamp(vMiddle) { CivilizationIndex = 1 });
        var (cityController, campController) = Controllers(state);
        cityController.OnCityBuilt += (_, e) => campController.DestroyCampsNear(e.Position, e.CivilizationIndex);
        civ.SetStorageCapacityCache(1000, 1000);
        civ.AddResource(Resource.Brick, 10);
        civ.AddResource(Resource.Wood, 10);
        civ.AddResource(Resource.Food, 15);

        cityController.BuildCity(0, v1);

        Assert.Empty(civ.MobileCamps);
        Assert.Single(enemyCiv.MobileCamps);
    }

    [Fact]
    public void CityRelocated_DestroysNearbyOwnMobileCamp_ButNotEnemyCamp_ViaOnCityRelocatedEvent()
    {
        // Mirrors CityBuilt_DestroysNearbyOwnMobileCamp_ButNotEnemyCamp_ViaOnCityBuiltEvent above, but for
        // relocation: MainGameController also wires CityBuilderController.OnCityRelocated to
        // MobileCampController.DestroyCampsNear, so relocating a city must clear nearby own camps the
        // same way founding one does.
        var (state, civ, v1, vMiddle, v2) = RibbonIsland();
        var enemyCiv = new Civilization { Index = 1 };
        state.AddCivilization(enemyCiv);
        var city = new City(v1) { CivilizationIndex = 0 };
        civ.AddCity(city);
        civ.AddMobileCamp(new MobileCamp(v2) { CivilizationIndex = 0 });
        enemyCiv.AddMobileCamp(new MobileCamp(v2) { CivilizationIndex = 1 });
        var (cityController, campController) = Controllers(state);
        cityController.OnCityRelocated += (_, e) => campController.DestroyCampsNear(e.Position, e.CivilizationIndex);
        civ.SetStorageCapacityCache(1000, 1000);
        civ.AddResource(Resource.Gold, CityBuilderController.RelocationCost()[Resource.Gold]);
        civ.AddResource(Resource.Food, CityBuilderController.RelocationCost()[Resource.Food]);

        // v2 hosts both camps and stays untouched by the move itself — vMiddle is the relocation
        // target, one edge away from v2, i.e. within MobileCampController.CityProximityDestroyDistance.
        var relocated = cityController.RelocateCity(city, vMiddle);

        Assert.True(relocated);
        Assert.Empty(civ.MobileCamps);
        Assert.Single(enemyCiv.MobileCamps);
    }

    [Fact]
    public void CityBuilt_OnOwnCampExactPosition_TransfersSoldiersAndGrantsFreeBarracks()
    {
        // Building an outpost exactly on top of one's own Mobile Camp (distance 0, unlike the
        // vMiddle/distance-1 case in CityBuilt_DestroysNearbyOwnMobileCamp...) absorbs the camp instead
        // of just destroying it: its soldiers move into the new city and a free Barracks is granted.
        var (state, civ, v1, _, _) = RibbonIsland();
        civ.AddMobileCamp(new MobileCamp(v1) { CivilizationIndex = 0, Soldiers = 3 });
        var (cityController, campController) = Controllers(state);
        cityController.OnCityBuilt += (_, e) => campController.DestroyCampsNear(e.Position, e.CivilizationIndex);
        civ.SetStorageCapacityCache(1000, 1000);
        civ.AddResource(Resource.Brick, 10);
        civ.AddResource(Resource.Wood, 10);
        civ.AddResource(Resource.Food, 15);

        var city = cityController.BuildCity(0, v1);

        Assert.NotNull(city);
        Assert.Empty(civ.MobileCamps);
        Assert.Contains(city!.Buildings, b => b.Type == BuildingType.Barracks);
        Assert.Equal(3, city.Soldiers);
    }

    [Fact]
    public void CityBuilt_OnOwnCampExactPosition_CapsTransferredSoldiersAtGarrisonCapacity()
    {
        // The free Barracks (level 1) is the city's only source of garrison capacity at this point
        // (5 soldiers), so a camp with more soldiers than that must not overflow the city's capacity.
        var (state, civ, v1, _, _) = RibbonIsland();
        civ.AddMobileCamp(new MobileCamp(v1) { CivilizationIndex = 0, Soldiers = 12 });
        var (cityController, campController) = Controllers(state);
        cityController.OnCityBuilt += (_, e) => campController.DestroyCampsNear(e.Position, e.CivilizationIndex);
        civ.SetStorageCapacityCache(1000, 1000);
        civ.AddResource(Resource.Brick, 10);
        civ.AddResource(Resource.Wood, 10);
        civ.AddResource(Resource.Food, 15);

        var city = cityController.BuildCity(0, v1);

        Assert.NotNull(city);
        Assert.Equal(city!.MaxSoldiers, city.Soldiers);
        Assert.Equal(5, city.Soldiers);
    }

    [Fact]
    public void DestroyCampsInvalidatedByTerrain_RemovesCamp_WhenAllThreeHexesBecomeWater()
    {
        var (state, civ, v1, _, _) = RibbonIsland();
        civ.AddMobileCamp(new MobileCamp(v1) { CivilizationIndex = 0 });
        foreach (var hex in v1.GetHexes())
            state.GetMapFor(v1)!.GetTile(hex)!.TerrainType = TerrainType.Water;
        var (_, campController) = Controllers(state);

        var destroyed = campController.DestroyCampsInvalidatedByTerrain();

        Assert.Single(destroyed);
        Assert.Empty(civ.MobileCamps);
    }

    [Fact]
    public void DestroyCampsInvalidatedByTerrain_KeepsCamp_WhenAtLeastOneHexStaysLand()
    {
        var (state, civ, v1, _, _) = RibbonIsland();
        civ.AddMobileCamp(new MobileCamp(v1) { CivilizationIndex = 0 });
        var hexes = v1.GetHexes();
        state.GetMapFor(v1)!.GetTile(hexes[0])!.TerrainType = TerrainType.Water;
        state.GetMapFor(v1)!.GetTile(hexes[1])!.TerrainType = TerrainType.Water;
        var (_, campController) = Controllers(state);

        var destroyed = campController.DestroyCampsInvalidatedByTerrain();

        Assert.Empty(destroyed);
        Assert.Single(civ.MobileCamps);
    }

    [Fact]
    public void GetBuildCost_ReturnsFixedValues()
    {
        var cost = MobileCampController.GetBuildCost();
        Assert.Equal(100, cost[Resource.Stone]);
        Assert.Equal(100, cost[Resource.Brick]);
        Assert.Equal(100, cost[Resource.Ore]);
        Assert.Equal(200, cost[Resource.Food]);
        Assert.Equal(200, cost[Resource.Gold]);
    }

    private static void FundMobileCampCost(Civilization civ)
    {
        civ.SetStorageCapacityCache(1000, 1000);
        civ.AddResource(Resource.Stone, 100);
        civ.AddResource(Resource.Brick, 100);
        civ.AddResource(Resource.Ore, 100);
        civ.AddResource(Resource.Food, 200);
        civ.AddResource(Resource.Gold, 200);
    }

    [Fact]
    public void BuildMobileCamp_SetsCreatedTickToCurrentClockTick()
    {
        var (state, civ, v1, _, v2) = RibbonIsland();
        civ.AddCity(new City(v1) { CivilizationIndex = 0 });
        GrantTech(civ);
        FundMobileCampCost(civ);

        var clock = new GameClock();
        clock.Start();
        var (_, campController) = Controllers(state, clock);
        clock.SimulateAdvance(1200);

        var camp = campController.BuildMobileCamp(0, v2);

        Assert.NotNull(camp);
        Assert.Equal(clock.CurrentTick, camp!.CreatedTick);
    }

    [Fact]
    public void SelfDestruct_DoesNotDestroyCampBeforeIntervalElapsed()
    {
        // A Camp Mobile is no longer manually destructible (see ConstructionInteractionService) — it
        // only disappears via DestroyCampsNear (nearby allied city) or, now, this self-destruct timer.
        var (state, civ, v1, _, v2) = RibbonIsland();
        civ.AddCity(new City(v1) { CivilizationIndex = 0 });
        GrantTech(civ);
        FundMobileCampCost(civ);

        var clock = new GameClock();
        clock.Start();
        var (_, campController) = Controllers(state, clock);
        var camp = campController.BuildMobileCamp(0, v2);
        Assert.NotNull(camp);

        clock.SimulateAdvance(MobileCampController.SelfDestructIntervalTicks - 100);

        Assert.Contains(civ.MobileCamps, c => c == camp);
    }

    [Fact]
    public void SelfDestruct_DestroysCampAfterIntervalElapsed()
    {
        var (state, civ, v1, _, v2) = RibbonIsland();
        civ.AddCity(new City(v1) { CivilizationIndex = 0 });
        GrantTech(civ);
        FundMobileCampCost(civ);

        var clock = new GameClock();
        clock.Start();
        var (_, campController) = Controllers(state, clock);
        var camp = campController.BuildMobileCamp(0, v2);
        Assert.NotNull(camp);

        clock.SimulateAdvance(MobileCampController.SelfDestructIntervalTicks + 100);

        Assert.DoesNotContain(civ.MobileCamps, c => c == camp);
    }

    [Fact]
    public void SelfDestruct_DestroysCampImmediately_WhenNotVisibleToOwner()
    {
        // Same relative offsets as RibbonIsland's own vertex (translated far away, so no road or city
        // covers it) — a camp with no road left connecting it to the civilization's territory (e.g.
        // pruned by RoadController.RemoveDisconnectedRoads after a distant city was destroyed) must be
        // treated as lost immediately, well before SelfDestructIntervalTicks would fire on its own.
        var hCamp1 = H(10, 10);
        var hCamp2 = H(11, 10);
        var hCamp3 = H(10, 11);
        var map = new IslandMap(new HexTile[]
        {
            new(hCamp1, TerrainType.Plain),
            new(hCamp2, TerrainType.Plain),
            new(hCamp3, TerrainType.Plain),
        });
        var civ = new Civilization { Index = 0 };
        var state = new WorldState(map, new List<Civilization> { civ }, AtlasController.InvalidIslandId);
        var vCamp = Vertex.Create(hCamp1, hCamp2, hCamp3);
        civ.AddMobileCamp(new MobileCamp(vCamp) { CivilizationIndex = 0 });

        var clock = new GameClock();
        clock.Start();
        Controllers(state, clock);

        clock.SimulateAdvance(100);

        Assert.Empty(civ.MobileCamps);
    }

    [Fact]
    public void SelfDestruct_KeepsCamp_WhileStillVisibleToOwner()
    {
        // Control case for SelfDestruct_DestroysCampImmediately_WhenNotVisibleToOwner: a camp built
        // normally (touching a road, per BuildMobileCamp) stays put well before the self-destruct
        // timer, exactly as SelfDestruct_DoesNotDestroyCampBeforeIntervalElapsed already checks — this
        // confirms the new visibility check does not fire a false positive on it.
        var (state, civ, v1, _, v2) = RibbonIsland();
        civ.AddCity(new City(v1) { CivilizationIndex = 0 });
        GrantTech(civ);
        FundMobileCampCost(civ);

        var clock = new GameClock();
        clock.Start();
        var (_, campController) = Controllers(state, clock);
        var camp = campController.BuildMobileCamp(0, v2);
        Assert.NotNull(camp);

        clock.SimulateAdvance(100);

        Assert.Contains(civ.MobileCamps, c => c == camp);
    }

    [Fact]
    public void GetRemainingSelfDestructTicks_CountsDownFromCreation()
    {
        var (state, civ, v1, _, v2) = RibbonIsland();
        civ.AddCity(new City(v1) { CivilizationIndex = 0 });
        GrantTech(civ);
        FundMobileCampCost(civ);

        var clock = new GameClock();
        clock.Start();
        var (_, campController) = Controllers(state, clock);
        var camp = campController.BuildMobileCamp(0, v2)!;

        clock.SimulateAdvance(5000);

        Assert.Equal(MobileCampController.SelfDestructIntervalTicks - 5000,
            campController.GetRemainingSelfDestructTicks(camp, clock.CurrentTick));
    }
}
