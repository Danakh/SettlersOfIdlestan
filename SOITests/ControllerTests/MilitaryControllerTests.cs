using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Controller.Military;
using SettlersOfIdlestan.Model.Bandits;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using System.Collections.Generic;
using Xunit;

namespace SOITests.ControllerTests
{
    public class MilitaryControllerTests
    {
        // Hexs de la ville : le vertex est l'intersection de NE, East et NE11.
        // Un bandit n'est attaqué que s'il est sur l'un de ces 3 hexs.
        private static HexCoord NE   => new(0, 1);
        private static HexCoord East => new(1, 0);
        private static HexCoord NE11 => new(1, 1);

        // Hexs hors ville (utilisés pour les tests "hors portée")
        private static HexCoord Center => new(0, 0);

        /// <summary>
        /// Ville au vertex Vertex(NE, East, NE11) avec une caserne.
        /// Seul MilitaryController est enregistré ; BanditController ne l'est pas,
        /// donc bandit.LastMovedTick reste à 0 et le combat se déclenche à chaque
        /// SimulateAdvance(CombatIntervalTicks).
        /// </summary>
        private static (IslandState state, GameClock clock, MilitaryController controller, Barracks barracks)
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
            var vertex = Vertex.Create(NE, East, NE11);
            var city = new City(vertex) { CivilizationIndex = 0 };
            var barracks = new Barracks { Level = barracksLevel, Soldiers = initialSoldiers };
            city.Buildings.Add(barracks);
            civ.Cities.Add(city);

            var state = new IslandState(map, new List<Civilization> { civ }, AtlasController.InvalidIslandId);

            var clock = new GameClock();
            clock.Start();
            var controller = new MilitaryController();
            controller.Initialize(state, clock);

            return (state, clock, controller, barracks);
        }

        // ── Production de soldats ─────────────────────────────────────────────

        [Fact]
        public void Barracks_Level2_ProducesSoldiers()
        {
            var (_, clock, _, barracks) = CreateSetup(initialSoldiers: 0);

            Assert.Equal(0, barracks.Soldiers);
            clock.SimulateAdvance(MilitaryController.SoldierProductionIntervalTicks);
            Assert.Equal(1, barracks.Soldiers);
            clock.SimulateAdvance(MilitaryController.SoldierProductionIntervalTicks);
            Assert.Equal(2, barracks.Soldiers);
        }

        [Fact]
        public void Barracks_Level1_DoesNotProduceSoldiers()
        {
            var (_, clock, _, barracks) = CreateSetup(initialSoldiers: 0, barracksLevel: 1);

            for (int i = 0; i < 5; i++)
                clock.SimulateAdvance(MilitaryController.SoldierProductionIntervalTicks);

            Assert.Equal(0, barracks.Soldiers);
        }

        [Fact]
        public void Barracks_SoldierCapIsTen()
        {
            var (_, clock, _, barracks) = CreateSetup(initialSoldiers: 0);

            for (int i = 0; i < MilitaryController.MaxSoldiers + 5; i++)
                clock.SimulateAdvance(MilitaryController.SoldierProductionIntervalTicks);

            Assert.Equal(MilitaryController.MaxSoldiers, barracks.Soldiers);
        }

        // ── Combat — bandits ──────────────────────────────────────────────────

        [Fact]
        public void Bandit_OnCityHex_TakesDamage()
        {
            // NE est l'un des 3 hexs de la ville — le bandit doit être attaqué.
            var (state, clock, _, barracks) = CreateSetup(initialSoldiers: 3);
            state.Bandits.Add(new Bandit(NE, 0));
            var bandit = state.Bandits[0];

            clock.SimulateAdvance(MilitaryController.CombatIntervalTicks);

            // 1 attaque (production trop lente : 1000 ticks vs combat 100 ticks) : soldats 3→2, bandit MaxHp→MaxHp-1.
            Assert.Equal(Bandit.MaxHp - 1, bandit.Hp);
            Assert.Equal(2, barracks.Soldiers);
        }

        [Fact]
        public void Bandit_KilledByBarracksSoldiers_IsRemovedFromState()
        {
            // Bandit sur East avec 5 PV ; caserne démarre avec 5 soldats.
            // 5 cycles de combat (5×CombatIntervalTicks) : 5 attaques → bandit mort, 5 soldats consommés.
            var (state, clock, _, barracks) = CreateSetup(initialSoldiers: 5);
            state.Bandits.Add(new Bandit(East, 0) { Hp = 5 });

            Assert.Single(state.Bandits);

            for (int i = 0; i < 5; i++)
                clock.SimulateAdvance(MilitaryController.CombatIntervalTicks);

            Assert.Empty(state.Bandits);
            Assert.Equal(0, barracks.Soldiers);
        }

        [Fact]
        public void Barracks_WithNoSoldiers_DoesNotAttackBandit()
        {
            // Bandit sur un hex de la ville, mais caserne vide → aucune attaque.
            var (state, clock, _, _) = CreateSetup(initialSoldiers: 0, barracksLevel: 1);
            state.Bandits.Add(new Bandit(NE11, 0));
            var bandit = state.Bandits[0];

            clock.SimulateAdvance(MilitaryController.CombatIntervalTicks);

            Assert.Equal(Bandit.MaxHp, bandit.Hp);
        }

        // ── Portée d'attaque — règle des 3 hexs ──────────────────────────────
        //
        // La caserne n'attaque QUE les bandits présents sur l'un des 3 hexs
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
            state.Bandits.Add(new Bandit(new HexCoord(q, r), 0));
            var bandit = state.Bandits[0];

            clock.SimulateAdvance(MilitaryController.CombatIntervalTicks);

            Assert.True(bandit.Hp < Bandit.MaxHp,
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
            state.Bandits.Add(new Bandit(new HexCoord(q, r), 0));
            var bandit = state.Bandits[0];

            clock.SimulateAdvance(MilitaryController.CombatIntervalTicks);

            Assert.True(bandit.Hp == Bandit.MaxHp,
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
                    state.Bandits.Add(new Bandit(new HexCoord(q, r), 0));
                    var bandit = state.Bandits[0];

                    clock.SimulateAdvance(MilitaryController.CombatIntervalTicks);

                    bool shouldAttack = cityHexSet.Contains((q, r));
                    bool wasAttacked = bandit.Hp < Bandit.MaxHp;

                    Assert.True(shouldAttack == wasAttacked,
                        $"Hex ({q},{r}): attendu {(shouldAttack ? "attaqué" : "hors portée")}, obtenu {(wasAttacked ? "attaqué" : "non attaqué")}.");
                }
            }
        }

        // ── Événement SoldierAttackedBandit ───────────────────────────────────

        [Fact]
        public void SoldierAttackedBandit_EventFired_WhenBanditOnCityHex()
        {
            var (state, clock, controller, _) = CreateSetup(initialSoldiers: 3, barracksLevel: 2);
            state.Bandits.Add(new Bandit(NE, 0));

            SoldierAttackEventArgs? firedArgs = null;
            controller.SoldierAttackedBandit += (_, args) => firedArgs = args;

            clock.SimulateAdvance(MilitaryController.CombatIntervalTicks);

            Assert.NotNull(firedArgs);
            Assert.Equal(NE.Q, firedArgs.BanditPosition.Q);
            Assert.Equal(NE.R, firedArgs.BanditPosition.R);
        }

        [Fact]
        public void SoldierAttackedBandit_EventNotFired_WhenBanditOffCityHexes()
        {
            // Center est voisin de la ville mais pas sur l'un de ses 3 hexs.
            var (state, clock, controller, _) = CreateSetup(initialSoldiers: 5, barracksLevel: 2);
            state.Bandits.Add(new Bandit(Center, 0));

            bool eventFired = false;
            controller.SoldierAttackedBandit += (_, _) => eventFired = true;

            clock.SimulateAdvance(MilitaryController.CombatIntervalTicks);

            Assert.False(eventFired);
        }
    }
}
