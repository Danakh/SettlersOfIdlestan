using System.Text.Json;
using System.Threading;
using SkiaSharp;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Renderers.Island;
using SettlersOfIdlestanSkia.Renderers.Overlay;
using SettlersOfIdlestanSkia.Renderers.Overlay.Panels;
using SettlersOfIdlestanSkia.Renderers.Overlay.Popup;
using SettlersOfIdlestanSkia.Renderers.Overlay.Tabs;
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
            onLog?.Invoke($"Séquence {i + 1}/{definition.Sequences.Count} : {sequence.Save} ({sequence.DurationSeconds}s)");

            var savePath = Path.Combine(savesDirectory, sequence.Save);
            int framesWritten = CaptureSequence(savePath, sequence, definition, canvasSize, framesDirectory, frameCursor, onLog);
            frameCursor += framesWritten;
        }

        string ffmpegCommand =
            $"ffmpeg -framerate {definition.Fps} -i \"{Path.Combine(framesDirectory, "frame_%05d.png")}\" " +
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

        var resourceManager = new ResourceManager();
        var localizationService = new LocalizationService();
        var inputService = new InputHandlingService();
        var uiLayoutService = new UILayoutService();
        uiLayoutService.UpdateCanvasSize(canvasSize);

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

        var camera = BuildCamera(sequence.Camera, gameControllerService, canvasSize);

        var videoExportController = new VideoExportController(gameControllerService, sceneRenderer);
        int totalFrames = definition.Fps * sequence.DurationSeconds;
        videoExportController.CaptureSequence(
            framesDirectory,
            definition.Width,
            definition.Height,
            definition.Fps,
            sequence.DurationSeconds,
            camera.Position,
            camera.ZoomLevel,
            sequence.SimulationSpeedMultiplier,
            onFrameCaptured: (frame, total) => onLog?.Invoke($"  frame {frame}/{total}"),
            startFrameIndex: startFrameIndex,
            writeFfmpegCommand: false);

        sceneRenderer.Dispose();
        resourceManager.Dispose();

        return totalFrames;
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
        var selectedMonumentPanelRenderer = new SelectedMonumentPanelRenderer(monumentService, inputService, localizationService, resourceManager);

        var settingsPopupRenderer = new SettingsPopupRenderer(gameControllerService.MainGameController, localizationService, fileSystemService);
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

    private static CameraService BuildCamera(TrailerCamera config, GameControllerService gameControllerService, SKSize canvasSize)
    {
        var camera = new CameraService();
        camera.Initialize(canvasSize);

        if (config.Mode == TrailerCameraMode.Fixed)
        {
            camera.SetZoom(config.Zoom, keepCenteredOnScreen: false);
            camera.CenterOn(config.X, config.Y);
            return camera;
        }

        var hexCoords = gameControllerService.CurrentWorldState?.CurrentViewedMap.Tiles.Keys
            ?? Enumerable.Empty<HexCoord>();
        camera.FitMapToView(hexCoords);
        return camera;
    }

    /// <summary>Aucune construction en cours dans une bande-annonce : aucun élément n'est jamais survolé/sélectionné.</summary>
    private sealed class EmptyConstructionHoverProvider : IConstructionHoverProvider
    {
        public ConstructionHoverState HoverState { get; } = new(
            Array.Empty<Vertex>(), Array.Empty<Edge>(), null, null, null, null, null, null, null);
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
