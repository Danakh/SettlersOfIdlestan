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
/// Balises maritimes et Flottes de Guerre vis-à-vis de la construction de ville classique — voir
/// aussi WarFleetControllerTests pour la construction de la flotte elle-même.
///
/// Layout partagé par ces tests (un "ruban" de hex en ligne) :
///   h1(0,0) — h2(1,0) — h3(0,1) — h4(1,1) — h5(0,2)
///   V1      = Vertex(h1,h2,h3) — touche la terre (h1)
///   VMiddle = Vertex(h2,h3,h4) — adjacent à V1 (arête h2-h3), sert de vertex balise/flotte
///   V2      = Vertex(h3,h4,h5) — adjacent à VMiddle (arête h3-h4), à distance 2 de V1
/// </summary>
public class CityBuilderControllerTests
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

    private static CityBuilderController Controller(WorldState state)
    {
        var controller = new CityBuilderController();
        controller.Initialize(state);
        return controller;
    }

    [Fact]
    public void GetBuildableVertices_ExcludesVertexWithMaritimeBeacon()
    {
        var (state, civ, v1, vMiddle, _) = RibbonIsland();
        civ.AddMaritimeBeacon(new MaritimeBeacon(vMiddle) { CivilizationIndex = 0 });

        var vertices = Controller(state).GetBuildableVertices(0);

        Assert.Contains(vertices, v => v.Equals(v1));
        Assert.DoesNotContain(vertices, v => v.Equals(vMiddle));
    }

    [Fact]
    public void GetBuildableVertices_ExcludesExactVertexOfOwnFleet()
    {
        var (state, civ, _, vMiddle, _) = RibbonIsland();
        civ.AddMaritimeBeacon(new MaritimeBeacon(vMiddle) { CivilizationIndex = 0 });
        civ.AddFleet(new WarFleet(vMiddle) { CivilizationIndex = 0 });

        var vertices = Controller(state).GetBuildableVertices(0);

        Assert.DoesNotContain(vertices, v => v.Equals(vMiddle));
    }

    [Fact]
    public void GetBuildableVertices_SameCivilizationFleet_DoesNotBlockNearbyCity()
    {
        var (state, civ, _, vMiddle, v2) = RibbonIsland();
        civ.AddMaritimeBeacon(new MaritimeBeacon(vMiddle) { CivilizationIndex = 0 });
        civ.AddFleet(new WarFleet(vMiddle) { CivilizationIndex = 0 });

        // v2 is only 1 edge away from the fleet — well under MinDistanceBetweenCivilizationCities (3),
        // which would block a normal city but must not block because a Flotte de Guerre lives outside
        // Civilization.Cities entirely (see IMilitaryVertex) and never enters the distance check.
        var vertices = Controller(state).GetBuildableVertices(0);

        Assert.Contains(vertices, v => v.Equals(v2));
    }

    [Fact]
    public void GetBuildableVertices_SameCivilizationRealCity_StillBlocksNearbyCity()
    {
        var (state, civ, _, vMiddle, v2) = RibbonIsland();
        civ.AddCity(new City(vMiddle) { CivilizationIndex = 0 });

        // Sanity check: a real city (not a fleet) still enforces the normal minimum-distance rule.
        var vertices = Controller(state).GetBuildableVertices(0);

        Assert.DoesNotContain(vertices, v => v.Equals(v2));
    }

    [Fact]
    public void GetBuildableVertices_EnemyFleet_DoesNotBlockNearbyCity()
    {
        var (state, civ, _, vMiddle, v2) = RibbonIsland();
        var enemyCiv = new Civilization { Index = 1 };
        state.Civilizations.Add(enemyCiv);
        enemyCiv.AddMaritimeBeacon(new MaritimeBeacon(vMiddle) { CivilizationIndex = 1 });
        enemyCiv.AddFleet(new WarFleet(vMiddle) { CivilizationIndex = 1 });

        // v2 is only 1 edge away from the enemy fleet — under MinDistanceBetweenCities (2), which
        // would block a normal enemy city but must not block because of a fleet.
        var vertices = Controller(state).GetBuildableVertices(0);

        Assert.Contains(vertices, v => v.Equals(v2));
    }

    [Fact]
    public void GetBuildableVertices_EnemyRealCity_StillBlocksNearbyCity()
    {
        var (state, civ, _, vMiddle, v2) = RibbonIsland();
        var enemyCiv = new Civilization { Index = 1 };
        state.Civilizations.Add(enemyCiv);
        enemyCiv.AddCity(new City(vMiddle) { CivilizationIndex = 1 });

        var vertices = Controller(state).GetBuildableVertices(0);

        Assert.DoesNotContain(vertices, v => v.Equals(v2));
    }
}
