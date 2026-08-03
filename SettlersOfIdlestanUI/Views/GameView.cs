using Avalonia.Controls;
using Avalonia.Threading;
using SettlersOfIdlestanSkia.Renderers.Overlay;
using SettlersOfIdlestanUI.Controls;
using SkiaLayer = SettlersOfIdlestanSkia.Services;

namespace SettlersOfIdlestanUI.Views;

/// <summary>
/// Vue racine du jeu : la carte Skia au fond, les elements d'overlay deja migres par-dessus.
///
/// Chaque element ajoute ici doit etre declare via <see cref="HostedOverlayPart"/>, sinon
/// il sera dessine deux fois — une fois par l'overlay Skia legacy, une fois par Avalonia.
/// </summary>
public sealed class GameView : Panel
{
    private readonly SkiaLayer.SkiaGameRuntime _runtime;
    private readonly ZoomControlView _zoomControl;
    private IDisposable? _stateSync;

    /// Cadence de synchronisation de l'etat vers l'UI. Deliberement decouplee des 60 fps du
    /// rendu : lever des notifications de changement a chaque frame ferait relayouter Avalonia
    /// en continu pour des valeurs qui ne bougent que quelques fois par seconde.
    private static readonly TimeSpan StateSyncInterval = TimeSpan.FromMilliseconds(100);

    public GameView(SkiaLayer.SkiaGameRuntime runtime)
    {
        _runtime = runtime;

        _zoomControl = new ZoomControlView(runtime.ZoomIn, runtime.ZoomOut) { IsVisible = false };

        Children.Add(new GameRuntimeControl(runtime));
        Children.Add(_zoomControl);

        runtime.MarkOverlayMigratedToHost(HostedOverlayPart.ZoomControl);
    }

    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _stateSync = DispatcherTimer.Run(SyncFromGameState, StateSyncInterval, DispatcherPriority.Normal);
    }

    protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _stateSync?.Dispose();
        _stateSync = null;
    }

    private bool SyncFromGameState()
    {
        // Les boutons de zoom n'ont de sens que sur une vue carte : ni sur l'ecran titre,
        // ni sur les onglets plein ecran (recherche, prestige...).
        _zoomControl.IsVisible = _runtime.IsMapViewActive;
        return true;
    }
}
