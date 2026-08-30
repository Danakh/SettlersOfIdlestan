using SettlersOfIdlestan.Controller.Ascension;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Ascension;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandFeatures;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Prestige;
using SOITests.TestUtilities;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SOITests.ControllerTests
{
    /// <summary>
    /// Tests de la Nécropole (voir NecropolisController) : placement réservé aux Os Divins non
    /// purifiés adjacents à une ville, destruction des ossements à la construction, montée de niveau
    /// par investissement, et majoration des points divins de l'Ascension (+10% par niveau, voir
    /// AscensionController.GetGodPointsGain).
    /// </summary>
    public class NecropolisControllerTests
    {
        /// <summary>Hex de la ville du joueur de l'île de test (vertex center/NE/E).</summary>
        private static HexCoord CityHex => new(1, 0, IslandMap.SurfaceLayer);

        /// <summary>Hex de l'île de test hors de portée de la ville du joueur.</summary>
        private static HexCoord RemoteHex => new(0, -1, IslandMap.SurfaceLayer);

        private const int TownHallLevel = 20;

        private static void UnlockNecropolis(Civilization civ)
            => civ.AddCustomAggregator(new StaticModifierProvider(new List<Modifier>
            {
                new(ECategory.UNLOCK_NECROPOLIS, EType.ADDITIVE, 1),
                // Les Os Divins ne sont sélectionnables qu'une fois Boussole du Vide acquise.
                new(ECategory.UNLOCK_DIVINE_BONES, EType.ADDITIVE, 1),
            }));

        /// <param name="godState">
        /// Fourni uniquement par les tests de Purification Supérieure : c'est ce qui permet à la
        /// construction de récolter l'essence divine des Os Divins (voir NecropolisController).
        /// </param>
        private static (WorldState state, GameClock clock, NecropolisController controller) CreateSetup(GodState? godState = null)
        {
            var state = IslandTestFactory.CreateSevenHexIslandState();
            state.PlayerCivilization.Cities[0].AddBuilding(new TownHall { Level = TownHallLevel });
            state.PlayerCivilization.RecalculateStorageCapacity();

            var clock = new GameClock();
            clock.Start();

            var controller = new NecropolisController();
            controller.Initialize(state, clock, harvestController: null, godState);

            return (state, clock, controller);
        }

        /// <summary>GodState de test, Purification Supérieure débloquée ou non.</summary>
        private static GodState CreateGodState(bool greaterPurification, int divineEssence = 0)
        {
            var godState = new GodState { DivineEssence = divineEssence };
            if (greaterPurification)
                godState.AscensionState.UnlockedPowers.Add(AscensionPowerId.GreaterPurification);
            return godState;
        }

        private static DivineBones AddBones(WorldState state, HexCoord hex)
        {
            var bones = new DivineBones(hex, corruptionLevel: 5);
            state.AddFeature(bones);
            return bones;
        }

        /// <summary>Amène la Nécropole au bord du level-up : tout est investi, il ne manque que le tick.</summary>
        private static void FillInvestment(Necropolis necropolis, Civilization playerCiv)
        {
            foreach (var kvp in necropolis.GetInvestmentCost(playerCiv))
            {
                necropolis.InvestedResources[kvp.Key] = kvp.Value;
                necropolis.InvestmentEnabled.Add(kvp.Key);
            }
        }

        // ── Déblocage et placement ───────────────────────────────────────────

        [Fact]
        public void CanPlaceNecropolis_FalseWithoutResearch()
        {
            var (state, _, controller) = CreateSetup();
            Assert.False(controller.CanPlaceNecropolis(state.PlayerCivilization));
        }

        [Fact]
        public void CanPlaceNecropolis_TrueWithResearch()
        {
            var (state, _, controller) = CreateSetup();
            UnlockNecropolis(state.PlayerCivilization);
            Assert.True(controller.CanPlaceNecropolis(state.PlayerCivilization));
        }

        [Fact]
        public void CanPlaceNecropolis_FalseWhenAlreadyPlaced()
        {
            var (state, _, controller) = CreateSetup();
            UnlockNecropolis(state.PlayerCivilization);
            AddBones(state, CityHex);
            controller.PlaceNecropolis(CityHex);
            Assert.False(controller.CanPlaceNecropolis(state.PlayerCivilization));
        }

        [Fact]
        public void GetPlaceableHexes_OnlyUnpurifiedDivineBonesAdjacentToPlayerCities()
        {
            var (state, _, controller) = CreateSetup();
            UnlockNecropolis(state.PlayerCivilization);

            // Aucun Os Divin sur l'île
            Assert.Empty(controller.GetPlaceableHexes());

            // Hors de portée d'une ville : la Nécropole ne pourrait jamais monter de niveau
            AddBones(state, RemoteHex);
            Assert.Empty(controller.GetPlaceableHexes());

            var bones = AddBones(state, CityHex);
            Assert.Equal(new[] { CityHex }, controller.GetPlaceableHexes());

            // Purifiés : plus rien à sacrifier
            bones.Purified = true;
            Assert.Empty(controller.GetPlaceableHexes());
        }

        [Fact]
        public void GetPlaceableHexes_EmptyWithoutVoidCompass()
        {
            var (state, _, controller) = CreateSetup();
            AddBones(state, CityHex);
            Assert.Empty(controller.GetPlaceableHexes());
        }

        [Fact]
        public void PlaceNecropolis_DestroysDivineBonesAndLogsEvent()
        {
            var (state, _, controller) = CreateSetup();
            AddBones(state, CityHex);

            var necropolis = controller.PlaceNecropolis(CityHex);

            Assert.NotNull(necropolis);
            Assert.Equal(0, necropolis!.Level);
            Assert.Contains(state.Features.OfType<Necropolis>(), n => n.Position.Equals(CityHex));
            Assert.Empty(state.Features.OfType<DivineBones>());
            Assert.Contains(state.EventLog.Entries, e => e.Type == GameEventType.NecropolisPlaced);
        }

        [Fact]
        public void PlaceNecropolis_FailsWithoutUnpurifiedBones()
        {
            var (state, _, controller) = CreateSetup();

            Assert.Null(controller.PlaceNecropolis(CityHex));

            var bones = AddBones(state, CityHex);
            bones.Purified = true;
            Assert.Null(controller.PlaceNecropolis(CityHex));

            Assert.Empty(state.Features.OfType<Necropolis>());
            Assert.Contains(state.Features.OfType<DivineBones>(), b => b.Position.Equals(CityHex));
        }

        // ── Purification Supérieure ──────────────────────────────────────────

        /// <summary>Les Os Divins de test portent un niveau de corruption 5, soit un plafond de 5 essences.</summary>
        [Fact]
        public void PlaceNecropolis_WithGreaterPurification_HarvestsDivineEssence()
        {
            var godState = CreateGodState(greaterPurification: true);
            var (state, _, controller) = CreateSetup(godState);
            AddBones(state, CityHex);

            Assert.NotNull(controller.PlaceNecropolis(CityHex));

            Assert.Equal(1, godState.DivineEssence);
            Assert.Equal(1, godState.TotalDivineEssenceEarned);
            Assert.Empty(state.Features.OfType<DivineBones>());
            Assert.Contains(state.EventLog.Entries, e => e.Type == GameEventType.DivineBonesPurified);
        }

        /// <summary>Plafond d'essences atteint : la Nécropole se bâtit quand même, sans rien octroyer.</summary>
        [Fact]
        public void PlaceNecropolis_WithGreaterPurificationAtEssenceCap_GrantsNothing()
        {
            var godState = CreateGodState(greaterPurification: true, divineEssence: 5);
            var (state, _, controller) = CreateSetup(godState);
            AddBones(state, CityHex);

            Assert.NotNull(controller.PlaceNecropolis(CityHex));

            Assert.Equal(5, godState.DivineEssence);
            Assert.Equal(0, godState.TotalDivineEssenceEarned);
            Assert.Contains(state.EventLog.Entries, e => e.Type == GameEventType.DivineBonesPurifiedNoEssence);
        }

        [Fact]
        public void PlaceNecropolis_WithoutGreaterPurification_DestroysBonesWithoutEssence()
        {
            var godState = CreateGodState(greaterPurification: false);
            var (state, _, controller) = CreateSetup(godState);
            AddBones(state, CityHex);

            Assert.NotNull(controller.PlaceNecropolis(CityHex));

            Assert.Equal(0, godState.DivineEssence);
            Assert.Equal(0, godState.TotalDivineEssenceEarned);
            Assert.Empty(state.Features.OfType<DivineBones>());
            Assert.DoesNotContain(state.EventLog.Entries, e => e.Type == GameEventType.DivineBonesPurified);
        }

        // ── Investissement ───────────────────────────────────────────────────

        [Fact]
        public void Investment_ConsumesResourcesAndLevelsUp()
        {
            var (state, clock, controller) = CreateSetup();
            var civ = state.PlayerCivilization;
            AddBones(state, CityHex);
            var necropolis = controller.PlaceNecropolis(CityHex)!;

            FillInvestment(necropolis, civ);

            clock.SimulateAdvance(NecropolisController.InvestmentIntervalTicks);

            Assert.Equal(1, necropolis.Level);
            Assert.Empty(necropolis.InvestedResources);
            Assert.Empty(necropolis.InvestmentEnabled);
            Assert.Contains(state.EventLog.Entries, e => e.Type == GameEventType.NecropolisLevelUp);
        }

        [Fact]
        public void Investment_IncompleteCost_DoesNotLevelUp()
        {
            var (state, clock, controller) = CreateSetup();
            var civ = state.PlayerCivilization;
            AddBones(state, CityHex);
            var necropolis = controller.PlaceNecropolis(CityHex)!;

            var cost = necropolis.GetInvestmentCost(civ);
            foreach (var kvp in cost)
                necropolis.InvestedResources[kvp.Key] = kvp.Value;
            necropolis.InvestedResources[Resource.Mithril] = cost[Resource.Mithril] - 1;

            clock.SimulateAdvance(NecropolisController.InvestmentIntervalTicks);

            Assert.Equal(0, necropolis.Level);
        }

        [Fact]
        public void Investment_StopsAtMaxLevel()
        {
            var (state, clock, controller) = CreateSetup();
            var civ = state.PlayerCivilization;
            AddBones(state, CityHex);
            var necropolis = controller.PlaceNecropolis(CityHex)!;

            for (int i = 0; i < Necropolis.MaxLevel; i++)
            {
                FillInvestment(necropolis, civ);
                clock.SimulateAdvance(NecropolisController.InvestmentIntervalTicks);
            }

            Assert.True(necropolis.IsMaxLevel);

            // Un cycle de plus ne dépasse jamais le niveau maximum
            FillInvestment(necropolis, civ);
            clock.SimulateAdvance(NecropolisController.InvestmentIntervalTicks);
            Assert.Equal(Necropolis.MaxLevel, necropolis.Level);
        }

        [Fact]
        public void GetLevelCost_GrowsWithLevelAndUsesExpectedResources()
        {
            var cost = Necropolis.GetLevelCost(1);
            Assert.Equal(new[] { Resource.Stone, Resource.Brick, Resource.Crystal, Resource.Mithril }.OrderBy(r => r),
                cost.Keys.OrderBy(r => r));

            foreach (var resource in cost.Keys)
                Assert.True(Necropolis.GetLevelCost(2)[resource] > cost[resource]);
        }

        // ── Effet sur les points divins de l'Ascension ───────────────────────

        [Fact]
        public void AscensionGainBonus_Is10PercentPerLevel()
        {
            Assert.Equal(0.0, Necropolis.GetAscensionGainBonusForLevel(0), 6);
            Assert.Equal(0.10, Necropolis.GetAscensionGainBonusForLevel(1), 6);
            Assert.Equal(0.10 * Necropolis.MaxLevel, Necropolis.GetAscensionGainBonusForLevel(Necropolis.MaxLevel), 6);

            // Jamais au-delà du niveau maximum
            Assert.Equal(Necropolis.GetAscensionGainBonusForLevel(Necropolis.MaxLevel),
                         Necropolis.GetAscensionGainBonusForLevel(Necropolis.MaxLevel + 3), 6);
        }

        [Fact]
        public void GetGodPointsGain_FollowsNecropolisLevel()
        {
            var state = IslandTestFactory.CreateSevenHexIslandState();
            var godState = new GodState { DivineEssence = 10 };
            var ascension = new AscensionController();
            ascension.Initialize(state, clock: null, new GamePRNG(1), new HarvestController(), godState);

            // Sans Nécropole : 1 point divin par essence
            Assert.Equal(0, ascension.GetNecropolisLevel());
            Assert.Equal(10, ascension.GetGodPointsGain(godState));

            var necropolis = new Necropolis(CityHex);
            state.AddFeature(necropolis);

            necropolis.Level = 2;
            Assert.Equal(2, ascension.GetNecropolisLevel());
            Assert.Equal(0.20, ascension.GetNecropolisAscensionBonus(), 6);
            Assert.Equal(12, ascension.GetGodPointsGain(godState));

            // Au niveau maximum (4), le bonus plafonne à +40%
            necropolis.Level = Necropolis.MaxLevel;
            Assert.Equal(14, ascension.GetGodPointsGain(godState));
        }
    }
}
