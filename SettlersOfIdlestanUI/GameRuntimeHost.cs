using SettlersOfIdlestanSkia.Renderers.Overlay;
using SkiaSharp;
using SkiaLayer = SettlersOfIdlestanSkia.Services;

namespace SettlersOfIdlestanUI;

/// <summary>
/// Point d'acces unique et serialise au <see cref="SkiaLayer.SkiaGameRuntime"/>.
///
/// Pourquoi ce verrou : sous OpenTK, Tick, Render et les callbacks d'input tournaient tous
/// sur le thread principal, donc l'etat du jeu n'etait jamais touche par deux threads a la
/// fois. Sous Avalonia ce n'est plus vrai — le rendu s'execute sur le thread de rendu, tandis
/// que la boucle de jeu et l'input vivent sur le thread UI. Sans serialisation, une recolte
/// qui emet des particules pendant un Tick corrompt l'enumeration en cours cote rendu
/// (InvalidOperationException "Collection was modified" dans HarvestRenderer).
///
/// Le runtime et tout le modele de jeu restant mono-thread par conception, on retablit le
/// contrat d'origine plutot que de rendre chaque collection thread-safe une par une.
///
/// REGLE : tout acces au runtime, y compris en lecture depuis un ViewModel, doit passer par
/// ce type. Un acces direct reintroduit la course.
/// </summary>
public sealed class GameRuntimeHost : IDisposable
{
    private readonly SkiaLayer.SkiaGameRuntime _runtime;
    private readonly object _gate = new();
    private bool _disposed;

    public GameRuntimeHost(SkiaLayer.SkiaGameRuntime runtime) => _runtime = runtime;

    /// <summary>
    /// Execute une lecture de l'etat du jeu sous verrou. Destine aux ViewModels et aux vues
    /// qui lisent depuis le thread UI pendant que le thread de rendu dessine.
    /// </summary>
    public T Read<T>(Func<SkiaLayer.SkiaGameRuntime, T> read)
    {
        lock (_gate)
        {
            return _disposed ? default! : read(_runtime);
        }
    }

    /// <summary>Execute une commande sur le runtime sous verrou.</summary>
    public void Invoke(Action<SkiaLayer.SkiaGameRuntime> command)
    {
        lock (_gate)
        {
            if (!_disposed) command(_runtime);
        }
    }

    // ── Boucle de jeu ─────────────────────────────────────────────────────────

    public void Tick() => Invoke(r => r.Tick());

    public void Render(SKCanvas canvas, SKSize canvasSize, ref SKSize lastCanvasSize)
    {
        lock (_gate)
        {
            if (_disposed) return;

            if (canvasSize != lastCanvasSize)
            {
                lastCanvasSize = canvasSize;
                _runtime.EnsureCanvasInitialized(canvasSize);
            }

            canvas.Clear(SKColors.Black);
            _runtime.Render(canvas);
        }
    }

    // ── Input ─────────────────────────────────────────────────────────────────

    public void PointerPressed(float x, float y, int id, SkiaLayer.PointerButton button) =>
        Invoke(r => r.HandlePointerPressed(x, y, id, button));

    public void PointerMoved(float x, float y, int id) =>
        Invoke(r => r.HandlePointerMoved(x, y, id));

    public void PointerReleased(float x, float y, int id, SkiaLayer.PointerButton button) =>
        Invoke(r => r.HandlePointerReleased(x, y, id, button));

    public void Zoom(float wheelDelta, float x, float y) =>
        Invoke(r => r.HandleZoom(wheelDelta, x, y));

    public void KeyPressed(string key) => Invoke(r => r.HandleKeyPressed(key));

    public void KeyReleased(string key) => Invoke(r => r.HandleKeyReleased(key));

    // ── Etat et commandes utilises par l'overlay Avalonia ─────────────────────

    public void SetUiScale(float scale) => Invoke(r => r.SetUiScale(scale));

    public bool IsMapViewActive => Read(r => r.IsMapViewActive);

    public void ZoomIn() => Invoke(r => r.ZoomIn());

    public void ZoomOut() => Invoke(r => r.ZoomOut());

    public void MarkOverlayMigratedToHost(HostedOverlayPart parts) =>
        Invoke(r => r.MarkOverlayMigratedToHost(parts));

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            _runtime.Dispose();
        }
    }
}
