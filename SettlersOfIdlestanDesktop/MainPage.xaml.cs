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
	private bool _allowDebugMode;
	private readonly Dictionary<long, SKPoint> _activePointers = [];
	private float _lastPinchDist;
	private SKPoint _lastPinchCenter;
	private bool _isPinching;

	// Zoom propre : ne recalculer la distance que quand les DEUX doigts ont bougé depuis le
	// dernier snapshot, afin d'éviter les faux zooms intermédiaires (un doigt à jour, l'autre non).
	private readonly Dictionary<long, SKPoint> _pinchSnapshot = [];
	private long _pinchLastMovedId = -1L;
	private int _pinchSameIdStreak;
	private const int PinchSameIdStreakThreshold = 3;

	public MainPage()
	{
		InitializeComponent();
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		_runtime = new SkiaGameRuntime();
		_runtime.QuitRequested         += () => MainThread.BeginInvokeOnMainThread(() => Application.Current?.Quit());
		_runtime.DiscordLinkClicked    += url => MainThread.BeginInvokeOnMainThread(async () =>
			await Launcher.OpenAsync(new Uri(url)));
		_runtime.FullscreenStateChanged += fullscreen => MainThread.BeginInvokeOnMainThread(() => ApplyFullscreen(fullscreen));
		bool allowDebugMode = false;
#if DEBUG
		allowDebugMode = Environment.GetCommandLineArgs().Contains("--debug");
#endif
		_allowDebugMode = allowDebugMode;
		bool demoMode = false;
		_runtime.Initialize(new DesktopFileSystemService(), allowDebugMode, demoMode);
		ApplyScreenBasedUiScale();
		MainThread.BeginInvokeOnMainThread(() => Dispatcher.StartTimer(TimeSpan.FromMilliseconds(16), RenderFrame));
	}

	// Échelle UI par défaut sur les grands écrans : DeviceDisplay donne la taille physique
	// de l'écran (et non celle de la fenêtre/zone d'affichage), ce qui permet de détecter
	// les résolutions élevées même si l'app démarre dans une petite fenêtre.
	private void ApplyScreenBasedUiScale()
	{
		try
		{
			var display = Microsoft.Maui.Devices.DeviceDisplay.Current.MainDisplayInfo;
			if (display.Width > 1920)
			{
				float scale = Math.Clamp((float)(display.Width / 1920.0), 1f, 2f);
				_runtime?.SetUiScale(scale);
			}
		}
		catch
		{
			// DeviceDisplay indisponible sur cette plateforme — pas d'échelle automatique.
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
				if (_runtime?.IsFullscreenEnabled == true) ApplyFullscreen(true);
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
			if (_runtime?.IsFullscreenEnabled == true) ApplyFullscreen(true);
		}
	}

	private static string? MapVirtualKey(Windows.System.VirtualKey vk) => vk switch
	{
		Windows.System.VirtualKey.I => "I",
		Windows.System.VirtualKey.R => "R",
		Windows.System.VirtualKey.P => "P",
		Windows.System.VirtualKey.S => "S",
		Windows.System.VirtualKey.C => "C",
		Windows.System.VirtualKey.Escape => "Escape",
		Windows.System.VirtualKey.Left => "ArrowLeft",
		Windows.System.VirtualKey.Right => "ArrowRight",
		Windows.System.VirtualKey.F9 => "F9",
		Windows.System.VirtualKey.F10 => "F10",
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
			if (key == "F10" && _allowDebugMode) ResizeWindowToIconPreview();
			e.Handled = key != "Control" && key != "Shift";
		}
	}

	/// <summary>Commande de debug (F10) : redimensionne la fenêtre à 256x256 pour prévisualiser le cadrage avant un export d'icône (F9).</summary>
	private static void ResizeWindowToIconPreview()
	{
#if WINDOWS
		var windows = Microsoft.Maui.Controls.Application.Current?.Windows;
		if (windows == null || windows.Count == 0) return;
		if (windows[0].Handler?.PlatformView is not Microsoft.Maui.MauiWinUIWindow winUIWindow) return;

		var handle    = WinRT.Interop.WindowNative.GetWindowHandle(winUIWindow);
		var windowId  = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(handle);
		var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);

		appWindow.ResizeClient(new Windows.Graphics.SizeInt32 { Width = 256, Height = 256 });
#endif
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
			_runtime?.NotifyError(ex);
		}
	}

	private void OnCanvasTouch(object? sender, SKTouchEventArgs e)
	{
		if (_runtime == null)
			return;

		switch (e.ActionType)
		{
			case SKTouchAction.Pressed:
				_activePointers[e.Id] = e.Location;
				if (_activePointers.Count == 2)
				{
					_isPinching = true;
					_lastPinchDist = GetPinchDistance();
					_lastPinchCenter = GetPinchCenter();
					_pinchSnapshot.Clear();
					foreach (var kvp in _activePointers) _pinchSnapshot[kvp.Key] = kvp.Value;
					_pinchLastMovedId = -1L;
					_pinchSameIdStreak = 0;
					// Annule le pan en cours au moment où le deuxième doigt touche
					_runtime.HandlePointerReleased(e.Location.X, e.Location.Y, (int)e.Id, ToPointerButton(e.MouseButton));
				}
				else if (!_isPinching)
				{
					_runtime.HandlePointerPressed(e.Location.X, e.Location.Y, (int)e.Id, ToPointerButton(e.MouseButton));
				}
				break;

			case SKTouchAction.Moved:
				_activePointers[e.Id] = e.Location;
				if (_isPinching && _activePointers.Count >= 2)
				{
					var newDist   = GetPinchDistance();
					var newCenter = GetPinchCenter();

					float panDx = newCenter.X - _lastPinchCenter.X;
					float panDy = newCenter.Y - _lastPinchCenter.Y;

					// Ne recalculer le zoom que quand on a vu les deux doigts depuis le dernier
					// snapshot (alternance A→B), ou après N events du même doigt (l'autre est fixe).
					if (e.Id == _pinchLastMovedId) _pinchSameIdStreak++;
					else                            _pinchSameIdStreak = 0;
					_pinchLastMovedId = e.Id;

					bool differentFinger = _pinchSameIdStreak == 0 && _pinchSnapshot.Count >= 2;
					bool singleFingerTimeout = _pinchSameIdStreak >= PinchSameIdStreakThreshold;

					float scaleRatio = 1f;
					if (differentFinger || singleFingerTimeout)
					{
						float snapDist = GetDistanceFrom(_pinchSnapshot);
						if (snapDist > 0f && newDist > 0f)
							scaleRatio = newDist / snapDist;
						foreach (var kvp in _activePointers) _pinchSnapshot[kvp.Key] = kvp.Value;
						_pinchSameIdStreak = 0;
					}

					_runtime.HandlePinch(scaleRatio, newCenter.X, newCenter.Y, panDx, panDy);
					_lastPinchDist   = newDist;
					_lastPinchCenter = newCenter;
				}
				else if (!_isPinching)
				{
					_runtime.HandlePointerMoved(e.Location.X, e.Location.Y, (int)e.Id);
				}
				break;

			case SKTouchAction.Released:
			case SKTouchAction.Cancelled:
				_activePointers.Remove(e.Id);
				_pinchSnapshot.Remove(e.Id);
				if (_activePointers.Count < 2)
				{
					_isPinching = false;
					_lastPinchDist = 0f;
					_pinchLastMovedId = -1L;
					_pinchSameIdStreak = 0;
				}
				if (!_isPinching)
				{
					_runtime.HandlePointerReleased(e.Location.X, e.Location.Y, (int)e.Id, ToPointerButton(e.MouseButton));
				}
				break;

			case SKTouchAction.WheelChanged:
				_runtime.HandleZoom(e.WheelDelta, e.Location.X, e.Location.Y);
				break;
		}

		e.Handled = true;
	}

	private float GetPinchDistance() => GetDistanceFrom(_activePointers);

	private static float GetDistanceFrom(Dictionary<long, SKPoint> pts)
	{
		var arr = pts.Values.ToArray();
		if (arr.Length < 2) return 0f;
		var dx = arr[0].X - arr[1].X;
		var dy = arr[0].Y - arr[1].Y;
		return MathF.Sqrt(dx * dx + dy * dy);
	}

	private SKPoint GetPinchCenter()
	{
		var pts = _activePointers.Values.ToArray();
		if (pts.Length < 2) return SKPoint.Empty;
		return new SKPoint((pts[0].X + pts[1].X) / 2f, (pts[0].Y + pts[1].Y) / 2f);
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
		GameCanvas.InvalidateSurface();
		return true;
	}

	private static void ApplyFullscreen(bool fullscreen)
	{
#if WINDOWS
		var windows = Microsoft.Maui.Controls.Application.Current?.Windows;
		if (windows == null || windows.Count == 0) return;
		if (windows[0].Handler?.PlatformView is not Microsoft.Maui.MauiWinUIWindow winUIWindow) return;

		var handle    = WinRT.Interop.WindowNative.GetWindowHandle(winUIWindow);
		var windowId  = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(handle);
		var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);

		if (fullscreen)
		{
			// Lire la hauteur de la titlebar AVANT le changement de presenter (après, elle vaut 0).
			double titleBarHeight = appWindow.TitleBar?.Height ?? 0;

			appWindow.SetPresenter(Microsoft.UI.Windowing.AppWindowPresenterKind.FullScreen);

			// MAUI laisse un padding haut (= titleBarHeight) dans WindowRootView même en plein écran.
			// On le compense avec une marge négative sur le contenu racine de la fenêtre.
			if (winUIWindow.Content is Microsoft.UI.Xaml.FrameworkElement root && titleBarHeight > 0)
				root.Margin = new Microsoft.UI.Xaml.Thickness(0, -titleBarHeight, 0, 0);
		}
		else
		{
			appWindow.SetPresenter(Microsoft.UI.Windowing.AppWindowPresenterKind.Default);

			// Rétablir la marge normale — MAUI va restaurer l'inset titlebar via AppWindow.Changed.
			if (winUIWindow.Content is Microsoft.UI.Xaml.FrameworkElement root)
				root.Margin = new Microsoft.UI.Xaml.Thickness(0);
		}
#endif
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
