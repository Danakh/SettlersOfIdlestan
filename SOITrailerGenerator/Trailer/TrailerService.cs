using System.Linq;
using System.Text.Json;
using System.Threading;
using SkiaSharp;
using SettlersOfIdlestan.Controller;
using SettlersOfIdlestan.Controller.Expand;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Prestige.PrestigeMap;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Renderers.Island;
using SettlersOfIdlestanSkia.Renderers.Overlay;
using SettlersOfIdlestanSkia.Renderers.Overlay.Panels;
using SettlersOfIdlestanSkia.Renderers.Overlay.Popup;
using SettlersOfIdlestanSkia.Renderers.Overlay.Tabs;
using SettlersOfIdlestanSkia.Screens;
using SettlersOfIdlestanSkia.Services;
using SettlersOfIdlestanSkia.Services.Localization;

namespace SOITrailerGenerator.Trailer;

/// <summary>
/// Lit une TrailerDefinition.json, rejoue chaque save listée (avancement de simulation headless via
/// VideoExportController/GameClock.SimulateAdvance) et capture des frames PNG (île + overlay UI complet,
/// comme à l'écran) dans Frames/ avec une numérotation continue entre séquences. Assemble ensuite la
/// vidéo finale via ffmpeg directement dans le dossier racine de la bande-annonce.
/// </summary>
public sealed class TrailerService
{
    private static readonly JsonSerializerOptions DefinitionJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    /// <param name="trailerRootDirectory">Dossier assets/Trailer (contient TrailerDefinition.json, saves/, Frames/).</param>
    /// <returns>La commande ffmpeg utilisée pour produire trailer.mp4 (déjà exécutée par ce dossier appelant si besoin).</returns>
    public string GenerateTrailer(string trailerRootDirectory, Action<string>? onLog = null)
    {
        var definitionPath = Path.Combine(trailerRootDirectory, "TrailerDefinition.json");
        var savesDirectory = Path.Combine(trailerRootDirectory, "saves");
        var framesDirectory = Path.Combine(trailerRootDirectory, "Frames");
        var outputVideoPath = Path.Combine(trailerRootDirectory, "trailer.mp4");

        var definition = JsonSerializer.Deserialize<TrailerDefinition>(
            File.ReadAllText(definitionPath), DefinitionJsonOptions)
            ?? throw new InvalidOperationException($"Impossible de lire {definitionPath}.");

        // Sous Windows, l'indexeur de recherche ou l'antivirus peut verrouiller brièvement un dossier
        // qui vient d'être recréé (beaucoup de petits PNG y seront écrits) ; on retente quelques fois.
        WithRetry(() =>
        {
            if (Directory.Exists(framesDirectory))
                Directory.Delete(framesDirectory, recursive: true);
            Directory.CreateDirectory(framesDirectory);
        }, maxAttempts: 30, delayMs: 1000, onRetry: ex => onLog?.Invoke($"  dossier Frames verrouillé, nouvelle tentative... ({ex.Message})"));

        var canvasSize = new SKSize(definition.Width, definition.Height);
        int frameCursor = 0;

        for (int i = 0; i < definition.Sequences.Count; i++)
        {
            var sequence = definition.Sequences[i];
            onLog?.Invoke($"Séquence {i + 1}/{definition.Sequences.Count} : {(sequence.IsTitleCard ? "[Title Card]" : sequence.Save)} ({sequence.DurationSeconds}s)");

            int framesWritten = sequence.IsTitleCard
                ? CaptureTitleCard(sequence, definition, canvasSize, framesDirectory, frameCursor)
                : CaptureSequence(Path.Combine(savesDirectory, sequence.Save), sequence, definition, canvasSize, framesDirectory, frameCursor, onLog);
            frameCursor += framesWritten;
        }

        string ffmpegCommand =
            $"ffmpeg -y -framerate {definition.Fps} -i \"{Path.Combine(framesDirectory, "frame_%05d.png")}\" " +
            $"-c:v libx264 -pix_fmt yuv420p \"{outputVideoPath}\"";
        File.WriteAllText(Path.Combine(trailerRootDirectory, "ffmpeg_command.txt"), ffmpegCommand);

        onLog?.Invoke($"{frameCursor} frames écrites dans {framesDirectory}.");
        return ffmpegCommand;
    }

    private static int CaptureSequence(
        string savePath,
        TrailerSequence sequence,
        TrailerDefinition definition,
        SKSize canvasSize,
        string framesDirectory,
        int startFrameIndex,
        Action<string>? onLog)
    {
        if (!File.Exists(savePath))
            throw new FileNotFoundException($"Save de bande-annonce introuvable : {savePath}", savePath);

        var gameControllerService = new GameControllerService();
        gameControllerService.ImportMainState(File.ReadAllText(savePath));

        ApplyCheats(sequence, gameControllerService);

        if (gameControllerService.CurrentWorldState is { } worldState)
        {
            worldState.CurrentViewedLayer = sequence.ViewedLayer == TrailerViewedLayer.Underworld
                ? LayerState.UnderworldZ
                : IslandMap.SurfaceLayer;
        }

        var resourceManager = new ResourceManager();
        var localizationService = new LocalizationService();
        var inputService = new InputHandlingService();
        var uiLayoutService = new UILayoutService();
        uiLayoutService.UpdateCanvasSize(canvasSize);
        uiLayoutService.SetForceMobile(sequence.DeviceProfile == TrailerDeviceProfile.Mobile);

        var tooltipRenderer = new TooltipRenderer(localizationService, gameControllerService, resourceManager);
        var hoverProvider = new EmptyConstructionHoverProvider();

        var islandRenderer = new IslandMainRenderer(
            hoverProvider,
            tooltipRenderer,
            localizationService,
            gameControllerService.MainGameController.HarvestController,
            resourceManager,
            gameControllerService.MainGameController.MilitaryController,
            currentLayer: () => gameControllerService.CurrentWorldState?.CurrentViewedLayer ?? 0);

        // Connecte les événements pour que les animations (attaques, renforts, récoltes) apparaissent dans la vidéo.
        islandRenderer.ConnectMilitaryEvents(
            gameControllerService.MainGameController.MilitaryController,
            gameControllerService,
            isPrestigeTransitionPending: () => false,
            isIslandTabActive: () => true);
        var harvestService = new HarvestService(gameControllerService);
        islandRenderer.ConnectHarvestEvents(
            harvestService,
            gameControllerService,
            isPrestigeTransitionPending: () => false,
            isIslandTabActive: () => true);

        var monumentService = new MonumentService();
        islandRenderer.ConnectMonumentService(monumentService);

        var overlayRenderer = BuildOverlayRenderer(
            gameControllerService, localizationService, resourceManager, inputService, uiLayoutService,
            tooltipRenderer, monumentService);

        var sceneRenderer = new CompositeGameRenderer(islandRenderer, overlayRenderer, tooltipRenderer);
        sceneRenderer.Initialize(canvasSize);

        switch (sequence.ForcedTab)
        {
            case TrailerForcedTab.Prestige: overlayRenderer.SwitchToPrestigeTab(); break;
            case TrailerForcedTab.Research: overlayRenderer.SwitchToResearchTab(); break;
            case TrailerForcedTab.Island: overlayRenderer.SwitchToIslandTab(); break;
        }

        var cameraAtTime = TrailerCameraTrack.Build(sequence.Camera, gameControllerService, canvasSize);
        var autoplayTick = BuildAutoplayTick(sequence.AutoplayProfile, gameControllerService);
        var timedAutoplayTick = WrapAutoplayWithInterval(autoplayTick, sequence.AutoplayIntervalSeconds, sequence.AutoplayInitialDelaySeconds, definition.Fps);

        var videoExportController = new VideoExportController(gameControllerService, sceneRenderer);
        int totalFrames = definition.Fps * sequence.DurationSeconds;
        videoExportController.CaptureSequence(
            framesDirectory,
            definition.Width,
            definition.Height,
            definition.Fps,
            sequence.DurationSeconds,
            cameraAtTime,
            sequence.SimulationSpeedMultiplier,
            onFrameCaptured: (frame, total) => onLog?.Invoke($"  frame {frame}/{total}"),
            startFrameIndex: startFrameIndex,
            writeFfmpegCommand: false,
            textCues: sequence.TextCues,
            autoplayTick: timedAutoplayTick);

        sceneRenderer.Dispose();
        resourceManager.Dispose();

        return totalFrames;
    }

    /// <summary>
    /// Enveloppe <paramref name="autoplayTick"/> pour ne l'appeler qu'une fois par intervalle de
    /// <paramref name="intervalSeconds"/> secondes plutôt qu'à chaque frame. Avec <paramref name="initialDelaySeconds"/> > 0,
    /// le premier appel est retardé d'autant (laisse le spectateur voir l'état initial avant la première action).
    /// Retourne null si autoplayTick est null. Avec intervalSeconds ≤ 0, appel à chaque frame sans délai.
    /// </summary>
    private static Action? WrapAutoplayWithInterval(Action? autoplayTick, float intervalSeconds, float initialDelaySeconds, int fps)
    {
        if (autoplayTick == null) return null;
        if (intervalSeconds <= 0f) return autoplayTick;

        int intervalFrames = Math.Max(1, (int)Math.Round(intervalSeconds * fps));
        int initialDelayFrames = Math.Max(0, (int)Math.Round(initialDelaySeconds * fps));
        int frame = 0;
        return () =>
        {
            if (frame >= initialDelayFrames && (frame - initialDelayFrames) % intervalFrames == 0)
                autoplayTick();
            frame++;
        };
    }

    /// <summary>
    /// Construit, si <paramref name="profile"/> n'est pas None, un CivilizationAutoplayer câblé sur les
    /// contrôleurs de la save chargée, et renvoie l'action à appeler une fois par frame pendant la
    /// capture pour que les constructions/combats du beat correspondant se produisent réellement
    /// (cf. STORYBOARD.md — Expand/Exploit/Exterminate).
    /// </summary>
    private static Action? BuildAutoplayTick(TrailerAutoplayProfile profile, GameControllerService gameControllerService)
    {
        if (profile == TrailerAutoplayProfile.None)
            return null;

        var mainController = gameControllerService.MainGameController;
        var civ = gameControllerService.PlayerCivilization
            ?? throw new InvalidOperationException("Aucune civilisation joueur dans la save chargée.");
        var worldState = gameControllerService.CurrentWorldState
            ?? throw new InvalidOperationException("Aucun WorldState dans la save chargée.");
        var surfaceMap = worldState.GetMapForZ(IslandMap.SurfaceLayer)
            ?? throw new InvalidOperationException("Aucune carte de surface dans la save chargée.");

        var autoplayer = new CivilizationAutoplayer(
            civ, surfaceMap,
            mainController.RoadController,
            mainController.HarvestController,
            mainController.BuildingController,
            mainController.CityBuilderController,
            mainController.TradeController,
            mainController.ResearchController,
            mainController.PrestigeController,
            mainController.PrestigeMapController,
            worldState,
            gameControllerService.CurrentGameState?.PrestigeState,
            mainController.PerformPrestige,
            mainController.WonderController);

        return profile switch
        {
            TrailerAutoplayProfile.Expand => () => autoplayer.TryExpandOnce(),
            TrailerAutoplayProfile.Exploit => () =>
            {
                CivilizationAutoplayerPriorities.Step2(autoplayer, mainController.BuildingController).TryStepOnce();
            },
            TrailerAutoplayProfile.Exterminate => () =>
            {
                CivilizationAutoplayerPriorities.Military(autoplayer, mainController.BuildingController).TryStepOnce();
                // Assigner les FlowTargets vers les villes ennemies à portée pour déclencher les combats
                foreach (var city in civ.Cities)
                {
                    if (city.FlowTarget == null)
                    {
                        var enemy = mainController.MilitaryController.FindNearbyEnemyCity(city);
                        if (enemy != null) mainController.MilitaryController.SetCityFlow(city, enemy.Position);
                    }
                }
            },
            TrailerAutoplayProfile.PrestigePurchase => () =>
            {
                var ps = gameControllerService.CurrentGameState?.PrestigeState;
                if (ps == null) return;
                var cheapest = PrestigeMapController.DefaultMap.Vertices
                    .OrderBy(v => v.Cost)
                    .FirstOrDefault(v => mainController.PrestigeMapController.CanPurchaseVertex(ps, v.Coord));
                if (cheapest != null)
                    mainController.PrestigeMapController.PurchaseVertex(ps, cheapest.Coord);
            },
            TrailerAutoplayProfile.ResearchPurchase => () => autoplayer.TryResearchOnce(),
            _ => null
        };
    }

    /// <summary>
    /// Reconstruit la pile d'overlay UI complète (barre de ressources, onglets, panneaux) telle qu'utilisée
    /// à l'écran (cf. GameScreen.SetupRenderers), pour que les frames de la bande-annonce affichent le HUD
    /// du jeu et pas seulement l'île nue. Les popups (commerce, prestige, paramètres...) restent fermés par
    /// défaut et ne dessinent donc rien de plus que leur état "fermé" habituel.
    /// </summary>
    private static OverlayRenderer BuildOverlayRenderer(
        GameControllerService gameControllerService,
        LocalizationService localizationService,
        ResourceManager resourceManager,
        InputHandlingService inputService,
        UILayoutService uiLayoutService,
        TooltipRenderer tooltipRenderer,
        MonumentService monumentService)
    {
        var cityBuildingService = gameControllerService.CityBuildingService
            ?? throw new InvalidOperationException("CityBuildingService non initialisé après le chargement de la save.");
        var fileSystemService = new NullFileSystemService();
        var targetSelectionService = new TargetSelectionService();

        var selectedCityPanelRenderer = new SelectedCityPanelRenderer(cityBuildingService, localizationService, inputService, resourceManager)
        {
            LayoutService = uiLayoutService
        };
        var selectedMonumentPanelRenderer = new SelectedMonumentPanelRenderer(monumentService, inputService, localizationService, resourceManager, gameControllerService);

        var settingsPopupRenderer = new SettingsPopupRenderer(gameControllerService.MainGameController, localizationService, fileSystemService, uiLayoutService);
        var settingsMenu = new SettingsMenu(
            gameControllerService.MainGameController, inputService, localizationService,
            settingsPopupRenderer, fileSystemService, cityBuildingService,
            uiLayout: uiLayoutService);

        var playerResourcesOverlayRenderer = new PlayerResourcesOverlayRenderer(localizationService, resourceManager);
        playerResourcesOverlayRenderer.ConnectLowStock(null, gameControllerService.PlayerCivilization!);

        var tradeRenderer = new TradePopupRenderer(gameControllerService, localizationService, tooltipRenderer, resourceManager);
        var prestigeRenderer = new PrestigeRenderer(gameControllerService, localizationService, _ => { }, tooltipRenderer);
        var prestigeMapRenderer = new PrestigeMapRenderer(gameControllerService, localizationService, tooltipRenderer);
        var prestigeHistoryRenderer = new PrestigeHistoryRenderer(gameControllerService, localizationService);
        var timeControlRenderer = new TimeControlRenderer(gameControllerService, inputService, localizationService);
        var researchRenderer = new ResearchRenderer(gameControllerService, localizationService, inputService);
        var eventLogRenderer = new EventLogRenderer(gameControllerService, localizationService);
        var automationRenderer = new AutomationRenderer(gameControllerService, localizationService);
        var ritualsRenderer = new RitualsRenderer(gameControllerService, localizationService, tooltipRenderer, targetSelectionService);
        var ascensionRenderer = new AscensionRenderer(gameControllerService, localizationService, tooltipRenderer);

        var overlayRenderer = new OverlayRenderer(
            inputService, gameControllerService, localizationService,
            playerResourcesOverlayRenderer, settingsMenu, settingsPopupRenderer,
            selectedCityPanelRenderer, selectedMonumentPanelRenderer,
            tradeRenderer, prestigeRenderer, prestigeMapRenderer, prestigeHistoryRenderer,
            timeControlRenderer, researchRenderer, eventLogRenderer, automationRenderer,
            ritualsRenderer, ascensionRenderer, tooltipRenderer, uiLayoutService);

        overlayRenderer.ConnectTargetSelectionService(targetSelectionService);
        overlayRenderer.ConnectZoomCallbacks(() => { }, () => { });

        return overlayRenderer;
    }

    /// <summary>Aucune construction en cours dans une bande-annonce : aucun élément n'est jamais survolé/sélectionné.</summary>
    private sealed class EmptyConstructionHoverProvider : IConstructionHoverProvider
    {
        public ConstructionHoverState HoverState { get; } = new(
            Array.Empty<Vertex>(), Array.Empty<Edge>(), null, null, null, null, null, null, null);
    }

    private static int CaptureTitleCard(
        TrailerSequence sequence,
        TrailerDefinition definition,
        SKSize canvasSize,
        string framesDirectory,
        int startFrameIndex)
    {
        int totalFrames = definition.Fps * sequence.DurationSeconds;

        using var surface = SKSurface.Create(new SKImageInfo(
            (int)canvasSize.Width, (int)canvasSize.Height, SKColorType.Rgba8888, SKAlphaType.Premul));
        var canvas = surface.Canvas;

        for (int frame = 0; frame < totalFrames; frame++)
        {
            canvas.Clear(SKColors.Black);
            TitleCardRenderer.Draw(canvas, canvasSize);

            using var image = surface.Snapshot();
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            using var stream = File.Create(Path.Combine(framesDirectory, $"frame_{startFrameIndex + frame:D5}.png"));
            data.SaveTo(stream);
        }

        return totalFrames;
    }

    /// <summary>
    /// Injecte des points de prestige et/ou de recherche dans la save avant le début de la séquence
    /// (triche de présentation pour que les séquences Prestige/Research montrent un vrai achat).
    /// Si CheatResearchPoints > 0 et que la recherche n'est pas encore débloquée, achète d'abord CentralVertex.
    /// </summary>
    private static void ApplyCheats(TrailerSequence sequence, GameControllerService gameControllerService)
    {
        var ps = gameControllerService.CurrentGameState?.PrestigeState;
        if (ps == null) return;

        if (sequence.CheatPrestigePoints > 0)
            ps.PrestigePoints += sequence.CheatPrestigePoints;

        if (sequence.CheatResearchPoints > 0)
        {
            if (!ps.PurchasedVertices.Any(v => v.Equals(PrestigeMap.CentralVertex)))
            {
                ps.PrestigePoints += 10;
                gameControllerService.MainGameController.PrestigeMapController
                    .PurchaseVertex(ps, PrestigeMap.CentralVertex);
            }
            ps.TechnologyTree.ResearchPoints += sequence.CheatResearchPoints;
        }
    }

    private static void WithRetry(Action action, int maxAttempts = 10, int delayMs = 300, Action<IOException>? onRetry = null)
    {
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                action();
                return;
            }
            catch (IOException ex) when (attempt < maxAttempts)
            {
                onRetry?.Invoke(ex);
                Thread.Sleep(delayMs);
            }
        }
    }
}
