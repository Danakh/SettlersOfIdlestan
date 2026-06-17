using SettlersOfIdlestan.Controller.Magic;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.GameplayModifier;
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

            var controller = new MagicController();
            controller.Initialize(state, clock, new GamePRNG(42));

            return (state, clock, controller);
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
        public void LaunchRitual_FailsWithoutMageTower()
        {
            var (state, _, controller) = CreateSetup();
            UnlockMagic(state.PlayerCivilization, RitualId.Growth);
            state.PlayerCivilization.AddResource(Resource.Crystal, 50);

            Assert.Equal(0, controller.MaxActiveRituals);
            Assert.False(controller.CanLaunchRitual(RitualId.Growth));
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
        public void LaunchRitual_LimitedByTowerCount()
        {
            var (state, _, controller) = CreateSetup();
            var civ = state.PlayerCivilization;
            UnlockMagic(civ, RitualId.Growth, RitualId.Clairvoyance);
            AddMageTower(state, level: 3);
            civ.AddResource(Resource.Crystal, 50);

            // 1 tour = 1 rituel actif max, même avec assez de puissance
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
            AddMageTower(state, level: 3);
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
            AddMageTower(state, level: 1); // budget de puissance = 1
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
        public void Upkeep_RitualCollapsesWhenTowerIsLost()
        {
            var (state, clock, controller) = CreateSetup();
            var civ = state.PlayerCivilization;
            UnlockMagic(civ, RitualId.Growth);
            var tower = AddMageTower(state);
            civ.AddResource(Resource.Crystal, 50);

            controller.LaunchRitual(RitualId.Growth);

            // La tour est détruite : plus aucun rituel ne peut être maintenu
            state.PlayerCivilization.Cities[0].Buildings.Remove(tower);
            clock.SimulateAdvance(MagicController.UpkeepIntervalTicks);

            Assert.Null(controller.GetActiveRitual(RitualId.Growth));
            Assert.Contains(state.EventLog.Entries, e => e.Type == GameEventType.RitualCollapsed);
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

        // ── Cercles de Fées & Dolmens ────────────────────────────────────────

        [Fact]
        public void EnsureMagicFeatures_SpawnsFeaturesFromModifiers()
        {
            var (state, _, controller) = CreateSetup();
            state.PlayerCivilization.AddCustomAggregator(new StaticModifierProvider(new List<Modifier>
            {
                new(ECategory.MAGIC_FEATURE_COUNT, "FairyCircle", EType.ADDITIVE, 2),
                new(ECategory.MAGIC_FEATURE_COUNT, "Dolmen", EType.ADDITIVE, 1),
            }));

            controller.EnsureMagicFeatures();

            Assert.Equal(2, state.Features.OfType<FairyCircle>().Count());
            Assert.Single(state.Features.OfType<Dolmen>());

            // Idempotent : pas de doublons au second appel
            controller.EnsureMagicFeatures();
            Assert.Equal(2, state.Features.OfType<FairyCircle>().Count());
        }

        [Fact]
        public void PassiveCycle_OnlyDolmenGeneratesCrystals_FairyCircleHandledByAlchimistHut()
        {
            var (state, clock, controller) = CreateSetup();
            var civ = state.PlayerCivilization;

            // Les Cercles de Fées sont récoltés par la Hutte d'Alchimie (HarvestController), pas par ce cycle passif.
            var circle = new FairyCircle(new SettlersOfIdlestan.Model.HexGrid.HexCoord(1, 0, IslandMap.SurfaceLayer)) { Found = true };
            var dolmen = new Dolmen(new SettlersOfIdlestan.Model.HexGrid.HexCoord(-1, 0, IslandMap.SurfaceLayer)) { Found = true };
            state.AddFeature(circle);
            state.AddFeature(dolmen);

            clock.SimulateAdvance(MagicController.UpkeepIntervalTicks);

            Assert.Equal(Dolmen.CrystalsPerCycle, civ.GetResourceQuantity(Resource.Crystal));
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
    }
}
