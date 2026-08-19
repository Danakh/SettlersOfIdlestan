using SettlersOfIdlestan.Controller;
using SettlersOfIdlestan.Model;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.Prestige;
using SOITests.TestUtilities;
using System.Text.Json;
using Xunit;

namespace SOITests.ControllerTests
{
    /// <summary>
    /// Limitations propres à la version démo : plafond de prestige accumulé et temps passé sur
    /// l'île ramené au chargement d'une sauvegarde de démo.
    /// </summary>
    public class DemoModeTests
    {
        private const long EightHoursInTicks = 8L * 60 * 60 * 100;

        // ── Plafond de prestige accumulé ─────────────────────────────────────

        private static MainGameController CreatePrestigeableGame(bool demoMode, int alreadyEarned)
        {
            var controller = new MainGameController();
            controller.CreateNewGame();
            var mainState = controller.CurrentMainState!;
            mainState.Settings.DemoMode = demoMode;
            mainState.PrestigeState!.TotalPrestigePointsEarned = alreadyEarned;

            var civ = mainState.CurrentWorldState!.PlayerCivilization;
            for (int i = 0; i < 20; i++)
                civ.Cities[0].AddBuilding(new Temple());
            civ.AddUniqueBuilding(BuildingType.ImperialPort);

            return controller;
        }

        [Fact]
        public void Demo_LeGainDePrestigeEstRogneAuPlafond()
        {
            var controller = CreatePrestigeableGame(demoMode: true, alreadyEarned: 990);

            controller.PerformPrestige();

            var prestige = controller.CurrentMainState!.PrestigeState!;
            Assert.Equal(PrestigeState.DemoMaxTotalPrestigePointsEarned, prestige.TotalPrestigePointsEarned);
            Assert.Equal(10, prestige.PrestigePoints);
        }

        [Fact]
        public void Demo_UneFoisLePlafondAtteint_LePrestigeNeRapportePlusRien()
        {
            var controller = CreatePrestigeableGame(
                demoMode: true, alreadyEarned: PrestigeState.DemoMaxTotalPrestigePointsEarned);

            controller.PerformPrestige();

            var prestige = controller.CurrentMainState!.PrestigeState!;
            Assert.Equal(PrestigeState.DemoMaxTotalPrestigePointsEarned, prestige.TotalPrestigePointsEarned);
            Assert.Equal(0, prestige.PrestigePoints);
        }

        /// <summary>
        /// Le plafond ne doit pas empêcher le prestige lui-même : la démo doit pouvoir atteindre sa
        /// troisième île même une fois le plafond atteint.
        /// </summary>
        [Fact]
        public void Demo_UneFoisLePlafondAtteint_LePrestigeResteDeclenchable()
        {
            var controller = CreatePrestigeableGame(
                demoMode: true, alreadyEarned: PrestigeState.DemoMaxTotalPrestigePointsEarned);
            var initialIsland = controller.CurrentMainState!.CurrentWorldState!;

            Assert.True(controller.PrestigeController.PrestigeIsAvailable());
            controller.PerformPrestige();

            Assert.NotSame(initialIsland, controller.CurrentMainState!.CurrentWorldState);
        }

        [Fact]
        public void HorsDemo_LeGainDePrestigeNestPasPlafonne()
        {
            var controller = CreatePrestigeableGame(demoMode: false, alreadyEarned: 990);
            int expected = controller.PrestigeController.CalculatePrestigePoints();

            controller.PerformPrestige();

            var prestige = controller.CurrentMainState!.PrestigeState!;
            Assert.Equal(990 + expected, prestige.TotalPrestigePointsEarned);
            Assert.True(prestige.TotalPrestigePointsEarned > PrestigeState.DemoMaxTotalPrestigePointsEarned);
        }

        // ── Temps passé sur l'île au chargement ──────────────────────────────

        private static string CreateSave(bool isDemoSave, long startTick, long currentTick)
        {
            var worldState = IslandTestFactory.CreateSevenHexIslandState();
            worldState.StartTick = startTick;
            var clock = new GameClock { CurrentTick = currentTick };
            var mainState = new MainGameState(worldState, clock, new GamePRNG(42)) { IsDemoSave = isDemoSave };
            return JsonSerializer.Serialize(mainState, SaveController.SerializationOptions());
        }

        [Fact]
        public void ChargementSauvegardeDemo_RameneLeTempsPasseSurIleA8h()
        {
            long currentTick = 100 + EightHoursInTicks * 3;
            var json = CreateSave(isDemoSave: true, startTick: 100, currentTick: currentTick);

            var imported = new MainGameController().ImportMainState(json);

            Assert.Equal(EightHoursInTicks, imported.Clock.CurrentTick - imported.CurrentWorldState!.StartTick);
        }

        [Fact]
        public void ChargementSauvegardeDemo_EnDecaDe8h_NeToucheARien()
        {
            long currentTick = 100 + EightHoursInTicks / 2;
            var json = CreateSave(isDemoSave: true, startTick: 100, currentTick: currentTick);

            var imported = new MainGameController().ImportMainState(json);

            Assert.Equal(100, imported.CurrentWorldState!.StartTick);
        }

        [Fact]
        public void ChargementSauvegardeNonDemo_ConserveLeTempsPasseSurIle()
        {
            long currentTick = 100 + EightHoursInTicks * 3;
            var json = CreateSave(isDemoSave: false, startTick: 100, currentTick: currentTick);

            var imported = new MainGameController().ImportMainState(json);

            Assert.Equal(100, imported.CurrentWorldState!.StartTick);
        }
    }
}
