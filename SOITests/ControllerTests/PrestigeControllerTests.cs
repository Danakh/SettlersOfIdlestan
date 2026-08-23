using Xunit;
using SettlersOfIdlestan.Controller;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.HexGrid;
using SOITests.TestUtilities;
using SettlersOfIdlestan.Model;
using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Controller.Expand;
using SettlersOfIdlestan.Model.Monsters;

namespace SOITests.ControllerTests
{
    public class PrestigeControllerTests
    {
        [Fact]
        public void Prestige_PrestigePointCount()
        {
            WorldState state = IslandTestFactory.CreateSevenHexIslandState();
            var civ = state.Civilizations[0];

            var controller = new PrestigeController();
            controller.Initialize(civ);

            Assert.Equal(0, controller.CalculatePrestigePoints());

            civ.Cities[0].AddBuilding(new TownHall());
            Assert.Equal(1, controller.CalculatePrestigePoints());

            civ.Cities[0].AddBuilding(new Temple());
            Assert.Equal(2, controller.CalculatePrestigePoints());

            civ.Cities[0].Buildings[0].Level = 2; // raise townhall to level 2 (no change)
            Assert.Equal(2, controller.CalculatePrestigePoints());
            civ.Cities[0].Buildings[0].Level = 3; // raise townhall to level 3 (+1 point)
            Assert.Equal(3, controller.CalculatePrestigePoints());
        }

        [Fact]
        public void Prestige_SourcesMatchCalculatedTotal()
        {
            WorldState state = IslandTestFactory.CreateSevenHexIslandState();
            var civ = state.Civilizations[0];

            var controller = new PrestigeController();
            controller.Initialize(civ);

            civ.Cities[0].AddBuilding(new TownHall { Level = 3 });
            civ.Cities[0].AddBuilding(new Library());
            civ.Cities[0].AddBuilding(new Temple());

            Assert.Equal(controller.CalculatePrestigePoints(), controller.GetPrestigePointSources().Sum(source => source.Points));
        }

        [Fact]
        public void Prestige_SourcesAreGroupedBySource()
        {
            WorldState state = IslandTestFactory.CreateSevenHexIslandState();
            var civ = state.Civilizations[0];

            var controller = new PrestigeController();
            controller.Initialize(civ);

            civ.Cities[0].AddBuilding(new Temple());
            civ.Cities[0].AddBuilding(new Temple());
            civ.Cities[0].AddBuilding(new TownHall());

            var sources = controller.GetPrestigePointSources();
            Assert.Equal(2, sources.Count);
            Assert.Equal(2, sources.Single(source => source.LabelKey == "building_temple_name").Points);
            Assert.Equal(1, sources.Single(source => source.LabelKey == "building_townhall_name").Points);
        }

        // ── Monster prestige bonus ───────────────────────────────────────────

        [Fact]
        public void Prestige_MonsterBonus_ZeroWhenMonstersPresent()
        {
            var state = IslandTestFactory.CreateSevenHexIslandState();
            state.Features.Add(new Bandit(new HexCoord(0, 0, IslandMap.SurfaceLayer)));
            var controller = new PrestigeController();
            controller.Initialize(state.Civilizations[0], state);

            Assert.Equal(0, controller.GetMonsterBonus());
        }

        [Fact]
        public void Prestige_MonsterBonus_TwentyPercentOfBuildingSubtotal()
        {
            var state = IslandTestFactory.CreateSevenHexIslandState();
            // pas de monstres sur le SurfaceLayer = bonus actif
            var civ = state.Civilizations[0];
            civ.Cities[0].AddBuilding(new Temple());
            civ.Cities[0].AddBuilding(new Temple());
            civ.Cities[0].AddBuilding(new Temple());
            civ.Cities[0].AddBuilding(new Temple());
            civ.Cities[0].AddBuilding(new Temple()); // 5 temples = subtotal 5
            var controller = new PrestigeController();
            controller.Initialize(civ, state);

            Assert.Equal(5, controller.GetBuildingSubtotal());
            Assert.Equal(1, controller.GetMonsterBonus()); // 5 / 5 = 1
        }

        // ── Wonder prestige bonus ────────────────────────────────────────────

        [Fact]
        public void Prestige_WonderBonus_ZeroWhenNoWonder()
        {
            var state = IslandTestFactory.CreateSevenHexIslandState();
            var clock = new SettlersOfIdlestan.Model.Game.GameClock();
            var controller = new PrestigeController();
            controller.Initialize(state.Civilizations[0], state, clock);

            Assert.Equal(0, controller.GetWonderBonus());
        }

        [Fact]
        public void Prestige_WonderBonus_ZeroWhenWonderAtLevel0()
        {
            var state = IslandTestFactory.CreateSevenHexIslandState();
            state.AddFeature(new SettlersOfIdlestan.Model.IslandFeatures.Wonder(new HexCoord(0, 0, IslandMap.SurfaceLayer)) { Level = 0 });
            var clock = new SettlersOfIdlestan.Model.Game.GameClock();
            var controller = new PrestigeController();
            controller.Initialize(state.Civilizations[0], state, clock);

            Assert.Equal(0, controller.GetWonderBonus());
        }

        [Fact]
        public void Prestige_WonderBonus_LevelTimesTimeFactor()
        {
            var state = IslandTestFactory.CreateSevenHexIslandState();
            state.StartTick = 1;
            state.AddFeature(new SettlersOfIdlestan.Model.IslandFeatures.Wonder(new HexCoord(0, 0, IslandMap.SurfaceLayer)) { Level = 2 });
            // runTicks = 720001 - 1 = 720000 = 2h exactement → ceil(2) = 2 → timeFactor = 3
            var clock = new SettlersOfIdlestan.Model.Game.GameClock { CurrentTick = 720001 };
            var controller = new PrestigeController();
            controller.Initialize(state.Civilizations[0], state, clock);

            Assert.Equal(6, controller.GetWonderBonus()); // 2 × (1+2) = 6
        }

        [Fact]
        public void Prestige_WonderBonus_HoursRoundedUp()
        {
            var state = IslandTestFactory.CreateSevenHexIslandState();
            state.StartTick = 1;
            state.AddFeature(new SettlersOfIdlestan.Model.IslandFeatures.Wonder(new HexCoord(0, 0, IslandMap.SurfaceLayer)) { Level = 1 });
            // runTicks = 180001 - 1 = 180000 = 30 min → ceil(0.5) = 1h → timeFactor = 2
            var clock = new SettlersOfIdlestan.Model.Game.GameClock { CurrentTick = 180001 };
            var controller = new PrestigeController();
            controller.Initialize(state.Civilizations[0], state, clock);

            Assert.Equal(2, controller.GetWonderBonus()); // 1 × (1+1) = 2
        }

        [Fact]
        public void Prestige_WonderBonus_CountedInTotal()
        {
            var state = IslandTestFactory.CreateSevenHexIslandState();
            state.StartTick = 1;
            state.AddFeature(new SettlersOfIdlestan.Model.IslandFeatures.Wonder(new HexCoord(0, 0, IslandMap.SurfaceLayer)) { Level = 1 });
            var civ = state.Civilizations[0];
            civ.Cities[0].AddBuilding(new Temple()); // subtotal = 1
            // runTicks = 360001 - 1 = 360000 = 1h → ceil(1) = 1 → timeFactor = 2
            var clock = new SettlersOfIdlestan.Model.Game.GameClock { CurrentTick = 360001 };
            var controller = new PrestigeController();
            controller.Initialize(civ, state, clock);

            // total = buildingSubtotal(1) × wonderMultiplier(2) + banditBonus(0) = 2
            Assert.Equal(2, controller.CalculatePrestigePoints());
        }

        [Fact]
        public void Prestige_WonderBonusDetails_ReturnsCorrectValues()
        {
            var state = IslandTestFactory.CreateSevenHexIslandState();
            state.StartTick = 1;
            state.AddFeature(new SettlersOfIdlestan.Model.IslandFeatures.Wonder(new HexCoord(0, 0, IslandMap.SurfaceLayer)) { Level = 3 });
            // runTicks = 360001 - 1 = 360000 = 1h
            var clock = new SettlersOfIdlestan.Model.Game.GameClock { CurrentTick = 360001 };
            var controller = new PrestigeController();
            controller.Initialize(state.Civilizations[0], state, clock);

            var (level, timeFactor, runTicks) = controller.GetWonderBonusDetails();
            Assert.Equal(3, level);
            Assert.Equal(2, timeFactor); // 1 + ceil(1h) = 2
            Assert.Equal(360000, runTicks);
        }

        private static WorldState CreateDesertIslandState()
        {
            var tiles = new List<HexTile>
            {
                new(new HexCoord(0, 0, IslandMap.SurfaceLayer), TerrainType.Desert),
                new(new HexCoord(1, 0, IslandMap.SurfaceLayer), TerrainType.Plain),
                new(new HexCoord(0, 1, IslandMap.SurfaceLayer), TerrainType.Plain),
            };
            var map = new IslandMap(tiles);
            var civ = new SettlersOfIdlestan.Model.Civilization.Civilization { Index = 0 };
            var vertex = Vertex.Create(new HexCoord(0, 0, IslandMap.SurfaceLayer), new HexCoord(1, 0, IslandMap.SurfaceLayer), new HexCoord(0, 1, IslandMap.SurfaceLayer));
            var city = new SettlersOfIdlestan.Model.Civilization.City(vertex) { CivilizationIndex = 0 };
            civ.AddCity(city);
            return new WorldState(map, new List<SettlersOfIdlestan.Model.Civilization.Civilization> { civ }, AtlasController.InvalidIslandId);
        }

        [Fact]
        public void MainGameController_PerformPrestige_AddsPointsAndCreatesNextIsland()
        {
            var controller = new MainGameController();
            controller.CreateNewGame();
            var initialIsland = controller.CurrentMainState!.CurrentWorldState!;
            var civ = initialIsland.PlayerCivilization;
            for (int i = 0; i < 20; i++)
            {
                civ.Cities[0].AddBuilding(new Temple());
            }
            civ.AddUniqueBuilding(BuildingType.ImperialPort);
            var expectedPrestigePoints = controller.PrestigeController.CalculatePrestigePoints();

            controller.PerformPrestige();

            var newIsland = controller.CurrentMainState!.CurrentWorldState!;
            Assert.Equal(expectedPrestigePoints, controller.CurrentMainState.PrestigeState!.PrestigePoints);
            Assert.NotSame(initialIsland, newIsland);
            Assert.Equal(initialIsland.WorldId + 1, newIsland.WorldId);
            Assert.False(controller.PrestigeController.PrestigeIsVisible());
        }

        [Fact]
        public void MainGameController_PerformPrestige_PreservesAutomationToggles()
        {
            var controller = new MainGameController();
            controller.CreateNewGame();
            var civ = controller.CurrentMainState!.CurrentWorldState!.PlayerCivilization;
            for (int i = 0; i < 20; i++)
                civ.Cities[0].AddBuilding(new Temple());
            civ.AddUniqueBuilding(BuildingType.ImperialPort);

            var automationSettings = controller.CurrentMainState.CurrentWorldState!.AutomationSettings;
            automationSettings.ProductionBuildingAutomationEnabled = true;
            automationSettings.TownHallAutomationEnabled = true;
            automationSettings.MonumentInvestmentAutomationEnabled = true;
            automationSettings.RestrictSoldierProductionToFreeSoldiersByLayer[IslandMap.SurfaceLayer] = true;

            controller.PerformPrestige();

            var newAutomationSettings = controller.CurrentMainState.CurrentWorldState!.AutomationSettings;
            Assert.True(newAutomationSettings.ProductionBuildingAutomationEnabled);
            Assert.True(newAutomationSettings.TownHallAutomationEnabled);
            Assert.True(newAutomationSettings.MonumentInvestmentAutomationEnabled);
            Assert.True(newAutomationSettings.RestrictSoldierProductionToFreeSoldiersByLayer[IslandMap.SurfaceLayer]);
        }

        [Fact]
        public void MainGameController_PerformPrestige_ResetsWalkOfGodUsesSinceLastPrestige()
        {
            var controller = new MainGameController();
            controller.CreateNewGame();
            var civ = controller.CurrentMainState!.CurrentWorldState!.PlayerCivilization;
            for (int i = 0; i < 20; i++)
                civ.Cities[0].AddBuilding(new Temple());
            civ.AddUniqueBuilding(BuildingType.ImperialPort);
            controller.CurrentMainState.PrestigeState!.WalkOfGodUsesSinceLastPrestige = 3;

            controller.PerformPrestige();

            Assert.Equal(0, controller.CurrentMainState.PrestigeState!.WalkOfGodUsesSinceLastPrestige);
        }

        [Fact]
        public void MainGameController_PerformPrestige_ResetsPresenceOfGodUsesSinceLastPrestige()
        {
            var controller = new MainGameController();
            controller.CreateNewGame();
            var civ = controller.CurrentMainState!.CurrentWorldState!.PlayerCivilization;
            for (int i = 0; i < 20; i++)
                civ.Cities[0].AddBuilding(new Temple());
            civ.AddUniqueBuilding(BuildingType.ImperialPort);
            controller.CurrentMainState.PrestigeState!.PresenceOfGodUsesSinceLastPrestige = 3;

            controller.PerformPrestige();

            Assert.Equal(0, controller.CurrentMainState.PrestigeState!.PresenceOfGodUsesSinceLastPrestige);
        }

        // ── Essences divines conservées au prestige (Reliquaire Sacré / Renforcé) ────

        [Fact]
        public void MainGameController_PerformPrestige_WithoutReliquary_LosesAllDivineEssence()
        {
            var controller = new MainGameController();
            controller.CreateNewGame();
            var civ = controller.CurrentMainState!.CurrentWorldState!.PlayerCivilization;
            for (int i = 0; i < 20; i++)
                civ.Cities[0].AddBuilding(new Temple());
            civ.AddUniqueBuilding(BuildingType.ImperialPort);
            controller.CurrentMainState.GodState.DivineEssence = 3;

            controller.PerformPrestige();

            Assert.Equal(0, controller.CurrentMainState.GodState.DivineEssence);
            Assert.Equal(0, controller.CurrentMainState.GodState.DivineEssenceReliquaryFloor);
        }

        [Fact]
        public void MainGameController_PerformPrestige_WithReliquaireSacre_KeepsOneDivineEssence()
        {
            var controller = new MainGameController();
            controller.CreateNewGame();
            var civ = controller.CurrentMainState!.CurrentWorldState!.PlayerCivilization;
            for (int i = 0; i < 20; i++)
                civ.Cities[0].AddBuilding(new Temple());
            civ.AddUniqueBuilding(BuildingType.ImperialPort);
            civ.TechnologyTree.CompleteResearch(SettlersOfIdlestan.Model.Civilization.TechnologyId.ReliquaireSacre);
            controller.CurrentMainState.GodState.DivineEssence = 3;

            controller.PerformPrestige();

            // DivineEssence (les essences du run) repart toujours de zéro — ce que garde le
            // Reliquaire est désormais suivi séparément dans DivineEssenceReliquaryFloor.
            Assert.Equal(0, controller.CurrentMainState.GodState.DivineEssence);
            Assert.Equal(1, controller.CurrentMainState.GodState.DivineEssenceReliquaryFloor);
        }

        [Fact]
        public void MainGameController_PerformPrestige_WithBothReliquaries_KeepsTwoDivineEssence()
        {
            var controller = new MainGameController();
            controller.CreateNewGame();
            var civ = controller.CurrentMainState!.CurrentWorldState!.PlayerCivilization;
            for (int i = 0; i < 20; i++)
                civ.Cities[0].AddBuilding(new Temple());
            civ.AddUniqueBuilding(BuildingType.ImperialPort);
            civ.TechnologyTree.CompleteResearch(SettlersOfIdlestan.Model.Civilization.TechnologyId.ReliquaireSacre);
            civ.TechnologyTree.CompleteResearch(SettlersOfIdlestan.Model.Civilization.TechnologyId.ReliquaireRenforce);
            controller.CurrentMainState.GodState.DivineEssence = 3;

            controller.PerformPrestige();

            Assert.Equal(0, controller.CurrentMainState.GodState.DivineEssence);
            Assert.Equal(2, controller.CurrentMainState.GodState.DivineEssenceReliquaryFloor);
        }

        [Fact]
        public void MainGameController_PerformPrestige_WithReliquaries_CapsAtAvailableDivineEssence()
        {
            var controller = new MainGameController();
            controller.CreateNewGame();
            var civ = controller.CurrentMainState!.CurrentWorldState!.PlayerCivilization;
            for (int i = 0; i < 20; i++)
                civ.Cities[0].AddBuilding(new Temple());
            civ.AddUniqueBuilding(BuildingType.ImperialPort);
            civ.TechnologyTree.CompleteResearch(SettlersOfIdlestan.Model.Civilization.TechnologyId.ReliquaireSacre);
            civ.TechnologyTree.CompleteResearch(SettlersOfIdlestan.Model.Civilization.TechnologyId.ReliquaireRenforce);
            controller.CurrentMainState.GodState.DivineEssence = 1;

            controller.PerformPrestige();

            Assert.Equal(0, controller.CurrentMainState.GodState.DivineEssence);
            Assert.Equal(1, controller.CurrentMainState.GodState.DivineEssenceReliquaryFloor);
        }

        // ── Bonus de nettoyage de la Corruption (Spire de Corruption / Dominion) ─────

        [Fact]
        public void CorruptionClearBonusMultiplier_OneWhenNothingCleared()
        {
            var state = IslandTestFactory.CreateSevenHexIslandState();
            var controller = new PrestigeController();
            controller.Initialize(state.Civilizations[0], state);

            Assert.Equal(1, controller.GetCorruptionClearBonusMultiplier());
        }

        [Fact]
        public void CorruptionClearBonusMultiplier_TwiceMaxLevelCleared()
        {
            var state = IslandTestFactory.CreateSevenHexIslandState();
            var prestigeState = new SettlersOfIdlestan.Model.Prestige.PrestigeState { MaxCorruptionLevelCleared = 3 };
            var controller = new PrestigeController();
            controller.Initialize(state.Civilizations[0], state, prestigeState: prestigeState);

            Assert.Equal(6, controller.GetCorruptionClearBonusMultiplier()); // 2 × 3
        }

        [Fact]
        public void CorruptionClearBonusMultiplier_DoesNotRequireCorruptionSpireBuilt()
        {
            // Le bonus vient désormais du record de nettoyage (Spire ou Dominion), pas de la
            // construction de la Spire elle-même — voir HasCorruptionSpireBuilt (mécanique séparée
            // gérant le Prestige Corrompu) qui reste false ici.
            var state = IslandTestFactory.CreateSevenHexIslandState();
            var prestigeState = new SettlersOfIdlestan.Model.Prestige.PrestigeState { MaxCorruptionLevelCleared = 3 };
            var controller = new PrestigeController();
            controller.Initialize(state.Civilizations[0], state, prestigeState: prestigeState);

            Assert.False(controller.HasCorruptionSpireBuilt());
            Assert.Equal(6, controller.GetCorruptionClearBonusMultiplier());
        }

        [Fact]
        public void CalculatePrestigePoints_AppliesCorruptionClearBonusMultiplier()
        {
            var state = IslandTestFactory.CreateSevenHexIslandState();
            var prestigeState = new SettlersOfIdlestan.Model.Prestige.PrestigeState { MaxCorruptionLevelCleared = 2 };
            var civ = state.Civilizations[0];
            civ.Cities[0].AddBuilding(new Temple()); // subtotal = 1, monster bonus ×1.2 (pas de monstre)
            var controller = new PrestigeController();
            controller.Initialize(civ, state, prestigeState: prestigeState);

            // (1 × 1.2) × (2 × 2) = 4.8 → (int) 4
            Assert.Equal(4, controller.CalculatePrestigePoints());
        }

        // ── Garde-fou : montée de corruption avant la première Ascension ────

        /// <summary>État minimal du garde-fou : une Spire bâtie (prestige corrompu disponible) et un niveau de corruption donné.</summary>
        private static PrestigeController CreateCorruptedPrestigeController(WorldState state, int corruptionLevel)
        {
            state.AddFeature(new SettlersOfIdlestan.Model.IslandFeatures.CorruptionSpire(
                state.Civilizations[0].Cities[0].Position.GetHexes().First()) { Built = true });
            var prestigeState = new SettlersOfIdlestan.Model.Prestige.PrestigeState { CurrentCorruptionLevel = corruptionLevel };
            var controller = new PrestigeController();
            controller.Initialize(state.Civilizations[0], state, prestigeState: prestigeState);
            return controller;
        }

        private static SettlersOfIdlestan.Model.Prestige.GodState CreateGodState(int ascensionsPerformed)
        {
            var godState = new SettlersOfIdlestan.Model.Prestige.GodState();
            godState.AscensionState.AscensionsPerformed = ascensionsPerformed;
            return godState;
        }

        [Fact]
        public void CorruptedPrestigeWarning_AtLevelFourWithoutAscension()
        {
            // Niveau 4 → 5 : au-delà du seuil, et aucune Ascension faite.
            var state = IslandTestFactory.CreateSevenHexIslandState();
            var controller = CreateCorruptedPrestigeController(state, corruptionLevel: 4);

            Assert.True(controller.CorruptedPrestigeNeedsAscensionWarning(CreateGodState(0)));
        }

        [Fact]
        public void CorruptedPrestigeWarning_NotBelowThreshold()
        {
            // Niveau 3 → 4 : le seuil n'est pas dépassé, pas de confirmation.
            var state = IslandTestFactory.CreateSevenHexIslandState();
            var controller = CreateCorruptedPrestigeController(state, corruptionLevel: 3);

            Assert.False(controller.CorruptedPrestigeNeedsAscensionWarning(CreateGodState(0)));
        }

        [Fact]
        public void CorruptedPrestigeWarning_NotAfterFirstAscension()
        {
            var state = IslandTestFactory.CreateSevenHexIslandState();
            var controller = CreateCorruptedPrestigeController(state, corruptionLevel: 6);

            Assert.False(controller.CorruptedPrestigeNeedsAscensionWarning(CreateGodState(1)));
        }

        [Fact]
        public void CorruptedPrestigeWarning_NotWithoutCorruptionSpire()
        {
            // Sans Spire bâtie, le prestige corrompu n'est pas proposé : rien à confirmer.
            var state = IslandTestFactory.CreateSevenHexIslandState();
            var prestigeState = new SettlersOfIdlestan.Model.Prestige.PrestigeState { CurrentCorruptionLevel = 5 };
            var controller = new PrestigeController();
            controller.Initialize(state.Civilizations[0], state, prestigeState: prestigeState);

            Assert.False(controller.CorruptedPrestigeNeedsAscensionWarning(CreateGodState(0)));
        }

        // ── Civilizations destroyed prestige bonus ──────────────────────────

        [Fact]
        public void Prestige_CivilizationsDestroyedBonus_TwentyPercentPerCivilization()
        {
            var state = IslandTestFactory.CreateSevenHexIslandState();
            state.RunRecord.CivilizationsDestroyed = 2;
            var controller = new PrestigeController();
            controller.Initialize(state.Civilizations[0], state);

            Assert.Equal(0.4, controller.GetCivilizationsDestroyedBonus());
        }

        [Fact]
        public void CalculatePrestigePoints_AppliesCivilizationsDestroyedBonus()
        {
            var state = IslandTestFactory.CreateSevenHexIslandState();
            var civ = state.Civilizations[0];
            civ.Cities[0].AddBuilding(new Temple());
            civ.Cities[0].AddBuilding(new Temple());
            civ.Cities[0].AddBuilding(new Temple());
            civ.Cities[0].AddBuilding(new Temple());
            civ.Cities[0].AddBuilding(new Temple()); // subtotal = 5
            state.RunRecord.CivilizationsDestroyed = 1;
            var controller = new PrestigeController();
            controller.Initialize(civ, state);

            // (5 × 1.2 monster bonus) × (1 + 0.2 civ bonus) = 7.2 → (int) 7
            Assert.Equal(7, controller.CalculatePrestigePoints());
        }

        private static (MainGameController controller, WorldState state, SettlersOfIdlestan.Model.Civilization.Civilization npcCiv)
            SetupGameWithSingleCityNpc()
        {
            var tileData = new List<(TerrainType, int)>
            {
                (TerrainType.Forest,   3),
                (TerrainType.Hill,     3),
                (TerrainType.Plain,    3),
                (TerrainType.Mountain, 3),
            };
            var islandParams = new SettlersOfIdlestan.Model.Game.IslandParameters(AtlasController.InvalidIslandId, tileData)
            {
                NpcCivilizations =
                [
                    new SettlersOfIdlestan.Model.Civilization.NpcParameters
                    {
                        EvolutionLevel        = SettlersOfIdlestan.Model.Civilization.NpcEvolutionLevel.Minimum,
                        AggressivityLevel     = SettlersOfIdlestan.Model.Civilization.NpcAggressivityLevel.Pacifist,
                        MinDistanceFromPlayer = 3,
                        CityCount             = 1,
                    }
                ]
            };

            var mainState = new SettlersOfIdlestan.Model.Game.MainGameState();
            var generator = new SettlersOfIdlestan.Controller.Generator.IslandMapGenerator(mainState.PRNG);
            var worldState = generator.GenerateWorldState(islandParams, mainState.Clock.CurrentTick);
            Assert.NotNull(worldState);

            var prestigeState = new SettlersOfIdlestan.Model.Prestige.PrestigeState(worldState);
            mainState.GodState = new SettlersOfIdlestan.Model.Prestige.GodState(prestigeState);

            var controller = new MainGameController();
            controller.SetGame(mainState);

            var npcCiv = worldState.Civilizations.Single(c => c.IsNpc);
            Assert.Single(npcCiv.Cities);

            return (controller, worldState, npcCiv);
        }

        [Fact]
        public void CityBuilderController_DestroyingLastCityOfNpcCivilization_GrantsPrestigeBonus()
        {
            var (controller, state, npcCiv) = SetupGameWithSingleCityNpc();
            var npcCity = npcCiv.Cities[0];

            controller.CityBuilderController.DestroyCity(npcCity, CityDestructionCause.Combat);

            Assert.Equal(1, state.RunRecord.CivilizationsDestroyed);
            Assert.Equal(1, controller.CurrentMainState!.GameRecord.TotalCivilizationsDestroyed);
            Assert.Equal(0.2, controller.PrestigeController.GetCivilizationsDestroyedBonus());
        }

        [Fact]
        public void CityBuilderController_DestroyingCityByMonster_DoesNotGrantPrestigeBonus()
        {
            var (controller, state, npcCiv) = SetupGameWithSingleCityNpc();
            var npcCity = npcCiv.Cities[0];

            controller.CityBuilderController.DestroyCity(npcCity, CityDestructionCause.Monster);

            Assert.Equal(0, state.RunRecord.CivilizationsDestroyed);
        }

        [Fact]
        public void PerformPrestige_Corrupted_IncrementsCorruptionLevelOnlyWhenSpireBuilt()
        {
            var mainController = new MainGameController();
            mainController.CreateNewGame();
            var civ = mainController.CurrentMainState!.CurrentWorldState!.PlayerCivilization;
            civ.AddUniqueBuilding(BuildingType.ImperialPort);
            for (int i = 0; i < 20; i++)
                civ.Cities[0].AddBuilding(new Temple());

            Assert.Equal(1, mainController.CurrentMainState.PrestigeState!.CurrentCorruptionLevel);

            mainController.PerformPrestige(corrupted: true);

            // Pas de Spire construite → le niveau de corruption ne bouge pas
            Assert.Equal(1, mainController.CurrentMainState.PrestigeState!.CurrentCorruptionLevel);
        }

        // ── Ascension Prestigieuse : bonus d'essence divine à chaque prestige ─

        [Fact]
        public void Prestige_DivineEssenceBonus_ZeroWithoutPrestigiousAscension()
        {
            var state = IslandTestFactory.CreateSevenHexIslandState();
            var godState = new SettlersOfIdlestan.Model.Prestige.GodState { TotalDivineEssenceEarned = 42 };
            var controller = new PrestigeController();
            controller.Initialize(state.Civilizations[0], state, godState: godState);

            Assert.Equal(0, controller.GetDivineEssenceBonus());
            Assert.DoesNotContain(controller.GetPrestigePointSources(), s => s.LabelKey == "prestige_divine_essence_bonus");
        }

        [Fact]
        public void Prestige_DivineEssenceBonus_OnePerDivineEssenceEverEarnedWithPrestigiousAscension()
        {
            var state = IslandTestFactory.CreateSevenHexIslandState();
            var godState = new SettlersOfIdlestan.Model.Prestige.GodState { TotalDivineEssenceEarned = 42 };
            godState.AscensionState.UnlockedPowers.Add(SettlersOfIdlestan.Model.Ascension.AscensionPowerId.PrestigiousAscension);
            var controller = new PrestigeController();
            controller.Initialize(state.Civilizations[0], state, godState: godState);

            Assert.Equal(42, controller.GetDivineEssenceBonus());
            Assert.Contains(controller.GetPrestigePointSources(), s => s.LabelKey == "prestige_divine_essence_bonus" && s.Points == 42);
            // ×1.2 : pas de monstres en surface sur une île de test fraîchement générée.
            Assert.Equal(50, controller.CalculatePrestigePoints());
        }

        [Fact]
        public void PerformPrestige_WithPrestigiousAscension_AddsDivineEssenceBonusToPrestigePointsGained()
        {
            var mainController = new MainGameController();
            mainController.CreateNewGame();
            var mainState = mainController.CurrentMainState!;
            var civ = mainState.CurrentWorldState!.PlayerCivilization;
            civ.AddUniqueBuilding(BuildingType.ImperialPort);
            for (int i = 0; i < 20; i++)
                civ.Cities[0].AddBuilding(new Temple());

            mainState.GodState.TotalDivineEssenceEarned = 5;
            mainState.GodState.AscensionState.UnlockedPowers.Add(SettlersOfIdlestan.Model.Ascension.AscensionPowerId.PrestigiousAscension);

            int expectedPoints = mainController.PrestigeController.CalculatePrestigePoints();
            Assert.Contains(mainController.PrestigeController.GetPrestigePointSources(),
                s => s.LabelKey == "prestige_divine_essence_bonus" && s.Points == 5);

            mainController.PerformPrestige(corrupted: false);

            Assert.Equal(expectedPoints, mainState.PrestigeState!.PrestigePoints);
        }
    }
}
