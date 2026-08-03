using Avalonia;
using Avalonia.Input;
using Avalonia.Threading;
using SkiaSharp;

// La couche Skia definit ses propres PointerEventArgs/KeyEventArgs, homonymes de ceux
// d'Avalonia.Input. On l'importe uniquement sous alias — un using classique rendrait
// toutes les signatures d'override ambigues.
using SkiaInput = SettlersOfIdlestanSkia.Services;

namespace SettlersOfIdlestanUI.Controls;

/// <summary>
/// Hote Avalonia du <see cref="SkiaInput.SkiaGameRuntime"/>.
///
/// Etape intermediaire assumee de la migration : ce controle porte encore l'integralite du
/// runtime Skia (carte ET overlay legacy), ce qui rend le jeu jouable sous Avalonia des la
/// phase 2. Les phases suivantes extraient l'overlay vers de vrais controles Avalonia poses
/// au-dessus ; ce controle se reduira alors a la seule carte hex.
///
/// Tant que l'overlay legacy vit ici, l'arbitrage carte/UI reste celui d'avant : les clics
/// sont tous transmis au runtime. Le gain sur les clics arrive au fur et a mesure que chaque
/// panneau sort d'ici.
/// </summary>
public class GameRuntimeControl : SkiaCanvasControl
{
    private readonly SkiaInput.SkiaGameRuntime _runtime;
    private IDisposable? _loop;
    private SKSize _lastCanvasSize;

    /// Cadence de la boucle de jeu. Avalonia 12 n'expose pas de RequestAnimationFrame :
    /// on pilote le Tick + l'invalidation avec un timer a priorite Render.
    private static readonly TimeSpan FrameInterval = TimeSpan.FromMilliseconds(16);

    public GameRuntimeControl(SkiaInput.SkiaGameRuntime runtime)
    {
        _runtime = runtime;
        Focusable = true;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        // L'echelle UI automatique reste a 1 : Avalonia exprime deja tout en unites logiques et
        // applique le facteur DPI lui-meme. Reporter RenderScaling dans AutoUiScale appliquerait
        // le zoom une seconde fois et doublerait la taille de l'UI sur les ecrans HiDPI.
        _runtime.SetUiScale(1f);

        _loop = DispatcherTimer.Run(OnFrame, FrameInterval, DispatcherPriority.Render);
        Focus();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _loop?.Dispose();
        _loop = null;
    }

    private bool OnFrame()
    {
        // Le Tick tourne meme quand rien n'est dessine (fenetre masquee) pour que le temps de
        // jeu ne derive pas — meme contrat que la boucle OpenTK d'origine.
        _runtime.Tick();
        InvalidateVisual();
        return true;
    }

    protected override void OnRenderSkia(SKCanvas canvas, SKSize size)
    {
        if (size.Width <= 0 || size.Height <= 0) return;

        if (size != _lastCanvasSize)
        {
            _lastCanvasSize = size;
            _runtime.EnsureCanvasInitialized(size);
        }

        canvas.Clear(SKColors.Black);
        _runtime.Render(canvas);
    }

    // ── Input ─────────────────────────────────────────────────────────────────
    // Les coordonnees Avalonia sont en unites logiques relatives au controle, soit exactement
    // le repere dans lequel OnRenderSkia dessine : aucune conversion n'est necessaire.

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();

        var p = e.GetPosition(this);
        var props = e.GetCurrentPoint(this).Properties;

        // Capture explicite : sans elle un glisser commence sur la carte (deplacement d'armee
        // depuis une ville) s'interrompt des que le pointeur passe au-dessus d'un panneau.
        e.Pointer.Capture(this);

        _runtime.HandlePointerPressed((float)p.X, (float)p.Y, 0, MapButton(props));
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var p = e.GetPosition(this);
        _runtime.HandlePointerMoved((float)p.X, (float)p.Y, 0);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        var p = e.GetPosition(this);

        _runtime.HandlePointerReleased((float)p.X, (float)p.Y, 0, MapButton(e.InitialPressMouseButton));
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        var p = e.GetPosition(this);

        // Avalonia compte en crans (~1.0) ; le runtime attend un WheelDelta facon Win32 (120/cran).
        _runtime.HandleZoom((float)e.Delta.Y * 120f, (float)p.X, (float)p.Y);
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (MapKey(e.Key) is { } key) _runtime.HandleKeyPressed(key);
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        if (MapKey(e.Key) is { } key) _runtime.HandleKeyReleased(key);
    }

    private static SkiaInput.PointerButton MapButton(PointerPointProperties props) =>
        props.IsRightButtonPressed  ? SkiaInput.PointerButton.Right
      : props.IsMiddleButtonPressed ? SkiaInput.PointerButton.Middle
      : SkiaInput.PointerButton.Left;

    private static SkiaInput.PointerButton MapButton(MouseButton button) => button switch
    {
        MouseButton.Right  => SkiaInput.PointerButton.Right,
        MouseButton.Middle => SkiaInput.PointerButton.Middle,
        MouseButton.Left   => SkiaInput.PointerButton.Left,
        _                  => SkiaInput.PointerButton.Unknown,
    };

    /// Seules les touches reellement consommees par le jeu sont traduites (pause, debug,
    /// modificateurs) ; le reste est ignore volontairement.
    private static string? MapKey(Key key) => key switch
    {
        Key.Space                        => "Space",
        Key.C                            => "C",
        Key.F9                           => "F9",
        Key.F10                          => "F10",
        Key.F11                          => "F11",
        Key.F12                          => "F12",
        Key.LeftShift or Key.RightShift  => "Shift",
        Key.LeftCtrl  or Key.RightCtrl   => "Control",
        _                                => null,
    };
}
