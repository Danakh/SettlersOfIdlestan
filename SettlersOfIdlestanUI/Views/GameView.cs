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
    private readonly MonumentPanelView _monumentPanel;
    private readonly CityPanelView _cityPanel;
    private readonly CivPanelView _civPanel;

    private readonly TabBarViewModel _tabs;
    private readonly ResourceBarViewModel _resources;
    private readonly TimeControlViewModel _time;
    private readonly MonumentPanelViewModel _monument;
    private readonly CityPanelViewModel _city;
    private readonly CivPanelViewModel _civ;

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
        _monument = new MonumentPanelViewModel(host);
        _city = new CityPanelViewModel(host);
        _civ = new CivPanelViewModel(host);

        _zoomControl = new ZoomControlView(host.ZoomIn, host.ZoomOut) { IsVisible = false };
        _topBar = new TopBarView(
            _tabs, _resources, _time, _icons,
            host.Localize,
            (key, args) => host.LocalizeFormat(key, args),
            host.ToggleSettingsMenu)
        {
            IsVisible = false,
        };

        _monumentPanel = new MonumentPanelView(_monument, _icons)
        {
            // Sous la barre du haut, comme le panneau Skia qu'il remplace.
            Margin = new Avalonia.Thickness(0, TopBarView.BarHeight + 10, 10, 0),
        };

        _cityPanel = new CityPanelView(_city, _icons)
        {
            Margin = new Avalonia.Thickness(0, TopBarView.BarHeight + 10, 10, 0),
        };

        // Ancre a gauche, sous la barre du haut, comme le panneau Skia qu'il remplace.
        _civPanel = new CivPanelView(_civ, _icons)
        {
            Margin = new Avalonia.Thickness(10, TopBarView.BarHeight + 10, 0, 0),
        };

        Children.Add(new GameRuntimeControl(host));
        Children.Add(_zoomControl);
        Children.Add(_topBar);
        Children.Add(_civPanel);
        Children.Add(_cityPanel);
        Children.Add(_monumentPanel);

        host.MarkOverlayMigratedToHost(
            HostedOverlayPart.ZoomControl | HostedOverlayPart.TopBar
            | HostedOverlayPart.MonumentPanel | HostedOverlayPart.CityPanel
            | HostedOverlayPart.CivPanel);
    }

    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _stateSync = DispatcherTimer.Run(SyncFromGameState, StateSyncInterval, DispatcherPriority.Normal);
    }

    protected override Avalonia.Size MeasureOverride(Avalonia.Size availableSize)
    {
        // Les panneaux latéraux s'ajustent à leur contenu : sans borne ils débordent sous la
        // fenêtre dès qu'une ville a beaucoup de bâtiments, emportant hors écran les onglets et
        // le pied de panneau. L'ancien rendu Skia calculait un nombre de lignes visibles ; ici
        // on borne la hauteur et le ScrollViewer interne prend le relais.
        //
        // À poser pendant la mesure, pas pendant l'arrangement : modifier MaxHeight après la
        // mesure n'a plus d'effet, les enfants ayant déjà été mesurés en hauteur infinie.
        if (!double.IsInfinity(availableSize.Height))
        {
            double available = availableSize.Height - TopBarView.BarHeight - PanelBottomMargin - PanelChromeHeight;
            _cityPanel.SetMaxContentHeight(available);
            _monumentPanel.SetMaxContentHeight(available);
            _civPanel.SetMaxContentHeight(available);
        }

        return base.MeasureOverride(availableSize);
    }

    private const double PanelBottomMargin = 30;

    /// En-tete du panneau (titre + fermeture) et marges internes, hors zone defilante.
    private const double PanelChromeHeight = 50;

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
        _monument.Refresh();
        _city.Refresh();
        _civ.Refresh();

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
