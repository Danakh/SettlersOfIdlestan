using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Controller.Generator;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.IslandMap;
using Xunit;

namespace SOITests.IslandMapTests;

/// <summary>
/// Génère 20 îles de suite sur plusieurs seeds et vérifie que toutes les villes NPC
/// respectent la distance minimale de 8 edges vis-à-vis de la ville de départ du joueur.
/// </summary>
public class IslandGenerationBatchTests
{
    private static readonly IEnumerable<(TerrainType, int)> StandardTileData =
    [
        (TerrainType.Forest,   15),
        (TerrainType.Hill,     15),
        (TerrainType.Plain,    15),
        (TerrainType.Mountain, 15),
    ];

    private static readonly IslandShapeType[] AllShapes = System.Enum.GetValues<IslandShapeType>();

    private static WorldState GenerateIsland(int seed, int islandIndex, int npcCount)
    {
        var shape = AllShapes[islandIndex % AllShapes.Length];
        var parameters = new IslandParameters(
            worldId: islandIndex,
            tileData: StandardTileData,
            shapeType: shape)
        {
            NpcCivilizations = Enumerable.Range(0, npcCount)
                .Select(_ => new NpcParameters { EvolutionLevel = NpcEvolutionLevel.Minimum })
                .ToList()
        };

        var state = new IslandMapGenerator(new GamePRNG(seed)).GenerateWorldState(parameters, currentTick: 0);
        Assert.NotNull(state);
        return state;
    }

    public static IEnumerable<object[]> Seeds()
    {
        yield return [1];
        yield return [42];
        yield return [100];
        yield return [999];
        yield return [12345];
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void Generate20Islands_NpcCitiesAlwaysAtLeast8EdgesFromPlayer(int baseSeed)
    {
        for (int i = 0; i < 20; i++)
        {
            int islandSeed = baseSeed * 37 + i;
            int npcCount = (i % 3) + 1;
            var shape = AllShapes[i % AllShapes.Length];

            var state = GenerateIsland(islandSeed, i, npcCount);
            var playerCity = state.PlayerCivilization.Cities[0].Position;

            foreach (var civ in state.Civilizations.Where(c => c.IsNpc))
            {
                foreach (var city in civ.Cities)
                {
                    int dist = city.Position.EdgeDistanceTo(playerCity);
                    Assert.True(dist >= NpcCivilizationPlacer.DefaultMinPlayerDistance,
                        $"[baseSeed={baseSeed}, île {i}, shape={shape}, {npcCount} NPC] " +
                        $"civ {civ.Index} ville à distance {dist} < {NpcCivilizationPlacer.DefaultMinPlayerDistance} du joueur");
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void Generate20Islands_PlayerCityAlwaysPlaced(int baseSeed)
    {
        for (int i = 0; i < 20; i++)
        {
            int islandSeed = baseSeed * 37 + i;
            int npcCount = (i % 3) + 1;

            var state = GenerateIsland(islandSeed, i, npcCount);

            Assert.NotEmpty(state.PlayerCivilization.Cities);
        }
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void Generate20Islands_AllNpcCivilizationsArePlaced(int baseSeed)
    {
        for (int i = 0; i < 20; i++)
        {
            int islandSeed = baseSeed * 37 + i;
            int npcCount = (i % 3) + 1;
            var shape = AllShapes[i % AllShapes.Length];

            var state = GenerateIsland(islandSeed, i, npcCount);

            var placedNpcCount = state.Civilizations.Count(c => c.IsNpc && c.Cities.Count > 0);
            Assert.True(placedNpcCount == npcCount,
                $"[baseSeed={baseSeed}, île {i}, shape={shape}] " +
                $"attendu {npcCount} NPC placés, reçu {placedNpcCount}");
        }
    }
}

/// <summary>
/// Même couverture que <see cref="IslandGenerationBatchTests"/>, mais en passant par
/// <see cref="AtlasController"/> pour obtenir les <see cref="IslandParameters"/> —
/// c'est-à-dire exactement le flux utilisé en jeu lors d'une campagne.
/// Un seul WorldPRNG est partagé entre AtlasController et IslandMapGenerator pour chaque seed,
/// comme dans <see cref="SettlersOfIdlestan.Controller.MainGameController"/>.
/// Les WorldIds 1..20 correspondent à la progression normale d'une campagne.
/// </summary>
public class IslandGenerationAtlasTests
{
    public static IEnumerable<object[]> Seeds()
    {
        yield return [1];
        yield return [42];
        yield return [100];
        yield return [999];
        yield return [12345];
    }

    private static IEnumerable<WorldState> GenerateCampaign(int seed, int islandCount)
    {
        var worldPrng = new GamePRNG(seed);
        var atlas = new AtlasController();
        atlas.Initialize(worldPrng);

        int worldId = atlas.GetFirstWorldId();
        for (int i = 0; i < islandCount; i++, worldId++)
        {
            var parameters = atlas.GetIslandParameters(worldId);
            var state = new IslandMapGenerator(worldPrng).GenerateWorldState(parameters, currentTick: 0);
            Assert.NotNull(state);
            yield return state;
        }
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void Generate20Islands_NpcCitiesAlwaysAtLeast8EdgesFromPlayer(int seed)
    {
        int worldId = new AtlasController().GetFirstWorldId();
        foreach (var state in GenerateCampaign(seed, islandCount: 20))
        {
            if (state.PlayerCivilization.Cities.Count == 0) { worldId++; continue; }
            var playerCity = state.PlayerCivilization.Cities[0].Position;

            foreach (var civ in state.Civilizations.Where(c => c.IsNpc))
            {
                foreach (var city in civ.Cities)
                {
                    int dist = city.Position.EdgeDistanceTo(playerCity);
                    Assert.True(dist >= NpcCivilizationPlacer.DefaultMinPlayerDistance,
                        $"[seed={seed}, worldId={worldId}] " +
                        $"civ {civ.Index} ville à distance {dist} < {NpcCivilizationPlacer.DefaultMinPlayerDistance} du joueur");
                }
            }
            worldId++;
        }
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void Generate20Islands_PlayerCityAlwaysPlaced(int seed)
    {
        int worldId = new AtlasController().GetFirstWorldId();
        foreach (var state in GenerateCampaign(seed, islandCount: 20))
        {
            Assert.NotEmpty(state.PlayerCivilization.Cities);
            worldId++;
        }
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void Generate20Islands_AllNpcCivilizationsArePlaced(int seed)
    {
        var atlasForIds = new AtlasController();
        atlasForIds.Initialize(new GamePRNG(seed));
        int worldId = atlasForIds.GetFirstWorldId();

        foreach (var state in GenerateCampaign(seed, islandCount: 20))
        {
            int expected = state.Civilizations.Count(c => c.IsNpc);
            int placed   = state.Civilizations.Count(c => c.IsNpc && c.Cities.Count > 0);
            Assert.True(placed == expected,
                $"[seed={seed}, worldId={worldId}] " +
                $"attendu {expected} NPC placés, reçu {placed}");
            worldId++;
        }
    }
}
