using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Controller.Military;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Monsters;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace SOITests.ControllerTests
{
    public class MilitaryControllerTests
    {
        // Hexs de la ville : le vertex est l'intersection de NE, East et NE11.
        // Un bandit n'est attaqué que s'il est sur l'un de ces 3 hexs.
        private static HexCoord NE   => new(0, 1, IslandMap.SurfaceLayer);
        private static HexCoord East => new(1, 0, IslandMap.SurfaceLayer);
        private static HexCoord NE11 => new(1, 1, IslandMap.SurfaceLayer);

        // Hexs hors ville (utilisés pour les tests "hors portée")
        private static HexCoord Center => new(0, 0, IslandMap.SurfaceLayer);

        /// <summary>
        /// Ville au vertex Vertex(NE, East, NE11) avec une caserne.
        /// Seul MilitaryController est enregistré ; BanditController ne l'est pas,
        /// donc bandit.LastMovedTick reste à 0 et le combat se déclenche à chaque
        /// SimulateAdvance(CombatIntervalTicks).
        /// </summary>
        private static (WorldState state, GameClock clock, MilitaryController controller, City city)
            CreateSetup(int initialSoldiers, int barracksLevel = 2)
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
            civ.Resources[Resource.Ore] = 999;
            civ.Resources[Resource.Food] = 999;
            var vertex = Vertex.Create(NE, East, NE11);
            var city = new City(vertex) { CivilizationIndex = 0, Soldiers = initialSoldiers };
            city.Buildings.Add(new Barracks { Level = barracksLevel });
            civ.Cities.Add(city);

            var state = new WorldState(map, new List<Civilization> { civ }, AtlasController.InvalidIslandId);

            var clock = new GameClock();
            clock.Start();
            var controller = new MilitaryController();
            controller.Initialize(state, clock);

            return (state, clock, controller, city);
        }

        // ── Production de soldats ─────────────────────────────────────────────

        [Fact]
        public void Barracks_Level2_ProducesSoldiers()
        {
            var (_, clock, _, city) = CreateSetup(initialSoldiers: 0);

            Assert.Equal(0, city.Soldiers);
            clock.SimulateAdvance(MilitaryController.SoldierProductionIntervalTicks);
            Assert.Equal(1, city.Soldiers);
            clock.SimulateAdvance(MilitaryController.SoldierProductionIntervalTicks);
            Assert.Equal(2, city.Soldiers);
        }

        [Fact]
        public void Barracks_Level1_SoldierCapIsFive()
        {
            var (_, clock, _, city) = CreateSetup(initialSoldiers: 0, barracksLevel: 1);

            for (int i = 0; i < Barracks.MaxSoldiersPerLevel + 5; i++)
                clock.SimulateAdvance(MilitaryController.SoldierProductionIntervalTicks);

            Assert.Equal(Barracks.MaxSoldiersPerLevel * 1, city.Soldiers);
        }

        [Fact]
        public void Barracks_Level2_SoldierCapIsTen()
        {
            var (_, clock, _, city) = CreateSetup(initialSoldiers: 0);

            for (int i = 0; i < Barracks.MaxSoldiersPerLevel * 2 + 5; i++)
                clock.SimulateAdvance(MilitaryController.SoldierProductionIntervalTicks);

            Assert.Equal(Barracks.MaxSoldiersPerLevel * 2, city.Soldiers);
        }

        // ── Combat — bandits ──────────────────────────────────────────────────

        [Fact]
        public void Bandit_OnCityHex_TakesDamage()
        {
            // NE est l'un des 3 hexs de la ville — le bandit doit être attaqué.
            var (state, clock, _, city) = CreateSetup(initialSoldiers: 3);
            state.AddFeature(new Bandit(NE, 0));
            var bandit = state.Features.OfType<Bandit>().First();

            clock.SimulateAdvance(MilitaryController.CombatIntervalTicks);

            // 1 attaque (production trop lente : 1000 ticks vs combat 100 ticks) : soldats 3→2, bandit MaxHp→MaxHp-1.
            Assert.Equal(bandit.MaxHp - 1, bandit.Hp);
            Assert.Equal(2, city.Soldiers);
        }

        [Fact]
        public void Bandit_KilledByBarracksSoldiers_IsRemovedFromState()
        {
            // Bandit sur East avec 5 PV ; ville démarre avec 5 soldats.
            // 5 cycles de combat (5×CombatIntervalTicks) : 5 attaques → bandit mort, 5 soldats consommés.
            var (state, clock, _, city) = CreateSetup(initialSoldiers: 5);
            state.AddFeature(new Bandit(East, 0) { Hp = 5 });

            Assert.Single(state.Features.OfType<Bandit>());

            for (int i = 0; i < 5; i++)
                clock.SimulateAdvance(MilitaryController.CombatIntervalTicks);

            Assert.Empty(state.Features.OfType<Bandit>());
            Assert.Equal(0, city.Soldiers);
        }

        [Fact]
        public void Barracks_WithNoSoldiers_DoesNotAttackBandit()
        {
            // Bandit sur un hex de la ville, mais ville vide de soldats → aucune attaque.
            var (state, clock, _, _) = CreateSetup(initialSoldiers: 0, barracksLevel: 1);
            state.AddFeature(new Bandit(NE11, 0));
            var bandit = state.Features.OfType<Bandit>().First();

            clock.SimulateAdvance(MilitaryController.CombatIntervalTicks);

            Assert.Equal(bandit.MaxHp, bandit.Hp);
        }

        // ── Portée d'attaque — règle des 3 hexs ──────────────────────────────
        //
        // La ville n'attaque QUE les bandits présents sur l'un des 3 hexs
        // qui composent le vertex de la ville : NE=(0,1), East=(1,0), NE11=(1,1).
        // Tout hex en dehors de cette zone — y compris les hexs immédiatement
        // adjacents à la ville — est hors portée.

        public static IEnumerable<object[]> CityHexes =>
        [
            [0, 1, "NE — premier hex de la ville"],
            [1, 0, "East — deuxième hex de la ville"],
            [1, 1, "NE11 — troisième hex de la ville"],
        ];

        [Theory]
        [MemberData(nameof(CityHexes))]
        public void Bandit_OnAnyCityHex_IsAttacked(int q, int r, string description)
        {
            var (state, clock, _, _) = CreateSetup(initialSoldiers: 3, barracksLevel: 2);
            state.AddFeature(new Bandit(new HexCoord(q, r, IslandMap.SurfaceLayer), 0));
            var bandit = state.Features.OfType<Bandit>().First();

            clock.SimulateAdvance(MilitaryController.CombatIntervalTicks);

            Assert.True(bandit.Hp < bandit.MaxHp,
                $"Le bandit sur ({q},{r}) aurait dû être attaqué ({description}).");
        }

        public static IEnumerable<object[]> HexesOutOfRange =>
        [
            // Hexs immédiatement autour de la ville (distance 1 d'un hex de ville, mais hors ville)
            [0, 0,  "Center — voisin de NE et East, mais hors des 3 hexs"],
            [-1, 1, "voisin de NE uniquement"],
            [0, 2,  "voisin de NE et NE11"],
            [-1, 2, "voisin de NE uniquement"],
            [2, 0,  "voisin de East et NE11"],
            [1, -1, "voisin de East uniquement"],
            [2, -1, "voisin de East uniquement"],
            [2, 1,  "voisin de NE11 uniquement"],
            [1, 2,  "voisin de NE11 uniquement"],
            // Hexs encore plus loin
            [0, -1, "distance 2 de tous les hexs de la ville"],
            [-1, 0, "distance 2 de tous les hexs de la ville"],
            [3, 0,  "loin à l'est"],
            [-3, 0, "loin à l'ouest"],
            [0, 3,  "loin au nord"],
        ];

        [Theory]
        [MemberData(nameof(HexesOutOfRange))]
        public void Bandit_OffCityHexes_IsNotAttacked(int q, int r, string description)
        {
            var (state, clock, _, _) = CreateSetup(initialSoldiers: 5, barracksLevel: 2);
            state.AddFeature(new Bandit(new HexCoord(q, r, IslandMap.SurfaceLayer), 0));
            var bandit = state.Features.OfType<Bandit>().First();

            clock.SimulateAdvance(MilitaryController.CombatIntervalTicks);

            Assert.True(bandit.Hp == bandit.MaxHp,
                $"Le bandit en ({q},{r}) ne devrait pas être attaqué ({description}).");
        }

        [Fact]
        public void AttackRange_ExactlyThreeCityHexes_CanAttack()
        {
            // Cartographie exhaustive sur une grille 7x7 : seuls les 3 hexs de la ville
            // déclenchent une attaque, tous les autres sont hors portée.
            var cityHexSet = new HashSet<(int, int)> { (0, 1), (1, 0), (1, 1) };

            for (int q = -3; q <= 4; q++)
            {
                for (int r = -3; r <= 4; r++)
                {
                    var (state, clock, _, _) = CreateSetup(initialSoldiers: 5, barracksLevel: 2);
                    state.AddFeature(new Bandit(new HexCoord(q, r, IslandMap.SurfaceLayer), 0));
                    var bandit = state.Features.OfType<Bandit>().First();

                    clock.SimulateAdvance(MilitaryController.CombatIntervalTicks);

                    bool shouldAttack = cityHexSet.Contains((q, r));
                    bool wasAttacked = bandit.Hp < bandit.MaxHp;

                    Assert.True(shouldAttack == wasAttacked,
                        $"Hex ({q},{r}): attendu {(shouldAttack ? "attaqué" : "hors portée")}, obtenu {(wasAttacked ? "attaqué" : "non attaqué")}.");
                }
            }
        }

        // ── Événement SoldierAttackedMonster ────────────────────────────────────

        [Fact]
        public void SoldierAttackedMonster_EventFired_WhenBanditOnCityHex()
        {
            var (state, clock, controller, _) = CreateSetup(initialSoldiers: 3, barracksLevel: 2);
            state.AddFeature(new Bandit(NE, 0));

            SoldierAttackEventArgs? firedArgs = null;
            controller.SoldierAttackedMonster += (_, args) => firedArgs = args;

            clock.SimulateAdvance(MilitaryController.CombatIntervalTicks);

            Assert.NotNull(firedArgs);
            Assert.Equal(NE.Q, firedArgs.BanditPosition.Q);
            Assert.Equal(NE.R, firedArgs.BanditPosition.R);
        }

        [Fact]
        public void SoldierAttackedMonster_EventNotFired_WhenBanditOffCityHexes()
        {
            // Center est voisin de la ville mais pas sur l'un de ses 3 hexs.
            var (state, clock, controller, _) = CreateSetup(initialSoldiers: 5, barracksLevel: 2);
            state.AddFeature(new Bandit(Center, 0));

            bool eventFired = false;
            controller.SoldierAttackedMonster += (_, _) => eventFired = true;

            clock.SimulateAdvance(MilitaryController.CombatIntervalTicks);

            Assert.False(eventFired);
        }

        // ── GetSoldierProductionRate ──────────────────────────────────────────

        [Fact]
        public void GetSoldierProductionRate_NoBarracks_ReturnsZero()
        {
            var (state, _, controller, city) = CreateSetup(initialSoldiers: 0);
            city.Buildings.Clear();
            var civ = state.Civilizations[0];

            Assert.Equal(0, controller.GetSoldierProductionRate(city, civ));
        }

        [Fact]
        public void GetSoldierProductionRate_WithBarracks_ReturnsBaseRate()
        {
            var (state, _, controller, city) = CreateSetup(initialSoldiers: 0);
            var civ = state.Civilizations[0];

            double rate = controller.GetSoldierProductionRate(city, civ);

            // base: 1 soldier per 1000 ticks, 100 ticks/s → 0.1 soldiers/s
            Assert.Equal(0.1, rate, precision: 6);
        }

        [Fact]
        public void MilitaryAcademy_Level4_DoublesSoldierProductionRate()
        {
            var (state, _, controller, city) = CreateSetup(initialSoldiers: 0);
            var civ = state.Civilizations[0];

            double baseRate = controller.GetSoldierProductionRate(city, civ);

            var academy = new MilitaryAcademy { Level = 4 };
            civ.SetupModifierAggregator(academy);

            double newRate = controller.GetSoldierProductionRate(city, civ);

            Assert.Equal(baseRate * 2, newRate, precision: 6);
        }

        [Fact]
        public void MilitaryAcademy_Level4_ProducesSoldiersAtDoubleSpeed()
        {
            var (state, clock, _, city) = CreateSetup(initialSoldiers: 0);
            var civ = state.Civilizations[0];

            var academy = new MilitaryAcademy { Level = 4 };
            civ.SetupModifierAggregator(academy);

            // Avec UnitProductionSpeed=2.0, l'intervalle effectif est 500 ticks
            clock.SimulateAdvance(MilitaryController.SoldierProductionIntervalTicks / 2);
            Assert.Equal(1, city.Soldiers);
        }
    }
}
