using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Controller.Ascension;
using SettlersOfIdlestan.Controller.Expand;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandFeatures;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Monsters;
using SettlersOfIdlestan.Model.Prestige;
using Xunit;

namespace SOITests.ControllerTests
{
    /// <summary>
    /// Tests de la mort du Dieu démon (PandemoniumGateController.RegisterDemonGodDefeat) : le record
    /// permanent de niveau, le bonus au plafond d'essence divine limité au cycle de prestige,
    /// l'essence octroyée sous ce plafond, et les trois annonces que le journal doit distinguer.
    /// </summary>
    public class DemonGodDefeatTests
    {
        private static HexCoord BossHex => new(0, 0, LayerState.PandemoniumZ);

        private static (WorldState state, GodState godState, PandemoniumGateController controller) CreateSetup(
            int corruptionLevel = 5)
        {
            var (state, godState, controller, _) = CreateSetupWithClock(corruptionLevel);
            return (state, godState, controller);
        }

        private static (WorldState state, GodState godState, PandemoniumGateController controller, GameClock clock)
            CreateSetupWithClock(int corruptionLevel = 5)
        {
            var surfaceMap = new IslandMap(new[] { new HexTile(new HexCoord(0, 0, IslandMap.SurfaceLayer), TerrainType.Plain) });
            var civ = new Civilization { Index = 0 };
            var state = new WorldState(surfaceMap, new List<Civilization> { civ }, AtlasController.InvalidIslandId);
            state.AddLayer(LayerState.PandemoniumZ,
                new LayerState(new IslandMap(new[] { new HexTile(BossHex, TerrainType.Mountain) }, LayerState.PandemoniumZ)));

            var prestigeState = new PrestigeState { CurrentCorruptionLevel = corruptionLevel };
            var godState = new GodState(prestigeState);

            var clock = new GameClock();
            clock.Start();

            var controller = new PandemoniumGateController();
            controller.Initialize(state, clock, prng: new GamePRNG(1), prestigeState: prestigeState, godState: godState);

            return (state, godState, controller, clock);
        }

        /// <summary>Tue le boss comme le fait le combat : PV à zéro, retrait de la feature, puis journalisation par l'appelant.</summary>
        private static DemonGod Kill(WorldState state, int level)
        {
            var boss = new DemonGod(BossHex, level);
            state.AddFeature(boss);
            boss.Hp = 0;
            state.RemoveFeature(boss);
            state.EventLog.Add(boss.RemovedEventType, boss.RemovedEventMessage, boss.RemovedEventIsToast);
            return boss;
        }

        /// <summary>
        /// Ce que le prestige remet à zéro et qui compte ici : l'essence du cycle, son bonus de
        /// plafond (PrestigeController.PerformPrestige) et le RunRecord, qui repart avec l'île neuve.
        /// Le record de niveau, lui, n'est touché par rien.
        /// </summary>
        private static void SimulatePrestige(WorldState state, GodState godState)
        {
            godState.DivineEssence = 0;
            godState.DivineEssenceCapBonusFromDemonGod = 0;
            state.RunRecord.DemonGodDefeated = false;
        }

        [Fact]
        public void FirstKill_RecordsLevelPermanently_AndRaisesCapByThatLevel()
        {
            var (state, godState, _) = CreateSetup();

            Kill(state, level: 7);

            Assert.Equal(7, godState.HighestDemonGodLevelDefeated);
            Assert.Equal(7, godState.DivineEssenceCapBonusFromDemonGod);
            // Plafond : corruption 5 + 0 pouvoir divin + 7 du boss.
            Assert.Equal(12, AscensionController.GetDivineEssenceCap(godState));
        }

        [Fact]
        public void Kill_GrantsAsMuchDivineEssenceAsTheBossLevel_WhenUnderTheCap()
        {
            var (state, godState, _) = CreateSetup();

            Kill(state, level: 7);

            Assert.Equal(7, godState.DivineEssence);
            Assert.Equal(7, godState.TotalDivineEssenceEarned);
        }

        /// <summary>
        /// Plafond déjà saturé avant la victoire : le bonus l'ayant relevé du niveau du boss, c'est
        /// exactement ce niveau qui est encore octroyé — la victoire ne perd rien à arriver tard.
        /// </summary>
        [Fact]
        public void Kill_StillGrantsFullLevel_WhenCapWasAlreadySaturated()
        {
            var (state, godState, _) = CreateSetup();
            godState.DivineEssence = AscensionController.GetDivineEssenceCap(godState);

            Kill(state, level: 4);

            Assert.Equal(9, godState.DivineEssence);
            Assert.Equal(9, AscensionController.GetDivineEssenceCap(godState));
        }

        /// <summary>Le plafond ne compte jamais les essences du Reliquaire, ici comme pour les Os Divins.</summary>
        [Fact]
        public void Kill_IgnoresReliquaryEssenceWhenClampingTheGain()
        {
            var (state, godState, _) = CreateSetup();
            godState.DivineEssenceReliquaryFloor = 100;

            Kill(state, level: 3);

            Assert.Equal(3, godState.DivineEssence);
        }

        /// <summary>
        /// Un cycle n'offre qu'un boss : la victoire suivante arrive après un prestige, qui a remis
        /// à zéro l'essence et le bonus de plafond. Vaincue à un niveau inférieur au record, elle
        /// rapporte quand même son plein dû — seul le record ne bouge pas.
        /// </summary>
        [Fact]
        public void NextCycleKillBelowTheRecord_KeepsTheRecord_ButStillPaysInFull()
        {
            var (state, godState, _) = CreateSetup();
            Kill(state, level: 7);

            SimulatePrestige(state, godState);
            Kill(state, level: 3);

            Assert.Equal(7, godState.HighestDemonGodLevelDefeated);
            Assert.Equal(3, godState.DivineEssenceCapBonusFromDemonGod);
            Assert.Equal(3, godState.DivineEssence);
        }

        [Fact]
        public void NextCycleKillAboveTheRecord_RaisesTheRecord()
        {
            var (state, godState, _) = CreateSetup();
            Kill(state, level: 3);

            SimulatePrestige(state, godState);
            Kill(state, level: 8);

            Assert.Equal(8, godState.HighestDemonGodLevelDefeated);
        }

        /// <summary>
        /// Les deux horodatages affichés par l'onglet Partie : la première victoire est figée, celle
        /// du record suit le record. Une victoire sans record ne touche ni l'un ni l'autre.
        /// </summary>
        [Fact]
        public void DefeatTicks_FreezeTheFirstKill_AndFollowTheRecord()
        {
            var (state, godState, _, clock) = CreateSetupWithClock();

            clock.CurrentTick = 1000;
            Kill(state, level: 3);

            Assert.Equal(3, godState.FirstDemonGodLevelDefeated);
            Assert.Equal(1000, godState.FirstDemonGodDefeatTick);
            Assert.Equal(1000, godState.HighestDemonGodDefeatTick);

            SimulatePrestige(state, godState);
            clock.CurrentTick = 2500;
            Kill(state, level: 2);

            // Sous le record : rien ne bouge.
            Assert.Equal(1000, godState.FirstDemonGodDefeatTick);
            Assert.Equal(1000, godState.HighestDemonGodDefeatTick);

            SimulatePrestige(state, godState);
            clock.CurrentTick = 9000;
            Kill(state, level: 8);

            Assert.Equal(3, godState.FirstDemonGodLevelDefeated);
            Assert.Equal(1000, godState.FirstDemonGodDefeatTick);
            Assert.Equal(8, godState.HighestDemonGodLevelDefeated);
            Assert.Equal(9000, godState.HighestDemonGodDefeatTick);
        }

        [Theory]
        [InlineData(GameEventType.DemonGodDefeatedFirst, 0, 5)]
        [InlineData(GameEventType.DemonGodDefeated, 9, 5)]
        [InlineData(GameEventType.DemonGodDefeatedRecord, 2, 5)]
        public void DefeatEvent_TellsApartFirstKillPlainKillAndNewRecord(
            GameEventType expected, int previousRecord, int level)
        {
            var (state, godState, _) = CreateSetup();
            godState.HighestDemonGodLevelDefeated = previousRecord;

            Kill(state, level);

            Assert.Equal(expected, state.EventLog.Entries[0].Type);
        }

        /// <summary>Le journal cite le niveau abattu, l'essence reçue et le record — figés au retrait.</summary>
        [Fact]
        public void DefeatEvent_CarriesLevelEssenceAndRecord_AndAlwaysToasts()
        {
            var (state, godState, _) = CreateSetup();
            godState.HighestDemonGodLevelDefeated = 9;

            Kill(state, level: 5);

            var entry = state.EventLog.Entries[0];
            Assert.True(entry.Toast);
            var args = GameLogEntry.SplitMessageArgs(entry.Message, 3);
            Assert.Equal(new[] { "5", "5", "9" }, args);
        }

        /// <summary>
        /// Vider la couche après la perte de la dernière ville du Pandémonium retire le boss vivant :
        /// ce n'est pas une victoire, ni pour le record, ni pour l'essence, ni pour le journal.
        /// </summary>
        [Fact]
        public void RemovingALivingBoss_IsNotAVictory()
        {
            var (state, godState, _) = CreateSetup();
            var boss = new DemonGod(BossHex, 7);
            state.AddFeature(boss);

            state.RemoveFeature(boss);

            Assert.Equal(0, godState.HighestDemonGodLevelDefeated);
            Assert.Equal(0, godState.DivineEssenceCapBonusFromDemonGod);
            Assert.Equal(0, godState.DivineEssence);
            Assert.Null(boss.Defeat);
            Assert.Equal(GameEventType.DemonGodDefeated, boss.RemovedEventType);
        }

        [Fact]
        public void OnDemonGodDefeated_ReportsTheFullOutcome()
        {
            var (state, godState, controller) = CreateSetup();
            godState.HighestDemonGodLevelDefeated = 2;
            DemonGodDefeat? received = null;
            controller.OnDemonGodDefeated += (_, d) => received = d;

            Kill(state, level: 6);

            Assert.NotNull(received);
            var defeat = received!.Value;
            Assert.Equal(6, defeat.Level);
            Assert.Equal(6, defeat.EssenceGained);
            Assert.Equal(6, defeat.RecordLevel);
            Assert.False(defeat.IsFirstEver);
            Assert.True(defeat.BeatsRecord);
        }

        /// <summary>
        /// Le Portail du Pandémonium s'efface avec son maître : plus rien à reconstruire, et
        /// TryInitializePandemonium ne peut plus redresser d'arène (elle exige un portail bâti).
        /// </summary>
        [Fact]
        public void Victory_RemovesThePandemoniumGate_AndFlagsTheRun()
        {
            var (state, _, _) = CreateSetup();
            state.AddFeature(new PandemoniumGate(new HexCoord(0, 0, LayerState.AbyssZ)) { Built = true, WasEverBuilt = true });

            Kill(state, level: 7);

            Assert.Empty(state.Features.OfType<PandemoniumGate>());
            Assert.True(state.RunRecord.DemonGodDefeated);
        }

        /// <summary>Une Tentacule de l'Abysse tuée après la victoire ne rouvre plus rien : la branche est close jusqu'au prestige.</summary>
        [Fact]
        public void AfterVictory_KillingAnotherAbyssTentacle_OpensNoNewGate()
        {
            var (state, _, _) = CreateSetup();
            state.AddFeature(new PandemoniumGate(new HexCoord(0, 0, LayerState.AbyssZ)) { Built = true });
            Kill(state, level: 7);

            var tentacle = new Tentacle(new HexCoord(2, 0, LayerState.AbyssZ));
            state.AddFeature(tentacle);
            tentacle.Hp = 0;
            state.RemoveFeature(tentacle);

            Assert.Empty(state.Features.OfType<PandemoniumGate>());
            Assert.False(tentacle.OpenedPandemoniumGate);
        }

        /// <summary>
        /// Perdre le Pandémonium après avoir vaincu son maître n'annonce plus rien : il n'y a plus
        /// ni portail ni accès à reconquérir. À distinguer d'une perte ordinaire, elle bien annoncée
        /// même sans portail — voir DeepLayerLossChainTests.
        /// </summary>
        [Fact]
        public void LosingThePandemoniumAfterTheVictory_AnnouncesNoGateLoss()
        {
            var (state, _, controller) = CreateSetup();
            var civ = state.PlayerCivilization;
            var cityVertex = Vertex.Create(BossHex, new HexCoord(1, 0, LayerState.PandemoniumZ), new HexCoord(0, 1, LayerState.PandemoniumZ));
            var city = new City(cityVertex) { CivilizationIndex = civ.Index };
            civ.AddCity(city);
            state.AddFeature(new PandemoniumGate(new HexCoord(0, 0, LayerState.AbyssZ)) { Built = true });

            Kill(state, level: 7);
            civ.RemoveCity(city);
            controller.OnCityDestroyed(cityVertex, civ.Index);

            Assert.DoesNotContain(state.EventLog.Entries, e => e.Type == GameEventType.PandemoniumGateLost);
        }

        /// <summary>Les Os Divins voient le même plafond que l'écran d'Ascension — sinon la Purification se croirait bloquée.</summary>
        [Fact]
        public void DivineBonesEssenceCap_FollowsTheDemonGodBonus()
        {
            var (state, godState, _) = CreateSetup();
            Kill(state, level: 7);

            var bones = new DivineBones { CorruptionLevel = 5, DemonGodCapBonus = godState.DivineEssenceCapBonusFromDemonGod };

            Assert.Equal(AscensionController.GetDivineEssenceCap(godState), bones.GetEssenceCap());
        }
    }
}
