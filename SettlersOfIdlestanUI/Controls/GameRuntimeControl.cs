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
/// Hote Avalonia du runtime de jeu.
///
/// Etape intermediaire assumee de la migration : ce controle porte encore l'integralite du
/// runtime Skia (carte ET overlay legacy), ce qui rend le jeu jouable sous Avalonia des la
/// phase 2. Les phases suivantes extraient l'overlay panneau par panneau vers de vrais
/// controles Avalonia ; ce controle se reduira alors a la seule carte hex.
///
/// Tout passe par <see cref="GameRuntimeHost"/> : le rendu s'execute sur le thread de rendu
/// d'Avalonia alors que la boucle de jeu et l'input vivent sur le thread UI, ce que le
/// runtime mono-thread ne supporte pas sans serialisation.
/// </summary>
public class GameRuntimeControl : SkiaCanvasControl
{
    private readonly GameRuntimeHost _host;
    private IDisposable? _loop;
    private SKSize _lastCanvasSize;

    private readonly PinchTracker _pinch = new();

    /// Pointeurs encore enfonces, pour pouvoir les relacher cote jeu quand le recognizer de
    /// pincement les capture : leur PointerReleased ne nous parviendra plus.
    private readonly HashSet<int> _pressedPointerIds = [];

    /// Cadence de la boucle de jeu. Avalonia 12 n'expose pas de RequestAnimationFrame :
    /// on pilote le Tick + l'invalidation avec un timer a priorite Render.
    private static readonly TimeSpan FrameInterval = TimeSpan.FromMilliseconds(16);

    public GameRuntimeControl(GameRuntimeHost host)
    {
        _host = host;
        Focusable = true;

        // Zoom a deux doigts sur les heads tactiles (iOS). Avalonia mesure lui-meme la distance
        // entre les contacts, la ou le head MAUI devait reconstituer le geste a la main.
        GestureRecognizers.Add(new PinchGestureRecognizer());
        Pinch += OnPinch;
        PinchEnded += OnPinchEnded;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        // L'echelle UI automatique reste a 1 : Avalonia exprime deja tout en unites logiques et
        // applique le facteur DPI lui-meme. Reporter RenderScaling dans AutoUiScale appliquerait
        // le zoom une seconde fois et doublerait la taille de l'UI sur les ecrans HiDPI.
        _host.SetUiScale(1f);

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
        _host.Tick();
        InvalidateVisual();
        return true;
    }

    protected override void OnRenderSkia(SKCanvas canvas, SKSize size)
    {
        if (size.Width <= 0 || size.Height <= 0) return;
        _host.Render(canvas, size, ref _lastCanvasSize);
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

        // L'identifiant reel du pointeur, et non 0 : c'est lui qui permet au jeu de savoir
        // quel doigt fait defiler la carte quand plusieurs sont poses (heads tactiles).
        _pressedPointerIds.Add(e.Pointer.Id);
        _host.PointerPressed((float)p.X, (float)p.Y, e.Pointer.Id, MapButton(props));
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var p = e.GetPosition(this);
        _host.PointerMoved((float)p.X, (float)p.Y, e.Pointer.Id);
    }

    // Les infobulles de la carte sont produites pendant le rendu, a partir d'un etat de survol
    // que ce controle cesse de recevoir des que le pointeur passe sur un controle de l'overlay.
    // Sans ces deux signaux, cet etat reste fige et l'infobulle Skia s'affiche en meme temps que
    // celle du controle Avalonia reellement survole.
    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        _host.SetPointerOverMap(true);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _host.SetPointerOverMap(false);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        var p = e.GetPosition(this);

        _pressedPointerIds.Remove(e.Pointer.Id);
        _host.PointerReleased((float)p.X, (float)p.Y, e.Pointer.Id, MapButton(e.InitialPressMouseButton));
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        var p = e.GetPosition(this);

        // Avalonia compte en crans (~1.0) ; le runtime attend un WheelDelta facon Win32 (120/cran).
        _host.Zoom((float)e.Delta.Y * 120f, (float)p.X, (float)p.Y);
        e.Handled = true;
    }

    /// <summary>
    /// Pincement a deux doigts : zoom sur le centre du geste, plus le deplacement de ce centre
    /// comme panoramique.
    /// </summary>
    private void OnPinch(object? sender, PinchEventArgs e)
    {
        var origin = e.ScaleOrigin;

        if (_pinch.Update(e.Scale, origin, out var ratio, out var panDx, out var panDy))
            _host.Pinch(ratio, (float)origin.X, (float)origin.Y, panDx, panDy);
        else
            // Premier evenement du geste : le premier doigt avait deja lance un panoramique, et
            // le recognizer vient de capturer les pointeurs — leur relachement ne nous reviendra
            // jamais. Sans cette annulation, la carte reste accrochee au doigt.
            ReleasePressedPointers((float)origin.X, (float)origin.Y);

        e.Handled = true;
    }

    private void OnPinchEnded(object? sender, PinchEndedEventArgs e)
    {
        _pinch.End();
        _pressedPointerIds.Clear();
        e.Handled = true;
    }

    private void ReleasePressedPointers(float x, float y)
    {
        foreach (var id in _pressedPointerIds)
            _host.PointerReleased(x, y, id, SkiaInput.PointerButton.Left);
        _pressedPointerIds.Clear();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (MapKey(e.Key) is { } key) _host.KeyPressed(key);
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        if (MapKey(e.Key) is { } key) _host.KeyReleased(key);
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
