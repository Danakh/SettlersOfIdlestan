using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Controller.Military;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Monsters;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SOITests.ControllerTests
{
    /// <summary>
    /// Scénario : un seul hex partagé, deux villes alliées, un Troll.
    ///
    /// Carte (5 hexes, tous Plains) :
    ///   NE=(0,1)  NE11=(1,1)
    ///   East=(1,0)              ← hex partagé / position du Troll
    ///   East2=(2,0)  SE=(2,-1)
    ///
    /// Ville1 au vertex (NE, East, NE11)
    /// Ville2 au vertex (East, East2, SE)
    /// Le Troll reste sur East (LastMovedTick élevé → pas de déplacement).
    ///
    /// Ce test vérifie trois propriétés :
    ///   1. La regen de défense des villes suffit à résister au Troll (villes non détruites).
    ///   2. La regen du Troll suffit à survivre aux soldats (sans armes en acier).
    ///   3. Avec armes en acier (+1 dégât/attaque) et armures, le Troll finit par mourir.
    /// </summary>
    public class TrollSiegeTests
    {
        private static HexCoord NE    => new(0,  1, IslandMap.SurfaceLayer);
        private static HexCoord East  => new(1,  0, IslandMap.SurfaceLayer);
        private static HexCoord NE11  => new(1,  1, IslandMap.SurfaceLayer);
        private static HexCoord East2 => new(2,  0, IslandMap.SurfaceLayer);
        private static HexCoord SE    => new(2, -1, IslandMap.SurfaceLayer);

        /// <summary>
        /// Crée le monde de test : 2 villes alliées avec Troll sur East.
        /// Le Troll a déjà été découvert (Found=true) et ne bougera pas pendant la simulation
        /// (LastMovedTick très élevé).
        /// </summary>
        private static (WorldState state, GameClock clock, Civilization civ, City city1, City city2, Troll troll)
            CreateSetup(
                int initialSoldiersPerCity,
                int barracksLevel  = 4,
                int palisadeLevel  = 3,
                int arsenalLevel   = 0)
        {
            var tiles = new List<HexTile>
            {
                new(NE,    TerrainType.Plain),
                new(East,  TerrainType.Plain),
                new(NE11,  TerrainType.Plain),
                new(East2, TerrainType.Plain),
                new(SE,    TerrainType.Plain),
            };
            var map = new IslandMap(tiles);

            var civ = new Civilization { Index = 0 };
            civ.Resources[Resource.Ore]   = 99_999;
            civ.Resources[Resource.Food]  = 99_999;
            civ.Resources[Resource.Wood]  = 99_999;
            civ.Resources[Resource.Stone] = 99_999;

            City MakeCity(Vertex vertex)
            {
                var city = new City(vertex)
                {
                    CivilizationIndex = 0,
                    Soldiers = initialSoldiersPerCity,
                };
                city.Buildings.Add(new TownHall { Level = 4 });
                city.Buildings.Add(new Palisade { Level = palisadeLevel });
                city.Buildings.Add(new Barracks { Level = barracksLevel });
                if (arsenalLevel > 0)
                    city.Buildings.Add(new Arsenal { Level = arsenalLevel });
                city.CurrentDefense = city.MaxDefense;
                return city;
            }

            var city1 = MakeCity(Vertex.Create(NE, East, NE11));
            var city2 = MakeCity(Vertex.Create(East, East2, SE));
            civ.AddCity(city1);
            civ.AddCity(city2);

            var state = new WorldState(map, new List<Civilization> { civ }, AtlasController.InvalidIslandId);

            // Troll fixe sur East : trouvé, ne bougera pas (LastMovedTick très élevé).
            var troll = new Troll(East)
            {
                Found         = true,
                LastMovedTick = 999_999_999L,
            };
            state.AddFeature(troll);

            var clock = new GameClock();
            clock.Start();

            // MilitaryController : production soldats + combat soldats→monstre
            new MilitaryController().Initialize(state, clock, prng: new GamePRNG());
            // MonsterFeatureController : regen PV + attaque du Troll sur les villes
            new MonsterFeatureController().Initialize(state, clock, new GamePRNG());

            return (state, clock, civ, city1, city2, troll);
        }

        private static void Simulate(GameClock clock, int totalTicks, int stepTicks = 100)
        {
            for (int elapsed = 0; elapsed < totalTicks; elapsed += stepTicks)
                clock.SimulateAdvance(stepTicks);
        }

        // ── Test 1 : la défense des villes résiste au Troll ──────────────────

        /// <summary>
        /// Avec Palissade L3 (regen défense) et Caserne L4 (soldats absorbant les attaques),
        /// les deux villes survivent à une longue exposition au Troll.
        /// </summary>
        [Fact]
        public void Troll_CitiesWithPalisadeAndBarracks_SurviveAfterExtendedSimulation()
        {
            var (state, clock, civ, _, _, _) = CreateSetup(initialSoldiersPerCity: 50);

            Simulate(clock, totalTicks: 5_000);

            // Les deux villes sont encore en vie.
            Assert.Equal(2, civ.Cities.Count);
            // Le Troll est encore en vie (regen l'empêche de mourir sans armes en acier).
            Assert.Single(state.Features.OfType<Troll>());
        }

        // ── Test 2 : la regen du Troll suffit à survivre aux soldats ─────────

        /// <summary>
        /// Sans armes en acier, la regen du Troll (2 PV toutes les 200 ticks) neutralise exactement
        /// les dégâts des soldats (1 dégât toutes les 100 ticks). Le Troll ne meurt pas et tous les
        /// soldats des deux villes sont consommés dans le combat.
        /// </summary>
        [Fact]
        public void Troll_HpRegen_OutpacesSoldiers_WithoutSteelWeapons()
        {
            int capPerCity = Barracks.MaxSoldiersPerLevel * 4; // Caserne L4 → 20 soldats max
            var (state, clock, civ, city1, city2, troll) = CreateSetup(
                initialSoldiersPerCity: capPerCity);

            int initialTotal = capPerCity * 2;

            // 80 pas × 100 ticks = 8 000 ticks : bien au-delà de l'épuisement des soldats initiaux.
            Simulate(clock, totalTicks: 8_000);

            // Le Troll est encore vivant : sa regen neutralise les dégâts.
            Assert.Single(state.Features.OfType<Troll>());
            Assert.True(troll.Hp > 0);

            // Les soldats initiaux ont tous été engagés contre le Troll (aucun autre ennemi).
            // À ce stade, il ne reste que la production courante ≈ 0-2 soldats par ville.
            int remainingSoldiers = city1.Soldiers + city2.Soldiers;
            Assert.True(remainingSoldiers < initialTotal,
                $"Attendu < {initialTotal} soldats restants, obtenu {remainingSoldiers}.");
        }

        // ── Test 3 : armes en acier + armures → le Troll meurt ───────────────

        /// <summary>
        /// Avec UNLOCK_STEEL_WEAPONS, chaque attaque de soldat inflige 1 dégât supplémentaire
        /// (consomme 1 Arme en Acier). Le dégât net devient positif (2 dmg/100t vs 1 regen/100t)
        /// et le Troll finit par mourir.
        /// Arsenal L3 assure suffisamment de soldats (32/ville) pour absorber la regen sur la durée.
        /// Armures en Acier (50 % de sauvegarde) prolongent la vie des soldats.
        /// </summary>
        [Fact]
        public void Troll_WithSteelWeaponsAndArmor_EventuallyDies()
        {
            // Arsenal L3 → +12 soldats max / ville → cap total = 32 × 2 = 64 soldats.
            int capPerCity = Barracks.MaxSoldiersPerLevel * 4 + Arsenal.MaxSoldiersPerLevel * 3;
            var (state, clock, civ, city1, city2, _) = CreateSetup(
                initialSoldiersPerCity: capPerCity,
                arsenalLevel: 3);

            // Débloquer armes et armures en acier + gros stock.
            civ.AddCustomAggregator(new StaticModifierProvider(new[]
            {
                new Modifier(ECategory.UNLOCK_STEEL_WEAPONS, EType.ADDITIVE, 1),
                new Modifier(ECategory.UNLOCK_STEEL_ARMOR,   EType.ADDITIVE, 1),
            }));
            civ.Resources[Resource.SteelWeapon] = 999;
            civ.Resources[Resource.SteelArmor]  = 999;

            // 60 pas × 100 ticks = 6 000 ticks (troll attendu mort vers 4 000 ticks).
            Simulate(clock, totalTicks: 6_000);

            // Le Troll est mort (retiré de l'état).
            Assert.Empty(state.Features.OfType<Troll>());
            // Les deux villes ont survécu.
            Assert.Equal(2, civ.Cities.Count);
        }
    }
}
