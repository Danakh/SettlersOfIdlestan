using SkiaSharp.Views.Maui.Controls;
using SkiaSharp.Views.Maui;
using SettlersOfIdlestanSkia.Services;
using SettlersOfIdlestanSkia.Renderers;
using SkiaSharp;

namespace SettlersOfIdlestanDesktop;

public partial class MainPage : ContentPage
{
	private RenderService? _renderService;
	private InputHandlingService? _inputService;
	private ResourceManager? _resourceManager;
	private GameControllerService? _gameControllerService;
	private CameraService? _cameraService;
	private HarvestService? _harvestService;
	private HexClickService? _hexClickService;
	private int _frameCount;
	private DateTime _lastFpsUpdate = DateTime.UtcNow;
	private bool _isInitialized = false;

	public MainPage()
	{
		InitializeComponent();
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		
		// Initialise les services (sans caméra pour l'instant)
		await InitializeGameServices();
	}

	private async Task InitializeGameServices()
	{
		try
		{
			// Crée les services (sauf la caméra qui attend les vraies dimensions du canvas)
			_resourceManager = new ResourceManager();
			_inputService = new InputHandlingService();
			_renderService = new RenderService();
			_gameControllerService = new GameControllerService();
			_cameraService = new CameraService();
			_harvestService = new HarvestService(_gameControllerService);

			// Initialise un nouveau jeu via le controller
			var gameState = _gameControllerService.InitializeNewGame();
			if (gameState == null)
				throw new InvalidOperationException("Impossible de créer le jeu.");

			// Enregistre les renderers dans l'ordre (de bas en haut):
			// 1. Plateau de jeu (hexagones et terrain)
			var gameboardRenderer = new GameBoardRenderer();
			_renderService.RegisterRenderer(gameboardRenderer);
			// 2. Routes
			_renderService.RegisterRenderer(new RoadRenderer());
			// 3. Villes
			_renderService.RegisterRenderer(new CityRenderer());
			// 4. Overlay des ressources du joueur (au-dessus de tout)
			_renderService.RegisterRenderer(new PlayerResourcesOverlayRenderer());
			// 5. Overlay debug (tout en haut)
			_renderService.RegisterRenderer(new DebugOverlayRenderer(_inputService, _cameraService, gameboardRenderer));
			// TODO: Ajouter d'autres renderers (UI, animations, etc.)

			// Crée le service de détection des clics sur hexagones
			// On utilise le GameBoardRenderer du RenderService
			if (gameboardRenderer == null)
				throw new InvalidOperationException("GameBoardRenderer non trouvé");
			_hexClickService = new HexClickService(_gameControllerService, _harvestService, _inputService, _cameraService, gameboardRenderer);

			StateLabel.Text = "Prêt";
			
			// Démarre la boucle de rendu - elle finira d'initialiser la caméra au premier frame
			MainThread.BeginInvokeOnMainThread(() => Dispatcher.StartTimer(TimeSpan.FromMilliseconds(16), RenderFrame));
		}
		catch (Exception ex)
		{
			StateLabel.Text = $"Erreur: {ex.Message}";
		}
	}

	private void OnCanvasPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
	{
		if (_renderService == null || _gameControllerService?.CurrentGameState == null || _cameraService == null)
			return;

		try
		{
			// PREMIER APPEL: Initialise la caméra avec les vraies dimensions du canvas
			if (!_isInitialized)
			{
				var canvasSize = new SKSize(e.Surface.Canvas.DeviceClipBounds.Width, e.Surface.Canvas.DeviceClipBounds.Height);
				_cameraService.Initialize(canvasSize);
				
				// Fit la caméra sur toute la carte
				var gameState = _gameControllerService.CurrentGameState;
				var hexCoords = gameState?.CurrentIslandState?.Map.Tiles.Keys ?? new List<SettlersOfIdlestan.Model.HexGrid.HexCoord>();
				_cameraService.FitMapToView(hexCoords);
				
				_renderService.Initialize(canvasSize);
				
				_isInitialized = true;
			}

			// Affiche des infos de débogage
			var gameState2 = _gameControllerService.CurrentGameState;
			var cityCount = gameState2?.CurrentIslandState?.Civilizations.FirstOrDefault()?.Cities.Count ?? 0;
			var roadCount = gameState2?.CurrentIslandState?.Civilizations.FirstOrDefault()?.Roads.Count ?? 0;
			MainThread.BeginInvokeOnMainThread(() => 
			{
				CameraLabel.Text = $"Villes: {cityCount}, Routes: {roadCount}";
			});

			_renderService.RenderFrame(e.Surface.Canvas, gameState2, _cameraService);
		}
		catch (Exception ex)
		{
			StateLabel.Text = $"Erreur rendu: {ex.Message}";
		}
	}

	private void OnCanvasTouch(object? sender, SKTouchEventArgs e)
	{
		if (_inputService == null)
			return;

		// Traduit les événements SkiaSharp en événements de jeu
		switch (e.ActionType)
		{
			case SKTouchAction.Pressed:
				_inputService.HandlePointerPressed(e.Location.X, e.Location.Y, (int)e.Id);
				break;

			case SKTouchAction.Moved:
				_inputService.HandlePointerMoved(e.Location.X, e.Location.Y, (int)e.Id);
				break;

			case SKTouchAction.Released:
			case SKTouchAction.Cancelled:
				_inputService.HandlePointerReleased(e.Location.X, e.Location.Y, (int)e.Id);
				break;
		}

		e.Handled = true;
	}

	private bool RenderFrame()
	{
		if (GameCanvas == null || _gameControllerService == null || _cameraService == null)
			return false;

		// Met à jour l'état du jeu
		_gameControllerService.Update(0.016f); // ~60 FPS

		// Force le redraw du canvas
		GameCanvas.InvalidateSurface();

		// Met à jour le FPS et la position de la caméra tous les 500ms
		_frameCount++;
		var now = DateTime.UtcNow;
		var elapsed = now - _lastFpsUpdate;
		
		if (elapsed.TotalMilliseconds >= 500)
		{
			var fps = _frameCount / elapsed.TotalSeconds;
			var camPos = _cameraService.Position;
			MainThread.BeginInvokeOnMainThread(() => 
			{
				FpsLabel.Text = $"FPS: {fps:F1}";
				CameraLabel.Text = $"Camera: {camPos.X:F1}, {camPos.Y:F1}";
			});
			
			_frameCount = 0;
			_lastFpsUpdate = now;
		}

		return true; // Continue la boucle
	}

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		
		_hexClickService?.Cleanup();
		_renderService?.Dispose();
		_resourceManager?.Dispose();
	}
}
