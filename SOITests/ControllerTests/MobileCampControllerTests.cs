using SettlersOfIdlestan.Controller.Generator;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Civilization;
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

    private static (CityBuilderController city, MobileCampController camp) Controllers(WorldState state)
    {
        var cityController = new CityBuilderController();
        cityController.Initialize(state);
        var campController = new MobileCampController();
        campController.Initialize(state, cityController);
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
        state.Civilizations.Add(enemyCiv);
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
        state.Civilizations.Add(enemyCiv);
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
        state.Civilizations.Add(enemyCiv);
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
    public void GetBuildCost_ReturnsFixedValues()
    {
        var cost = MobileCampController.GetBuildCost();
        Assert.Equal(100, cost[Resource.Stone]);
        Assert.Equal(100, cost[Resource.Brick]);
        Assert.Equal(100, cost[Resource.Ore]);
        Assert.Equal(200, cost[Resource.Food]);
        Assert.Equal(200, cost[Resource.Gold]);
    }
}
