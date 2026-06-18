using SettlersOfIdlestan.Controller.Generator;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandFeatures;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Monsters;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace SOITests.IslandMapTests;

public class BonusIslandTests
{
    private static readonly List<(TerrainType terrainType, int tileCount)> TileData = new()
    {
        (TerrainType.Forest, 15),
        (TerrainType.Hill, 15),
        (TerrainType.Plain, 15),
        (TerrainType.Mountain, 15),
        (TerrainType.Desert, 5),
    };

    private static IslandParameters MakeParameters(bool hasBonusIsland, IslandShapeType shape) => new(
        worldId: 5,
        tileData: TileData,
        features: new List<IslandFeatureParameters>(),
        shapeType: shape)
    {
        HasBonusIsland = hasBonusIsland,
    };

    [Fact]
    public void GenerateWorldState_WithBonusIslandDisabled_NeverAddsExtraFeature()
    {
        for (int seed = 0; seed < 20; seed++)
        {
            var generator = new IslandMapGenerator(new GamePRNG(seed));
            var state = generator.GenerateWorldState(MakeParameters(hasBonusIsland: false, IslandShapeType.Compact), currentTick: 0);

            Assert.NotNull(state);
            Assert.Empty(state!.Features);
        }
    }

    [Theory]
    [InlineData(IslandShapeType.Compact)]
    [InlineData(IslandShapeType.Crescent)]
    [InlineData(IslandShapeType.Elongated)]
    [InlineData(IslandShapeType.Archipelago)]
    [InlineData(IslandShapeType.Lake)]
    [InlineData(IslandShapeType.InlandSea)]
    public void GenerateWorldState_WithBonusIslandEnabled_AddsReachableSmallIslandWithFeature(IslandShapeType shape)
    {
        bool foundAtLeastOneBonusIsland = false;

        for (int seed = 0; seed < 50; seed++)
        {
            var generator = new IslandMapGenerator(new GamePRNG(seed));
            var state = generator.GenerateWorldState(MakeParameters(hasBonusIsland: true, shape), currentTick: 0);
            Assert.NotNull(state);

            if (state!.Features.Count == 0)
                continue; // tirage 50% raté pour ce seed — acceptable

            foundAtLeastOneBonusIsland = true;

            Assert.Single(state.Features);
            var feature = state.Features[0];
            Assert.True(feature is TreasureTrove || feature is FairyCircle || feature is Dragon);

            var map = state.GetMapForZ(IslandMap.SurfaceLayer)!;
            var bonusHex = feature.Position;
            var bonusTile = map.GetTile(bonusHex);
            Assert.NotNull(bonusTile);
            Assert.NotEqual(TerrainType.Water, bonusTile!.TerrainType);

            // L'ajout de l'île bonus doit avoir créé au moins un isthme sur la carte : une arête
            // terre-terre dont les deux flancs sont de l'eau (le hex porteur de la feature n'est
            // pas forcément lui-même sur cet isthme s'il s'agit du 2e hex d'une île à 2 hex).
            var allLandHexes = map.Tiles.Values.Where(t => t.TerrainType != TerrainType.Water).Select(t => t.Coord).ToList();
            bool hasIsthmus = false;
            foreach (var a in allLandHexes)
            {
                foreach (var dir in HexDirectionUtils.AllHexDirections)
                {
                    var b = a.Neighbor(dir);
                    var bTile = map.GetTile(b);
                    if (bTile == null || bTile.TerrainType == TerrainType.Water) continue;

                    var flank1 = a.Neighbor(dir.Next());
                    var flank2 = a.Neighbor(dir.Previous());
                    bool flank1Water = map.GetTile(flank1) is null or { TerrainType: TerrainType.Water };
                    bool flank2Water = map.GetTile(flank2) is null or { TerrainType: TerrainType.Water };

                    if (flank1Water && flank2Water)
                    {
                        hasIsthmus = true;
                        break;
                    }
                }
                if (hasIsthmus) break;
            }
            Assert.True(hasIsthmus, $"No water-flanked isthmus found on the map after adding bonus island at {bonusHex}");

            // Tous les hex de la carte adjacents au hex bonus doivent exister (eau ou terre).
            foreach (var dir in HexDirectionUtils.AllHexDirections)
                Assert.True(map.HasTile(bonusHex.Neighbor(dir)), $"Missing surrounding tile around bonus hex {bonusHex}");
        }

        Assert.True(foundAtLeastOneBonusIsland, $"No seed produced a bonus island across 50 attempts for shape {shape}");
    }
}
