using System.Diagnostics;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Common.Input;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using SettlersOfIdlestan.Controller.Store;
using SettlersOfIdlestanOpenTK.Services;
using SettlersOfIdlestanOpenTK.Services.Store;
using SettlersOfIdlestanSkia.Services;
using SkiaSharp;

namespace SettlersOfIdlestanOpenTK;

sealed class SoiGameWindow : GameWindow
{
    private readonly SkiaGameRuntime _runtime = new();
    private StoreController?         _storeController;
    private GRContext?               _grContext;
    private GRBackendRenderTarget?   _renderTarget;
    private SKSurface?               _surface;

    public SoiGameWindow() : base(
        new GameWindowSettings { UpdateFrequency = 0 },
        new NativeWindowSettings
        {
            Title       = "Settlers of Idlestan",
            ClientSize  = new Vector2i(1280, 720),
            StencilBits = 8,
        })
    {
    }

    protected override void OnLoad()
    {
        base.OnLoad();

        Icon = LoadIcon();
        VSync = VSyncMode.On;

        _runtime.QuitRequested          += Close;
        _runtime.DiscordLinkClicked     += OpenUrl;
        _runtime.FullscreenStateChanged += ApplyFullscreen;

        var args = Environment.GetCommandLineArgs();
        bool allowDebug = args.Contains("--debug");
        bool demoMode   = true;

        _storeController = new StoreController([new StoreServiceSteam()]);
        _runtime.Initialize(new DesktopFileSystemService(), allowDebug, demoMode, _storeController);

        var glInterface = GRGlInterface.Create();
        _grContext = GRContext.CreateGl(glInterface);
        RecreateRenderTarget(ClientSize.X, ClientSize.Y);
        _runtime.EnsureCanvasInitialized(new SKSize(ClientSize.X, ClientSize.Y));
    }

    protected override void OnResize(ResizeEventArgs e)
    {
        base.OnResize(e);
        GL.Viewport(0, 0, e.Width, e.Height);
        RecreateRenderTarget(e.Width, e.Height);
        _runtime.EnsureCanvasInitialized(new SKSize(e.Width, e.Height));
    }

    private void RecreateRenderTarget(int width, int height)
    {
        _surface?.Dispose();
        _renderTarget?.Dispose();

        GL.GetInteger(GetPName.FramebufferBinding, out int fb);
        // GL_RGBA8 = 0x8058
        var fbInfo = new GRGlFramebufferInfo((uint)fb, 0x8058u);
        _renderTarget = new GRBackendRenderTarget(width, height, 0, 8, fbInfo);
        _surface = SKSurface.Create(_grContext!, _renderTarget, GRSurfaceOrigin.BottomLeft, SKColorType.Rgba8888);
    }

    protected override void OnRenderFrame(FrameEventArgs args)
    {
        base.OnRenderFrame(args);
        if (_surface == null || _grContext == null) return;

        _runtime.Tick();
        _surface.Canvas.Clear(SKColors.Black);
        _runtime.Render(_surface.Canvas);
        _grContext.Flush();
        SwapBuffers();
    }

    // ── Input ─────────────────────────────────────────────────────────────────

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);
        _runtime.HandlePointerPressed(MouseState.X, MouseState.Y, 0, MapButton(e.Button));
    }

    protected override void OnMouseMove(MouseMoveEventArgs e)
    {
        base.OnMouseMove(e);
        _runtime.HandlePointerMoved(e.X, e.Y, 0);
    }

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        base.OnMouseUp(e);
        _runtime.HandlePointerReleased(MouseState.X, MouseState.Y, 0, MapButton(e.Button));
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        // OffsetY est en "lignes" (≈1 par cran de molette) ; le runtime attend l'équivalent WheelDelta (120/cran)
        _runtime.HandleZoom(e.OffsetY * 120f, MouseState.X, MouseState.Y);
    }

    protected override void OnKeyDown(KeyboardKeyEventArgs e)
    {
        base.OnKeyDown(e);
        var key = MapKey(e.Key);
        if (key != null) _runtime.HandleKeyPressed(key);
    }

    protected override void OnKeyUp(KeyboardKeyEventArgs e)
    {
        base.OnKeyUp(e);
        var key = MapKey(e.Key);
        if (key != null) _runtime.HandleKeyReleased(key);
    }

    // ── Fullscreen ────────────────────────────────────────────────────────────

    private void ApplyFullscreen(bool fullscreen) =>
        WindowState = fullscreen ? WindowState.Fullscreen : WindowState.Normal;

    // ── Cleanup ───────────────────────────────────────────────────────────────

    protected override void OnUnload()
    {
        base.OnUnload();
        _surface?.Dispose();
        _renderTarget?.Dispose();
        _grContext?.Dispose();
        _runtime.Dispose();
        _storeController?.Dispose();
    }

    // ── Icon ──────────────────────────────────────────────────────────────────

    private static WindowIcon LoadIcon()
    {
        using var stream = typeof(SoiGameWindow).Assembly
            .GetManifestResourceStream("SettlersOfIdlestanOpenTK.Resources.appicon.png")!;
        using var bmp = SKBitmap.Decode(stream).Copy(SKColorType.Rgba8888);
        var pixels = new byte[bmp.Width * bmp.Height * 4];
        System.Runtime.InteropServices.Marshal.Copy(bmp.GetPixels(), pixels, 0, pixels.Length);
        return new WindowIcon(new OpenTK.Windowing.Common.Input.Image(bmp.Width, bmp.Height, pixels));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void OpenUrl(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch { }
    }

    private static PointerButton MapButton(MouseButton btn) => btn switch
    {
        MouseButton.Left   => PointerButton.Left,
        MouseButton.Middle => PointerButton.Middle,
        MouseButton.Right  => PointerButton.Right,
        _                  => PointerButton.Unknown,
    };

    private static string? MapKey(Keys key) => key switch
    {
        Keys.I                                    => "I",
        Keys.R                                    => "R",
        Keys.P                                    => "P",
        Keys.S                                    => "S",
        Keys.C                                    => "C",
        Keys.Escape                               => "Escape",
        Keys.LeftControl or Keys.RightControl     => "Control",
        Keys.LeftShift   or Keys.RightShift       => "Shift",
        _                                         => null,
    };
}
