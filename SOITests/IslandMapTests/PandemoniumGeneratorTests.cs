using System.Linq;
using SettlersOfIdlestan.Controller.Generator;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Monsters;
using Xunit;

namespace SOITests.IslandMapTests
{
    /// <summary>
    /// Génération du Pandémonium : une île hexagonale de 61 hexes cernée de Void, un dieu démon au
    /// centre, 8 tentacules serrées autour de lui (jamais collées au joueur) et l'avant-poste du
    /// joueur au bord.
    /// </summary>
    public class PandemoniumGeneratorTests
    {
        private static HexCoord Center => new(0, 0, LayerState.PandemoniumZ);

        private static (PandemoniumGenerator.PandemoniumLayout layout, Civilization civ) Generate(int seed)
        {
            var civ = new Civilization { Index = 0 };
            var layout = PandemoniumGenerator.Create(civ, new GamePRNG(seed));
            return (layout, civ);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(7)]
        [InlineData(42)]
        public void Island_HasSixtyOneLandHexes_SurroundedByVoid(int seed)
        {
            var (layout, _) = Generate(seed);
            var tiles = layout.Layer.Map.Tiles;

            var land = tiles.Values.Where(t => t.TerrainType != TerrainType.Void).ToList();
            Assert.Equal(PandemoniumGenerator.IslandHexCount, land.Count);
            Assert.All(land, t => Assert.True(t.Coord.DistanceTo(Center) <= PandemoniumGenerator.IslandRadius));

            // Anneau de Void complet : chaque hex de terre n'a que des voisins présents sur la carte.
            foreach (var tile in land)
                foreach (var neighbor in tile.Coord.Neighbors())
                    Assert.True(tiles.ContainsKey(neighbor), $"Voisin {neighbor} absent de la carte");
        }

        [Theory]
        [InlineData(1)]
        [InlineData(7)]
        [InlineData(42)]
        public void DemonGod_SitsAtCenter_WithEightTentacles(int seed)
        {
            var (layout, _) = Generate(seed);

            var god = Assert.Single(layout.Monsters.OfType<DemonGod>());
            Assert.Equal(Center, god.Position);

            var tentacles = layout.Monsters.OfType<Tentacle>().ToList();
            Assert.Equal(PandemoniumGenerator.TentacleCount, tentacles.Count);
            Assert.Equal(tentacles.Count, tentacles.Select(t => t.Position).Distinct().Count());
            Assert.All(tentacles, t => Assert.True(t.Position.DistanceTo(Center) <= PandemoniumGenerator.IslandRadius));
        }

        /// <summary>
        /// Les Tentacules gardent le dieu démon de près : toutes dans un rayon de
        /// <see cref="PandemoniumGenerator.TentacleRadius"/>, jamais sur le centre (le dieu démon y
        /// est). C'est ce qui laisse au joueur des hexes hors de portée pour bâtir son siège — mais
        /// combien exactement dépend du tirage, donc seul le rayon est un invariant testable.
        /// </summary>
        [Theory]
        [InlineData(1)]
        [InlineData(7)]
        [InlineData(42)]
        [InlineData(123)]
        [InlineData(2024)]
        public void Tentacles_GuardTheCenter_WithinTwoHexes(int seed)
        {
            var (layout, _) = Generate(seed);

            var tentacles = layout.Monsters.OfType<Tentacle>().ToList();
            Assert.Equal(PandemoniumGenerator.TentacleCount, tentacles.Count);
            Assert.All(tentacles, t =>
            {
                int distance = t.Position.DistanceTo(Center);
                Assert.InRange(distance, 1, PandemoniumGenerator.TentacleRadius);
            });
        }

        [Theory]
        [InlineData(1)]
        [InlineData(7)]
        [InlineData(42)]
        public void Player_StartsOnTheBorder_WithNoTentacleAdjacent(int seed)
        {
            var (layout, civ) = Generate(seed);

            var arrival = layout.Layer.ArrivalVertex!;
            var city = Assert.Single(civ.Cities);
            Assert.Equal(arrival, city.Position);

            var arrivalHexes = arrival.GetHexes();
            // Au bord : un des trois hexes est sur le dernier anneau de l'île…
            Assert.Contains(arrivalHexes, h => h.DistanceTo(Center) == PandemoniumGenerator.IslandRadius);
            // …mais aucun n'est du Void ni de l'Eau : l'avant-poste reste posé sur de la terre ferme.
            Assert.All(arrivalHexes, h =>
            {
                var terrain = layout.Layer.Map.GetTile(h)!.TerrainType;
                Assert.NotEqual(TerrainType.Void, terrain);
                Assert.False(terrain.IsWater(), $"Hex d'arrivée {h} posé sur de l'Eau");
            });

            foreach (var tentacle in layout.Monsters.OfType<Tentacle>())
                foreach (var arrivalHex in arrivalHexes)
                    Assert.True(tentacle.Position.DistanceTo(arrivalHex) > 1,
                        $"Tentacule en {tentacle.Position} collée à l'hex d'arrivée {arrivalHex}");
        }

        [Theory]
        [InlineData(1)]
        [InlineData(7)]
        [InlineData(42)]
        [InlineData(123)]
        [InlineData(2024)]
        public void DemonGodAndTentacles_NeverStandOnWater(int seed)
        {
            var (layout, _) = Generate(seed);

            foreach (var monster in layout.Monsters)
            {
                var terrain = layout.Layer.Map.GetTile(monster.Position)!.TerrainType;
                Assert.False(terrain.IsWater(), $"{monster.GetType().Name} en {monster.Position} posé sur de l'Eau");
            }
        }

        [Theory]
        [InlineData(1)]
        [InlineData(7)]
        [InlineData(42)]
        public void ArrivalTriangle_CoversForestMountainHill_ByDefault(int seed)
        {
            var (layout, _) = Generate(seed);

            var arrivalHexes = layout.Layer.ArrivalVertex!.GetHexes();
            var terrains = arrivalHexes.Select(h => layout.Layer.Map.GetTile(h)!.TerrainType).OrderBy(t => t.ToString());

            Assert.Equal(
                new[] { TerrainType.Forest, TerrainType.Hill, TerrainType.Mountain },
                terrains.ToArray());
        }

        [Theory]
        [InlineData(1)]
        [InlineData(7)]
        [InlineData(42)]
        public void ArrivalTriangle_ReplacesHillWithPreferredTerrain_WhenGiven(int seed)
        {
            var civ = new Civilization { Index = 0 };
            var layout = PandemoniumGenerator.Create(civ, new GamePRNG(seed), preferredTerrain: TerrainType.Water);

            var arrivalHexes = layout.Layer.ArrivalVertex!.GetHexes();
            var terrains = arrivalHexes.Select(h => layout.Layer.Map.GetTile(h)!.TerrainType).OrderBy(t => t.ToString());

            Assert.Equal(
                new[] { TerrainType.Forest, TerrainType.Mountain, TerrainType.Water },
                terrains.ToArray());
        }

        [Fact]
        public void CanGenerateWaterHexes_AcrossSeeds()
        {
            bool sawWater = false;
            for (int seed = 0; seed < 200 && !sawWater; seed++)
            {
                var (layout, _) = Generate(seed);
                sawWater = layout.Layer.Map.Tiles.Values.Any(t => t.TerrainType == TerrainType.Water);
            }

            Assert.True(sawWater, "L'Eau devrait pouvoir apparaître sur l'île du Pandémonium.");
        }

        [Fact]
        public void Layer_IsNotAutoExtended()
        {
            var (layout, _) = Generate(1);
            Assert.False(layout.Layer.AutoExtend);
        }

        [Fact]
        public void Generation_IsDeterministicForAGivenSeed()
        {
            var (first, _) = Generate(123);
            var (second, _) = Generate(123);

            Assert.Equal(first.Layer.ArrivalVertex, second.Layer.ArrivalVertex);
            Assert.Equal(
                first.Monsters.Select(m => (m.GetType().Name, m.Position)).ToList(),
                second.Monsters.Select(m => (m.GetType().Name, m.Position)).ToList());
            Assert.Equal(
                first.Layer.Map.Tiles.OrderBy(t => t.Key.Q).ThenBy(t => t.Key.R).Select(t => t.Value.TerrainType).ToList(),
                second.Layer.Map.Tiles.OrderBy(t => t.Key.Q).ThenBy(t => t.Key.R).Select(t => t.Value.TerrainType).ToList());
        }

        [Fact]
        public void MonsterLevel_IsAppliedToGodAndTentacles()
        {
            var civ = new Civilization { Index = 0 };
            var layout = PandemoniumGenerator.Create(civ, new GamePRNG(1), monsterLevel: 5);

            Assert.All(layout.Monsters, m => Assert.Equal(5, m.Level));
        }
    }
}
