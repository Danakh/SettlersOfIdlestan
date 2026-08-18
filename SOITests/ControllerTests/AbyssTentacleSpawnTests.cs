using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Monsters;
using SettlersOfIdlestan.Model.Prestige;
using Xunit;

namespace SOITests.ControllerTests
{
    /// <summary>
    /// Apparition des Tentacules sur les îles de l'Abysse générées dynamiquement : (niveau de
    /// corruption global - 5)% de chance par île, donc rien tant que la corruption n'a pas atteint 6.
    /// L'île d'arrivée du joueur ne passe jamais par ce chemin (elle est posée par
    /// AbyssGateController), ce qui l'exclut de fait du tirage.
    ///
    /// Dispositif repris d'AbyssVisibilityExtensionTests : une Tour de Guet étend le rayon de vision,
    /// révèle l'hex de Void voisin et déclenche la génération de l'île au-delà.
    /// </summary>
    public class AbyssTentacleSpawnTests
    {
        private static HexCoord Arrival1 => new(0, 0, LayerState.AbyssZ);
        private static HexCoord Arrival2 => new(1, 0, LayerState.AbyssZ);
        private static HexCoord Arrival3 => new(0, 1, LayerState.AbyssZ);

        private static readonly HashSet<HexCoord> ArrivalSet = new() { Arrival1, Arrival2, Arrival3 };

        /// <summary>Génère une île de l'Abysse avec le niveau de corruption donné et retourne l'état résultant.</summary>
        private static WorldState GenerateIsland(int corruptionLevel, int seed)
        {
            var surfaceMap = new IslandMap(new[] { new HexTile(new HexCoord(0, 0, IslandMap.SurfaceLayer), TerrainType.Plain) });
            var civ = new Civilization { Index = 0 };
            var state = new WorldState(surfaceMap, new List<Civilization> { civ }, AtlasController.InvalidIslandId);

            var voidHex = Arrival2.Neighbors().First(n => !ArrivalSet.Contains(n));
            var tiles = new List<HexTile>
            {
                new(Arrival1, TerrainType.Mountain),
                new(Arrival2, TerrainType.Mountain),
                new(Arrival3, TerrainType.Mountain),
                new(voidHex, TerrainType.Void),
            };
            var arrivalVertex = Vertex.Create(Arrival1, Arrival2, Arrival3);
            state.AddLayer(LayerState.AbyssZ, new LayerState(new IslandMap(tiles)) { AutoExtend = true, ArrivalVertex = arrivalVertex });

            var city = new City(arrivalVertex) { CivilizationIndex = civ.Index };
            civ.AddCity(city);
            state.Visibility.RecalculateFor(civ.Index);

            var controller = new AutoExtendController();
            controller.Initialize(state, new GamePRNG(seed), null, new PrestigeState { CurrentCorruptionLevel = corruptionLevel });

            city.AddBuilding(new Watchtower { Level = 1 });
            state.Visibility.RecalculateFor(civ.Index);

            return state;
        }

        [Theory]
        [InlineData(1)]
        [InlineData(5)]
        public void NoTentacle_BelowCorruptionThreshold(int corruptionLevel)
        {
            // Chance = niveau - 5, donc nulle ou négative sous le seuil : aucune graine ne doit produire
            // de Tentacule.
            for (int seed = 0; seed < 40; seed++)
            {
                var state = GenerateIsland(corruptionLevel, seed);
                Assert.Empty(state.Features.OfType<Tentacle>());
            }
        }

        [Fact]
        public void SpawnsAtMostOneTentaclePerIsland_AtGuaranteedChance()
        {
            // Chance = niveau - 5 = 100% : chaque île générée porte exactement une Tentacule,
            // sur un de ses hexes de terre.
            const int corruptionLevel = AutoExtendController.TentacleMinCorruptionLevel + 99;

            for (int seed = 0; seed < 5; seed++)
            {
                var state = GenerateIsland(corruptionLevel, seed);
                var tentacle = Assert.Single(state.Features.OfType<Tentacle>());

                var map = state.Layers[LayerState.AbyssZ].Map;
                Assert.Equal(LayerState.AbyssZ, tentacle.Position.Z);
                Assert.NotEqual(TerrainType.Void, map.GetTile(tentacle.Position)!.TerrainType);
                Assert.DoesNotContain(tentacle.Position, ArrivalSet);
            }
        }

        [Fact]
        public void NewTentacle_CorruptsItsHexAndItsNeighbours()
        {
            // Semé après PlaceAbyssCorruption, qui corrompt déjà toute l'île neuve : l'assertion porte
            // donc surtout sur l'unicité de la Corruption par hex, et sur les voisins hors de l'île
            // neuve (arrivée du joueur), que seul le semis peut corrompre.
            const int corruptionLevel = AutoExtendController.TentacleMinCorruptionLevel + 99;

            for (int seed = 0; seed < 5; seed++)
            {
                var state = GenerateIsland(corruptionLevel, seed);
                var tentacle = state.Features.OfType<Tentacle>().Single();
                var map = state.Layers[LayerState.AbyssZ].Map;

                foreach (var hex in tentacle.Position.Neighbors().Append(tentacle.Position))
                {
                    if (map.GetTile(hex) is not { } tile || tile.TerrainType == TerrainType.Void) continue;
                    var corruption = Assert.Single(state.GetFeaturesAt(hex).OfType<SettlersOfIdlestan.Model.IslandFeatures.Corruption>());
                    Assert.True(corruption.Level >= corruptionLevel);
                }
            }
        }

        [Fact]
        public void Tentacle_HasMajorDemonStats_ButNeverMoves()
        {
            var tentacle = new Tentacle(Arrival1, level: 3);
            var demon = new MajorDemon(Arrival1, level: 3);

            Assert.Equal(demon.MaxHp, tentacle.MaxHp);
            Assert.Equal(demon.AttackDamage, tentacle.AttackDamage);
            Assert.Equal(demon.AttackRangeInHexes, tentacle.AttackRangeInHexes);
            Assert.Equal(demon.AttackIntervalTicks, tentacle.AttackIntervalTicks);
            Assert.Equal(demon.AttackResources, tentacle.AttackResources);
            Assert.Equal(demon.Armor, tentacle.Armor);
            Assert.Equal(demon.HpRegenAmount, tentacle.HpRegenAmount);

            Assert.True(demon.CanMove);
            Assert.False(tentacle.CanMove);
        }
    }
}
