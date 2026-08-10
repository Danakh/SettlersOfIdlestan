using System.Collections.Generic;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Controller.Military;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Monsters;
using Xunit;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SOITests.ControllerTests
{
    /// <summary>
    /// L'Ogre a AttackRangeInHexes = 1, ce qui — voir MonsterFeatureController.FindAttackTarget —
    /// signifie qu'il ne peut attaquer que si son propre hex fait partie des hexes de la ville
    /// (comme le Troll et le Bandit), pas seulement un hex voisin. Seuls les monstres à portée 2
    /// (Dragon, démons) peuvent frapper depuis un hex adjacent à la ville. Ces tests documentent
    /// ce comportement, jusqu'ici non couvert pour l'Ogre.
    /// </summary>
    public class OgreSiegeTests
    {
        private static HexCoord Center => new(0, 0, IslandMap.SurfaceLayer);
        private static HexCoord NE     => new(0, 1, IslandMap.SurfaceLayer);
        private static HexCoord East   => new(1, 0, IslandMap.SurfaceLayer);
        private static HexCoord NE11   => new(1, 1, IslandMap.SurfaceLayer);

        private static (WorldState state, GameClock clock, City city, Ogre ogre) CreateSetup(HexCoord ogrePosition)
        {
            var tiles = new List<HexTile>
            {
                new(Center, TerrainType.Plain),
                new(NE,     TerrainType.Plain),
                new(East,   TerrainType.Plain),
                new(NE11,   TerrainType.Plain),
            };
            var map = new IslandMap(tiles);
            var civ = new Civilization { Index = 0 };

            // Ville sur (NE, East, NE11) — Center est un voisin de NE et East, mais n'appartient
            // pas à la ville.
            var city = new City(Vertex.Create(NE, East, NE11)) { CivilizationIndex = 0, Soldiers = 10 };
            city.AddBuilding(new TownHall { Level = 4 });
            civ.AddCity(city);

            var state = new WorldState(map, new List<Civilization> { civ }, AtlasController.InvalidIslandId);
            var ogre = new Ogre(ogrePosition) { Found = true };
            state.AddFeature(ogre);

            var clock = new GameClock();
            clock.Start();
            new MonsterFeatureController().Initialize(state, clock, new GamePRNG());

            return (state, clock, city, ogre);
        }

        [Fact]
        public void Ogre_OnCityHex_DamagesSoldiers()
        {
            var (_, clock, city, ogre) = CreateSetup(ogrePosition: East);

            clock.SimulateAdvance(300); // un intervalle d'attaque

            Assert.Equal(10 - ogre.AttackDamage, city.Soldiers);
            Assert.Equal(city.Position, ogre.LastAttackTargetVertex);
        }

        [Fact]
        public void Ogre_ConsumingArmorToSaveSoldier_RaisesConsumableConsumedEvent()
        {
            // Vérifie que MonsterFeatureController.ConsumableConsumed est bien déclenché avec la
            // bonne ressource et la bonne position quand une attaque de monstre déclenche une
            // sauvegarde par Armure d'Acier (utilisé par le rendu — voir MilitaryRenderer).
            var tiles = new List<HexTile>
            {
                new(Center, TerrainType.Plain),
                new(NE,     TerrainType.Plain),
                new(East,   TerrainType.Plain),
                new(NE11,   TerrainType.Plain),
            };
            var map = new IslandMap(tiles);
            var civ = new Civilization { Index = 0 };
            civ.Resources[Resource.SteelArmor] = 999;
            civ.AddCustomAggregator(new StaticModifierProvider(new[]
            {
                new Modifier(ECategory.UNLOCK_STEEL_ARMOR, EType.ADDITIVE, 1),
            }));

            var city = new City(Vertex.Create(NE, East, NE11)) { CivilizationIndex = 0, Soldiers = 10 };
            city.AddBuilding(new TownHall { Level = 4 });
            civ.AddCity(city);

            var state = new WorldState(map, new List<Civilization> { civ }, AtlasController.InvalidIslandId);
            var ogre = new Ogre(East) { Found = true };
            state.AddFeature(ogre);

            var clock = new GameClock();
            clock.Start();
            var controller = new MonsterFeatureController();
            controller.Initialize(state, clock, new GamePRNG());

            var raised = new List<Resource>();
            controller.ConsumableConsumed += (_, args) =>
            {
                Assert.Equal(city.Position, args.Position);
                raised.Add(args.Resource);
            };

            clock.SimulateAdvance(300); // un intervalle d'attaque

            Assert.Contains(Resource.SteelArmor, raised);
        }

        [Fact]
        public void Ogre_OnNeighboringHex_NeverAttacksCity()
        {
            // Center est adjacent à NE et East (deux des trois hexes de la ville) mais n'en fait
            // pas partie : à portée 1, l'Ogre ne peut pas l'atteindre depuis là.
            var (_, clock, city, ogre) = CreateSetup(ogrePosition: Center);

            clock.SimulateAdvance(5_000);

            Assert.Equal(10, city.Soldiers);
            Assert.Null(ogre.LastAttackTargetVertex);
        }
    }
}
