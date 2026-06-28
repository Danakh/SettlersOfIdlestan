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
            maxIterations: 150000);

        // ── Diagnostic ───────────────────────────────────────────────────────────
        var buildable = mainController.CityBuilderController.GetBuildableVertices(playerCiv.Index);
        string buildableStr = string.Join(", ", buildable.Select(v => $"[{string.Join(",", v.GetHexes().Select(h => $"({h.Q},{h.R})"))}]"));
        string cityPositions = string.Join(", ", playerCiv.Cities.Select(c => $"[{string.Join(",", c.Position.GetHexes().Select(h => $"({h.Q},{h.R})"))}]"));
        string npcPositions = string.Join(", ", npcCiv.Cities.Select(c => $"[{string.Join(",", c.Position.GetHexes().Select(h => $"({h.Q},{h.R})"))}]"));
        string cityBuildings = string.Join(" | ", playerCiv.Cities.Select(c => $"[{string.Join(",", c.Buildings.Select(b => $"{b.Type}L{b.Level}"))}]"));
        int roadCount = playerCiv.Roads.Count;

        string resources = string.Join(", ", System.Enum.GetValues<Resource>()
            .Select(r => $"{r}={playerCiv.GetResourceQuantity(r)}"));
        string soldiersPerCity = string.Join(", ", playerCiv.Cities.Select(c => $"{c.Soldiers}"));
        int storageAdv = playerCiv.StorageCapacityAdvanced;

        // ── Export de la partie pour inspection ───────────────────────────────
        var savesDir = Path.Combine(
            SaveUtils.GetSolutionRootDirectory(Directory.GetCurrentDirectory()), "saves");
        Directory.CreateDirectory(savesDir);
        File.WriteAllText(
            Path.Combine(savesDir, "NpcExtermination.json"),
            mainController.ExportMainState());

        // ── Assertions ────────────────────────────────────────────────────────
        int npcDist = npcTargetCity.Position.EdgeDistanceTo(playerStartCity.Position);
        int playerSoldiers = playerCiv.Cities.Sum(c => c.Soldiers);
        int barracks = playerCiv.Cities.Count(c => c.Buildings.Any(b => b.Type == BuildingType.Barracks));
        Assert.True(npcCiv.Cities.Count == 0,
            $"NPC devrait être éradiqué. iter={iterCount}, dist_npc={npcDist}, soldats={playerSoldiers}[{soldiersPerCity}], casernes={barracks}, villes={playerCiv.Cities.Count}, routes={roadCount}, storageAdv={storageAdv}\n" +
            $"Villes joueur: {cityPositions}\n" +
            $"Villes NPC: {npcPositions}\n" +
            $"Buildable vertices ({buildable.Count}): {buildableStr}\n" +
            $"Bâtiments: {cityBuildings}\n" +
            $"Ressources: {resources}");
        Assert.Contains(PrestigeMap.BarracksVertex, prestige.PurchasedVertices);
    }
}
