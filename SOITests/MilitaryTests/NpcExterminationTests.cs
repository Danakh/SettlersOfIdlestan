using SettlersOfIdlestan.Controller;
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
using System.IO;
using System.Linq;
using Xunit;

namespace SOITests.MilitaryTests;

/// <summary>
/// Le joueur (avec prestige BarracksVertex) doit éradiquer un NPC Pacifiste Low via la
/// stratégie unifiée, sans aucune intervention manuelle sur les flux d'attaque.
///
/// La feature testée : en posant <c>PriorityTargetCivilization</c>, les flux d'attaque
/// et de renfort vers le NPC sont créés automatiquement par
/// <c>TryUpdatePriorityTargetFlowsOnce()</c>, appelé à chaque itération de
/// <c>RunPriorityStrategyUntil</c>.
/// </summary>
public class NpcExterminationTests
{
    [Fact]
    public void PlayerBuildsBarracksFromScratch_ProducesSoldiers_ExterminatesNpcWithPalisade()
    {
        // ── Génération de l'île (même pattern que CreateNewGame) ─────────────
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

        // ── Palissade sur la ville NPC la plus proche du joueur ─────────────
        var npcCiv = WorldState.Civilizations.First(c => c.IsNpc);
        Assert.NotEmpty(npcCiv.Cities);
        var playerStartCity = WorldState.PlayerCivilization.Cities[0];
        var npcTargetCity = npcCiv.Cities
            .OrderBy(c => c.Position.EdgeDistanceTo(playerStartCity.Position))
            .First();
        npcTargetCity.Buildings.Add(new Palisade { Level = 1 });

        // ── Prestige joueur injecté AVANT SetGame ────────────────────────────
        var prestige = new PrestigeState(WorldState);
        prestige.PurchasedVertices.Add(PrestigeMap.CentralVertex);
        prestige.PurchasedVertices.Add(PrestigeMap.BarracksVertex);
        mainState.GodState = new GodState(prestige);

        var mainController = new MainGameController();
        mainController.SetGame(mainState);
        mainController.PrestigeMapController.ApplyPrestigeToNewGame(WorldState, prestige);

        // ── Autoplayer joueur avec cible prioritaire ─────────────────────────
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
            mainController.PerformPrestige,
            militaryController: mainController.MilitaryController);
        auto.PriorityTargetCivilization = npcCiv;
        var runner = new CivilizationAutoplayerRunner(auto, playerCiv, mainController);

        // ── Stratégie unifiée jusqu'à éradication ────────────────────────────
        int iterCount = runner.RunPriorityStrategyUntil(
            CivilizationAutoplayerPriorities.Unified(auto, mainController.BuildingController),
            () => npcCiv.Cities.Count == 0,
            maxIterations: 50000);

        // ── Assertions ────────────────────────────────────────────────────────
        int npcDist = npcTargetCity.Position.EdgeDistanceTo(playerStartCity.Position);
        int playerSoldiers = playerCiv.Cities.Sum(c => c.Soldiers);
        int barracks = playerCiv.Cities.Count(c => c.Buildings.Any(b => b.Type == BuildingType.Barracks));
        Assert.True(npcCiv.Cities.Count == 0,
            $"NPC devrait être éradiqué. iter={iterCount}, dist_npc={npcDist}, soldats={playerSoldiers}, casernes={barracks}, villes={playerCiv.Cities.Count}\n");
        Assert.Contains(PrestigeMap.BarracksVertex, prestige.PurchasedVertices);
    }
}
