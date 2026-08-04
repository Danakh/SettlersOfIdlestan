using Avalonia.Controls;
using Avalonia.Threading;
using SettlersOfIdlestanSkia.Renderers.Overlay;
using SettlersOfIdlestanUI.Controls;
using SettlersOfIdlestanUI.ViewModels;

namespace SettlersOfIdlestanUI.Views;

/// <summary>
/// Vue racine du jeu : la carte Skia au fond, les elements d'overlay deja migres par-dessus.
///
/// Chaque element ajoute ici doit etre declare via <see cref="HostedOverlayPart"/>, sinon
/// il sera dessine deux fois — une fois par l'overlay Skia legacy, une fois par Avalonia.
/// </summary>
public sealed class GameView : Panel, IDisposable
{
    private readonly GameRuntimeHost _host;
    private readonly SvgIconCache _icons = new();

    private readonly ZoomControlView _zoomControl;
    private readonly TopBarView _topBar;

    private readonly TabBarViewModel _tabs;
    private readonly ResourceBarViewModel _resources;
    private readonly TimeControlViewModel _time;

    private IDisposable? _stateSync;

    /// Cadence de synchronisation de l'etat vers l'UI. Deliberement decouplee des 60 fps du
    /// rendu : lever des notifications de changement a chaque frame ferait relayouter Avalonia
    /// en continu pour des valeurs qui ne bougent que quelques fois par seconde.
    private static readonly TimeSpan StateSyncInterval = TimeSpan.FromMilliseconds(100);

    public GameView(GameRuntimeHost host)
    {
        _host = host;

        _tabs = new TabBarViewModel(host);
        _resources = new ResourceBarViewModel(host);
        _time = new TimeControlViewModel(host);

        _zoomControl = new ZoomControlView(host.ZoomIn, host.ZoomOut) { IsVisible = false };
        _topBar = new TopBarView(
            _tabs, _resources, _time, _icons,
            host.Localize,
            (key, args) => host.LocalizeFormat(key, args),
            host.ToggleSettingsMenu)
        {
            IsVisible = false,
        };

        Children.Add(new GameRuntimeControl(host));
        Children.Add(_zoomControl);
        Children.Add(_topBar);

        host.MarkOverlayMigratedToHost(HostedOverlayPart.ZoomControl | HostedOverlayPart.TopBar);
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
        // Lectures sous verrou : on est sur le thread UI pendant que le thread de rendu dessine.
        _tabs.Refresh();
        _resources.Refresh();
        _time.Refresh();

        // Les boutons de zoom n'ont de sens que sur une vue carte : ni sur l'ecran titre,
        // ni sur les onglets plein ecran (recherche, prestige...).
        _zoomControl.IsVisible = _host.IsMapViewActive;

        // La barre du haut suit la presence d'une partie, pas la vue courante : elle reste
        // affichee sur les onglets plein ecran.
        _topBar.IsVisible = _resources.IsAvailable;

        return true;
    }

    public void Dispose() => _icons.Dispose();
}
