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
	private object? _gameState;
	private int _frameCount;
	private DateTime _lastFpsUpdate = DateTime.UtcNow;

	public MainPage()
	{
		InitializeComponent();
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		
		// Initialise les services
		await InitializeGameServices();
	}

	private async Task InitializeGameServices()
	{
		try
		{
			// Crée les services
			_resourceManager = new ResourceManager();
			_inputService = new InputHandlingService();
			_renderService = new RenderService();

			// Enregistre les renderers (ordre = z-order)
			_renderService.RegisterRenderer(new GameBoardRenderer());
			// TODO: Ajouter d'autres renderers (UI, animations, etc.)

			// Initialise avec les dimensions du canvas
			var canvasSize = new SKSize((float)GameCanvas.Width, (float)GameCanvas.Height);
			_renderService.Initialize(canvasSize);

			// Crée un état de jeu factice pour la démo
			_gameState = new object(); // À remplacer par le vrai GameState

			StateLabel.Text = "Prêt";
			
			// Démarre la boucle de rendu
			MainThread.BeginInvokeOnMainThread(() => Dispatcher.StartTimer(TimeSpan.FromMilliseconds(16), RenderFrame));
		}
		catch (Exception ex)
		{
			StateLabel.Text = $"Erreur: {ex.Message}";
		}
	}

	private void OnCanvasPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
	{
		if (_renderService == null || _gameState == null)
			return;

		try
		{
			_renderService.RenderFrame(e.Surface.Canvas, _gameState);
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
		if (GameCanvas == null)
			return false;

		// Force le redraw du canvas
		GameCanvas.InvalidateSurface();

		// Met à jour le FPS tous les 500ms
		_frameCount++;
		var now = DateTime.UtcNow;
		var elapsed = now - _lastFpsUpdate;
		
		if (elapsed.TotalMilliseconds >= 500)
		{
			var fps = _frameCount / elapsed.TotalSeconds;
			MainThread.BeginInvokeOnMainThread(() => FpsLabel.Text = $"FPS: {fps:F1}");
			
			_frameCount = 0;
			_lastFpsUpdate = now;
		}

		return true; // Continue la boucle
	}

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		
		_renderService?.Dispose();
		_resourceManager?.Dispose();
	}
}
