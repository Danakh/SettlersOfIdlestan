using SettlersOfIdlestan.Controller.Generator;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.IslandFeatures;
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
    public void GetBuildableVertices_IncludesExactVertexOfOwnMobileCamp()
    {
        // Unlike a fleet or a beacon, a Camp Mobile of this same civilization must not block city
        // construction at its own vertex — building the city directly replaces the camp (see
        // ConstructionInteractionService, which now prioritizes city construction on a double-click
        // over manual camp destruction), and the Builder's Guild automation relies on the same model
        // behavior to replace a camp with an outpost.
        var (state, civ, _, vMiddle, _) = RibbonIsland();
        civ.AddMobileCamp(new MobileCamp(vMiddle) { CivilizationIndex = 0 });

        var vertices = Controller(state).GetBuildableVertices(0);

        Assert.Contains(vertices, v => v.Equals(vMiddle));
    }

    [Fact]
    public void GetBuildableVertices_ExcludesExactVertexOfEnemyMobileCamp()
    {
        var (state, civ, _, vMiddle, _) = RibbonIsland();
        var enemyCiv = new Civilization { Index = 1 };
        state.Civilizations.Add(enemyCiv);
        enemyCiv.AddMobileCamp(new MobileCamp(vMiddle) { CivilizationIndex = 1 });

        var vertices = Controller(state).GetBuildableVertices(0);

        Assert.DoesNotContain(vertices, v => v.Equals(vMiddle));
    }

    [Fact]
    public void BuildCity_SucceedsOnTopOfOwnMobileCamp()
    {
        var (state, civ, _, vMiddle, _) = RibbonIsland();
        civ.AddMobileCamp(new MobileCamp(vMiddle) { CivilizationIndex = 0 });
        civ.SetStorageCapacityCache(1000, 1000);
        civ.AddResource(Resource.Brick, 10);
        civ.AddResource(Resource.Wood, 10);
        civ.AddResource(Resource.Food, 15);

        var city = Controller(state).BuildCity(0, vMiddle);

        Assert.NotNull(city);
        Assert.Equal(vMiddle, city!.Position);
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

    [Fact]
    public void GetRelocationTargets_CivilizationHasCitiesOnAnotherLayer_DoesNotThrow()
    {
        // Reproduit un crash observé en jeu : une civilisation avec des villes en Inframonde (Z différent)
        // fait remonter des vertex de Z différent dans le bassin de candidats de GetBuildableVertices
        // (les routes de toutes les couches sont mélangées dans GetRoadTouchingVertices). GetRelocationTargets
        // comparait alors origin.EdgeDistanceTo(v) sans filtrer sur Z, et EdgeDistanceTo lève une
        // ArgumentException via EnsureSameZ dès que Z diffère.
        var (state, civ, v1, _, _) = RibbonIsland();
        var city = new City(v1) { CivilizationIndex = 0 };
        civ.AddCity(city);

        var underworldLayer = LayerState.EstablishOupostInNewAutoExpandLayer(civ);
        state.AddLayer(LayerState.UnderworldZ, underworldLayer);

        // Route loin de l'avant-poste (0,0)-(1,0)-(0,1) pour que le vertex généré ne soit pas
        // exclu par la distance minimale entre villes propres — on veut que la couche Inframonde
        // contribue bien un vertex candidat, pas qu'il soit filtré pour une autre raison.
        var h1u = new HexCoord(10, 10, LayerState.UnderworldZ);
        var h2u = new HexCoord(11, 10, LayerState.UnderworldZ);
        civ.AddRoad(new Road(Edge.Create(h1u, h2u)) { CivilizationIndex = 0 });

        var targets = Controller(state).GetRelocationTargets(city);

        Assert.All(targets, v => Assert.Equal(v1.Z, v.Z));
    }

    [Fact]
    public void RelocateCity_OntoHexWithTreasureTrove_ClaimsTheTreasure()
    {
        var (state, civ, v1, vMiddle, _) = RibbonIsland();
        var city = new City(v1) { CivilizationIndex = 0 };
        civ.AddCity(city);
        civ.SetStorageCapacityCache(1000, 1000);
        civ.AddResource(Resource.Gold, CityBuilderController.RelocationCost()[Resource.Gold]);
        civ.AddResource(Resource.Food, CityBuilderController.RelocationCost()[Resource.Food]);

        // h4 is only adjacent to vMiddle (and v2) on this ribbon, not v1 — so the trove is unclaimed
        // until the city relocates onto a vertex touching h4.
        var trove = new TreasureTrove(H(1, 1));
        state.AddFeature(trove);

        var goldBefore = civ.GetResourceQuantity(Resource.Gold);

        var relocated = Controller(state).RelocateCity(city, vMiddle);

        Assert.True(relocated);
        Assert.DoesNotContain(state.GetFeaturesAt(H(1, 1)), f => f is TreasureTrove);
        Assert.Equal(goldBefore - CityBuilderController.RelocationCost()[Resource.Gold] + 100, civ.GetResourceQuantity(Resource.Gold));
        Assert.Equal(1, state.RunRecord.TreasuresTroveClaimed);
    }

    // ── NewCityBuildingCostFor : surcharge par nombre de villes existantes ──

    [Fact]
    public void NewCityBuildingCostFor_ExtraSurfaceCities_ScalesLinearly()
    {
        var (state, civ, v1, vMiddle, v2) = RibbonIsland();
        civ.AddCity(new City(v1) { CivilizationIndex = 0 });
        civ.AddCity(new City(vMiddle) { CivilizationIndex = 0 });
        civ.AddCity(new City(v2) { CivilizationIndex = 0 });

        // 3 villes de surface -> 2 "supplémentaires" (la ville de départ ne compte pas) ->
        // multiplicateur 1 + 0.1 * 2 = 1.2.
        var cost = Controller(state).NewCityBuildingCostFor(v1, civ);

        Assert.Equal(12, cost[Resource.Brick]);
        Assert.Equal(12, cost[Resource.Wood]);
        Assert.Equal(18, cost[Resource.Food]);
    }

    [Fact]
    public void NewCityBuildingCostFor_UnderworldCities_ScalesSuperLinearly()
    {
        var (state, civ, v1, _, _) = RibbonIsland();
        civ.AddCity(new City(v1) { CivilizationIndex = 0 });

        var hu1 = new HexCoord(0, 0, LayerState.UnderworldZ);
        var hu2 = new HexCoord(1, 0, LayerState.UnderworldZ);
        var hu3 = new HexCoord(0, 1, LayerState.UnderworldZ);
        var hu4 = new HexCoord(1, 1, LayerState.UnderworldZ);
        var hu5 = new HexCoord(0, 2, LayerState.UnderworldZ);
        var underworldTarget = Vertex.Create(hu1, hu2, hu3);
        civ.AddCity(new City(underworldTarget) { CivilizationIndex = 0 });
        civ.AddCity(new City(Vertex.Create(hu3, hu4, hu5)) { CivilizationIndex = 0 });

        // 2 villes d'Inframonde -> surcharge en Math.Pow(2, 1.5) ≈ 2.828, avec un facteur exponentiel
        // additionnel 2^(2/100) ≈ 1.014 (doublement tous les 100 villes) -> multiplicateur
        // 1 + 0.5 * 2.828 * 1.014 ≈ 2.434.
        var cost = Controller(state).NewCityBuildingCostFor(underworldTarget, civ);

        Assert.Equal(24, cost[Resource.Gold]);
        Assert.Equal(24, cost[Resource.Brick]);
        Assert.Equal(24, cost[Resource.Wood]);
        Assert.Equal(37, cost[Resource.Food]);
    }

    [Fact]
    public void NewCityBuildingCostFor_UnderworldCities_DoublesEvery100Cities()
    {
        var (state, civ, v1, _, _) = RibbonIsland();
        civ.AddCity(new City(v1) { CivilizationIndex = 0 });

        Vertex? target = null;
        for (int i = 0; i < 100; i++)
        {
            int q = i * 10; // Espacé pour que les triangles de villes successives ne partagent aucun hexagone.
            var vertex = Vertex.Create(
                new HexCoord(q, 0, LayerState.UnderworldZ),
                new HexCoord(q + 1, 0, LayerState.UnderworldZ),
                new HexCoord(q, 1, LayerState.UnderworldZ));
            civ.AddCity(new City(vertex) { CivilizationIndex = 0 });
            target ??= vertex;
        }

        // 100 villes d'Inframonde -> surcharge en Math.Pow(100, 1.5) = 1000, avec le facteur
        // exponentiel additionnel 2^(100/100) = 2 (doublement tous les 100 villes) ->
        // multiplicateur 1 + 0.5 * 1000 * 2 = 1001.
        var cost = Controller(state).NewCityBuildingCostFor(target!, civ);

        Assert.Equal(10010, cost[Resource.Gold]);
        Assert.Equal(10010, cost[Resource.Brick]);
        Assert.Equal(10010, cost[Resource.Wood]);
        Assert.Equal(15015, cost[Resource.Food]);
    }

    [Fact]
    public void NewCityBuildingCostFor_AbyssCities_ScalesQuadratically()
    {
        var (state, civ, v1, _, _) = RibbonIsland();
        civ.AddCity(new City(v1) { CivilizationIndex = 0 });

        var ha1 = new HexCoord(0, 0, LayerState.AbyssZ);
        var ha2 = new HexCoord(1, 0, LayerState.AbyssZ);
        var ha3 = new HexCoord(0, 1, LayerState.AbyssZ);
        var ha4 = new HexCoord(1, 1, LayerState.AbyssZ);
        var ha5 = new HexCoord(0, 2, LayerState.AbyssZ);
        var abyssTarget = Vertex.Create(ha1, ha2, ha3);
        civ.AddCity(new City(abyssTarget) { CivilizationIndex = 0 });
        civ.AddCity(new City(Vertex.Create(ha3, ha4, ha5)) { CivilizationIndex = 0 });

        // 2 villes d'Abysses -> surcharge en Math.Pow(2, 2) = 4 -> multiplicateur 1 + 1.0 * 4 = 5.
        var cost = Controller(state).NewCityBuildingCostFor(abyssTarget, civ);

        Assert.Equal(50, cost[Resource.Gold]);
        Assert.Equal(25, cost[Resource.Crystal]);
        Assert.Equal(50, cost[Resource.Brick]);
        Assert.Equal(50, cost[Resource.Wood]);
        Assert.Equal(75, cost[Resource.Food]);
    }

    [Fact]
    public void NewCityBuildingCostFor_BuildersGuild_HalvesUnderworldSurcharge()
    {
        var (state, civ, v1, _, _) = RibbonIsland();
        var startingCity = new City(v1) { CivilizationIndex = 0 };
        startingCity.AddBuilding(new BuildersGuild { Level = 1 });
        civ.AddCity(startingCity);

        var hu1 = new HexCoord(0, 0, LayerState.UnderworldZ);
        var hu2 = new HexCoord(1, 0, LayerState.UnderworldZ);
        var hu3 = new HexCoord(0, 1, LayerState.UnderworldZ);
        var hu4 = new HexCoord(1, 1, LayerState.UnderworldZ);
        var hu5 = new HexCoord(0, 2, LayerState.UnderworldZ);
        var underworldTarget = Vertex.Create(hu1, hu2, hu3);
        civ.AddCity(new City(underworldTarget) { CivilizationIndex = 0 });
        civ.AddCity(new City(Vertex.Create(hu3, hu4, hu5)) { CivilizationIndex = 0 });

        // Avec la Guilde des Bâtisseurs (facteur 0.5), la surcharge de 2 villes d'Inframonde devient
        // Math.Pow(0.5 * 2, 1.5) = 1. Le facteur exponentiel additionnel se base sur le nombre réel de
        // villes (2, non réduit par la guilde) -> 2^(2/100) ≈ 1.014 -> multiplicateur
        // 1 + 0.5 * 1 * 1.014 ≈ 1.507, contre 2.434 sans la guilde.
        var cost = Controller(state).NewCityBuildingCostFor(underworldTarget, civ);

        Assert.Equal(15, cost[Resource.Gold]);
        Assert.Equal(15, cost[Resource.Brick]);
        Assert.Equal(15, cost[Resource.Wood]);
        Assert.Equal(23, cost[Resource.Food]);
    }
}
