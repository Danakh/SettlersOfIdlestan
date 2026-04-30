using SkiaSharp.Views.Maui.Controls;
using SkiaSharp.Views.Maui;
using SettlersOfIdlestanSkia.Services;
using SkiaSharp;
using Microsoft.Maui.Controls;

namespace SettlersOfIdlestanDesktop;

public partial class MainPage : ContentPage
{
	private SkiaGameRuntime? _runtime;
	private string _lastBuildingsSignature = string.Empty;

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

		var selection = _runtime.GetCitySelectionInfo();
		MainThread.BeginInvokeOnMainThread(() =>
		{
			SelectedCityTypeLabel.Text = selection.CityType;
			RefreshBuildingsList(selection);
		});

		return true; // Continue la boucle
	}

	protected override void OnDisappearing()
	{
		base.OnDisappearing();

		_runtime?.Dispose();
		_runtime = null;
	}

	private void RefreshBuildingsList(CitySelectionInfo selection)
	{
		if (SelectedCityBuildingsList == null)
			return;

		var signature = selection.SelectedCityVertex == null
			? "none"
			: string.Join("|", selection.Buildings.Select(b => $"{b.BuildingType}:{b.IsBuilt}:{b.CanBuild}:{b.Level}"));
		if (signature == _lastBuildingsSignature)
			return;

		_lastBuildingsSignature = signature;
		SelectedCityBuildingsList.Children.Clear();

		if (selection.SelectedCityVertex == null || selection.Buildings.Count == 0)
		{
			SelectedCityBuildingsList.Children.Add(new Label
			{
				Text = "-",
				FontSize = 13,
				TextColor = Colors.White
			});
			return;
		}

		foreach (var building in selection.Buildings)
		{
			var row = new Grid
			{
				ColumnDefinitions =
				{
					new ColumnDefinition { Width = GridLength.Star },
					new ColumnDefinition { Width = GridLength.Auto }
				},
				ColumnSpacing = 8
			};

			var suffix = building.IsBuilt ? $" (Niv {building.Level})" : string.Empty;
			var label = new Label
			{
				Text = $"{building.BuildingType}{suffix}",
				FontSize = 13,
				TextColor = Colors.White,
				VerticalOptions = LayoutOptions.Center
			};

			var actionButton = new Button
			{
				Text = building.IsBuilt ? "Activer" : "Construire",
				Padding = new Thickness(10, 4),
				FontSize = 12,
				BackgroundColor = building.IsBuilt ? Color.FromArgb("#2E7D32") : Color.FromArgb("#1565C0"),
				TextColor = Colors.White,
				CornerRadius = 6,
				IsEnabled = building.IsBuilt || building.CanBuild,
				CommandParameter = building.BuildingType
			};
			actionButton.Clicked += OnBuildingActionClicked;

			row.Add(label, 0, 0);
			row.Add(actionButton, 1, 0);
			SelectedCityBuildingsList.Children.Add(row);
		}
	}

	private void OnBuildingActionClicked(object? sender, EventArgs e)
	{
		if (_runtime == null || sender is not Button button || button.CommandParameter is not string buildingType)
			return;

		var ok = _runtime.TryExecuteSelectedCityBuildingAction(buildingType);
		if (!ok)
		{
			StateLabel.Text = $"Action impossible: {buildingType}";
		}
	}
}
