using SettlersOfIdlestan.Controller;
using SettlersOfIdlestan.Controller.Expand;
using SettlersOfIdlestan.Controller.Generator;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Controller.Military;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Prestige;
using SettlersOfIdlestan.Model.Prestige.PrestigeMap;
using SOITests.TestUtilities;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace SOITests.MilitaryTests;

/// <summary>
/// La civilisation joueur (prestige BarracksVertex + LaboratoryVertex) extermination un NPC Low/Pacifiste
/// qui dispose d'une Palissade en défense pleine.
///
/// Le joueur démarre SANS Caserne et sans bâtiment bonus : il doit :
///   1. Développer son économie via Step1/Step2 pour avoir le stockage suffisant (Warehouse),
///   2. Construire la Caserne par le step militaire,
///   3. Produire des soldats et attaquer jusqu'à extermination.
///
/// L'île est générée comme dans StepIslandTest (IslandMapGenerator) avec :
///   • 16 tuiles terrestres (Forest×4, Hill×4, Plain×4, Mountain×4)
///   • 1 NPC Low/Pacifiste dont la ville de départ reçoit une Palissade
///   • Prestige joueur (CentralVertex + BarracksVertex + LaboratoryVertex) injecté
///     AVANT SetGame pour que SetupModifierAggregators les prenne en compte.
/// </summary>
public class NpcExterminationTests
{
    [Fact]
    public void PlayerBuildsBarracksFromScratch_ProducesSoldiers_ExterminatesNpcWithPalisade()
    {
        // ── Génération de l'île (même pattern que CreateNewGame) ─────────────────
        var tileData = new List<(TerrainType, int)>
        {
            (TerrainType.Forest,   3),
            (TerrainType.Hill,     3),
            (TerrainType.Plain,    3),
            (TerrainType.Mountain, 3),
        };
        var islandParams = new IslandParameters(
            AtlasController.InvalidIslandId,
            tileData)
        {
            NpcCivilizations =
            [
                new NpcParameters
                {
                    EvolutionLevel       = NpcEvolutionLevel.Minimum,
                    AggressivityLevel    = NpcAggressivityLevel.Pacifist,
                    MinDistanceFromPlayer = 3,
                }
            ]
        };

        var mainState = new MainGameState();
        var generator = new IslandMapGenerator(mainState.PRNG);
        var WorldState = generator.GenerateWorldState(islandParams, mainState.Clock.CurrentTick);
        Assert.NotNull(WorldState);

        // ── Prestige joueur injecté AVANT SetGame ────────────────────────────────
        // BarracksVertex déverrouille la Caserne (GetDefaultMaxLevel = 0 → +2).
        // Doit être présent dans PurchasedVertices quand SetupModifierAggregators() s'exécute.
        var prestige = new PrestigeState(WorldState);
        prestige.PurchasedVertices.Add(PrestigeMap.CentralVertex);
        prestige.PurchasedVertices.Add(PrestigeMap.BarracksVertex);
        mainState.GodState = new GodState(prestige);

        var mainController = new MainGameController();
        mainController.SetGame(mainState);
        // ApplyPrestigeToNewGame APRÈS SetGame, comme dans CreateNewGame
        mainController.PrestigeMapController.ApplyPrestigeToNewGame(WorldState, prestige);

        // ── Palissade sur la ville NPC la plus proche du joueur ──────────────────
        var npcCiv = WorldState.Civilizations.First(c => c.IsNpc);
        Assert.NotEmpty(npcCiv.Cities);
        var playerStartCity = WorldState.PlayerCivilization.Cities[0];
        var npcTargetCity = npcCiv.Cities
            .OrderBy(c => c.Position.EdgeDistanceTo(playerStartCity.Position))
            .First();
        npcTargetCity.Buildings.Add(new Palisade { Level = 1 });

        // ── Autoplayer joueur ────────────────────────────────────────────────────
        var playerCiv = WorldState.PlayerCivilization;
        var auto = new CivilizationAutoplayer(
            playerCiv,
            WorldState.GetMapForZ(IslandMap.SurfaceLayer)!,
            mainController.RoadController,
            mainController.HarvestController,
            mainController.BuildingController,
            mainController.CityBuilderController,
            mainController.TradeController,
            mainController.ResearchController,
            mainController.PrestigeController,
            mainController.PrestigeMapController,
            WorldState,
            mainController.CurrentMainState?.PrestigeState,
            mainController.PerformPrestige);
        var runner = new CivilizationAutoplayerRunner(auto, playerCiv, mainController);

        // Phase économique : atteindre 4 villes, Warehouse et Mine via la stratégie unifiée.
        runner.RunPriorityStrategyUntil(
            CivilizationAutoplayerPriorities.Unified(auto, mainController.BuildingController),
            () => playerCiv.Cities.Count >= 4
               && playerCiv.Cities.All(c => c.Buildings.Any(b => b.Type == BuildingType.TownHall)),
            maxIterations: 5000);
        Assert.True(
            playerCiv.Cities.Count >= 4,
            "Le joueur devrait avoir au moins 4 villes après la phase économique.");

        runner.RunPriorityStrategyUntil(
            CivilizationAutoplayerPriorities.Unified(auto, mainController.BuildingController),
            () => playerCiv.Cities.Any(c => c.Buildings.Any(b => b.Type == BuildingType.Warehouse)) &&
                  playerCiv.Cities.Any(c => c.Buildings.Any(b => b.Type == BuildingType.Mine)),
            maxIterations: 5000);
        Assert.True(
            playerCiv.Cities.Any(c => c.Buildings.Any(b => b.Type == BuildingType.Warehouse)),
            "Le joueur devrait avoir un Warehouse.");
        Assert.True(
            playerCiv.Cities.Any(c => c.Buildings.Any(b => b.Type == BuildingType.Mine)),
            "Le joueur devrait avoir une Mine.");

        // Phase militaire : la Caserne est construite conditionnellement (menaces visibles + minerai).
        runner.RunPriorityStrategyUntil(
            CivilizationAutoplayerPriorities.Unified(auto, mainController.BuildingController),
            () => playerCiv.Cities.Count(c => c.Buildings.Any(b => b.Type == BuildingType.Barracks)) >= 5,
            maxIterations: 5000);
        Assert.True(
            playerCiv.Cities.Count(c => c.Buildings.Any(b => b.Type == BuildingType.Barracks)) >= 5,
            "Le joueur devrait avoir construit la Caserne via la stratégie unifiée.");

        // Mets en place les Attack Flow
        foreach (var c in playerCiv.Cities)
        {
            c.FlowTarget = npcTargetCity.Position;
        }

        // ── Autoplay : production de soldats + attaques ──────────────────────────
        runner.RunPriorityStrategyUntil(
            CivilizationAutoplayerPriorities.Unified(auto, mainController.BuildingController),
            () => npcCiv.Cities.Count == 0,
            maxIterations: 5000);


        // ── Assertions ────────────────────────────────────────────────────────────
        Assert.Empty(npcCiv.Cities);
        Assert.Contains(PrestigeMap.BarracksVertex,   prestige.PurchasedVertices);
    }
}
