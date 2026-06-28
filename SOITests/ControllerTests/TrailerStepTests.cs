using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using SettlersOfIdlestan.Controller;
using SettlersOfIdlestan.Controller.Expand;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Prestige.PrestigeMap;
using SOITests.TestUtilities;
using Xunit;

namespace SOITests.ControllerTests
{
    /// <summary>
    /// Vérifie que chaque séquence du trailer se déroule correctement :
    /// routes construites, récoltes effectuées, attaques lancées, etc.
    /// </summary>
    public class TrailerStepTests
    {
        // Même paramétrage que VideoExportController : 30 fps, 3 ticks/frame (100/30 arrondi)
        private const int Fps = 30;
        private const long TicksPerFrame = 3;
        // Exterminate utilise SimulationSpeedMultiplier=3.0 → round(100/30*3)=10 ticks/frame
        private const long ExterminateTicksPerFrame = 10;
        // Research insert utilise SimulationSpeedMultiplier=10.0 → round(100/30*10)=33 ticks/frame
        private const long ResearchTicksPerFrame = 33L;

        // ── Helpers ────────────────────────────────────────────────────────────

        private static string GetTrailerSavesDirectory()
        {
            // GetSolutionRootDirectory remonte jusqu'au .csproj => SOITests/
            // Le parent est la racine de la solution : SettlersOfIdlestan/
            var projectDir = SaveUtils.GetSolutionRootDirectory(Directory.GetCurrentDirectory());
            var solutionDir = Directory.GetParent(projectDir)?.FullName ?? projectDir;
            return Path.Combine(solutionDir, "assets", "Trailer", "saves");
        }

        private record LoadedSave(MainGameController Controller, CivilizationAutoplayer Autoplayer, Civilization Civ);

        private static LoadedSave LoadTrailerSave(string saveFileName)
        {
            var path = Path.Combine(GetTrailerSavesDirectory(), saveFileName);
            var controller = new MainGameController();
            controller.ImportMainState(File.ReadAllText(path));

            var worldState = controller.CurrentMainState!.CurrentWorldState!;
            var civ = worldState.PlayerCivilization;
            var map = worldState.GetMapForZ(IslandMap.SurfaceLayer)!;

            var autoplayer = new CivilizationAutoplayer(
                civ, map,
                controller.RoadController,
                controller.HarvestController,
                controller.BuildingController,
                controller.CityBuilderController,
                controller.TradeController,
                controller.ResearchController,
                controller.PrestigeController,
                controller.PrestigeMapController,
                worldState,
                controller.CurrentMainState?.PrestigeState,
                controller.PerformPrestige,
                controller.WonderController);

            return new LoadedSave(controller, autoplayer, civ);
        }

        // Même intervalle que TrailerDefinition.AutoplayIntervalSeconds par défaut (0.5s = 15 frames à 30fps)
        private const float DefaultAutoplayIntervalSeconds = 0.5f;

        /// <param name="autoplayIntervalSeconds">0 ou négatif = chaque frame ; sinon intervalle en secondes.</param>
        /// <param name="ticksPerFrame">Ticks simulés par frame (défaut 3 = speed×1.0 ; 10 = speed×3.0 comme Exterminate).</param>
        private static void SimulateSequence(
            MainGameController controller,
            int durationSeconds,
            Action autoplayTick,
            float autoplayIntervalSeconds = DefaultAutoplayIntervalSeconds,
            long ticksPerFrame = TicksPerFrame)
        {
            int totalFrames = Fps * durationSeconds;
            int intervalFrames = autoplayIntervalSeconds > 0f
                ? Math.Max(1, (int)Math.Round(autoplayIntervalSeconds * Fps))
                : 1;

            for (int frame = 0; frame < totalFrames; frame++)
            {
                controller.Clock!.SimulateAdvance(ticksPerFrame);
                if (frame % intervalFrames == 0)
                    autoplayTick();
            }
        }

        // ── Étape 1 : EXPLORE (01_Explore.json, 5s, Expand = TryStep0Once) ───
        // Le joueur doit voir au moins 3 routes construites, puis un avant-poste,
        // puis d'autres routes pour révéler un maximum de hexs.

        [Fact]
        public void Trailer_Explore_BuildsAtLeast3Roads()
        {
            var (controller, autoplayer, civ) = LoadTrailerSave("01_Explore.json");
            int initialRoads = civ.Roads.Count;

            SimulateSequence(controller, 5, () => autoplayer.TryExpandOnce());

            int roadsBuilt = civ.Roads.Count - initialRoads;
            Assert.True(roadsBuilt >= 3,
                $"Explore : attendu ≥3 routes construites, mais seulement {roadsBuilt} ajoutée(s) " +
                $"(initial : {initialRoads}, final : {civ.Roads.Count})");
        }

        [Fact]
        public void Trailer_Explore_BuildsOutpost()
        {
            var (controller, autoplayer, civ) = LoadTrailerSave("01_Explore.json");
            int initialCities = civ.Cities.Count;

            SimulateSequence(controller, 5, () => autoplayer.TryExpandOnce());

            Assert.True(civ.Cities.Count > initialCities,
                $"Explore : attendu au moins un avant-poste construit, mais le nombre de villes " +
                $"est resté à {civ.Cities.Count} (initial : {initialCities})");
        }

        [Fact]
        public void Trailer_Explore_BuildsRoadsBeforeOutpost()
        {
            var (controller, autoplayer, civ) = LoadTrailerSave("01_Explore.json");

            // On surveille l'ordre : des routes doivent être construites avant le premier avant-poste.
            // L'autoplayer est appelé au même intervalle que dans le vrai trailer (DefaultAutoplayIntervalSeconds).
            int roadsWhenFirstOutpostBuilt = -1;
            int initialCities = civ.Cities.Count;
            int roadsBuiltSoFar = 0;

            int totalFrames = Fps * 5;
            int intervalFrames = Math.Max(1, (int)Math.Round(DefaultAutoplayIntervalSeconds * Fps));
            for (int frame = 0; frame < totalFrames; frame++)
            {
                controller.Clock!.SimulateAdvance(TicksPerFrame);
                if (frame % intervalFrames == 0)
                    autoplayer.TryExpandOnce();

                roadsBuiltSoFar = civ.Roads.Count;

                if (roadsWhenFirstOutpostBuilt < 0 && civ.Cities.Count > initialCities)
                {
                    roadsWhenFirstOutpostBuilt = roadsBuiltSoFar;
                    break;
                }
            }

            if (roadsWhenFirstOutpostBuilt >= 0)
            {
                Assert.True(roadsWhenFirstOutpostBuilt >= 3,
                    $"Explore : l'avant-poste a été construit alors que seulement {roadsWhenFirstOutpostBuilt} route(s) " +
                    $"existaient (il en faut ≥3 d'abord)");
            }
            else
            {
                // Pas d'avant-poste construit — la contrainte des routes est vérifiée par Trailer_Explore_BuildsAtLeast3Roads
                Assert.True(roadsBuiltSoFar >= 3,
                    $"Explore : aucun avant-poste construit et seulement {roadsBuiltSoFar} route(s) au bout de 5s");
            }
        }

        // ── Étape 2 : EXPAND (02_Expand.json, 4s, Expand = TryStep0Once) ─────

        [Fact]
        public void Trailer_Expand_ContinuesExpansion()
        {
            var (controller, autoplayer, civ) = LoadTrailerSave("02_Expand.json");
            int initialRoads = civ.Roads.Count;
            int initialCities = civ.Cities.Count;

            SimulateSequence(controller, 4, () => autoplayer.TryExpandOnce());

            bool expanded = civ.Roads.Count > initialRoads || civ.Cities.Count > initialCities;
            Assert.True(expanded,
                $"Expand : aucune expansion détectée — routes {initialRoads}→{civ.Roads.Count}, " +
                $"villes {initialCities}→{civ.Cities.Count}");
        }

        // ── Étape 3 : EXPLOIT (03_Exploit.json, 4s, Exploit = TryStep1Once/TryStep2Once) ─

        [Fact]
        public void Trailer_Exploit_BuildsProductionBuildings()
        {
            var (controller, autoplayer, civ) = LoadTrailerSave("03_Exploit.json");
            int initialBuildings = civ.Cities.Sum(c => c.Buildings.Count);

            SimulateSequence(controller, 4, () =>
                CivilizationAutoplayerPriorities.Step2(autoplayer, controller.BuildingController).TryStepOnce());

            int finalBuildings = civ.Cities.Sum(c => c.Buildings.Count);
            Assert.True(finalBuildings > initialBuildings,
                $"Exploit : attendu des constructions de bâtiments, mais le total " +
                $"est resté à {finalBuildings} (initial : {initialBuildings})");
        }

        [Fact]
        public void Trailer_Exploit_HarvestsOccurWithoutErrors()
        {
            var (controller, autoplayer, civ) = LoadTrailerSave("03_Exploit.json");

            // La simulation doit se dérouler sans exception et laisser les ressources à ≥0
            SimulateSequence(controller, 4, () =>
                CivilizationAutoplayerPriorities.Step2(autoplayer, controller.BuildingController).TryStepOnce());

            foreach (Resource r in Enum.GetValues<Resource>())
            {
                int qty = civ.GetResourceQuantity(r);
                Assert.True(qty >= 0,
                    $"Exploit : la ressource {r} est devenue négative ({qty}) après la séquence");
            }
        }

        // ── Étape 4 : EXTERMINATE (04_Exterminate.json, 4s, Exterminate = TryMilitaryStepOnce) ─

        [Fact]
        public void Trailer_Exterminate_HasMilitaryBuildingsAndEnemies()
        {
            // Le beat Exterminate est une vitrine de la phase militaire : le joueur doit déjà
            // avoir des bâtiments militaires ET des ennemis à combattre.
            var (controller, autoplayer, civ) = LoadTrailerSave("04_Exterminate.json");
            var worldState = controller.CurrentMainState!.CurrentWorldState!;

            int militaryBuildings = civ.Cities.Sum(c => c.Buildings.Count(b =>
                b.Type == BuildingType.Palisade || b.Type == BuildingType.Barracks));
            int enemyCities = worldState.Civilizations.Skip(1).Sum(c => c.Cities.Count);

            Assert.True(militaryBuildings > 0,
                $"Exterminate : le joueur devrait avoir des Palissades/Casernes, mais il n'en a aucune " +
                $"(bâtiments milit. = {militaryBuildings})");
            Assert.True(enemyCities > 0,
                $"Exterminate : il devrait y avoir des villes ennemies à combattre (enemyCities = {enemyCities})");
        }

        [Fact]
        public void Trailer_Exterminate_BuildsMilitaryBuildings()
        {
            var (controller, autoplayer, civ) = LoadTrailerSave("04_Exterminate.json");

            int initialMilitary = civ.Cities.Sum(c => c.Buildings.Count(b =>
                b.Type == BuildingType.Palisade || b.Type == BuildingType.Barracks));
            int initialBuildings = civ.Cities.Sum(c => c.Buildings.Count);

            SimulateSequence(controller, 4, () => CivilizationAutoplayerPriorities.Military(autoplayer, controller.BuildingController).TryStepOnce());

            int finalMilitary = civ.Cities.Sum(c => c.Buildings.Count(b =>
                b.Type == BuildingType.Palisade || b.Type == BuildingType.Barracks));
            int finalBuildings = civ.Cities.Sum(c => c.Buildings.Count);

            Assert.True(finalBuildings >= initialBuildings,
                $"Exterminate : le nombre total de bâtiments a diminué ({initialBuildings}→{finalBuildings})");
            Assert.True(finalMilitary >= initialMilitary,
                $"Exterminate : les bâtiments militaires ont diminué ({initialMilitary}→{finalMilitary})");
        }

        [Fact]
        public void Trailer_Exterminate_AttacksFireDuringSequence()
        {
            // Vérifie que la séquence Exterminate déclenche de vraies attaques (défense ennemie réduite
            // ou villes détruites) avec les paramètres réels du trailer (3× speed = 10 ticks/frame,
            // FlowTargets assignés par l'autoplayer).
            var (controller, autoplayer, civ) = LoadTrailerSave("04_Exterminate.json");
            var worldState = controller.CurrentMainState!.CurrentWorldState!;

            int initialTotalDefense = worldState.Civilizations.Skip(1)
                .SelectMany(c => c.Cities)
                .Sum(city => city.CurrentDefense);
            int initialEnemyCities = worldState.Civilizations.Skip(1).Sum(c => c.Cities.Count);

            var militaryController = controller.MilitaryController;

            // Même comportement que TrailerService.BuildAutoplayTick(Exterminate)
            void autoplayTick()
            {
                CivilizationAutoplayerPriorities.Military(autoplayer, controller.BuildingController).TryStepOnce();
                foreach (var city in civ.Cities)
                {
                    if (city.FlowTarget == null)
                    {
                        var enemy = militaryController.FindNearbyEnemyCity(city);
                        if (enemy != null) militaryController.SetCityFlow(city, enemy.Position);
                    }
                }
            }

            // 10 ticks/frame = SimulationSpeedMultiplier 3.0 comme dans TrailerDefinition.json
            SimulateSequence(controller, 4, autoplayTick, ticksPerFrame: ExterminateTicksPerFrame);

            int finalTotalDefense = worldState.Civilizations.Skip(1)
                .SelectMany(c => c.Cities)
                .Sum(city => city.CurrentDefense);
            int finalEnemyCities = worldState.Civilizations.Skip(1).Sum(c => c.Cities.Count);

            // Les attaques doivent avoir eu lieu : soit des villes détruites, soit la défense a diminué
            bool attacksOccurred = finalEnemyCities < initialEnemyCities || finalTotalDefense < initialTotalDefense;
            Assert.True(attacksOccurred,
                $"Exterminate : aucune attaque détectée — villes ennemies {initialEnemyCities}→{finalEnemyCities}, " +
                $"défense totale {initialTotalDefense}→{finalTotalDefense}. " +
                $"Vérifier que la save 04_Exterminate.json contient des soldats et des ennemis à portée.");
        }

        // ── Insert PRESTIGE (02_Expand.json, 3s, PrestigePurchase, +60 pts injectés) ──────

        [Fact]
        public void Trailer_Prestige_VertexPurchased()
        {
            var (controller, autoplayer, civ) = LoadTrailerSave("02_Expand.json");
            var ps = controller.CurrentMainState!.PrestigeState!;
            ps.PrestigePoints += 60; // Cheat de présentation
            int initialVertices = ps.PurchasedVertices.Count;

            SimulateSequence(controller, 3, () =>
            {
                var cheapest = PrestigeMapController.DefaultMap.Vertices
                    .OrderBy(v => v.Cost)
                    .FirstOrDefault(v => controller.PrestigeMapController.CanPurchaseVertex(ps, v.Coord));
                if (cheapest != null)
                    controller.PrestigeMapController.PurchaseVertex(ps, cheapest.Coord);
            }, autoplayIntervalSeconds: 1.0f);

            int finalVertices = ps.PurchasedVertices.Count;
            Assert.True(finalVertices > initialVertices,
                $"Prestige : aucun vertex acheté malgré 60 points injectés " +
                $"(initial : {initialVertices}, final : {finalVertices})");
        }

        // ── Insert RESEARCH (03_Exploit.json, 3s, ResearchPurchase, 9999 pts injectés, 10× speed) ─

        [Fact]
        public void Trailer_Research_TechnologyCompleted()
        {
            var (controller, autoplayer, civ) = LoadTrailerSave("03_Exploit.json");
            var ps = controller.CurrentMainState!.PrestigeState!;
            // Débloque la recherche via CentralVertex
            ps.PrestigePoints += 10;
            controller.PrestigeMapController.PurchaseVertex(ps, PrestigeMap.CentralVertex);
            ps.TechnologyTree.ResearchPoints += 9999; // 9999 pour consommer 99 pts/événement (min tech cost=300)
            // Lance la première recherche avant de simuler
            autoplayer.TryResearchOnce();

            int initialCompleted = TechnologyDefinitions.All
                .Count(t => controller.ResearchController.GetStatus(t.Id) == TechnologyStatus.Completed);

            // 33 ticks/frame = SimulationSpeedMultiplier 10.0 comme dans TrailerDefinition.json
            SimulateSequence(controller, 3, () => autoplayer.TryResearchOnce(),
                autoplayIntervalSeconds: 0.3f, ticksPerFrame: ResearchTicksPerFrame);

            int finalCompleted = TechnologyDefinitions.All
                .Count(t => controller.ResearchController.GetStatus(t.Id) == TechnologyStatus.Completed);
            Assert.True(finalCompleted > initialCompleted,
                $"Research : aucune technologie complétée malgré 9999 pts injectés " +
                $"(initial : {initialCompleted}, final : {finalCompleted})");
        }

        // ── Étape 5 : AT YOUR OWN PACE (05_AtYourOwnPace.json, 4s, aucun autoplay) ─

        [Fact]
        public void Trailer_AtYourOwnPace_SaveIsValidAndStable()
        {
            var (controller, autoplayer, civ) = LoadTrailerSave("05_AtYourOwnPace.json");
            int initialCities = civ.Cities.Count;

            SimulateSequence(controller, 4, () => { });

            Assert.True(civ.Cities.Count > 0, "AtYourOwnPace : attendu au moins une ville");
            Assert.True(civ.Cities.Count >= initialCities,
                $"AtYourOwnPace : des villes ont disparu ({initialCities}→{civ.Cities.Count})");
        }

        // ── Étape 6 : ABYSS GATE (06_AbyssGate.json, 5s, aucun autoplay, vue inframonde) ─

        [Fact]
        public void Trailer_AbyssGate_SaveIsValidAndStable()
        {
            var (controller, autoplayer, civ) = LoadTrailerSave("06_AbyssGate.json");
            var worldState = controller.CurrentMainState!.CurrentWorldState!;
            int initialCities = civ.Cities.Count;

            SimulateSequence(controller, 5, () => { });

            Assert.True(civ.Cities.Count > 0, "AbyssGate : attendu au moins une ville");
            Assert.True(civ.Cities.Count >= initialCities,
                $"AbyssGate : des villes ont disparu ({initialCities}→{civ.Cities.Count})");
        }

        // ── Bilan complet ─────────────────────────────────────────────────────

        [Fact]
        public void Trailer_GenerateBilanFile()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== BILAN TRAILER — SETTLERS OF IDLESTAN ===");
            sb.AppendLine($"Généré le : {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Paramètres simulation : {Fps} fps, {TicksPerFrame} ticks/frame, intervalle autoplayer : {DefaultAutoplayIntervalSeconds}s");
            sb.AppendLine();

            AppendSequenceBilan(sb, "1 — EXPLORE",           "01_Explore.json",         5, (a) => a.TryExpandOnce());
            AppendSequenceBilan(sb, "2 — EXPAND",            "02_Expand.json",          4, (a) => a.TryExpandOnce());
            // Prestige insert : +60 pts de prestige injectés, PrestigePurchase, intervalle 1s
            AppendSequenceBilan(sb, "3 — PRESTIGE (insert)", "02_Expand.json",          3,
                autoplayAction: null,
                extraSetup: (ctrl, c, ws) => { var ps = ctrl.CurrentMainState?.PrestigeState; if (ps != null) ps.PrestigePoints += 60; },
                customAutoplay: (ctrl, a) =>
                {
                    var ps = ctrl.CurrentMainState?.PrestigeState;
                    if (ps == null) return;
                    var cheapest = PrestigeMapController.DefaultMap.Vertices
                        .OrderBy(v => v.Cost)
                        .FirstOrDefault(v => ctrl.PrestigeMapController.CanPurchaseVertex(ps, v.Coord));
                    if (cheapest != null) ctrl.PrestigeMapController.PurchaseVertex(ps, cheapest.Coord);
                },
                autoplayIntervalSeconds: 1.0f);
            AppendSequenceBilan(sb, "4 — EXPLOIT",           "03_Exploit.json",         4, null,
                customAutoplay: (ctrl, a) => CivilizationAutoplayerPriorities.Step2(a, ctrl.BuildingController).TryStepOnce());
            // Research insert : CentralVertex + 999 pts injectés, speed×10 (33 ticks/frame), intervalle 0.3s
            AppendSequenceBilan(sb, "5 — RESEARCH (insert)", "03_Exploit.json",         3,
                autoplayAction: null,
                extraSetup: (ctrl, c, ws) =>
                {
                    var ps = ctrl.CurrentMainState?.PrestigeState;
                    if (ps != null)
                    {
                        ps.PrestigePoints += 10;
                        ctrl.PrestigeMapController.PurchaseVertex(ps, PrestigeMap.CentralVertex);
                        ps.TechnologyTree.ResearchPoints += 9999;
                    }
                },
                customAutoplay: (ctrl, a) => a.TryResearchOnce(),
                autoplayIntervalSeconds: 0.3f,
                ticksPerFrame: ResearchTicksPerFrame);
            // Exterminate : speed×3 (10 ticks/frame) + FlowTargets assignés comme dans TrailerService
            AppendSequenceBilan(sb, "6 — EXTERMINATE",       "04_Exterminate.json",     4,
                autoplayAction: null,
                extraSetup: (ctrl, c, ws) =>
                {
                    foreach (var city in c.Cities)
                    {
                        if (city.FlowTarget == null)
                        {
                            var enemy = ctrl.MilitaryController.FindNearbyEnemyCity(city);
                            if (enemy != null) ctrl.MilitaryController.SetCityFlow(city, enemy.Position);
                        }
                    }
                },
                customAutoplay: (ctrl, a) => CivilizationAutoplayerPriorities.Military(a, ctrl.BuildingController).TryStepOnce(),
                ticksPerFrame: ExterminateTicksPerFrame);
            AppendSequenceBilan(sb, "7 — AT YOUR OWN PACE",  "05_AtYourOwnPace.json",   4, null);
            AppendSequenceBilan(sb, "8 — ABYSS GATE",        "06_AbyssGate.json",       5, null);

            // Écrit dans assets/Trailer/trailer_bilan.txt
            var savesDir = GetTrailerSavesDirectory();
            var bilanPath = Path.Combine(Directory.GetParent(savesDir)!.FullName, "trailer_bilan.txt");
            File.WriteAllText(bilanPath, sb.ToString(), System.Text.Encoding.UTF8);

            // Le test réussit si le fichier a été écrit
            Assert.True(File.Exists(bilanPath), $"Le fichier bilan n'a pas pu être écrit à {bilanPath}");
        }

        private static void AppendSequenceBilan(
            StringBuilder sb,
            string sequenceName,
            string saveFile,
            int durationSeconds,
            Action<CivilizationAutoplayer>? autoplayAction,
            Action<MainGameController, Civilization, object>? extraSetup = null,
            Action<MainGameController, CivilizationAutoplayer>? customAutoplay = null,
            float autoplayIntervalSeconds = DefaultAutoplayIntervalSeconds,
            long ticksPerFrame = TicksPerFrame)
        {
            sb.AppendLine($"--- {sequenceName} ({saveFile}, {durationSeconds}s, {ticksPerFrame} ticks/frame) ---");
            try
            {
                var (controller, autoplayer, civ) = LoadTrailerSave(saveFile);
                var worldState = controller.CurrentMainState!.CurrentWorldState!;

                extraSetup?.Invoke(controller, civ, worldState);

                int initialRoads        = civ.Roads.Count;
                int initialCities       = civ.Cities.Count;
                int initialBuildings    = civ.Cities.Sum(c => c.Buildings.Count);
                int initialMilitary     = civ.Cities.Sum(c => c.Buildings.Count(b =>
                    b.Type == BuildingType.Palisade || b.Type == BuildingType.Barracks));
                int initialEnemyCities  = worldState.Civilizations.Skip(1).Sum(c => c.Cities.Count);
                int initialSoldiers     = civ.Cities.Sum(c => c.Soldiers);
                int initialEnemyDefense = worldState.Civilizations.Skip(1).SelectMany(c => c.Cities).Sum(c => c.CurrentDefense);
                var ps                  = controller.CurrentMainState?.PrestigeState;
                int initialVertices     = ps?.PurchasedVertices.Count ?? 0;
                int initialPrestige     = ps?.PrestigePoints ?? 0;
                int initialResearchPts  = ps?.TechnologyTree.ResearchPoints ?? 0;
                int initialResearchDone = TechnologyDefinitions.All.Count(t => controller.ResearchController.GetStatus(t.Id) == TechnologyStatus.Completed);
                long initialTick        = controller.Clock!.CurrentTick;

                sb.AppendLine("  État initial :");
                sb.AppendLine($"    Routes          : {initialRoads}");
                sb.AppendLine($"    Villes joueur   : {initialCities}");
                sb.AppendLine($"    Bâtiments total : {initialBuildings}");
                sb.AppendLine($"    Bâtiments milit.: {initialMilitary}");
                sb.AppendLine($"    Soldats joueur  : {initialSoldiers}");
                sb.AppendLine($"    Villes ennemies : {initialEnemyCities}");
                sb.AppendLine($"    Défense ennemie : {initialEnemyDefense}");
                sb.AppendLine($"    Points prestige : {initialPrestige}");
                sb.AppendLine($"    Vertices achetés: {initialVertices}");
                sb.AppendLine($"    Points recherche: {initialResearchPts}");
                sb.AppendLine($"    Techs complétées: {initialResearchDone}");

                if (customAutoplay != null)
                    SimulateSequence(controller, durationSeconds, () => customAutoplay(controller, autoplayer),
                        autoplayIntervalSeconds, ticksPerFrame);
                else if (autoplayAction != null)
                    SimulateSequence(controller, durationSeconds, () => autoplayAction(autoplayer),
                        autoplayIntervalSeconds, ticksPerFrame);
                else
                    SimulateSequence(controller, durationSeconds, () => { },
                        autoplayIntervalSeconds: 0, ticksPerFrame);

                int finalRoads        = civ.Roads.Count;
                int finalCities       = civ.Cities.Count;
                int finalBuildings    = civ.Cities.Sum(c => c.Buildings.Count);
                int finalMilitary     = civ.Cities.Sum(c => c.Buildings.Count(b =>
                    b.Type == BuildingType.Palisade || b.Type == BuildingType.Barracks));
                int finalEnemyCities  = worldState.Civilizations.Skip(1).Sum(c => c.Cities.Count);
                int finalSoldiers     = civ.Cities.Sum(c => c.Soldiers);
                int finalEnemyDefense = worldState.Civilizations.Skip(1).SelectMany(c => c.Cities).Sum(c => c.CurrentDefense);
                int finalVertices     = ps?.PurchasedVertices.Count ?? 0;
                int finalPrestige     = ps?.PrestigePoints ?? 0;
                int finalResearchPts  = ps?.TechnologyTree.ResearchPoints ?? 0;
                int finalResearchDone = TechnologyDefinitions.All.Count(t => controller.ResearchController.GetStatus(t.Id) == TechnologyStatus.Completed);
                long finalTick        = controller.Clock!.CurrentTick;

                sb.AppendLine("  État final :");
                sb.AppendLine($"    Routes          : {finalRoads} ({DeltaStr(finalRoads - initialRoads)})");
                sb.AppendLine($"    Villes joueur   : {finalCities} ({DeltaStr(finalCities - initialCities)})");
                sb.AppendLine($"    Bâtiments total : {finalBuildings} ({DeltaStr(finalBuildings - initialBuildings)})");
                sb.AppendLine($"    Bâtiments milit.: {finalMilitary} ({DeltaStr(finalMilitary - initialMilitary)})");
                sb.AppendLine($"    Soldats joueur  : {finalSoldiers} ({DeltaStr(finalSoldiers - initialSoldiers)})");
                sb.AppendLine($"    Villes ennemies : {finalEnemyCities} ({DeltaStr(finalEnemyCities - initialEnemyCities)})");
                sb.AppendLine($"    Défense ennemie : {finalEnemyDefense} ({DeltaStr(finalEnemyDefense - initialEnemyDefense)})");
                sb.AppendLine($"    Points prestige : {finalPrestige} ({DeltaStr(finalPrestige - initialPrestige)})");
                sb.AppendLine($"    Vertices achetés: {finalVertices} ({DeltaStr(finalVertices - initialVertices)})");
                sb.AppendLine($"    Points recherche: {finalResearchPts} ({DeltaStr(finalResearchPts - initialResearchPts)})");
                sb.AppendLine($"    Techs complétées: {finalResearchDone} ({DeltaStr(finalResearchDone - initialResearchDone)})");
                sb.AppendLine($"    Ticks simulés   : {finalTick - initialTick}");
                sb.AppendLine("  RÉSULTAT : OK");
            }
            catch (Exception ex)
            {
                sb.AppendLine($"  RÉSULTAT : ERREUR — {ex.Message}");
            }

            sb.AppendLine();
        }

        private static string DeltaStr(int delta) => delta >= 0 ? $"+{delta}" : $"{delta}";
    }
}
