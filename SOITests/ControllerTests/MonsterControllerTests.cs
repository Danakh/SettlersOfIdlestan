using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Controller.Military;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using System.Linq;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandFeatures;
using SettlersOfIdlestan.Model.IslandMap;
using System.Collections.Generic;
using Xunit;
using SettlersOfIdlestan.Model.Monsters;

namespace SOITests.ControllerTests
{
    public class MonsterControllerTests
    {
        // Map layout for trapped scenario (axial coordinates):
        //
        //   NW(-1,1)  NE(0,1)
        // W(-1,0)  [0,0]  E(1,0)
        //   SW(0,-1)  SE(1,-1)
        //
        // Water: W and E
        // Plain: NW, NE, SW, SE
        // City A at Vertex(center, NE, NW) → barracks cover NE and NW
        // City B at Vertex(center, SW, SE) → barracks cover SW and SE
        //
        // With active barracks: all non-water neighbors are protected → bandit stays.
        // With inactive barracks: bandit can move to any plain neighbor.

        private static HexCoord Center => new(0, 0, IslandMap.SurfaceLayer);
        private static HexCoord East   => new(1, 0, IslandMap.SurfaceLayer);
        private static HexCoord West   => new(-1, 0, IslandMap.SurfaceLayer);
        private static HexCoord NE     => new(0, 1, IslandMap.SurfaceLayer);
        private static HexCoord NW     => new(-1, 1, IslandMap.SurfaceLayer);
        private static HexCoord SE     => new(1, -1, IslandMap.SurfaceLayer);
        private static HexCoord SW     => new(0, -1, IslandMap.SurfaceLayer);

        private static (WorldState state, GameClock clock, MonsterFeatureController controller) CreateTrappedSetup(bool activeBarracks)
        {
            var tiles = new List<HexTile>
            {
                new(Center, TerrainType.Desert),
                new(East,   TerrainType.Water),
                new(West,   TerrainType.Water),
                new(NE,     TerrainType.Plain),
                new(NW,     TerrainType.Plain),
                new(SE,     TerrainType.Plain),
                new(SW,     TerrainType.Plain),
            };

            var map = new IslandMap(tiles);
            var civ = new Civilization { Index = 0 };

            // Vertex A: center + NE + NW  →  protects NE and NW (and center itself)
            var vertexA = Vertex.Create(Center, NE, NW);
            var cityA = new City(vertexA) { CivilizationIndex = 0 };
            cityA.AddBuilding(new Barracks { Level = activeBarracks ? 1 : 0 });

            // Vertex B: center + SW + SE  →  protects SW and SE (and center itself)
            var vertexB = Vertex.Create(Center, SW, SE);
            var cityB = new City(vertexB) { CivilizationIndex = 0 };
            cityB.AddBuilding(new Barracks { Level = activeBarracks ? 1 : 0 });

            civ.AddCity(cityA);
            civ.AddCity(cityB);

            var state = new WorldState(map, new List<Civilization> { civ }, AtlasController.InvalidIslandId);
            state.AddFeature(new Bandit(Center, 0) { Found = true });

            var clock = new GameClock();
            clock.Start();

            var controller = new MonsterFeatureController();
            controller.Initialize(state, clock, new GamePRNG());

            return (state, clock, controller);
        }

        // ── Water exclusion ──────────────────────────────────────────────────

        [Fact]
        public void Bandit_NeverMovesToWaterTile()
        {
            // Small map: center (desert) with one plain neighbor and one water neighbor.
            // The bandit should always end up on the plain tile, never on water.
            var plain = new HexCoord(1, 0, IslandMap.SurfaceLayer);
            var water = new HexCoord(-1, 0, IslandMap.SurfaceLayer);

            var tiles = new List<HexTile>
            {
                new(Center, TerrainType.Desert),
                new(plain,  TerrainType.Plain),
                new(water,  TerrainType.Water),
            };

            var map = new IslandMap(tiles);
            var civ = new Civilization { Index = 0 };
            var state = new WorldState(map, new List<Civilization> { civ }, AtlasController.InvalidIslandId);
            state.AddFeature(new Bandit(Center, 0) { Found = true });

            var clock = new GameClock();
            clock.Start();
            var controller = new MonsterFeatureController();
            controller.Initialize(state, clock, new GamePRNG());

            for (int i = 0; i < 10; i++)
                clock.SimulateAdvance(MonsterFeatureController.MovementIntervalTicks);

            Assert.NotEqual(water, state.Features.OfType<Bandit>().First().Position);
        }

        // ── Harvest blocking and cooldown ────────────────────────────────────

        [Fact]
        public void IsHarvestBlocked_ReturnsTrueWhileBanditPresent()
        {
            var state = new WorldState(
                new IslandMap(new List<HexTile> { new(Center, TerrainType.Desert) }),
                new List<Civilization> { new() { Index = 0 } },
                AtlasController.InvalidIslandId);

            state.AddFeature(new Bandit(Center, 0));

            var clock = new GameClock();
            clock.Start();
            var controller = new MonsterFeatureController();
            controller.Initialize(state, clock, new GamePRNG());

            Assert.True(controller.IsHarvestBlocked(Center, clock.CurrentTick));
        }

        [Fact]
        public void IsHarvestBlocked_ReturnsTrueImmediatelyAfterBanditLeaves()
        {
            // Only two tiles: bandit starts at center and can only go to plain.
            var plain = new HexCoord(1, 0, IslandMap.SurfaceLayer);

            var tiles = new List<HexTile>
            {
                new(Center, TerrainType.Desert),
                new(plain,  TerrainType.Plain),
            };

            var map = new IslandMap(tiles);
            var civ = new Civilization { Index = 0 };
            var state = new WorldState(map, new List<Civilization> { civ }, AtlasController.InvalidIslandId);
            state.AddFeature(new Bandit(Center, 0) { Found = true });

            var clock = new GameClock();
            clock.Start();
            var controller = new MonsterFeatureController();
            controller.Initialize(state, clock, new GamePRNG());

            // Trigger one move: bandit must go to plain (only valid destination).
            clock.SimulateAdvance(MonsterFeatureController.MovementIntervalTicks);

            Assert.Equal(plain, state.Features.OfType<Bandit>().First().Position);
            Assert.True(controller.IsHarvestBlocked(Center, clock.CurrentTick),
                "Cooldown should be active on the tile the bandit just left");
        }

        [Fact]
        public void IsHarvestBlocked_ReturnsFalseAfterCooldownExpires()
        {
            var plain = new HexCoord(1, 0, IslandMap.SurfaceLayer);

            var tiles = new List<HexTile>
            {
                new(Center, TerrainType.Desert),
                new(plain,  TerrainType.Plain),
            };

            var map = new IslandMap(tiles);
            var civ = new Civilization { Index = 0 };
            var state = new WorldState(map, new List<Civilization> { civ }, AtlasController.InvalidIslandId);
            state.AddFeature(new Bandit(Center, 0) { Found = true });

            var clock = new GameClock();
            clock.Start();
            var controller = new MonsterFeatureController();
            controller.Initialize(state, clock, new GamePRNG());

            clock.SimulateAdvance(MonsterFeatureController.MovementIntervalTicks);
            Assert.Equal(plain, state.Features.OfType<Bandit>().First().Position);

            // Advance past the departure cooldown (bandit won't move again because its
            // LastMovedTick was just updated — next move requires another MovementIntervalTicks).
            var bandit = state.Features.OfType<Bandit>().First();
            clock.SimulateAdvance(bandit.DepartureCooldownTicks + 1);

            Assert.False(controller.IsHarvestBlocked(Center, clock.CurrentTick),
                "Cooldown should have expired");
        }

        // ── Raid mechanic ────────────────────────────────────────────────────

        private static (WorldState state, GameClock clock, MonsterFeatureController controller, Civilization civ)
            CreateRaidSetup()
        {
            // City at Vertex(NE, East, NE11) — bandit at Center is adjacent to NE and East.
            var ne   = new HexCoord(0, 1, IslandMap.SurfaceLayer);
            var east = new HexCoord(1, 0, IslandMap.SurfaceLayer);
            var ne11 = new HexCoord(1, 1, IslandMap.SurfaceLayer);

            var tiles = new List<HexTile>
            {
                new(Center, TerrainType.Desert),
                new(ne,     TerrainType.Plain),
                new(east,   TerrainType.Plain),
                new(ne11,   TerrainType.Plain),
            };

            var map = new IslandMap(tiles);
            var civ = new Civilization { Index = 0 };
            var city = new City(Vertex.Create(ne, east, Center)) { CivilizationIndex = 0 };
            civ.AddCity(city);

            var state = new WorldState(map, new List<Civilization> { civ }, AtlasController.InvalidIslandId);
            state.AddFeature(new Bandit(Center, 0) { Found = true });

            var clock = new GameClock();
            clock.Start();
            var controller = new MonsterFeatureController();
            controller.Initialize(state, clock, new GamePRNG());

            return (state, clock, controller, civ);
        }

        [Fact]
        public void Bandit_AdjacentToCity_RaidsAndStealsResource()
        {
            var (state, clock, _, civ) = CreateRaidSetup();
            civ.AddResource(Resource.Wood, 5);

            clock.SimulateAdvance(Bandit.RaidIntervalTicks);

            Assert.Equal(4, civ.GetResourceQuantity(Resource.Wood));
            var bandit = state.Features.OfType<Bandit>().First();
            Assert.NotNull(bandit.LastAttackTargetVertex);
            Assert.NotNull(bandit.LastAttackResourcesString);
            Assert.Contains(nameof(Resource.Wood), bandit.LastAttackResourcesString);
        }

        [Fact]
        public void Bandit_AdjacentToCity_NoResources_DoesNotCrash()
        {
            var (state, clock, _, civ) = CreateRaidSetup();
            // No resources given to civ

            clock.SimulateAdvance(Bandit.RaidIntervalTicks);

            Assert.Equal(0, civ.GetResourceQuantity(Resource.Wood));
            var bandit = state.Features.OfType<Bandit>().First();
            Assert.Null(bandit.LastAttackTargetVertex);
            Assert.Null(bandit.LastAttackResourcesString);
        }

        [Fact]
        public void Bandit_RaidsOncePerInterval()
        {
            var (state, clock, _, civ) = CreateRaidSetup();
            civ.AddResource(Resource.Wood, 10);

            // First raid at tick 100
            clock.SimulateAdvance(Bandit.RaidIntervalTicks);
            Assert.Equal(9, civ.GetResourceQuantity(Resource.Wood));

            // No second raid until another interval passes (bandit also hasn't moved)
            clock.SimulateAdvance(Bandit.RaidIntervalTicks - 1);
            Assert.Equal(9, civ.GetResourceQuantity(Resource.Wood));

            // Second raid fires at tick 200
            clock.SimulateAdvance(1);
            Assert.Equal(8, civ.GetResourceQuantity(Resource.Wood));
        }

        // ── Attaques alternées : zone / concentrée (Tentacule) ─────────────────

        /// <summary>
        /// Tentacule sur Center, deux villes à portée (l'une sur son hex, l'autre à deux anneaux) et
        /// un Aventurier voisin. Les compteurs de l'Aventurier sont placés loin dans le futur : sans
        /// carte de visibilité, il ne verrait de toute façon aucune proie, mais il chercherait à
        /// rentrer vers une ville et bougerait donc de son hex, ce qui fausserait les distances.
        /// </summary>
        private static (GameClock clock, Tentacle tentacle, City onHex, City twoRings, Adventurer adventurer)
            AlternatingAttackSetup()
        {
            var adventurerHex = new HexCoord(1, 0, IslandMap.SurfaceLayer);

            var tiles = new List<HexTile>
            {
                new(Center, TerrainType.Desert),
                new(NE, TerrainType.Plain),
                new(NW, TerrainType.Plain),
                new(adventurerHex, TerrainType.Plain),
            };
            foreach (var hex in VertexAtRing2) tiles.Add(new HexTile(hex, TerrainType.Plain));

            var civ = new Civilization { Index = 0 };
            var onHex = new City(Vertex.Create(Center, NE, NW)) { CivilizationIndex = 0, Soldiers = 100 };
            onHex.AddBuilding(new TownHall { Level = 5 });
            var twoRings = new City(Vertex.Create(VertexAtRing2[0], VertexAtRing2[1], VertexAtRing2[2]))
            {
                CivilizationIndex = 0,
                Soldiers = 100,
            };
            twoRings.AddBuilding(new TownHall { Level = 5 });
            civ.AddCity(onHex);
            civ.AddCity(twoRings);

            var state = new WorldState(new IslandMap(tiles), new List<Civilization> { civ }, AtlasController.InvalidIslandId);
            var tentacle = new Tentacle(Center) { Found = true };
            var adventurer = new Adventurer(adventurerHex)
            {
                Found = true,
                LastMovedTick = long.MaxValue / 2,
                LastAttackTick = long.MaxValue / 2,
            };
            state.AddFeature(tentacle);
            state.AddFeature(adventurer);

            var clock = new GameClock();
            clock.Start();
            var controller = new MonsterFeatureController();
            controller.Initialize(state, clock, new GamePRNG());
            return (clock, tentacle, onHex, twoRings, adventurer);
        }

        [Fact]
        public void Tentacle_FirstAttackSweepsEveryTargetInRange()
        {
            var (clock, tentacle, onHex, twoRings, adventurer) = AlternatingAttackSetup();
            int damage = tentacle.AttackDamage;
            int adventurerHp = adventurer.Hp;

            clock.SimulateAdvance(tentacle.AttackIntervalTicks);

            // Un coup pour chacune des deux villes ET pour l'Aventurier, dans la même salve.
            Assert.Equal(100 - damage, onHex.Soldiers);
            Assert.Equal(100 - damage, twoRings.Soldiers);
            Assert.Equal(adventurerHp - damage, adventurer.Hp);
            Assert.Equal(3, tentacle.LastAttackImpacts.Count);
            Assert.All(tentacle.LastAttackImpacts, i => Assert.Equal(1, i.Strikes));
        }

        [Fact]
        public void Tentacle_SecondAttackConcentratesFiveStrikesOnASingleTarget()
        {
            var (clock, tentacle, onHex, twoRings, adventurer) = AlternatingAttackSetup();
            int damage = tentacle.AttackDamage;

            clock.SimulateAdvance(tentacle.AttackIntervalTicks); // salve de zone
            int afterSweepOnHex = onHex.Soldiers;
            int afterSweepTwoRings = twoRings.Soldiers;
            int afterSweepAdventurer = adventurer.Hp;

            clock.SimulateAdvance(tentacle.AttackIntervalTicks); // salve concentrée

            // La cible prioritaire est la ville posée sur l'hex de la Tentacule ; elle seule encaisse,
            // et elle encaisse cinq coups.
            Assert.Equal(afterSweepOnHex - 5 * damage, onHex.Soldiers);
            Assert.Equal(afterSweepTwoRings, twoRings.Soldiers);
            // L'Aventurier régénère de son côté : seul compte qu'il n'ait pas encaissé de coup de plus.
            Assert.True(adventurer.Hp >= afterSweepAdventurer);

            var impact = Assert.Single(tentacle.LastAttackImpacts);
            Assert.Equal(onHex.Position, impact.Vertex);
            Assert.Equal(Tentacle.TentacleFocusedAttackStrikes, impact.Strikes);
        }

        /// <summary>Frappant à distance, elle n'encaisse pas le coup en retour de l'Aventurier qu'elle balaie.</summary>
        [Fact]
        public void Tentacle_TakesNoReturnBlowFromTheAdventurerItSweeps()
        {
            var (clock, tentacle, _, _, adventurer) = AlternatingAttackSetup();
            int initialHp = tentacle.Hp;
            Assert.True(adventurer.AttackDamage > 0);

            clock.SimulateAdvance(tentacle.AttackIntervalTicks);

            Assert.Equal(initialHp, tentacle.Hp);
        }

        /// <summary>
        /// L'alternance bascule même quand rien n'est à portée : sinon la salve de zone resterait en
        /// réserve et la Tentacule frapperait deux fois de suite de la même façon en retrouvant une cible.
        /// </summary>
        [Fact]
        public void Tentacle_AlternatesEvenWithNothingInRange()
        {
            var tentacle = new Tentacle(Center) { Found = true };
            var (_, clock) = CorruptionSetup(tentacle);

            Assert.True(tentacle.NextAttackIsAreaSweep);
            clock.SimulateAdvance(tentacle.AttackIntervalTicks);
            Assert.False(tentacle.NextAttackIsAreaSweep);
            clock.SimulateAdvance(tentacle.AttackIntervalTicks);
            Assert.True(tentacle.NextAttackIsAreaSweep);
        }

        // ── Deux attaques de front (Dieu démon) ────────────────────────────────

        /// <summary>
        /// Même décor que <see cref="AlternatingAttackSetup"/>, mais avec le Dieu démon au centre :
        /// deux villes (l'une sur son hex, l'autre à deux anneaux — hors de sa portée 2) et un
        /// Aventurier voisin. Les soldats sont assez nombreux pour absorber toute la volée, si bien
        /// que la cascade de dégâts ne touche jamais l'Hôtel de Ville : c'est le décompte de soldats
        /// qui mesure ici les coups reçus.
        /// </summary>
        private static (GameClock clock, DemonGod boss, City onHex, City twoRings, Adventurer adventurer)
            DemonGodAttackSetup()
        {
            var adventurerHex = new HexCoord(1, 0, IslandMap.SurfaceLayer);

            var tiles = new List<HexTile>
            {
                new(Center, TerrainType.Desert),
                new(NE, TerrainType.Plain),
                new(NW, TerrainType.Plain),
                new(adventurerHex, TerrainType.Plain),
            };
            foreach (var hex in VertexAtRing2) tiles.Add(new HexTile(hex, TerrainType.Plain));

            var civ = new Civilization { Index = 0 };
            var onHex = new City(Vertex.Create(Center, NE, NW)) { CivilizationIndex = 0, Soldiers = 10_000 };
            onHex.AddBuilding(new TownHall { Level = 5 });
            var twoRings = new City(Vertex.Create(VertexAtRing2[0], VertexAtRing2[1], VertexAtRing2[2]))
            {
                CivilizationIndex = 0,
                Soldiers = 10_000,
            };
            twoRings.AddBuilding(new TownHall { Level = 5 });
            civ.AddCity(onHex);
            civ.AddCity(twoRings);

            var state = new WorldState(new IslandMap(tiles), new List<Civilization> { civ }, AtlasController.InvalidIslandId);
            var boss = new DemonGod(Center) { Found = true };
            var adventurer = new Adventurer(adventurerHex)
            {
                Found = true,
                LastMovedTick = long.MaxValue / 2,
                LastAttackTick = long.MaxValue / 2,
            };
            state.AddFeature(boss);
            state.AddFeature(adventurer);

            var clock = new GameClock();
            clock.Start();
            var controller = new MonsterFeatureController();
            controller.Initialize(state, clock, new GamePRNG());
            return (clock, boss, onHex, twoRings, adventurer);
        }

        /// <summary>Deux attaques distinctes, chacune avec sa cadence — la seconde une fois et demie plus lente.</summary>
        [Fact]
        public void DemonGod_DeclaresARushAndASlowerFireballSweep()
        {
            var boss = new DemonGod(Center);

            Assert.Equal(2, boss.AttackCount);

            var rush = boss.GetAttack(0);
            Assert.Equal(MonsterAttackPattern.Focused, rush.Pattern);
            Assert.False(rush.IsRanged);
            Assert.Equal(DemonGod.DemonGodAttackIntervalTicks, rush.IntervalTicks);

            var sweep = boss.GetAttack(1);
            Assert.Equal(MonsterAttackPattern.AreaSweep, sweep.Pattern);
            Assert.True(sweep.IsRanged);
            Assert.Equal(2, sweep.RangeInHexes);
            Assert.Equal(rush.IntervalTicks * 3 / 2, sweep.IntervalTicks);

            // La ruée frappe deux fois plus fort que le déluge, à tous les niveaux.
            Assert.Equal(sweep.Damage * 2, rush.Damage);
            var leveled = new DemonGod(Center, level: 4);
            Assert.Equal(leveled.GetAttack(1).Damage * 2, leveled.GetAttack(0).Damage);
        }

        /// <summary>
        /// Au premier intervalle, seule la ruée est due (le déluge est une fois et demie plus lent) :
        /// une seule cible encaisse, et l'Aventurier — que seule la salve de zone atteint — est
        /// épargné.
        /// </summary>
        [Fact]
        public void DemonGod_RushFiresAloneOnTheFirstInterval()
        {
            var (clock, boss, onHex, _, adventurer) = DemonGodAttackSetup();
            int rushDamage = boss.GetAttack(0).Damage;
            int adventurerHp = adventurer.Hp;

            clock.SimulateAdvance(DemonGod.DemonGodAttackIntervalTicks);

            Assert.Equal(10_000 - rushDamage, onHex.Soldiers);
            Assert.Equal(adventurerHp, adventurer.Hp);
            var impact = Assert.Single(boss.LastAttackImpacts);
            Assert.Equal(onHex.Position, impact.Vertex);
            Assert.False(impact.Ranged);
            Assert.False(boss.LastAttackWasRanged);
        }

        /// <summary>
        /// Le déluge balaie tout ce qui est à portée 2 — la ville posée sur son hex ET l'Aventurier
        /// voisin, que la ruée seule n'atteint jamais (elle ne vise que des emplacements militaires)
        /// — mais pas la ville à deux anneaux, hors de portée. Il tire de loin : aucun coup en
        /// retour de l'Aventurier balayé, alors que celui-ci rendrait le sien à un assaillant au
        /// corps-à-corps.
        /// </summary>
        [Fact]
        public void DemonGod_FireballSweepHitsEveryTargetInRangeWithoutReturnBlow()
        {
            var (clock, boss, onHex, twoRings, adventurer) = DemonGodAttackSetup();
            int rushDamage = boss.GetAttack(0).Damage;
            int sweepDamage = boss.GetAttack(1).Damage;
            int bossHp = boss.Hp;
            int adventurerHp = adventurer.Hp;
            Assert.True(adventurer.AttackDamage > 0);

            // Une ruée à T, puis le déluge à 1,5 T — la ruée suivante n'est due qu'à 2 T.
            clock.SimulateAdvance(DemonGod.DemonGodAreaAttackIntervalTicks);

            Assert.Equal(10_000 - rushDamage - sweepDamage, onHex.Soldiers);
            Assert.Equal(10_000, twoRings.Soldiers);
            Assert.Equal(adventurerHp - sweepDamage, adventurer.Hp);

            // Deux cibles balayées, toutes deux bombardées : l'icône ne s'élance pas.
            Assert.Equal(2, boss.LastAttackImpacts.Count);
            Assert.All(boss.LastAttackImpacts, i => Assert.True(i.Ranged));
            Assert.True(boss.LastAttackWasRanged);

            Assert.Equal(bossHp, boss.Hp);
        }

        /// <summary>
        /// Un cycle sur trois, les deux cadences retombent sur le même tick : le déluge s'ajoute
        /// alors à la ruée au lieu de la remplacer — ce qui distingue le boss de la Tentacule, dont
        /// les deux salves ne peuvent jamais tomber ensemble. La ruée reste le premier impact de la
        /// volée : c'est elle qui donne son élan à l'icône, le déluge n'étant que des boules de feu.
        /// </summary>
        [Fact]
        public void DemonGod_BothAttacksLandTogetherEveryThirdCycle()
        {
            var (clock, boss, onHex, _, _) = DemonGodAttackSetup();
            int rushDamage = boss.GetAttack(0).Damage;
            int sweepDamage = boss.GetAttack(1).Damage;
            int bossHp = boss.Hp;

            // Ruées à T, 2 T et 3 T ; déluges à 1,5 T et 3 T — les deux dernières sur le même tick.
            clock.SimulateAdvance(DemonGod.DemonGodAttackIntervalTicks * 3);

            Assert.Equal(10_000 - 3 * rushDamage - 2 * sweepDamage, onHex.Soldiers);

            // La dernière volée porte les deux attaques : la ruée d'abord, puis le déluge sur
            // chacune de ses cibles (la ville et l'Aventurier).
            Assert.Equal(3, boss.LastAttackImpacts.Count);
            Assert.False(boss.LastAttackImpacts[0].Ranged);
            Assert.Equal(onHex.Position, boss.LastAttackImpacts[0].Vertex);
            Assert.All(boss.LastAttackImpacts.Skip(1), i => Assert.True(i.Ranged));
            Assert.False(boss.LastAttackWasRanged);

            Assert.Equal(bossHp, boss.Hp);
        }

        /// <summary>
        /// Une sauvegarde d'avant le déluge n'a qu'une cadence enregistrée : la case manquante
        /// repart de la dernière attaque connue, et non de zéro — sans quoi le déluge frapperait à
        /// l'instant même du chargement.
        /// </summary>
        [Fact]
        public void DemonGod_MissingAttackSlotResumesFromTheLastKnownAttack()
        {
            var boss = new DemonGod(Center) { Found = true, LastAttackTick = 500 };
            Assert.Empty(boss.AttackSlotTicks);

            boss.EnsureAttackSlots(now: 900);

            Assert.Equal(new List<long> { 500, 500 }, boss.AttackSlotTicks);
        }

        // ── Renforcement par la Corruption (Tentacule) ─────────────────────────

        /// <summary>Un seul hex, le monstre dessus, et le contrôleur branché sur l'horloge.</summary>
        private static (WorldState state, GameClock clock) CorruptionSetup(MonsterFeature monster)
        {
            var map = new IslandMap(new List<HexTile> { new(Center, TerrainType.Desert) });
            var civ = new Civilization { Index = 0 };
            var state = new WorldState(map, new List<Civilization> { civ }, AtlasController.InvalidIslandId);
            state.AddFeature(monster);

            var clock = new GameClock();
            clock.Start();
            var controller = new MonsterFeatureController();
            controller.Initialize(state, clock, new GamePRNG());
            return (state, clock);
        }

        [Fact]
        public void Tentacle_GainsArmorAndRegenFromTheCorruptionOnItsHex()
        {
            var tentacle = new Tentacle(Center) { Found = true };
            var (state, clock) = CorruptionSetup(tentacle);

            double baseArmor = tentacle.Armor;
            double baseRegen = tentacle.HpRegenAmount;

            state.AddFeature(new Corruption(Center, level: 3));
            clock.SimulateAdvance(1);

            Assert.Equal(3, tentacle.CorruptionBonus);
            Assert.Equal(baseArmor + 3, tentacle.Armor);
            Assert.Equal(baseRegen + 3, tentacle.HpRegenAmount);
        }

        [Fact]
        public void Tentacle_LosesTheBonusOnceItsHexIsCleansed()
        {
            var tentacle = new Tentacle(Center) { Found = true };
            var (state, clock) = CorruptionSetup(tentacle);

            var corruption = new Corruption(Center, level: 2);
            state.AddFeature(corruption);
            clock.SimulateAdvance(1);
            Assert.Equal(2, tentacle.CorruptionBonus);

            // Réduite d'un niveau, puis entièrement dissipée : le bonus suit dans les deux sens.
            corruption.Level = 1;
            clock.SimulateAdvance(1);
            Assert.Equal(1, tentacle.CorruptionBonus);

            state.RemoveFeature(corruption);
            clock.SimulateAdvance(1);
            Assert.Equal(0, tentacle.CorruptionBonus);
            Assert.Equal(1, tentacle.Armor);
        }

        /// <summary>Garde-fou : le bonus est opt-in, un monstre ordinaire posé sur un hex corrompu n'y gagne rien.</summary>
        [Fact]
        public void Dragon_GainsNothingFromTheCorruptionOnItsHex()
        {
            var dragon = new Dragon(Center) { Found = true };
            var (state, clock) = CorruptionSetup(dragon);

            double baseArmor = dragon.Armor;
            state.AddFeature(new Corruption(Center, level: 4));
            clock.SimulateAdvance(1);

            Assert.False(dragon.EmpoweredByCorruption);
            Assert.Equal(0, dragon.CorruptionBonus);
            Assert.Equal(baseArmor, dragon.Armor);
        }

        // ── Portée d'attaque étendue (Tentacule : 3 hexes) ─────────────────────

        // Un vertex est fait de 3 hexes mutuellement adjacents ; c'est le plus PROCHE qui décide de
        // la portée (voir MonsterFeatureController.IsVertexWithinRange). Les triangles ci-dessous
        // sont choisis pour que ce plus proche tombe exactement sur l'anneau voulu autour de Center.
        private static readonly HexCoord[] VertexAtRing2 =
        {
            new(2, 0, IslandMap.SurfaceLayer),  // distance 2 ← le plus proche
            new(1, 1, IslandMap.SurfaceLayer),  // distance 2
            new(2, 1, IslandMap.SurfaceLayer),  // distance 3
        };

        private static readonly HexCoord[] VertexAtRing3 =
        {
            new(3, 0, IslandMap.SurfaceLayer),  // distance 3 ← le plus proche
            new(2, 1, IslandMap.SurfaceLayer),  // distance 3
            new(3, 1, IslandMap.SurfaceLayer),  // distance 4
        };

        /// <summary>Monstre sur Center et une unique ville (20 soldats, Hôtel de ville 5) au vertex donné.</summary>
        private static (GameClock clock, City city) RangeSetup(MonsterFeature monster, HexCoord[] cityHexes)
        {
            var tiles = new List<HexTile> { new(Center, TerrainType.Desert) };
            foreach (var hex in cityHexes) tiles.Add(new HexTile(hex, TerrainType.Plain));

            var civ = new Civilization { Index = 0 };
            var city = new City(Vertex.Create(cityHexes[0], cityHexes[1], cityHexes[2]))
            {
                CivilizationIndex = 0,
                Soldiers = 20,
            };
            city.AddBuilding(new TownHall { Level = 5 });
            civ.AddCity(city);

            var state = new WorldState(new IslandMap(tiles), new List<Civilization> { civ }, AtlasController.InvalidIslandId);
            state.AddFeature(monster);

            var clock = new GameClock();
            clock.Start();
            var controller = new MonsterFeatureController();
            controller.Initialize(state, clock, new GamePRNG());
            return (clock, city);
        }

        [Fact]
        public void Tentacle_ReachesACityTwoRingsAway()
        {
            var tentacle = new Tentacle(Center) { Found = true };
            var (clock, city) = RangeSetup(tentacle, VertexAtRing2);

            clock.SimulateAdvance(tentacle.AttackIntervalTicks);

            Assert.Equal(city.Position, tentacle.LastAttackTargetVertex);
            Assert.Equal(20 - tentacle.AttackDamage, city.Soldiers);
        }

        [Fact]
        public void Tentacle_DoesNotReachACityThreeRingsAway()
        {
            var tentacle = new Tentacle(Center) { Found = true };
            var (clock, city) = RangeSetup(tentacle, VertexAtRing3);

            clock.SimulateAdvance(tentacle.AttackIntervalTicks);

            Assert.Null(tentacle.LastAttackTargetVertex);
            Assert.Equal(20, city.Soldiers);
        }

        /// <summary>Garde-fou : la portée 2 du Dragon n'a pas bougé en généralisant le rayon.</summary>
        [Fact]
        public void Dragon_DoesNotReachACityTwoRingsAway()
        {
            var dragon = new Dragon(Center) { Found = true };
            var (clock, city) = RangeSetup(dragon, VertexAtRing2);

            clock.SimulateAdvance(Dragon.DragonAttackIntervalTicks);

            Assert.Null(dragon.LastAttackTargetVertex);
            Assert.Equal(20, city.Soldiers);
        }

        // ── Dragon target consistency after a city is destroyed ────────────────

        [Fact]
        public void Dragon_AfterDestroyingCity_StopsTargetingItAndKeepsCountsConsistent()
        {
            // City A sits on the dragon's own hex (always in range). City B is a
            // separate cluster far away, never adjacent to the dragon, and must
            // never be attacked nor destroyed.
            var ne   = new HexCoord(0, 1, IslandMap.SurfaceLayer);
            var east = new HexCoord(1, 0, IslandMap.SurfaceLayer);

            var farCenter = new HexCoord(20, 0, IslandMap.SurfaceLayer);
            var farNE     = new HexCoord(20, 1, IslandMap.SurfaceLayer);
            var farEast   = new HexCoord(21, 0, IslandMap.SurfaceLayer);

            var tiles = new List<HexTile>
            {
                new(Center,    TerrainType.Desert),
                new(ne,        TerrainType.Plain),
                new(east,      TerrainType.Plain),
                new(farCenter, TerrainType.Plain),
                new(farNE,     TerrainType.Plain),
                new(farEast,   TerrainType.Plain),
            };

            var map = new IslandMap(tiles);
            var civ = new Civilization { Index = 0 };

            // City A: 5 soldiers absorb the first hit, a level-1 TownHall falls on the second.
            var cityA = new City(Vertex.Create(Center, ne, east)) { CivilizationIndex = 0, Soldiers = 5 };
            cityA.AddBuilding(new TownHall { Level = 1 });

            // City B: out of the dragon's attack range (own hex + neighbors) for its entire life.
            var cityB = new City(Vertex.Create(farCenter, farNE, farEast)) { CivilizationIndex = 0 };
            cityB.AddBuilding(new TownHall { Level = 5 });

            civ.AddCity(cityA);
            civ.AddCity(cityB);

            var state = new WorldState(map, new List<Civilization> { civ }, AtlasController.InvalidIslandId);
            var dragon = new Dragon(Center) { Found = true };
            state.AddFeature(dragon);

            var clock = new GameClock();
            clock.Start();
            var cityBuilder = new CityBuilderController();
            cityBuilder.Initialize(state, clock, new GamePRNG());
            var controller = new MonsterFeatureController();
            controller.Initialize(state, clock, new GamePRNG(), cityBuilder);

            void AssertCityCountsConsistent()
            {
                Assert.Equal(civ.Cities.Count, state.GetAllCities().Count());
                Assert.Contains(cityB, civ.Cities);
                Assert.NotEqual(cityB.Position, dragon.LastAttackTargetVertex);
            }

            // Attack 1: city A's garrison absorbs the hit, city A survives.
            clock.SimulateAdvance(Dragon.DragonAttackIntervalTicks);
            Assert.Equal(2, civ.Cities.Count);
            Assert.Equal(cityA.Position, dragon.LastAttackTargetVertex);
            AssertCityCountsConsistent();

            // Attack 2: garrison is gone, the TownHall falls → city A is destroyed.
            clock.SimulateAdvance(Dragon.DragonAttackIntervalTicks);
            Assert.Single(civ.Cities);
            Assert.DoesNotContain(cityA, civ.Cities);
            AssertCityCountsConsistent();

            // From now on the dragon has no reachable target (its former target is gone).
            // At every following attack interval, the dragon must NOT keep reporting a
            // target at the destroyed city's vertex — that was the reported bug (the
            // dragon visually keeps "attacking" a location after its city was destroyed).
            for (int i = 0; i < 5; i++)
            {
                clock.SimulateAdvance(Dragon.DragonAttackIntervalTicks);

                Assert.Null(dragon.LastAttackTargetVertex);
                Assert.Null(state.FindCityAt(cityA.Position));
                Assert.Single(civ.Cities);
                AssertCityCountsConsistent();
            }
        }

        // ── Aventurier hors de la carte visible ──────────────────────────────

        [Fact]
        public void Adventurer_StrandedOutsideVisibleMap_StopsFightingAndReturnsToCity()
        {
            // City vertex covers exactly {Center, NE, NW} (radius 1, no watchtower).
            // SE is adjacent to Center but is never visible on its own — the Adventurer starts
            // there, stranded next to a Bandit it must not engage since the Bandit isn't visible.
            var tiles = new List<HexTile>
            {
                new(Center, TerrainType.Desert),
                new(NE,     TerrainType.Plain),
                new(NW,     TerrainType.Plain),
                new(SE,     TerrainType.Plain),
            };

            var map = new IslandMap(tiles);
            var civ = new Civilization { Index = 0 };
            var city = new City(Vertex.Create(Center, NE, NW)) { CivilizationIndex = 0 };
            civ.AddCity(city);

            var state = new WorldState(map, new List<Civilization> { civ }, AtlasController.InvalidIslandId);
            var adventurer = new Adventurer(SE) { Found = true };
            var bandit = new Bandit(SE) { Found = true };
            state.AddFeature(adventurer);
            state.AddFeature(bandit);

            var clock = new GameClock();
            clock.Start();
            var controller = new MonsterFeatureController();
            controller.Initialize(state, clock, new GamePRNG());

            // First tick: the Adventurer is outside the visible map (SE) → it must retreat toward
            // the city instead of fighting the co-located Bandit.
            clock.SimulateAdvance(adventurer.MovementIntervalTicks);

            Assert.Equal(Center, adventurer.Position);
            Assert.Equal(bandit.MaxHp, bandit.Hp);

            // Further ticks: the Adventurer is now inside the visible map, but the Bandit sits on
            // SE, which stays out of sight forever here — still off-limits to the Adventurer.
            // Kept well under the Bandit's own MovementIntervalTicks (3_000L, unrelated to this
            // test) so it never wanders off SE into the Adventurer's visible territory on its own.
            for (int i = 0; i < 8; i++)
                clock.SimulateAdvance(300);

            Assert.Equal(bandit.MaxHp, bandit.Hp);
            Assert.Contains(bandit, state.Features);
        }

        [Fact]
        public void Adventurer_NeverDiscovered_StillReturnsToCityInsteadOfFreezing()
        {
            // Reproduces a real save: an Adventurer can end up with Found == false (never
            // discovered — e.g. its guild's city was destroyed before FeatureController's next
            // discovery pass) while sitting right outside the visible map. Before the fix, the
            // very first check in Update() ("if (!monster.Found) { ...; continue; }") returned
            // immediately for every tick, so the Adventurer never moved — the "locate hero" camera
            // action always centered on the same frozen, invisible hex.
            var tiles = new List<HexTile>
            {
                new(Center, TerrainType.Desert),
                new(NE,     TerrainType.Plain),
                new(NW,     TerrainType.Plain),
                new(SE,     TerrainType.Plain),
            };

            var map = new IslandMap(tiles);
            var civ = new Civilization { Index = 0 };
            var city = new City(Vertex.Create(Center, NE, NW)) { CivilizationIndex = 0 };
            civ.AddCity(city);

            var state = new WorldState(map, new List<Civilization> { civ }, AtlasController.InvalidIslandId);
            var adventurer = new Adventurer(SE) { Found = false };
            state.AddFeature(adventurer);

            var clock = new GameClock();
            clock.Start();
            var controller = new MonsterFeatureController();
            controller.Initialize(state, clock, new GamePRNG());

            clock.SimulateAdvance(adventurer.MovementIntervalTicks);

            Assert.Equal(Center, adventurer.Position);
        }

        // ── Guilde des Aventuriers perdue ─────────────────────────────────────

        [Fact]
        public void Adventurer_GuildCityDestroyed_DiesInstantly()
        {
            var tiles = new List<HexTile>
            {
                new(Center, TerrainType.Desert),
                new(NE,     TerrainType.Plain),
                new(NW,     TerrainType.Plain),
            };

            var map = new IslandMap(tiles);
            var civ = new Civilization { Index = 0 };
            var guildVertex = Vertex.Create(Center, NE, NW);
            var city = new City(guildVertex) { CivilizationIndex = 0 };
            city.AddBuilding(new TownHall { Level = 1 });
            city.AddBuilding(new AdventurersGuild { Level = 1 });
            civ.AddCity(city);

            var state = new WorldState(map, new List<Civilization> { civ }, AtlasController.InvalidIslandId);
            var adventurer = new Adventurer(Center) { Found = true, SpawnCityPosition = guildVertex };
            state.AddFeature(adventurer);

            var clock = new GameClock();
            clock.Start();
            var cityBuilder = new CityBuilderController();
            cityBuilder.Initialize(state, clock, new GamePRNG());
            var controller = new MonsterFeatureController();
            controller.Initialize(state, clock, new GamePRNG(), cityBuilder);

            // Whatever destroys the guild's city (conquest or a monster attack, both funnel
            // through CityBuilderController.DestroyCity) must kill the Adventurer it spawned
            // instantly — the guild is gone, so there's nothing left for it to return to or
            // respawn from.
            cityBuilder.DestroyCity(city, CityDestructionCause.Monster);

            Assert.Empty(state.Features.OfType<Adventurer>());
        }

        [Fact]
        public void Adventurer_OtherCityDestroyed_SurvivesWhenItsOwnGuildStillStands()
        {
            // A second, unrelated city on the same layer is destroyed — the Adventurer's own
            // guild city is untouched, so it must NOT die.
            var far = new HexCoord(20, 0, IslandMap.SurfaceLayer);
            var farNE = new HexCoord(20, 1, IslandMap.SurfaceLayer);
            var farNW = new HexCoord(19, 1, IslandMap.SurfaceLayer);

            var tiles = new List<HexTile>
            {
                new(Center, TerrainType.Desert),
                new(NE,     TerrainType.Plain),
                new(NW,     TerrainType.Plain),
                new(far,    TerrainType.Plain),
                new(farNE,  TerrainType.Plain),
                new(farNW,  TerrainType.Plain),
            };

            var map = new IslandMap(tiles);
            var civ = new Civilization { Index = 0 };
            var guildVertex = Vertex.Create(Center, NE, NW);
            var guildCity = new City(guildVertex) { CivilizationIndex = 0 };
            guildCity.AddBuilding(new TownHall { Level = 1 });
            guildCity.AddBuilding(new AdventurersGuild { Level = 1 });

            var otherVertex = Vertex.Create(far, farNE, farNW);
            var otherCity = new City(otherVertex) { CivilizationIndex = 0 };
            otherCity.AddBuilding(new TownHall { Level = 1 });

            civ.AddCity(guildCity);
            civ.AddCity(otherCity);

            var state = new WorldState(map, new List<Civilization> { civ }, AtlasController.InvalidIslandId);
            var adventurer = new Adventurer(Center) { Found = true, SpawnCityPosition = guildVertex };
            state.AddFeature(adventurer);

            var clock = new GameClock();
            clock.Start();
            var cityBuilder = new CityBuilderController();
            cityBuilder.Initialize(state, clock, new GamePRNG());
            var controller = new MonsterFeatureController();
            controller.Initialize(state, clock, new GamePRNG(), cityBuilder);

            cityBuilder.DestroyCity(otherCity, CityDestructionCause.Monster);

            Assert.Single(state.Features.OfType<Adventurer>());
        }
    }
}
