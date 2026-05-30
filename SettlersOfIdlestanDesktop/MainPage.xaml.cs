using Microsoft.Maui.Controls;
using SettlersOfIdlestanDesktop.Services;
using SettlersOfIdlestanSkia.Services;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

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
			_runtime.Initialize(new DesktopFileSystemService(), allowDebugMode: true);
			StateLabel.Text = "Prêt";
			MainThread.BeginInvokeOnMainThread(() => Dispatcher.StartTimer(TimeSpan.FromMilliseconds(16), RenderFrame));
		}
		catch (Exception ex)
		{
			StateLabel.Text = $"Erreur: {ex.Message}";
		}
	}

#if WINDOWS
	private bool _keyboardSubscribed;
	private Microsoft.UI.Xaml.UIElement? _keyboardSource;

	protected override void OnHandlerChanged()
	{
		base.OnHandlerChanged();
		// Defer to next pump to ensure the native window is fully wired up
		if (!_keyboardSubscribed)
			MainThread.BeginInvokeOnMainThread(SubscribeToKeyboard);
	}

	private void SubscribeToKeyboard()
	{
		if (_keyboardSubscribed) return;

		// Iterate MAUI windows and look for the MauiWinUIWindow (WinUI3 native window)
		foreach (var mauiWindow in Microsoft.Maui.Controls.Application.Current?.Windows
		         ?? Enumerable.Empty<Microsoft.Maui.Controls.Window>())
		{
			if (mauiWindow.Handler?.PlatformView is Microsoft.Maui.MauiWinUIWindow nativeWindow &&
			    nativeWindow.Content is Microsoft.UI.Xaml.UIElement root)
			{
				root.PreviewKeyDown += OnPlatformKeyDown;
				root.PreviewKeyUp += OnPlatformKeyUp;
				_keyboardSource = root;
				_keyboardSubscribed = true;
				return;
			}
		}

		// Fallback: hook on the page's own content panel (fires when focus is within it)
		if (Handler?.PlatformView is Microsoft.UI.Xaml.UIElement pageRoot)
		{
			pageRoot.PreviewKeyDown += OnPlatformKeyDown;
			pageRoot.PreviewKeyUp += OnPlatformKeyUp;
			_keyboardSource = pageRoot;
			_keyboardSubscribed = true;
		}
	}

	private static string? MapVirtualKey(Windows.System.VirtualKey vk) => vk switch
	{
		Windows.System.VirtualKey.I => "I",
		Windows.System.VirtualKey.R => "R",
		Windows.System.VirtualKey.P => "P",
		Windows.System.VirtualKey.S => "S",
		Windows.System.VirtualKey.C => "C",
		Windows.System.VirtualKey.Control or
		Windows.System.VirtualKey.LeftControl or
		Windows.System.VirtualKey.RightControl => "Control",
		Windows.System.VirtualKey.Shift or
		Windows.System.VirtualKey.LeftShift or
		Windows.System.VirtualKey.RightShift => "Shift",
		_ => null
	};

	private void OnPlatformKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
	{
		if (_runtime == null) return;
		var key = MapVirtualKey(e.Key);
		if (key != null)
		{
			_runtime.HandleKeyPressed(key);
			e.Handled = key != "Control" && key != "Shift";
		}
	}

	private void OnPlatformKeyUp(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
	{
		if (_runtime == null) return;
		var key = MapVirtualKey(e.Key);
		if (key != null)
			_runtime.HandleKeyReleased(key);
	}
#endif

	private void OnCanvasPaintSurface(object? sender, SKPaintGLSurfaceEventArgs e)
	{
		if (_runtime == null)
			return;

		try
		{
			var canvasSize = new SKSize(e.BackendRenderTarget.Width, e.BackendRenderTarget.Height);
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
				_runtime.HandlePointerPressed(e.Location.X, e.Location.Y, (int)e.Id, ToPointerButton(e.MouseButton));
				break;

			case SKTouchAction.Moved:
				_runtime.HandlePointerMoved(e.Location.X, e.Location.Y, (int)e.Id);
				break;

			case SKTouchAction.Released:
			case SKTouchAction.Cancelled:
				_runtime.HandlePointerReleased(e.Location.X, e.Location.Y, (int)e.Id, ToPointerButton(e.MouseButton));
				break;

			case SKTouchAction.WheelChanged:
				_runtime.HandleZoom(e.WheelDelta, e.Location.X, e.Location.Y);
				break;
		}

		e.Handled = true;
	}

	private static PointerButton ToPointerButton(SKMouseButton mouseButton)
	{
		return mouseButton switch
		{
			SKMouseButton.Left => PointerButton.Left,
			SKMouseButton.Middle => PointerButton.Middle,
			SKMouseButton.Right => PointerButton.Right,
			_ => PointerButton.Unknown
		};
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

#if WINDOWS
		if (_keyboardSubscribed && _keyboardSource != null)
		{
			_keyboardSource.PreviewKeyDown -= OnPlatformKeyDown;
			_keyboardSource.PreviewKeyUp -= OnPlatformKeyUp;
			_keyboardSource = null;
			_keyboardSubscribed = false;
		}
#endif

		_runtime?.Dispose();
		_runtime = null;
	}
}
