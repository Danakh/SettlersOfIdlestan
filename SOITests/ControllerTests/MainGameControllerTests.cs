using SettlersOfIdlestan.Controller;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandFeatures;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Prestige;
using SOITests.TestUtilities;
using System;
using System.Linq;
using System.Text.Json;
using Xunit;

namespace SOITests.ControllerTests
{
    public class MainGameControllerTests
    {
        private static JsonSerializerOptions CreateOptions(bool writeIndented = false, bool caseInsensitive = false)
        {
            var options = new JsonSerializerOptions { WriteIndented = writeIndented, PropertyNameCaseInsensitive = caseInsensitive };
            options.Converters.Add(new HexCoordJsonConverter());
            options.Converters.Add(new SettlersOfIdlestan.Model.IslandMap.IslandMapJsonConverter());
            options.Converters.Add(new VertexJsonConverter());
            return options;
        }

        [Fact]
        public void ImportMainState_PreservesCities()
        {
            var WorldState = IslandTestFactory.CreateSevenHexIslandState();
            var clock = new GameClock();
            var mainState = new MainGameState(WorldState, clock, new GamePRNG(42));

            var json = JsonSerializer.Serialize(mainState, SaveController.SerializationOptions());

            var controller = new MainGameController();
            var imported = controller.ImportMainState(json);

            Assert.NotNull(imported);
            var importedIsland = imported.CurrentWorldState;
            Assert.NotNull(importedIsland);
            Assert.NotEmpty(importedIsland.Civilizations);
            var civ = importedIsland.Civilizations[0];
            Assert.NotEmpty(civ.Cities);
            Assert.NotNull(civ.Cities[0].Position);
            Assert.Equal(3, civ.Cities[0].Position.GetHexes().Length);
        }

        [Fact]
        public void ExportMainState_RoundtripPreservesCities()
        {
            var WorldState = IslandTestFactory.CreateSevenHexIslandState();
            var clock = new GameClock();
            var mainState = new MainGameState(WorldState, clock, new GamePRNG(42));

            var json = JsonSerializer.Serialize(mainState, SaveController.SerializationOptions());

            var controller = new MainGameController();
            controller.ImportMainState(json);

            var exported = controller.ExportMainState();

            // L'export est chiffré — on passe par ImportMainState pour le round-trip
            var controller2 = new MainGameController();
            var round = controller2.ImportMainState(exported);

            Assert.NotNull(round);
            var island = round.CurrentWorldState;
            Assert.NotNull(island);
            Assert.NotEmpty(island.Civilizations);
            var civ = island.Civilizations[0];
            Assert.NotEmpty(civ.Cities);
        }

        [Fact]
        public void ExportMainState_RoundtripPreservesAutomationSettings()
        {
            var WorldState = IslandTestFactory.CreateSevenHexIslandState();
            var clock = new GameClock();
            var mainState = new MainGameState(WorldState, clock, new GamePRNG(42));
            mainState.CurrentWorldState!.AutomationSettings.MilitaryReinforcementAutomationEnabled = true;
            mainState.Settings.PinnedCivPanelKeys.Add("MilitaryReinforcement");

            var json = JsonSerializer.Serialize(mainState, SaveController.SerializationOptions());

            var controller = new MainGameController();
            controller.ImportMainState(json);

            var exported = controller.ExportMainState();

            var controller2 = new MainGameController();
            var round = controller2.ImportMainState(exported);

            Assert.True(round.CurrentWorldState!.AutomationSettings.MilitaryReinforcementAutomationEnabled);
            Assert.Contains("MilitaryReinforcement", round.Settings.PinnedCivPanelKeys);
        }

        [Fact]
        public void ImportMainState_MigratesLegacyPerIslandPinsIntoPersistentSettings()
        {
            var WorldState = IslandTestFactory.CreateSevenHexIslandState();
            var clock = new GameClock();
            var mainState = new MainGameState(WorldState, clock, new GamePRNG(42));
            // Ancien format (pré-migration) : les épingles étaient stockées par île.
            mainState.CurrentWorldState!.AutomationSettings.PinnedToCivPanel.Add("MilitaryReinforcement");

            var json = JsonSerializer.Serialize(mainState, SaveController.SerializationOptions());

            var controller = new MainGameController();
            var imported = controller.ImportMainState(json);

            Assert.Contains("MilitaryReinforcement", imported.Settings.PinnedCivPanelKeys);
        }

        [Fact]
        public void ExportMainState_RoundtripPreservesNpcAggressivityEscalation()
        {
            var WorldState = IslandTestFactory.CreateSevenHexIslandState();
            var clock = new GameClock();
            var mainState = new MainGameState(WorldState, clock, new GamePRNG(42));

            var npcCiv = new SettlersOfIdlestan.Model.Civilization.Civilization
            {
                Index = mainState.CurrentWorldState!.Civilizations.Max(c => c.Index) + 1,
                IsNpc = true,
                NpcParameters = new SettlersOfIdlestan.Model.Civilization.NpcParameters
                {
                    // État tel que laissé par NpcGameController.OnCityAttacked après une attaque du joueur.
                    AggressivityLevel = SettlersOfIdlestan.Model.Civilization.NpcAggressivityLevel.Warlike,
                },
                WarEnemyCivIndices = { 0 },
            };
            // Une ville est nécessaire : MainGameController.SetGameFromSave retire au chargement
            // toute civ PNJ à 0 ville (nettoyage des civs éliminées, voir RemoveEliminatedCivilization/
            // PruneEliminatedCivilizations) — sans ville, cette civ de test disparaîtrait avant même
            // l'assertion, alors que ce test vise justement à vérifier qu'elle survit au round-trip.
            var npcCityVertex = SettlersOfIdlestan.Model.HexGrid.Vertex.Create(
                new HexCoord(0, 0, IslandMap.SurfaceLayer),
                new HexCoord(-1, 0, IslandMap.SurfaceLayer),
                new HexCoord(0, -1, IslandMap.SurfaceLayer));
            npcCiv.AddCity(new SettlersOfIdlestan.Model.Civilization.City(npcCityVertex) { CivilizationIndex = npcCiv.Index });
            mainState.CurrentWorldState.AddCivilization(npcCiv);

            var json = JsonSerializer.Serialize(mainState, SaveController.SerializationOptions());

            var controller = new MainGameController();
            controller.ImportMainState(json);

            var exported = controller.ExportMainState();

            var controller2 = new MainGameController();
            var round = controller2.ImportMainState(exported);

            var roundNpc = round.CurrentWorldState!.Civilizations.Single(c => c.IsNpc);
            Assert.NotNull(roundNpc.NpcParameters);
            Assert.Equal(SettlersOfIdlestan.Model.Civilization.NpcAggressivityLevel.Warlike, roundNpc.NpcParameters!.AggressivityLevel);
            Assert.Contains(0, roundNpc.WarEnemyCivIndices);
        }

        /// <summary>
        /// Couvre l'intervalle entre RequestAscension et ConfirmAscensionRace (voir
        /// AscensionController.RequestAscension) : PrestigeState/WorldState viennent d'être détruits
        /// et le choix de race n'a pas encore été fait — la partie doit malgré tout pouvoir être
        /// sauvegardée puis reprise à ce stade précis (régression potentielle : ExportMainState/
        /// ImportMainState ne doivent pas supposer un WorldState présent).
        /// </summary>
        [Fact]
        public void ExportMainState_DuringAscensionPending_SurvivesSaveAndReloadThenResumes()
        {
            var controller = SaveUtils.LoadSave("specifics", "before_first_ascension");
            var godState = controller.CurrentMainState!.GodState;
            Assert.True(controller.AscensionController.CanAscend(godState),
                "La sauvegarde de test doit contenir assez d'essence divine pour déclencher l'Ascension.");
            int expectedGodPoints = godState.GodPoints + controller.AscensionController.GetGodPointsGain(godState);

            controller.RequestAscension();

            Assert.True(controller.AscensionController.IsAscensionPending);
            Assert.Null(controller.CurrentMainState!.PrestigeState);
            Assert.Null(controller.CurrentMainState!.CurrentWorldState);
            Assert.Null(controller.PlayerCivilization);
            Assert.Equal(expectedGodPoints, controller.CurrentMainState!.GodState.GodPoints);

            var exported = controller.ExportMainState();

            var reloadedController = new MainGameController();
            var reloaded = reloadedController.ImportMainState(exported);

            // L'état "en attente de race" doit survivre au round-trip de sauvegarde tel quel.
            Assert.True(reloadedController.AscensionController.IsAscensionPending);
            Assert.Null(reloaded.PrestigeState);
            Assert.Null(reloaded.CurrentWorldState);
            Assert.Null(reloadedController.PlayerCivilization);
            Assert.Equal(expectedGodPoints, reloaded.GodState.GodPoints);

            // La partie doit pouvoir reprendre normalement depuis cet état rechargé.
            reloadedController.ConfirmAscensionRace(SettlersOfIdlestan.Model.Races.RaceId.Human);

            Assert.False(reloadedController.AscensionController.IsAscensionPending);
            Assert.NotNull(reloadedController.CurrentMainState!.PrestigeState);
            Assert.NotNull(reloadedController.CurrentMainState!.CurrentWorldState);
            Assert.NotNull(reloadedController.PlayerCivilization);
        }

        /// <summary>
        /// Régression : l'île détruite par une Ascension continuait de tourner pendant le choix de
        /// race. InitializeControllersForCurrentIsland câble tous les contrôleurs d'île sous un
        /// `if (WorldState != null)` ; l'appel qui suit RequestAscension ne recâblait donc rien, et
        /// chacun gardait son abonnement à l'horloge avec, en main, le WorldState que l'Ascension
        /// venait de détruire. RequestAscension met bien l'horloge en pause, mais la barre du haut
        /// (bouton lecture compris) reste visible pendant le choix de race — voir
        /// AscensionPendingTopBarVisibilityTests : un appui sur lecture relançait la simulation de
        /// l'île fantôme. Vécu en partie : une Purification d'Os Divins terminée sur cette île
        /// recréditait une essence divine juste après sa remise à zéro par l'Ascension (0 → 1).
        /// Voir GameClock.ClearAdvancedSubscribers.
        /// </summary>
        [Fact]
        public void RequestAscension_ThenResumingTheClock_DoesNotKeepSimulatingTheDestroyedIsland()
        {
            var controller = SaveUtils.LoadSave("specifics", "before_first_ascension");
            var mainState = controller.CurrentMainState!;
            var destroyedWorld = mainState.CurrentWorldState!;
            var playerCiv = destroyedWorld.PlayerCivilization;

            // Des Os Divins entièrement investis sur un hex d'une ville du joueur : le prochain
            // cycle d'investissement les purifie, ce qui octroie une essence divine (voir
            // DivineBonesController.GrantPurificationEssence). C'est le témoin le plus lisible
            // qu'un tick a bien tourné sur cette île — et c'est le symptôme d'origine.
            var bones = new DivineBones(playerCiv.Cities[0].Position.GetHexes()[0], corruptionLevel: 1);
            foreach (var resource in new[] { Resource.Crystal, Resource.Mithril, Resource.Steel })
                bones.InvestedResources[resource] = int.MaxValue;
            destroyedWorld.AddFeature(bones);

            var godState = mainState.GodState;
            Assert.True(controller.AscensionController.CanAscend(godState),
                "La sauvegarde de test doit contenir assez d'essence divine pour déclencher l'Ascension.");

            var resourcesBefore = System.Enum.GetValues<Resource>()
                .ToDictionary(r => r, r => playerCiv.GetResourceQuantity(r));

            controller.RequestAscension();

            Assert.True(controller.AscensionController.IsAscensionPending);
            Assert.Null(mainState.CurrentWorldState);
            Assert.Equal(0, godState.DivineEssence);
            int totalEssenceEarned = godState.TotalDivineEssenceEarned;

            // Le joueur appuie sur lecture depuis l'écran d'Ascension, avant d'avoir choisi sa race.
            mainState.Clock.Resume();
            mainState.Clock.SimulateAdvance(10_000);

            Assert.False(bones.Purified,
                "L'île détruite par l'Ascension a continué d'être simulée pendant le choix de race.");
            Assert.Equal(0, godState.DivineEssence);
            Assert.Equal(totalEssenceEarned, godState.TotalDivineEssenceEarned);

            // Rien d'autre non plus : récolte, commerce, monstres... tout tenait au même abonnement.
            foreach (var (resource, quantity) in resourcesBefore)
                Assert.Equal(quantity, playerCiv.GetResourceQuantity(resource));

            // Et la partie doit repartir normalement une fois la race choisie.
            controller.ConfirmAscensionRace(controller.AscensionController.GetSelectableRaces().First());

            Assert.False(controller.AscensionController.IsAscensionPending);
            Assert.NotNull(mainState.CurrentWorldState);

            long tickBeforeResume = mainState.Clock.CurrentTick;
            mainState.Clock.SimulateAdvance(1_000);
            Assert.True(mainState.Clock.CurrentTick > tickBeforeResume);
            Assert.NotEmpty(mainState.Clock.GetAdvancedSubscribersInOrder());
        }

        /// <summary>
        /// Pendant le choix de race, il n'y a plus d'île à simuler : l'horloge doit rester en pause,
        /// seul état où GameClock.Advance verse le temps réel écoulé dans OfflineBankTicks au lieu
        /// de le brûler pour rien. RequestAscension met bien en pause, mais une sauvegarde faite
        /// pendant l'attente pouvait parfaitement être écrite horloge en marche (avant le verrou du
        /// contrôle de temps, un appui sur lecture suffisait, et l'auto-save suivait) :
        /// WasPausedAtSave part alors à faux et SetGameFromSave rappelait Clock.Start().
        /// </summary>
        [Fact]
        public void ReloadingASaveMadeDuringAscensionPending_StaysPaused_SoTimeGoesToTheOfflineBank()
        {
            var controller = SaveUtils.LoadSave("specifics", "before_first_ascension");
            controller.RequestAscension();

            // Horloge relancée avant la sauvegarde : c'est ce que faisaient les sauvegardes
            // produites pendant l'attente jusqu'ici.
            controller.CurrentMainState!.Clock.Resume();
            var exported = controller.ExportMainState();

            var reloadedController = new MainGameController();
            var reloaded = reloadedController.ImportMainState(exported);

            Assert.True(reloadedController.AscensionController.IsAscensionPending);
            Assert.False(reloaded.Clock.WasPausedAtSave,
                "La sauvegarde doit bien avoir été écrite horloge en marche, sinon le test ne prouve rien.");
            Assert.Equal(0, reloaded.Clock.SpeedMultiplier);

            // Le temps réel qui passe part dans la banque, et le tick de simulation ne bouge pas.
            long bankBefore = reloaded.Clock.OfflineBankTicks;
            long tickBefore = reloaded.Clock.CurrentTick;
            var now = DateTimeOffset.UtcNow;
            reloaded.Clock.Advance(now);
            reloaded.Clock.Advance(now.AddSeconds(5));

            Assert.True(reloaded.Clock.OfflineBankTicks > bankBefore,
                "Le temps passé à choisir sa race doit alimenter la banque hors-ligne.");
            Assert.Equal(tickBefore, reloaded.Clock.CurrentTick);
        }
    }
}
