using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Controller.Generator;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using Xunit;

namespace SOITests.IslandMapTests
{
    /// <summary>
    /// Génération d'îles de l'Abysse : 3 à 5 hexes de terrain (Forest/Hill/Mountain/Plain/Water)
    /// entourés d'un anneau de Void, générés au-delà d'un hex de Void donné.
    /// </summary>
    public class AbyssIslandGeneratorTests
    {
        private static readonly HashSet<TerrainType> AllowedTerrains = new()
        {
            TerrainType.Forest, TerrainType.Hill, TerrainType.Mountain, TerrainType.Plain, TerrainType.Water,
        };

        private static (IslandMap map, HexCoord voidHex) CreateMapWithVoidHex()
        {
            var voidHex = new HexCoord(0, 0, LayerState.AbyssZ);
            var map = new IslandMap(new[] { new HexTile(voidHex, TerrainType.Void) }, LayerState.AbyssZ);
            return (map, voidHex);
        }

        [Fact]
        public void GeneratesIslandOfThreeToFiveHexes_WithAllowedTerrainOnly()
        {
            var (map, voidHex) = CreateMapWithVoidHex();
            var prng = new GamePRNG(1);

            var newTiles = AbyssIslandGenerator.GenerateIslandBeyondVoid(map, voidHex, prng);

            var islandTiles = newTiles.Where(t => t.TerrainType != TerrainType.Void).ToList();
            Assert.InRange(islandTiles.Count, AbyssIslandGenerator.MinIslandHexCount, AbyssIslandGenerator.MaxIslandHexCount);
            Assert.All(islandTiles, t => Assert.Contains(t.TerrainType, AllowedTerrains));
        }

        [Fact]
        public void SurroundsIslandWithVoidRing()
        {
            var (map, voidHex) = CreateMapWithVoidHex();
            var prng = new GamePRNG(1);

            var newTiles = AbyssIslandGenerator.GenerateIslandBeyondVoid(map, voidHex, prng);

            var islandCoords = new HashSet<HexCoord>(newTiles.Where(t => t.TerrainType != TerrainType.Void).Select(t => t.Coord));
            var ringTiles = newTiles.Where(t => t.TerrainType == TerrainType.Void).ToList();

            Assert.NotEmpty(ringTiles);
            foreach (var ring in ringTiles)
            {
                // Chaque hex de l'anneau touche au moins un hex de l'île.
                Assert.Contains(ring.Coord.Neighbors(), n => islandCoords.Contains(n));
            }
        }

        [Fact]
        public void IsDeterministic_ForSameSeed()
        {
            var (map1, voidHex1) = CreateMapWithVoidHex();
            var (map2, voidHex2) = CreateMapWithVoidHex();

            var result1 = AbyssIslandGenerator.GenerateIslandBeyondVoid(map1, voidHex1, new GamePRNG(42));
            var result2 = AbyssIslandGenerator.GenerateIslandBeyondVoid(map2, voidHex2, new GamePRNG(42));

            var set1 = result1.Select(t => (t.Coord, t.TerrainType)).OrderBy(t => t.Coord.Q).ThenBy(t => t.Coord.R).ToList();
            var set2 = result2.Select(t => (t.Coord, t.TerrainType)).OrderBy(t => t.Coord.Q).ThenBy(t => t.Coord.R).ToList();

            Assert.Equal(set1, set2);
        }

        [Fact]
        public void ReturnsEmpty_WhenVoidHexFullyEnclosed()
        {
            var voidHex = new HexCoord(0, 0, LayerState.AbyssZ);
            var tiles = new List<HexTile> { new(voidHex, TerrainType.Void) };
            foreach (var n in voidHex.Neighbors())
                tiles.Add(new HexTile(n, TerrainType.Plain));

            var map = new IslandMap(tiles, LayerState.AbyssZ);
            var prng = new GamePRNG(7);

            var newTiles = AbyssIslandGenerator.GenerateIslandBeyondVoid(map, voidHex, prng);

            Assert.Empty(newTiles);
        }

        [Fact]
        public void NeverOverwritesExistingTiles()
        {
            var voidHex = new HexCoord(0, 0, LayerState.AbyssZ);
            var preExisting = voidHex.Neighbors().First();
            var tiles = new List<HexTile>
            {
                new(voidHex, TerrainType.Void),
                new(preExisting, TerrainType.Plain),
            };
            var map = new IslandMap(tiles, LayerState.AbyssZ);
            var prng = new GamePRNG(3);

            var newTiles = AbyssIslandGenerator.GenerateIslandBeyondVoid(map, voidHex, prng);

            Assert.DoesNotContain(newTiles, t => t.Coord.Equals(preExisting));
        }
    }
}
