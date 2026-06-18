using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Controller.Magic;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandFeatures;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Magic;
using SOITests.TestUtilities;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SOITests.ControllerTests
{
    public class MagicControllerTests
    {
        private const int TownHallLevel = 20;

        private static void UnlockMagic(Civilization civ, params RitualId[] knownRituals)
        {
            var modifiers = new List<Modifier> { new(ECategory.UNLOCK_MAGIC, EType.ADDITIVE, 1) };
            foreach (var ritual in knownRituals)
                modifiers.Add(new(ECategory.UNLOCK_RITUAL, ritual.ToString(), EType.ADDITIVE, 1));
            civ.AddCustomAggregator(new StaticModifierProvider(modifiers));
        }

        private static void UnlockSpells(Civilization civ, params SpellId[] knownSpells)
        {
            var modifiers = new List<Modifier> { new(ECategory.UNLOCK_MAGIC, EType.ADDITIVE, 1) };
            foreach (var spell in knownSpells)
                modifiers.Add(new(ECategory.UNLOCK_SPELL, spell.ToString(), EType.ADDITIVE, 1));
            civ.AddCustomAggregator(new StaticModifierProvider(modifiers));
        }

        private static MageTower AddMageTower(WorldState state, int level = 1)
        {
            var tower = new MageTower { Level = level };
            state.PlayerCivilization.Cities[0].Buildings.Add(tower);
            return tower;
        }

        private static (WorldState state, GameClock clock, MagicController controller) CreateSetup()
        {
            var state = IslandTestFactory.CreateSevenHexIslandState();
            state.PlayerCivilization.Cities[0].Buildings.Add(new TownHall { Level = TownHallLevel });

            var clock = new GameClock();
            clock.Start();

            var cityBuilder = new CityBuilderController(state);
            var buildingController = new BuildingController(state);

            var controller = new MagicController();
            controller.Initialize(state, clock, new GamePRNG(42), cityBuilder, buildingController);

            return (state, clock, controller);
        }

        /// <summary>Ajoute une route lointaine offrant un vertex constructible, hors de portée de la ville existante.</summary>
        private static Vertex AddFarBuildableVertex(Civilization civ)
        {
            var farA = new HexCoord(20, 0, IslandMap.SurfaceLayer);
            var farB = new HexCoord(21, 0, IslandMap.SurfaceLayer);
            var farC = new HexCoord(20, 1, IslandMap.SurfaceLayer);
            civ.AddRoad(new Road(Edge.Create(farA, farB)));
            return Vertex.Create(farA, farB, farC);
        }

        // ── Déblocage ────────────────────────────────────────────────────────

        [Fact]
        public void LaunchRitual_FailsWithoutMagicUnlock()
        {
            var (state, _, controller) = CreateSetup();
            AddMageTower(state);
            state.PlayerCivilization.AddResource(Resource.Crystal, 50);

            Assert.False(controller.CanLaunchRitual(RitualId.Growth));
            Assert.False(controller.LaunchRitual(RitualId.Growth));
        }

        [Fact]
        public void LaunchRitual_SucceedsWithoutMageTower_AtBasePower()
        {
            // Le nombre de rituels (1 de base) et la puissance maximale (1 de base) ne dépendent
            // plus des Tours de Mages : un rituel de puissance 1 peut être lancé sans aucune tour.
            var (state, _, controller) = CreateSetup();
            UnlockMagic(state.PlayerCivilization, RitualId.Growth);
            state.PlayerCivilization.AddResource(Resource.Crystal, 50);

            Assert.Equal(1, controller.MaxActiveRituals);
            Assert.Equal(1, controller.TotalPowerBudget);
            Assert.True(controller.CanLaunchRitual(RitualId.Growth));
        }

        [Fact]
        public void LaunchRitual_FailsWithoutCrystals()
        {
            var (state, _, controller) = CreateSetup();
            UnlockMagic(state.PlayerCivilization, RitualId.Growth);
            AddMageTower(state);

            Assert.False(controller.CanLaunchRitual(RitualId.Growth));
        }

        // ── Lancement ────────────────────────────────────────────────────────

        [Fact]
        public void LaunchRitual_ConsumesCrystalsAndAppliesModifiers()
        {
            var (state, _, controller) = CreateSetup();
            var civ = state.PlayerCivilization;
            UnlockMagic(civ, RitualId.Growth);
            AddMageTower(state);
            civ.AddResource(Resource.Crystal, 50);

            Assert.True(controller.LaunchRitual(RitualId.Growth));

            // Coût de lancement : base 5 × 1² = 5 cristaux
            Assert.Equal(45, civ.GetResourceQuantity(Resource.Crystal));
            Assert.NotNull(controller.GetActiveRitual(RitualId.Growth));

            // Effet : +10% de vitesse de récolte par puissance
            double harvestSpeed = civ.ModifierAggregator.ApplyModifiers(ECategory.HARVEST_SPEED, "", 1.0);
            Assert.Equal(1.1, harvestSpeed, 3);
        }

        [Fact]
        public void LaunchRitual_LimitedToOneActiveRitualByDefault()
        {
            var (state, _, controller) = CreateSetup();
            var civ = state.PlayerCivilization;
            UnlockMagic(civ, RitualId.Growth, RitualId.Clairvoyance);
            AddMageTower(state, level: 3);
            civ.AddResource(Resource.Crystal, 50);

            // 1 rituel actif max par défaut (fixe), même avec assez de puissance
            Assert.True(controller.LaunchRitual(RitualId.Growth));
            Assert.False(controller.CanLaunchRitual(RitualId.Clairvoyance));
        }

        [Fact]
        public void StopRitual_RemovesModifiers()
        {
            var (state, _, controller) = CreateSetup();
            var civ = state.PlayerCivilization;
            UnlockMagic(civ, RitualId.Growth);
            AddMageTower(state);
            civ.AddResource(Resource.Crystal, 50);

            controller.LaunchRitual(RitualId.Growth);
            Assert.True(controller.StopRitual(RitualId.Growth));

            Assert.Null(controller.GetActiveRitual(RitualId.Growth));
            double harvestSpeed = civ.ModifierAggregator.ApplyModifiers(ECategory.HARVEST_SPEED, "", 1.0);
            Assert.Equal(1.0, harvestSpeed, 3);
        }

        // ── Puissance : effet linéaire, coût quadratique ─────────────────────

        [Fact]
        public void IncreaseRitualPower_CostsQuadratic_EffectIsLinear()
        {
            var (state, _, controller) = CreateSetup();
            var civ = state.PlayerCivilization;
            UnlockMagic(civ, RitualId.Growth);
            AddMageTower(state, level: 10); // budget de puissance = floor(1 + 10×10%) = 2
            civ.AddResource(Resource.Crystal, 50);

            controller.LaunchRitual(RitualId.Growth); // 5 cristaux (p1)

            // Passage p1 → p2 : 5×2² − 5×1² = 15 cristaux
            Assert.Equal(15, controller.GetPowerIncreaseCost(RitualId.Growth));
            Assert.True(controller.IncreaseRitualPower(RitualId.Growth));
            Assert.Equal(30, civ.GetResourceQuantity(Resource.Crystal));

            // Effet linéaire : +10% × 2 = +20%
            double harvestSpeed = civ.ModifierAggregator.ApplyModifiers(ECategory.HARVEST_SPEED, "", 1.0);
            Assert.Equal(1.2, harvestSpeed, 3);
        }

        [Fact]
        public void IncreaseRitualPower_LimitedByTowerLevels()
        {
            var (state, _, controller) = CreateSetup();
            var civ = state.PlayerCivilization;
            UnlockMagic(civ, RitualId.Growth);
            AddMageTower(state, level: 1); // budget de puissance = floor(1 + 1×10%) = 1
            civ.AddResource(Resource.Crystal, 50);

            controller.LaunchRitual(RitualId.Growth);
            Assert.False(controller.CanIncreaseRitualPower(RitualId.Growth));
        }

        // ── Entretien & effondrement ─────────────────────────────────────────

        [Fact]
        public void Upkeep_ConsumesCrystalsEachCycle()
        {
            var (state, clock, controller) = CreateSetup();
            var civ = state.PlayerCivilization;
            UnlockMagic(civ, RitualId.Growth);
            AddMageTower(state);
            civ.AddResource(Resource.Crystal, 50);

            controller.LaunchRitual(RitualId.Growth); // reste 45

            clock.SimulateAdvance(MagicController.UpkeepIntervalTicks);

            // Entretien : base 1 × 1² = 1 cristal par cycle
            Assert.Equal(44, civ.GetResourceQuantity(Resource.Crystal));
            Assert.NotNull(controller.GetActiveRitual(RitualId.Growth));
        }

        [Fact]
        public void Upkeep_RitualCollapsesWithoutCrystals()
        {
            var (state, clock, controller) = CreateSetup();
            var civ = state.PlayerCivilization;
            UnlockMagic(civ, RitualId.Growth);
            AddMageTower(state);
            civ.AddResource(Resource.Crystal, 5); // juste le coût de lancement

            controller.LaunchRitual(RitualId.Growth); // reste 0

            clock.SimulateAdvance(MagicController.UpkeepIntervalTicks);

            Assert.Null(controller.GetActiveRitual(RitualId.Growth));
            Assert.Contains(state.EventLog.Entries, e => e.Type == GameEventType.RitualCollapsed);
        }

        [Fact]
        public void Upkeep_RitualPowerDecreasesWhenTowerLevelsAreLost()
        {
            // Le budget de puissance de base (1) ne dépend plus des tours : perdre la seule tour
            // ne fait donc plus s'effondrer un rituel de puissance 1, mais réduit la puissance
            // d'un rituel qui dépassait ce budget de base grâce à la tour.
            var (state, clock, controller) = CreateSetup();
            var civ = state.PlayerCivilization;
            UnlockMagic(civ, RitualId.Growth);
            var tower = AddMageTower(state, level: 10); // budget de puissance = floor(1 + 10×10%) = 2
            civ.AddResource(Resource.Crystal, 50);

            controller.LaunchRitual(RitualId.Growth);
            controller.IncreaseRitualPower(RitualId.Growth);
            Assert.Equal(2, controller.GetActiveRitual(RitualId.Growth)!.Power);

            // La tour est détruite : le budget retombe à 1 (base sans tour)
            state.PlayerCivilization.Cities[0].Buildings.Remove(tower);
            clock.SimulateAdvance(MagicController.UpkeepIntervalTicks);

            Assert.NotNull(controller.GetActiveRitual(RitualId.Growth));
            Assert.Equal(1, controller.GetActiveRitual(RitualId.Growth)!.Power);
        }

        [Fact]
        public void Upkeep_ReductionModifierLowersCost()
        {
            var (state, clock, controller) = CreateSetup();
            var civ = state.PlayerCivilization;
            UnlockMagic(civ, RitualId.MartialBlessing);
            civ.AddCustomAggregator(new StaticModifierProvider(new List<Modifier>
            {
                new(ECategory.RITUAL_UPKEEP_REDUCTION, EType.ADDITIVE, 0.5),
            }));
            AddMageTower(state);

            var def = RitualDefinitions.Get(RitualId.MartialBlessing)!;
            // Base 2 × 1² × (1 − 0.5) = 1
            Assert.Equal(1, controller.GetUpkeepCost(def, 1));
        }

        // ── Cercles de Fées ───────────────────────────────────────────────────

        [Fact]
        public void EnsureMagicFeatures_SpawnsFeaturesFromModifiers()
        {
            var (state, _, controller) = CreateSetup();
            state.PlayerCivilization.AddCustomAggregator(new StaticModifierProvider(new List<Modifier>
            {
                new(ECategory.MAGIC_FEATURE_COUNT, "FairyCircle", EType.ADDITIVE, 2),
            }));

            controller.EnsureMagicFeatures();

            Assert.Equal(2, state.Features.OfType<FairyCircle>().Count());

            // Idempotent : pas de doublons au second appel
            controller.EnsureMagicFeatures();
            Assert.Equal(2, state.Features.OfType<FairyCircle>().Count());
        }

        [Fact]
        public void PassiveCycle_UndiscoveredFeaturesGenerateNothing()
        {
            var (state, clock, _) = CreateSetup();
            var civ = state.PlayerCivilization;

            state.AddFeature(new FairyCircle(new SettlersOfIdlestan.Model.HexGrid.HexCoord(1, 0, IslandMap.SurfaceLayer)));

            clock.SimulateAdvance(MagicController.UpkeepIntervalTicks);

            Assert.Equal(0, civ.GetResourceQuantity(Resource.Crystal));
        }

        // ── Sorts instantanés ────────────────────────────────────────────────

        [Fact]
        public void CastSpell_FailsWithoutMagicUnlock()
        {
            var (state, _, controller) = CreateSetup();
            state.PlayerCivilization.AddResource(Resource.Crystal, 50);

            Assert.False(controller.CanCastSpell(SpellId.Abundance));
            Assert.False(controller.CastSpell(SpellId.Abundance));
        }

        [Fact]
        public void CastSpell_FailsWithoutCrystals()
        {
            var (state, _, controller) = CreateSetup();
            UnlockSpells(state.PlayerCivilization, SpellId.Abundance);

            Assert.False(controller.CanCastSpell(SpellId.Abundance));
        }

        [Fact]
        public void CastSpell_ConsumesCrystalsAndGrantsGold()
        {
            var (state, _, controller) = CreateSetup();
            var civ = state.PlayerCivilization;
            UnlockSpells(civ, SpellId.Abundance);
            civ.AddCustomAggregator(new StaticModifierProvider(new List<Modifier>
            {
                new(ECategory.STORAGE_CAPACITY_BASIC, EType.ADDITIVE, 2000),
            }));
            civ.AddResource(Resource.Crystal, 20);

            Assert.True(controller.CastSpell(SpellId.Abundance));

            // Coût : 10 cristaux → récompense : 1000 or
            Assert.Equal(10, civ.GetResourceQuantity(Resource.Crystal));
            Assert.Equal(1000, civ.GetResourceQuantity(Resource.Gold));
        }

        [Fact]
        public void GetKnownSpells_ReturnsOnlyUnlockedSpells()
        {
            var (state, _, controller) = CreateSetup();
            UnlockSpells(state.PlayerCivilization, SpellId.Abundance);

            var known = controller.GetKnownSpells();
            Assert.Single(known);
            Assert.Equal(SpellId.Abundance, known[0].Id);
        }

        // ── Sorts ciblés (invocation de troupes) ─────────────────────────────

        [Fact]
        public void CastSpell_FailsForTargetedSpell()
        {
            var (state, _, controller) = CreateSetup();
            UnlockSpells(state.PlayerCivilization, SpellId.SummonTroops);
            state.PlayerCivilization.AddResource(Resource.Crystal, 200);

            Assert.False(controller.CastSpell(SpellId.SummonTroops));
        }

        [Fact]
        public void GetAllyCityTargets_ReturnsPlayerCitiesOnCurrentLayer()
        {
            var (state, _, controller) = CreateSetup();

            var targets = controller.GetAllyCityTargets();
            Assert.Contains(state.PlayerCivilization.Cities[0].Position, targets);
        }

        [Fact]
        public void CastSpellOnCity_FailsWithoutMagicUnlock()
        {
            var (state, _, controller) = CreateSetup();
            var civ = state.PlayerCivilization;
            civ.AddResource(Resource.Crystal, 200);
            civ.Cities[0].Buildings.Add(new Barracks { Level = 5 });

            Assert.False(controller.CastSpellOnCity(SpellId.SummonTroops, civ.Cities[0].Position));
        }

        [Fact]
        public void CastSpellOnCity_ConsumesCrystalsAndAddsSoldiers()
        {
            var (state, _, controller) = CreateSetup();
            var civ = state.PlayerCivilization;
            UnlockSpells(civ, SpellId.SummonTroops);
            civ.Cities[0].Buildings.Add(new Barracks { Level = 30 });
            civ.AddCustomAggregator(new StaticModifierProvider(new List<Modifier>
            {
                new(ECategory.STORAGE_CAPACITY_ADVANCED, EType.ADDITIVE, 200),
            }));
            civ.AddResource(Resource.Crystal, 200);

            Assert.True(controller.CastSpellOnCity(SpellId.SummonTroops, civ.Cities[0].Position));

            Assert.Equal(100, civ.GetResourceQuantity(Resource.Crystal));
            Assert.Equal(100, civ.Cities[0].Soldiers);
        }

        [Fact]
        public void CastSpellOnCity_AppliesSpellCostReduction()
        {
            var (state, _, controller) = CreateSetup();
            var civ = state.PlayerCivilization;
            UnlockSpells(civ, SpellId.SummonTroops);
            civ.Cities[0].Buildings.Add(new Barracks { Level = 30 });
            civ.AddCustomAggregator(new StaticModifierProvider(new List<Modifier>
            {
                new(ECategory.STORAGE_CAPACITY_ADVANCED, EType.ADDITIVE, 200),
                new(ECategory.SPELL_COST_REDUCTION, "SummonTroops", EType.ADDITIVE, 0.25),
            }));
            civ.AddResource(Resource.Crystal, 200);

            // Coût de base 100 cristaux, réduit de 25% (Cercle d'Invocation) → 75
            var def = SpellDefinitions.Get(SpellId.SummonTroops)!;
            Assert.Equal(75, controller.GetSpellCost(def));

            Assert.True(controller.CastSpellOnCity(SpellId.SummonTroops, civ.Cities[0].Position));
            Assert.Equal(125, civ.GetResourceQuantity(Resource.Crystal));
        }

        [Fact]
        public void CastSpellOnCity_CapsAtMaxSoldiers()
        {
            var (state, _, controller) = CreateSetup();
            var civ = state.PlayerCivilization;
            UnlockSpells(civ, SpellId.SummonTroops);
            civ.Cities[0].Buildings.Add(new Barracks { Level = 1 }); // MaxSoldiers = 5
            civ.AddCustomAggregator(new StaticModifierProvider(new List<Modifier>
            {
                new(ECategory.STORAGE_CAPACITY_ADVANCED, EType.ADDITIVE, 200),
            }));
            civ.AddResource(Resource.Crystal, 200);

            Assert.True(controller.CastSpellOnCity(SpellId.SummonTroops, civ.Cities[0].Position));

            Assert.Equal(5, civ.Cities[0].Soldiers);
        }

        [Fact]
        public void CastSpellOnCity_FailsForUnknownVertex()
        {
            var (state, _, controller) = CreateSetup();
            var civ = state.PlayerCivilization;
            UnlockSpells(civ, SpellId.SummonTroops);
            civ.Cities[0].Buildings.Add(new Barracks { Level = 5 });
            civ.AddCustomAggregator(new StaticModifierProvider(new List<Modifier>
            {
                new(ECategory.STORAGE_CAPACITY_ADVANCED, EType.ADDITIVE, 200),
            }));
            civ.AddResource(Resource.Crystal, 200);

            var farAwayVertex = SettlersOfIdlestan.Model.HexGrid.Vertex.Create(
                new SettlersOfIdlestan.Model.HexGrid.HexCoord(50, 0, IslandMap.SurfaceLayer),
                new SettlersOfIdlestan.Model.HexGrid.HexCoord(51, 0, IslandMap.SurfaceLayer),
                new SettlersOfIdlestan.Model.HexGrid.HexCoord(50, 1, IslandMap.SurfaceLayer));

            Assert.False(controller.CastSpellOnCity(SpellId.SummonTroops, farAwayVertex));
            Assert.Equal(200, civ.GetResourceQuantity(Resource.Crystal));
        }

        // ── Sort ciblé (édification arcanique) ────────────────────────────────

        private static void GrantCrystalStorage(Civilization civ, int amount)
            => civ.AddCustomAggregator(new StaticModifierProvider(new List<Modifier>
            {
                new(ECategory.STORAGE_CAPACITY_ADVANCED, EType.ADDITIVE, amount),
            }));

        [Fact]
        public void CastSpellOnVertex_FailsForUntargetedCast()
        {
            var (state, _, controller) = CreateSetup();
            var civ = state.PlayerCivilization;
            UnlockSpells(civ, SpellId.ArcaneEdification);
            GrantCrystalStorage(civ, 1000);
            civ.AddResource(Resource.Crystal, 1000);

            Assert.False(controller.CastSpell(SpellId.ArcaneEdification));
        }

        [Fact]
        public void CastSpellOnVertex_FailsForNonBuildableVertex()
        {
            var (state, _, controller) = CreateSetup();
            var civ = state.PlayerCivilization;
            UnlockSpells(civ, SpellId.ArcaneEdification);
            GrantCrystalStorage(civ, 1000);
            civ.AddResource(Resource.Crystal, 1000);

            var unreachableVertex = Vertex.Create(
                new HexCoord(50, 0, IslandMap.SurfaceLayer),
                new HexCoord(51, 0, IslandMap.SurfaceLayer),
                new HexCoord(50, 1, IslandMap.SurfaceLayer));

            Assert.False(controller.CastSpellOnVertex(SpellId.ArcaneEdification, unreachableVertex));
            Assert.Equal(1000, civ.GetResourceQuantity(Resource.Crystal));
        }

        [Fact]
        public void CastSpellOnVertex_FoundsFullyDevelopedCity()
        {
            var (state, _, controller) = CreateSetup();
            var civ = state.PlayerCivilization;
            UnlockSpells(civ, SpellId.ArcaneEdification);
            GrantCrystalStorage(civ, 1000);
            civ.AddResource(Resource.Crystal, 1000);

            var targetVertex = AddFarBuildableVertex(civ);

            Assert.True(controller.CastSpellOnVertex(SpellId.ArcaneEdification, targetVertex));

            // Coût : 500 cristaux, sans récompense en or/troupes
            Assert.Equal(500, civ.GetResourceQuantity(Resource.Crystal));

            var city = civ.Cities.Single(c => c.Position.Equals(targetVertex));
            var townHall = city.Buildings.Single(b => b.Type == BuildingType.TownHall);
            Assert.Equal(MagicController.ArcaneEdificationTownHallLevel, townHall.Level);

            var otherBuildings = city.Buildings.Where(b => b.Type != BuildingType.TownHall).ToList();
            Assert.NotEmpty(otherBuildings);
            Assert.All(otherBuildings, b => Assert.Equal(MagicController.ArcaneEdificationBuildingLevel, b.Level));

            // Défense et garnison fournies au maximum, sans coût supplémentaire
            Assert.Equal(city.MaxDefense, city.CurrentDefense);
            Assert.Equal(city.MaxSoldiers, city.Soldiers);
        }

        [Fact]
        public void CastSpellOnVertex_FailsWithoutMagicUnlock()
        {
            var (state, _, controller) = CreateSetup();
            var civ = state.PlayerCivilization;
            civ.AddResource(Resource.Crystal, 1000);
            var targetVertex = AddFarBuildableVertex(civ);

            Assert.False(controller.CastSpellOnVertex(SpellId.ArcaneEdification, targetVertex));
            Assert.DoesNotContain(civ.Cities, c => c.Position.Equals(targetVertex));
        }

        // ── Raisons de blocage (UI : sort grisé + explication) ────────────────

        [Fact]
        public void CanCastSpell_FailsWithoutBuildableVertex()
        {
            var (state, _, controller) = CreateSetup();
            var civ = state.PlayerCivilization;
            UnlockSpells(civ, SpellId.ArcaneEdification);
            GrantCrystalStorage(civ, 1000);
            civ.AddResource(Resource.Crystal, 1000);

            // Aucune route n'a été ajoutée : pas de vertex constructible.
            Assert.False(controller.CanCastSpell(SpellId.ArcaneEdification));
            Assert.Equal("spell_blocked_no_buildable_vertex", controller.GetSpellBlockedReasonKey(SpellId.ArcaneEdification));
        }

        [Fact]
        public void GetSpellBlockedReasonKey_ReturnsCrystalsReasonWhenTargetExists()
        {
            var (state, _, controller) = CreateSetup();
            var civ = state.PlayerCivilization;
            UnlockSpells(civ, SpellId.ArcaneEdification);
            AddFarBuildableVertex(civ);

            // Pas assez de cristaux (capacité de stockage par défaut très basse).
            Assert.False(controller.CanCastSpell(SpellId.ArcaneEdification));
            Assert.Equal("spell_blocked_crystals", controller.GetSpellBlockedReasonKey(SpellId.ArcaneEdification));
        }

        [Fact]
        public void GetSpellBlockedReasonKey_ReturnsNoAllyCityReasonWhenNoCities()
        {
            var (state, _, controller) = CreateSetup();
            var civ = state.PlayerCivilization;
            UnlockSpells(civ, SpellId.SummonTroops);
            civ.RemoveCity(civ.Cities[0]);

            Assert.False(controller.CanCastSpell(SpellId.SummonTroops));
            Assert.Equal("spell_blocked_no_ally_city", controller.GetSpellBlockedReasonKey(SpellId.SummonTroops));
        }

        [Fact]
        public void GetSpellBlockedReasonKey_ReturnsNullWhenCastable()
        {
            var (state, _, controller) = CreateSetup();
            var civ = state.PlayerCivilization;
            UnlockSpells(civ, SpellId.ArcaneEdification);
            GrantCrystalStorage(civ, 1000);
            civ.AddResource(Resource.Crystal, 1000);
            AddFarBuildableVertex(civ);

            Assert.True(controller.CanCastSpell(SpellId.ArcaneEdification));
            Assert.Null(controller.GetSpellBlockedReasonKey(SpellId.ArcaneEdification));
        }
    }
}
