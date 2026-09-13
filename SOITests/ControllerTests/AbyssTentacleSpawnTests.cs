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
    /// Apparition des Tentacules sur les îles de l'Abysse générées dynamiquement : rien tant que la
    /// corruption n'a pas atteint 6, puis une cadence — n = TentacleSpawnInterval îles muettes après
    /// la dernière Tentacule, puis une Tentacule garantie dans les n suivantes.
    /// L'île d'arrivée du joueur ne passe jamais par ce chemin (elle est posée par
    /// AbyssGateController), ce qui l'exclut de fait du tirage.
    ///
    /// Dispositif repris d'AbyssVisibilityExtensionTests : une Tour de Guet étend le rayon de vision,
    /// révèle l'hex de Void voisin et déclenche la génération de l'île au-delà. Une seule île par
    /// état généré : le compteur d'îles est donc posé à la main avant la révélation, ce qui permet de
    /// tester chaque position de la cadence isolément.
    /// </summary>
    public class AbyssTentacleSpawnTests
    {
        private static HexCoord Arrival1 => new(0, 0, LayerState.AbyssZ);
        private static HexCoord Arrival2 => new(1, 0, LayerState.AbyssZ);
        private static HexCoord Arrival3 => new(0, 1, LayerState.AbyssZ);

        private static readonly HashSet<HexCoord> ArrivalSet = new() { Arrival1, Arrival2, Arrival3 };

        /// <summary>
        /// Génère une île de l'Abysse avec le niveau de corruption donné et retourne l'état résultant.
        /// <paramref name="islandsSinceTentacle"/> place l'île générée à cette position de la cadence
        /// (nombre d'îles déjà venues depuis la dernière Tentacule).
        /// </summary>
        private static WorldState GenerateIsland(int corruptionLevel, int seed, int islandsSinceTentacle = 0)
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
            state.AddLayer(LayerState.AbyssZ, new LayerState(new IslandMap(tiles))
            {
                AutoExtend = true,
                ArrivalVertex = arrivalVertex,
                AbyssIslandsSinceTentacle = islandsSinceTentacle,
            });

            var city = new City(arrivalVertex) { CivilizationIndex = civ.Index };
            civ.AddCity(city);
            state.Visibility.RecalculateFor(civ.Index);

            var controller = new AutoExtendController();
            controller.Initialize(state, new GamePRNG(seed), null, new PrestigeState { CurrentCorruptionLevel = corruptionLevel });

            city.AddBuilding(new Watchtower { Level = 1 });
            state.Visibility.RecalculateFor(civ.Index);

            return state;
        }

        /// <summary>Position de la cadence à laquelle la Tentacule est certaine : dernière île de la fenêtre garantie.</summary>
        private static int GuaranteedPosition(int corruptionLevel) =>
            2 * AutoExtendController.TentacleSpawnInterval(corruptionLevel) - 1;

        [Theory]
        [InlineData(1)]
        [InlineData(5)]
        public void NoTentacle_BelowCorruptionThreshold(int corruptionLevel)
        {
            // Sous le seuil, la cadence n'existe pas : même à la position qui serait celle de la
            // Tentacule certaine, aucune graine ne doit en produire.
            for (int seed = 0; seed < 40; seed++)
            {
                var state = GenerateIsland(corruptionLevel, seed, islandsSinceTentacle: 99);
                Assert.Empty(state.Features.OfType<Tentacle>());
            }
        }

        [Theory]
        [InlineData(AutoExtendController.TentacleMinCorruptionLevel, 10)]
        [InlineData(AutoExtendController.TentacleMinCorruptionLevel + 1, 9)]
        [InlineData(11, 5)]
        [InlineData(30, 5)]
        public void SpawnInterval_ShrinksWithCorruption_DownToFloor(int corruptionLevel, int expected)
        {
            Assert.Equal(expected, AutoExtendController.TentacleSpawnInterval(corruptionLevel));
        }

        [Fact]
        public void NoTentacle_DuringSilentStretchFollowingLastOne()
        {
            // Les n premières îles depuis la dernière Tentacule n'en portent jamais, quelle que soit
            // la graine : c'est le répit garanti, pas une simple faible probabilité.
            const int corruptionLevel = AutoExtendController.TentacleMinCorruptionLevel;
            int interval = AutoExtendController.TentacleSpawnInterval(corruptionLevel);

            for (int islandsSince = 0; islandsSince < interval; islandsSince++)
                for (int seed = 0; seed < 20; seed++)
                {
                    var state = GenerateIsland(corruptionLevel, seed, islandsSince);
                    Assert.Empty(state.Features.OfType<Tentacle>());
                }
        }

        [Fact]
        public void TentacleIsCertain_OnLastIslandOfTheWindow()
        {
            // Dernière île de la fenêtre : une chance sur une, donc une Tentacule quelle que soit la graine.
            const int corruptionLevel = AutoExtendController.TentacleMinCorruptionLevel;

            for (int seed = 0; seed < 20; seed++)
            {
                var state = GenerateIsland(corruptionLevel, seed, GuaranteedPosition(corruptionLevel));
                Assert.Single(state.Features.OfType<Tentacle>());
            }
        }

        [Fact]
        public void TentacleAppearsSomewhereInsideTheWindow_ThenCounterRestarts()
        {
            // Au milieu de la fenêtre l'apparition est un tirage (1 chance sur les îles restantes) :
            // on vérifie que les deux issues existent, et que le compteur repart de zéro dès qu'une
            // Tentacule est apparue — c'est lui qui rouvre le palier muet.
            const int corruptionLevel = AutoExtendController.TentacleMinCorruptionLevel;
            int interval = AutoExtendController.TentacleSpawnInterval(corruptionLevel);

            int spawned = 0, skipped = 0;
            for (int seed = 0; seed < 40; seed++)
            {
                var state = GenerateIsland(corruptionLevel, seed, interval);
                var layer = state.Layers[LayerState.AbyssZ];

                if (state.Features.OfType<Tentacle>().Any())
                {
                    spawned++;
                    Assert.Equal(0, layer.AbyssIslandsSinceTentacle);
                }
                else
                {
                    skipped++;
                    // Sinon l'île générée compte, et rapproche la suivante de la certitude.
                    Assert.Equal(interval + 1, layer.AbyssIslandsSinceTentacle);
                }
            }

            Assert.True(spawned > 0, "aucune Tentacule sur 40 graines à l'ouverture de la fenêtre");
            Assert.True(skipped > 0, "Tentacule à toutes les graines à l'ouverture de la fenêtre");
        }

        [Fact]
        public void SpawnsAtMostOneTentaclePerIsland_AtGuaranteedChance()
        {
            // Dernière île de la fenêtre garantie : chaque île générée porte exactement une Tentacule,
            // sur un de ses hexes de terre.
            const int corruptionLevel = AutoExtendController.TentacleMinCorruptionLevel + 99;

            for (int seed = 0; seed < 5; seed++)
            {
                var state = GenerateIsland(corruptionLevel, seed, GuaranteedPosition(corruptionLevel));
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
                var state = GenerateIsland(corruptionLevel, seed, GuaranteedPosition(corruptionLevel));
                var tentacle = state.Features.OfType<Tentacle>().Single();
                var map = state.Layers[LayerState.AbyssZ].Map;

                foreach (var hex in tentacle.Position.Neighbors().Append(tentacle.Position))
                {
                    if (map.GetTile(hex) is not { } tile || tile.TerrainType == TerrainType.Void) continue;
                    var corruption = Assert.Single(state.GetFeaturesAt(hex).OfType<SettlersOfIdlestan.Model.IslandFeatures.Corruption>());
                    // Niveau de l'île borné par le plafond dur d'une zone corrompue (Corruption.MaxLevel).
                    Assert.True(corruption.Level >= System.Math.Min(corruptionLevel, SettlersOfIdlestan.Model.IslandFeatures.Corruption.MaxLevel));
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
            Assert.Equal(demon.AttackIntervalTicks, tentacle.AttackIntervalTicks);
            Assert.Equal(demon.AttackResources, tentacle.AttackResources);
            Assert.Equal(demon.Armor, tentacle.Armor);
            Assert.Equal(demon.HpRegenAmount, tentacle.HpRegenAmount);

            Assert.True(demon.CanMove);
            Assert.False(tentacle.CanMove);

            // Contrepartie de l'immobilité : elle frappe un anneau plus loin que le démon, et à
            // distance — donc sans rendre les coups à ce qu'elle vise.
            Assert.Equal(demon.AttackRangeInHexes + 1, tentacle.AttackRangeInHexes);
            Assert.False(demon.HasRangedAttack);
            Assert.True(tentacle.HasRangedAttack);
        }
    }
}
