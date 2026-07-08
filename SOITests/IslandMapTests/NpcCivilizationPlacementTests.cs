using System;
using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Controller.Generator;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.IslandMap;
using Xunit;

namespace SOITests.IslandMapTests;

public class NpcCivilizationPlacementTests
{
    private static readonly IEnumerable<(TerrainType, int)> TileData40 =
    [
        (TerrainType.Forest,   11),
        (TerrainType.Hill,     11),
        (TerrainType.Plain,    11),
        (TerrainType.Mountain, 11),
    ];

    // Île grande pour les tests d'évolution avancée (Medium/Strong = 5-7 villes NPC).
    // 20 tuiles par type suffisent maintenant que l'expansion nettoie les routes orphelines.
    private static readonly IEnumerable<(TerrainType, int)> TileData80 =
    [
        (TerrainType.Forest,   20),
        (TerrainType.Hill,     20),
        (TerrainType.Plain,    20),
        (TerrainType.Mountain, 20),
    ];

    private static WorldState CreateIsland(IslandShapeType shape, int npcCount,
        NpcEvolutionLevel level = NpcEvolutionLevel.Minimum)
    {
        var tileData = level is NpcEvolutionLevel.Medium or NpcEvolutionLevel.Strong
            ? TileData80
            : TileData40;

        var parameters = new IslandParameters(
            worldId: 0,
            tileData: tileData,
            shapeType: shape)
        {
            NpcCivilizations = Enumerable.Range(0, npcCount)
                .Select(_ => new NpcParameters { EvolutionLevel = level })
                .ToList()
        };

        var state = new IslandMapGenerator(new GamePRNG(42)).GenerateWorldState(parameters, currentTick: 0);
        Assert.NotNull(state);
        return state;
    }

    private static bool IsProductionBuilding(BuildingType type) => type
        is BuildingType.Sawmill or BuildingType.Mill or BuildingType.Brickworks
        or BuildingType.Quarry or BuildingType.Mine or BuildingType.Seaport;

    // ── Placement initial (toutes les shapes, 1–3 NPC) ───────────────────

    public static IEnumerable<object[]> ShapeAndNpcCounts()
    {
        foreach (var shape in Enum.GetValues<IslandShapeType>())
            for (int npc = 1; npc <= 3; npc++)
                yield return [shape, npc];
    }

    [Theory]
    [MemberData(nameof(ShapeAndNpcCounts))]
    public void AllNpcCivilizations_ArePlacedAndRespectMinPlayerDistance(IslandShapeType shape, int npcCount)
    {
        var state = CreateIsland(shape, npcCount);

        var playerCity = state.PlayerCivilization.Cities[0].Position;
        var npcCivs = state.Civilizations.Where(c => c.IsNpc).ToList();

        // Toutes les civilisations NPC demandées doivent avoir été placées
        Assert.All(npcCivs, civ => Assert.NotEmpty(civ.Cities));

        foreach (var civ in npcCivs)
        {
            foreach (var city in civ.Cities)
            {
                int dist = city.Position.EdgeDistanceTo(playerCity);
                Assert.True(dist >= NpcCivilizationPlacer.DefaultMinPlayerDistance,
                    $"[{shape}, {npcCount} NPC] civ {civ.Index} ville à distance {dist} < {NpcCivilizationPlacer.DefaultMinPlayerDistance} du joueur");
            }
        }
    }

    [Theory]
    [MemberData(nameof(ShapeAndNpcCounts))]
    public void NpcCity_HasTownHallLevel2_Market(IslandShapeType shape, int npcCount)
    {
        var state = CreateIsland(shape, npcCount);

        foreach (var civ in state.Civilizations.Where(c => c.IsNpc))
        {
            Assert.NotEmpty(civ.Cities); // toutes les civs NPC doivent avoir été placées
            var city = civ.Cities[0]; // ville initiale, avant expansion

            var townHall = city.Buildings.FirstOrDefault(b => b.Type == BuildingType.TownHall);
            Assert.NotNull(townHall);
            Assert.Equal(2, townHall.Level);

            Assert.Contains(city.Buildings, b => b.Type == BuildingType.Market);
        }
    }

    [Theory]
    [MemberData(nameof(ShapeAndNpcCounts))]
    public void NpcCiv_HasMaritimeRoutesUnlocked(IslandShapeType shape, int npcCount)
    {
        var state = CreateIsland(shape, npcCount);

        foreach (var civ in state.Civilizations.Where(c => c.IsNpc))
        {
            Assert.NotEmpty(civ.Cities);
            Assert.True(civ.ModifierAggregator.HasModifier(Modifier.ECategory.UNLOCK_MARITIME_ROUTES),
                $"[{shape}, {npcCount} NPC] civ {civ.Index} n'a pas UNLOCK_MARITIME_ROUTES");
        }
    }

    [Theory]
    [MemberData(nameof(ShapeAndNpcCounts))]
    public void NpcCity_HasProductionBuildingsMatchingAdjacentTerrain(IslandShapeType shape, int npcCount)
    {
        var state = CreateIsland(shape, npcCount);

        foreach (var civ in state.Civilizations.Where(c => c.IsNpc))
        {
            Assert.NotEmpty(civ.Cities);
            var city = civ.Cities[0];
            var hexes = city.Position.GetHexes();

            foreach (var hex in hexes)
            {
                var tile = state.GetMapForZ(IslandMap.SurfaceLayer)!.GetTile(hex);
                if (tile == null) continue;

                switch (tile.TerrainType)
                {
                    case TerrainType.Forest:
                        Assert.Contains(city.Buildings, b => b.Type == BuildingType.Sawmill);
                        break;
                    case TerrainType.Plain:
                        Assert.Contains(city.Buildings, b => b.Type == BuildingType.Mill);
                        break;
                    case TerrainType.Hill:
                        Assert.Contains(city.Buildings, b => b.Type == BuildingType.Brickworks);
                        break;
                    case TerrainType.Mountain:
                        Assert.Contains(city.Buildings, b => b.Type == BuildingType.Quarry);
                        break;
                    case TerrainType.Water:
                        Assert.Contains(city.Buildings, b => b.Type == BuildingType.Seaport);
                        break;
                }
            }
        }
    }

    // ── Tests par niveau d'évolution (1 NPC, toutes les shapes) ─────────

    public static IEnumerable<object[]> ShapesAndEvolutionLevels()
    {
        foreach (var shape in Enum.GetValues<IslandShapeType>())
            foreach (var level in Enum.GetValues<NpcEvolutionLevel>())
                yield return [shape, level];
    }

    private static int ExpectedCityCount(NpcEvolutionLevel level) => level switch
    {
        NpcEvolutionLevel.Minimum => 1,
        NpcEvolutionLevel.Low     => 2,
        NpcEvolutionLevel.Medium  => 3,
        NpcEvolutionLevel.Strong  => 4,
        _                         => 1,
    };

    [Theory]
    [MemberData(nameof(ShapesAndEvolutionLevels))]
    public void NpcEvolution_HasExpectedCityCount(IslandShapeType shape, NpcEvolutionLevel level)
    {
        var state = CreateIsland(shape, npcCount: 1, level);
        var npcCiv = state.Civilizations.First(c => c.IsNpc);

        Assert.Equal(ExpectedCityCount(level), npcCiv.Cities.Count);
    }

    [Theory]
    [MemberData(nameof(ShapesAndEvolutionLevels))]
    public void NpcEvolution_AllCitiesHaveTownHallAndMarket(IslandShapeType shape, NpcEvolutionLevel level)
    {
        var state = CreateIsland(shape, npcCount: 1, level);
        var npcCiv = state.Civilizations.First(c => c.IsNpc);

        Assert.NotEmpty(npcCiv.Cities);

        foreach (var city in npcCiv.Cities)
        {
            Assert.Contains(city.Buildings, b => b.Type == BuildingType.TownHall);
            Assert.Contains(city.Buildings, b => b.Type == BuildingType.Market);
        }
    }

    [Theory]
    [InlineData(IslandShapeType.Compact)]
    [InlineData(IslandShapeType.Crescent)]
    [InlineData(IslandShapeType.Archipelago)]
    [InlineData(IslandShapeType.Elongated)]
    public void NpcMedium_HalfProductionBuildingsAreLevelTwoOrAbove(IslandShapeType shape)
    {
        var state = CreateIsland(shape, npcCount: 1, NpcEvolutionLevel.Medium);
        var npcCiv = state.Civilizations.First(c => c.IsNpc);

        Assert.NotEmpty(npcCiv.Cities);

        var prodBuildings = npcCiv.Cities
            .SelectMany(c => c.Buildings)
            .Where(b => IsProductionBuilding(b.Type))
            .ToList();

        Assert.NotEmpty(prodBuildings);

        int level3Plus = prodBuildings.Count(b => b.Level >= 2);
        int halfCount = (prodBuildings.Count + 1) / 2;
        Assert.True(level3Plus >= halfCount,
            $"[{shape}] attendu ≥ {halfCount} bâtiments de production niveau 2+, reçu {level3Plus}/{prodBuildings.Count}");
    }

    [Theory]
    [InlineData(IslandShapeType.Compact)]
    [InlineData(IslandShapeType.Crescent)]
    [InlineData(IslandShapeType.Archipelago)]
    [InlineData(IslandShapeType.Elongated)]
    public void NpcMedium_AllCitiesHaveWarehouse(IslandShapeType shape)
    {
        var state = CreateIsland(shape, npcCount: 1, NpcEvolutionLevel.Medium);
        var npcCiv = state.Civilizations.First(c => c.IsNpc);

        Assert.NotEmpty(npcCiv.Cities);

        foreach (var city in npcCiv.Cities)
            Assert.Contains(city.Buildings, b => b.Type == BuildingType.Warehouse);
    }

    [Theory]
    [InlineData(IslandShapeType.Compact)]
    [InlineData(IslandShapeType.Crescent)]
    [InlineData(IslandShapeType.Archipelago)]
    [InlineData(IslandShapeType.Elongated)]
    public void NpcStrong_FirstFiveCitiesHaveWarehouse(IslandShapeType shape)
    {
        // Les villes 1-5 héritent du step-2 de Medium (Warehouse inclus).
        // Les villes 6-7 sont des expansions step-1 sans Warehouse.
        var state = CreateIsland(shape, npcCount: 1, NpcEvolutionLevel.Strong);
        var npcCiv = state.Civilizations.First(c => c.IsNpc);

        Assert.NotEmpty(npcCiv.Cities);

        foreach (var city in npcCiv.Cities.Take(5))
            Assert.Contains(city.Buildings, b => b.Type == BuildingType.Warehouse);
    }

    [Theory]
    [MemberData(nameof(ShapesAndEvolutionLevels))]
    public void NpcEvolution_CitiesWithinSameCivilizationAreSpacedAtLeast3Edges(IslandShapeType shape, NpcEvolutionLevel level)
    {
        const int minIntraCivDistance = 3;

        var state = CreateIsland(shape, npcCount: 1, level);
        var npcCiv = state.Civilizations.First(c => c.IsNpc);

        Assert.NotEmpty(npcCiv.Cities);

        var positions = npcCiv.Cities.Select(c => c.Position).ToList();

        for (int i = 0; i < positions.Count; i++)
        {
            for (int j = i + 1; j < positions.Count; j++)
            {
                int dist = positions[i].EdgeDistanceTo(positions[j]);
                Assert.True(dist >= minIntraCivDistance,
                    $"[{shape}/{level}] villes {i} et {j} de la même civ : distance {dist} < {minIntraCivDistance}");
            }
        }
    }
}
