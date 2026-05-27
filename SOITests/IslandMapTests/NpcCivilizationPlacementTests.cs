using System;
using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Controller.Generator;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.IslandMap;
using Xunit;

namespace SOITests.IslandMapTests;

public class NpcCivilizationPlacementTests
{
    private static readonly IEnumerable<(TerrainType, int)> TileData32 =
    [
        (TerrainType.Forest,   8),
        (TerrainType.Hill,     8),
        (TerrainType.Plain,    8),
        (TerrainType.Mountain, 8),
    ];

    private const int MinEdgeDistance = 7;

    private static IslandState CreateIsland(IslandShapeType shape, int npcCount)
    {
        var parameters = new IslandParameters(
            islandID: 0,
            tileData: TileData32,
            civilizationCount: 1,
            shapeType: shape)
        {
            NpcCivilizations = Enumerable.Range(0, npcCount)
                .Select(_ => new NpcParameters { EvolutionLevel = NpcEvolutionLevel.Minimum })
                .ToList()
        };

        var state = new IslandMapGenerator().GenerateIslandState(parameters, currentTick: 0);
        Assert.NotNull(state);
        return state;
    }

    public static IEnumerable<object[]> ShapeAndNpcCounts()
    {
        foreach (var shape in Enum.GetValues<IslandShapeType>())
            for (int npc = 1; npc <= 3; npc++)
                yield return [shape, npc];
    }

    [Theory]
    [MemberData(nameof(ShapeAndNpcCounts))]
    public void AllCities_HaveMinimumEdgeDistanceBetweenThem(IslandShapeType shape, int npcCount)
    {
        var state = CreateIsland(shape, npcCount);

        Assert.Equal(npcCount + 1, state.Civilizations.Count);

        var positions = state.Civilizations
            .SelectMany(c => c.Cities)
            .Select(c => c.Position)
            .ToList();

        Assert.Equal(npcCount + 1, positions.Count);

        for (int i = 0; i < positions.Count; i++)
        {
            for (int j = i + 1; j < positions.Count; j++)
            {
                int dist = positions[i].EdgeDistanceTo(positions[j]);
                Assert.True(dist >= MinEdgeDistance,
                    $"[{shape}, {npcCount + 1} civs] villes {i} et {j} : distance {dist} < {MinEdgeDistance}");
            }
        }
    }

    [Theory]
    [MemberData(nameof(ShapeAndNpcCounts))]
    public void NpcCity_HasTownHallLevel2_MarketAndWarehouse(IslandShapeType shape, int npcCount)
    {
        var state = CreateIsland(shape, npcCount);

        foreach (var civ in state.Civilizations.Where(c => c.IsNpc))
        {
            Assert.Single(civ.Cities);
            var city = civ.Cities[0];

            var townHall = city.Buildings.FirstOrDefault(b => b.Type == BuildingType.TownHall);
            Assert.NotNull(townHall);
            Assert.Equal(2, townHall.Level);

            Assert.Contains(city.Buildings, b => b.Type == BuildingType.Market);
            Assert.Contains(city.Buildings, b => b.Type == BuildingType.Warehouse);
        }
    }

    [Theory]
    [MemberData(nameof(ShapeAndNpcCounts))]
    public void NpcCity_HasMaxResources(IslandShapeType shape, int npcCount)
    {
        var state = CreateIsland(shape, npcCount);

        foreach (var civ in state.Civilizations.Where(c => c.IsNpc))
        {
            foreach (Resource resource in Enum.GetValues<Resource>())
            {
                int max = civ.GetResourceMaxQuantity(resource);
                if (max > 0)
                    Assert.Equal(max, civ.GetResourceQuantity(resource));
            }
        }
    }

    [Theory]
    [MemberData(nameof(ShapeAndNpcCounts))]
    public void NpcCity_HasProductionBuildingsMatchingAdjacentTerrain(IslandShapeType shape, int npcCount)
    {
        var state = CreateIsland(shape, npcCount);

        foreach (var civ in state.Civilizations.Where(c => c.IsNpc))
        {
            var city = civ.Cities[0];
            var hexes = city.Position.GetHexes();

            foreach (var hex in hexes)
            {
                var tile = state.Map.GetTile(hex);
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
}
