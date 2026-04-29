using SkiaSharp.Views.Maui.Controls;
using SkiaSharp.Views.Maui;
using SettlersOfIdlestanSkia.Services;
using SkiaSharp;

namespace SettlersOfIdlestanDesktop;

public partial class MainPage : ContentPage
{
	private SkiaGameRuntime? _runtime;

	public MainPage()
	{
		InitializeComponent();
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();

		try
		{
			_runtime = new SkiaGameRuntime();
			_runtime.Initialize();

			StateLabel.Text = "Prêt";

			// Démarre la boucle de "tick" + invalide le canvas.
			// Le rendu exact est fait dans OnCanvasPaintSurface.
			MainThread.BeginInvokeOnMainThread(() => Dispatcher.StartTimer(TimeSpan.FromMilliseconds(16), RenderFrame));
		}
		catch (Exception ex)
		{
			StateLabel.Text = $"Erreur: {ex.Message}";
		}
	}

	private void OnCanvasPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
	{
		if (_runtime == null)
			return;

		try
		{
			var canvasSize = new SKSize(e.Surface.Canvas.DeviceClipBounds.Width, e.Surface.Canvas.DeviceClipBounds.Height);
			_runtime.EnsureCanvasInitialized(canvasSize);
			_runtime.Render(e.Surface.Canvas);
		}
		catch (Exception ex)
		{
			StateLabel.Text = $"Erreur rendu: {ex.Message}";
		}
	}

	private void OnCanvasTouch(object? sender, SKTouchEventArgs e)
	{
		if (_runtime == null)
			return;

		// Traduit les événements SkiaSharp en événements de jeu
		switch (e.ActionType)
		{
			case SKTouchAction.Pressed:
				_runtime.HandlePointerPressed(e.Location.X, e.Location.Y, (int)e.Id);
				break;

			case SKTouchAction.Moved:
				_runtime.HandlePointerMoved(e.Location.X, e.Location.Y, (int)e.Id);
				break;

			case SKTouchAction.Released:
			case SKTouchAction.Cancelled:
				_runtime.HandlePointerReleased(e.Location.X, e.Location.Y, (int)e.Id);
				break;
		}

		e.Handled = true;
	}

	private bool RenderFrame()
	{
		if (GameCanvas == null || _runtime == null)
			return false;

		_runtime.Tick();

		// Force le redraw du canvas
		GameCanvas.InvalidateSurface();

		if (_runtime.TryGetDebugStats(out var stats))
		{
			MainThread.BeginInvokeOnMainThread(() =>
			{
				FpsLabel.Text = $"FPS: {stats.fps:F1}";
				CameraLabel.Text =
					$"Camera: {stats.cameraX:F1}, {stats.cameraY:F1} | Villes: {stats.cityCount}, Routes: {stats.roadCount}";
			});
		}

		return true; // Continue la boucle
	}

	protected override void OnDisappearing()
	{
		base.OnDisappearing();

		_runtime?.Dispose();
		_runtime = null;
	}
}
