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

    public SkiaLayer.TabBarSnapshot GetTabBarSnapshot() =>
        Read(r => r.GetTabBarSnapshot()) ?? SkiaLayer.TabBarSnapshot.Unavailable;

    public void SetActiveTab(int tabId) => Invoke(r => r.SetActiveTab(tabId));

    public void ToggleSettingsMenu() => Invoke(r => r.ToggleSettingsMenu());

    public SkiaLayer.CityPanelSnapshot GetCityPanelSnapshot() =>
        Read(r => r.GetCityPanelSnapshot()) ?? SkiaLayer.CityPanelSnapshot.Hidden;

    public void CloseCityPanel() => Invoke(r => r.CloseCityPanel());
    public void SetCityShowUnique(bool v) => Invoke(r => r.SetCityShowUnique(v));
    public void ToggleCityBuildingActivation(string k) => Invoke(r => r.ToggleCityBuildingActivation(k));
    public void ExecuteCityBuildingAction(string k) => Invoke(r => r.ExecuteCityBuildingAction(k));
    public void GoToOtherCity(string k) => Invoke(r => r.GoToOtherCity(k));
    public void SetHoveredCityBuilding(string? k, float x, float y) => Invoke(r => r.SetHoveredCityBuilding(k, x, y));

    public SkiaLayer.MonumentPanelSnapshot GetMonumentPanelSnapshot() =>
        Read(r => r.GetMonumentPanelSnapshot()) ?? SkiaLayer.MonumentPanelSnapshot.Hidden;

    public void CloseMonumentPanel() => Invoke(r => r.CloseMonumentPanel());
    public void ToggleMonumentInvestment(string rowKey) => Invoke(r => r.ToggleMonumentInvestment(rowKey));
    public void EvolveMonument() => Invoke(r => r.EvolveMonument());
    public void SkipWonder() => Invoke(r => r.SkipWonder());

    public SkiaLayer.ResourceBarSnapshot GetResourceBarSnapshot() =>
        Read(r => r.GetResourceBarSnapshot()) ?? SkiaLayer.ResourceBarSnapshot.Unavailable;

    public SkiaLayer.TimeControlSnapshot GetTimeControlSnapshot() =>
        Read(r => r.GetTimeControlSnapshot()) ?? SkiaLayer.TimeControlSnapshot.Unavailable;

    public string Localize(string key) => Read(r => r.Localize(key)) ?? key;

    public string LocalizeFormat(string key, params object[] args) =>
        Read(r => r.LocalizeFormat(key, args)) ?? key;

    public void TogglePause() => Invoke(r => r.TogglePause());

    public void SetGameSpeed(int multiplier) => Invoke(r => r.SetGameSpeed(multiplier));

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
