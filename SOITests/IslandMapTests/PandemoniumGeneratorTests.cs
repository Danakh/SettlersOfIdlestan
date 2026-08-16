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
    /// Génération du Pandémonium : une île hexagonale de 37 hexes cernée de Void, un dieu démon au
    /// centre, 8 tentacules ailleurs (jamais collées au joueur) et l'avant-poste du joueur au bord.
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
        public void Island_Has37LandHexes_SurroundedByVoid(int seed)
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
            // …mais aucun n'est du Void : l'avant-poste reste posé sur de la terre.
            Assert.All(arrivalHexes, h => Assert.NotEqual(TerrainType.Void, layout.Layer.Map.GetTile(h)!.TerrainType));

            foreach (var tentacle in layout.Monsters.OfType<Tentacle>())
                foreach (var arrivalHex in arrivalHexes)
                    Assert.True(tentacle.Position.DistanceTo(arrivalHex) > 1,
                        $"Tentacule en {tentacle.Position} collée à l'hex d'arrivée {arrivalHex}");
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
