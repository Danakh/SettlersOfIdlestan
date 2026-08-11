using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using SettlersOfIdlestanSkia.Renderers.Overlay;
using SettlersOfIdlestanUI.Controls;
using SettlersOfIdlestanUI.ViewModels;

namespace SettlersOfIdlestanUI.Views;

/// <summary>
/// Vue racine du jeu : la carte Skia au fond, tout l'overlay par-dessus.
///
/// L'ordre d'ajout dans <c>Children</c> fait foi — c'est lui qui decide ce qui recouvre quoi
/// et, du meme coup, quel controle intercepte un clic donne.
/// </summary>
public sealed class GameView : Panel, IDisposable
{
    private readonly GameRuntimeHost _host;
    private readonly SvgIconCache _icons = new();

    private readonly ZoomControlView _zoomControl;
    private readonly TopBarView _topBar;
    private readonly TabBarView _bottomTabBar;
    private readonly MonumentPanelView _monumentPanel;
    private readonly CityPanelView _cityPanel;
    private readonly CivPanelView _civPanel;
    private readonly ModalPopupView _modalPopup;
    private readonly TimeJumpView _timeJump;
    private readonly ToastStackView _toasts;
    private readonly EventLogView _eventLog;
    private readonly StatsView _stats;
    private readonly RitualsView _rituals;
    private readonly AutomationView _automation;
    private readonly SettingsMenuView _settingsMenu;
    private readonly TradePopupView _tradePopup;
    private readonly PrestigePopupView _prestigePopup;
    private readonly SettingsPopupView _settingsPopup;
    private readonly TitleScreenView _titleScreen;
    private readonly TooltipLayerControl _tooltipLayer;

    private readonly TabBarViewModel _tabs;
    private readonly ResourceBarViewModel _resources;
    private readonly TimeControlViewModel _time;
    private readonly MonumentPanelViewModel _monument;
    private readonly CityPanelViewModel _city;
    private readonly CivPanelViewModel _civ;
    private readonly ModalPopupViewModel _modal;
    private readonly TimeJumpViewModel _timeJumpModel;
    private readonly ToastViewModel _toast;
    private readonly EventLogViewModel _events;
    private readonly StatsViewModel _statsModel;
    private readonly RitualsViewModel _ritualsModel;
    private readonly AutomationViewModel _automationModel;
    private readonly SettingsMenuViewModel _settingsMenuModel;
    private readonly TradePopupViewModel _tradeModel;
    private readonly PrestigePopupViewModel _prestigeModel;
    private readonly SettingsPopupViewModel _settingsModel;
    private readonly TitleScreenViewModel _titleModel;

    private IDisposable? _stateSync;

    /// Hauteur courante de la barre du haut, seconde ligne de ressources comprise.
    private double _topBarHeight = TopBarView.BarHeight;

    /// Vrai quand les onglets sont ancres en bas : tout ce qui se cale sur le bas de l'ecran
    /// (zoom, onglets plein ecran, hauteur maximale des panneaux) doit leur laisser la place.
    private bool _tabsAtBottom;

    /// Jeu entre la barre du bas et le bord de l'ecran, repris du rendu Skia.
    private const double BottomTabBarGap = 2;

    /// Place totale prise par les onglets quand ils sont en bas, ce jeu compris.
    private double BottomTabBarSpace =>
        _tabsAtBottom ? TabBarView.BottomBarHeight + BottomTabBarGap : 0;

    /// Cadence de synchronisation de l'etat vers l'UI. Deliberement decouplee des 60 fps du
    /// rendu : lever des notifications de changement a chaque frame ferait relayouter Avalonia
    /// en continu pour des valeurs qui ne bougent que quelques fois par seconde.
    private static readonly TimeSpan StateSyncInterval = TimeSpan.FromMilliseconds(100);

    public GameView(GameRuntimeHost host)
    {
        _host = host;

        _tabs = new TabBarViewModel(host);

        // Le changement d'onglet ne peut pas attendre le tick de synchronisation : la page
        // demandee s'afficherait vide pendant jusqu'a 100 ms.
        _tabs.TabChanged += () => SyncFromGameState();

        _resources = new ResourceBarViewModel(host);
        _time = new TimeControlViewModel(host);
        _monument = new MonumentPanelViewModel(host);
        _city = new CityPanelViewModel(host);
        _civ = new CivPanelViewModel(host);
        _modal = new ModalPopupViewModel(host);
        _timeJumpModel = new TimeJumpViewModel(host);
        _toast = new ToastViewModel(host);
        _events = new EventLogViewModel(host);
        _statsModel = new StatsViewModel(host);
        _ritualsModel = new RitualsViewModel(host);
        _automationModel = new AutomationViewModel(host);
        _settingsMenuModel = new SettingsMenuViewModel(host);
        _tradeModel = new TradePopupViewModel(host);
        _prestigeModel = new PrestigePopupViewModel(host);
        _settingsModel = new SettingsPopupViewModel(host);
        _titleModel = new TitleScreenViewModel(host);

        _zoomControl = new ZoomControlView(host.ZoomIn, host.ZoomOut) { IsVisible = false };
        _topBar = new TopBarView(
            _tabs, _resources, _time, _icons,
            host.Localize,
            (key, args) => host.LocalizeFormat(key, args),
            host.ToggleSettingsMenu)
        {
            IsVisible = false,
        };
        _topBar.TotalHeightChanged += OnTopBarHeightChanged;

        // Disposition compacte : les onglets quittent la barre du haut pour le bas de l'ecran,
        // a portee du pouce. C'est le meme exemplaire de ViewModel que la barre du haut — seule
        // celle des deux vues que designe la disposition courante est visible.
        _bottomTabBar = new TabBarView(_tabs, TabBarPlacement.Bottom)
        {
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Bottom,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            Height = TabBarView.BottomBarHeight,
            Margin = new Avalonia.Thickness(0, 0, 0, BottomTabBarGap),
        };

        // Les ancrages sur les barres sont poses par ApplyBarLayout : la barre du haut gagne une
        // seconde ligne quand les ressources n'y tiennent plus, et les onglets passent en bas en
        // disposition compacte — tout ce qui se cale dessus doit suivre.
        _monumentPanel = new MonumentPanelView(_monument, _icons);
        _cityPanel = new CityPanelView(_city, _icons);
        _civPanel = new CivPanelView(_civ, _icons);

        _modalPopup = new ModalPopupView(_modal);
        _timeJump = new TimeJumpView(_timeJumpModel);
        _toasts = new ToastStackView(_toast);

        _eventLog = new EventLogView(_events);
        _stats = new StatsView(_statsModel);
        _rituals = new RitualsView(_ritualsModel);
        _automation = new AutomationView(_automationModel);
        _settingsMenu = new SettingsMenuView(_settingsMenuModel, TopBarView.BarHeight + 5);
        _tradePopup = new TradePopupView(_tradeModel, _icons);
        _prestigePopup = new PrestigePopupView(_prestigeModel);
        _settingsPopup = new SettingsPopupView(_settingsModel);
        _titleScreen = new TitleScreenView(_titleModel);
        _tooltipLayer = new TooltipLayerControl(host);

        Children.Add(new GameRuntimeControl(host));

        // Avant les panneaux et la barre du haut : l'onglet plein ecran couvre la carte, mais
        // reste sous le reste de l'overlay.
        Children.Add(_eventLog);
        Children.Add(_stats);
        Children.Add(_rituals);
        Children.Add(_automation);

        // Avant la barre du haut : le capteur de fermeture ne doit pas voler son clic a
        // l'engrenage, qui referme le menu en le rebasculant.
        Children.Add(_settingsMenu.Catcher);

        Children.Add(_zoomControl);
        Children.Add(_topBar);

        // Meme rang que la barre du haut : la navigation passe au-dessus des onglets plein
        // ecran, mais reste sous les panneaux, les infobulles et les popups bloquants.
        Children.Add(_bottomTabBar);
        Children.Add(_civPanel);
        Children.Add(_cityPanel);
        Children.Add(_monumentPanel);

        // Apres les panneaux : la boite du menu s'ouvre sous la barre du haut a droite, soit
        // exactement l'emplacement du panneau de ville, qui la recouvrirait.
        Children.Add(_settingsMenu);

        Children.Add(_toasts);

        // Les infobulles Skia se dessinent ici, et non sur le canevas de la carte : ancrees a une
        // ligne du panneau de ville ou au bord d'un hexagone, elles debordent sur les panneaux et
        // passaient donc derriere eux. Sous les popups, qui sont bloquants.
        Children.Add(_tooltipLayer);

        // Le popup de commerce est bloquant lui aussi : au-dessus des panneaux, mais sous les
        // modales, qui priment sur tout.
        Children.Add(_tradePopup);
        Children.Add(_prestigePopup);
        Children.Add(_settingsPopup);

        // L'ecran-titre couvre tout le jeu : il passe avant les modales, qui restent au-dessus
        // (sa confirmation de remise a zero emprunte la meme vue).
        Children.Add(_titleScreen);

        // En dernier : une modale bloquante doit couvrir tout le reste de l'overlay.
        Children.Add(_modalPopup);

        // Apres les modales : pendant un saut de temps la boucle de jeu ne traite plus rien
        // d'autre, aucun bouton d'aucune modale n'aurait d'effet visible tant qu'il dure.
        Children.Add(_timeJump);

        // Clavier du popup de commerce : Ctrl/Maj pour le multiplicateur temporaire, Echap pour
        // fermer. L'ecoute est ici et non sur GameRuntimeControl : le popup s'ouvre depuis un
        // bouton Avalonia, qui prend le focus clavier, et la carte ne verrait alors jamais la
        // touche. GameView est l'ancetre commun de tout l'overlay — l'evenement lui remonte quel
        // que soit le controle focalise, y compris s'il l'a marque traite.
        AddHandler(InputElement.KeyDownEvent, OnGameKeyDown, RoutingStrategies.Bubble, handledEventsToo: true);
        AddHandler(InputElement.KeyUpEvent, OnGameKeyUp, RoutingStrategies.Bubble, handledEventsToo: true);

        ApplyBarLayout();
    }

    private void OnGameKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _tradeModel.IsOpen)
        {
            _tradeModel.Close();
            e.Handled = true;
            return;
        }

        if (MapModifier(e.Key) is not { } key) return;
        _host.KeyPressed(key);

        // Sans ce rafraichissement immediat, les quantites et le multiplicateur mis en avant
        // attendraient le tick de synchronisation, jusqu'a 100 ms apres l'appui.
        _tradeModel.Refresh();
    }

    private void OnGameKeyUp(object? sender, KeyEventArgs e)
    {
        if (MapModifier(e.Key) is not { } key) return;
        _host.KeyReleased(key);
        _tradeModel.Refresh();
    }

    private static string? MapModifier(Key key) => key switch
    {
        Key.LeftShift or Key.RightShift => "Shift",
        Key.LeftCtrl or Key.RightCtrl => "Control",
        _ => null,
    };

    /// <summary>
    /// Recale tout ce qui s'ancre sur les barres. Cote Avalonia ce sont des marges ; cote Skia,
    /// les vues plein ecran encore dessinees par le runtime (recherche, carte de prestige,
    /// ascension) lisent la hauteur poussee dans UILayoutService.
    /// </summary>
    private void ApplyBarLayout()
    {
        double barHeight = _topBarHeight;
        double bottom = BottomTabBarSpace;

        // Panneaux lateraux : sous la barre, avec le meme jeu de 10 px que le rendu Skia.
        _monumentPanel.Margin = new Avalonia.Thickness(0, barHeight + 10, 10, 0);
        _cityPanel.Margin = new Avalonia.Thickness(0, barHeight + 10, 10, 0);
        _civPanel.Margin = new Avalonia.Thickness(10, barHeight + 10, 0, 0);

        // Onglets plein ecran : ils remplacent la carte, pas la navigation — donc jamais
        // par-dessus les barres, en haut comme en bas.
        _eventLog.Margin = new Avalonia.Thickness(0, barHeight, 0, bottom);
        _stats.Margin = new Avalonia.Thickness(0, barHeight, 0, bottom);
        _rituals.Margin = new Avalonia.Thickness(0, barHeight, 0, bottom);
        _automation.Margin = new Avalonia.Thickness(0, barHeight, 0, bottom);

        // Les boutons de zoom sont ancres en bas a droite : ils passeraient sous les onglets.
        _zoomControl.Margin = new Avalonia.Thickness(0, 0, 10, bottom + 10);

        _settingsMenu.SetTopOffset(barHeight + 5);

        _host.SetTopBarHeight((float)barHeight);
    }

    private void OnTopBarHeightChanged(double barHeight)
    {
        _topBarHeight = barHeight;
        ApplyBarLayout();
        InvalidateMeasure();
    }

    private void OnTabsPlacementChanged(bool atBottom)
    {
        if (_tabsAtBottom == atBottom) return;
        _tabsAtBottom = atBottom;
        ApplyBarLayout();
        InvalidateMeasure();
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
            double available = availableSize.Height
                             - _topBarHeight - BottomTabBarSpace - PanelBottomMargin - PanelChromeHeight;
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

        // La disposition peut basculer en cours de partie : rotation de l'ecran ou
        // redimensionnement de la fenetre (detection auto), ou reglage « menus en bas ».
        OnTabsPlacementChanged(_tabs.ShowAtBottom);
        _resources.Refresh();
        _time.Refresh();
        _monument.Refresh();
        _city.Refresh();
        _civ.Refresh();
        _modal.Refresh();
        _timeJumpModel.Refresh();
        _toast.Refresh();
        _events.Refresh();
        _statsModel.Refresh();
        _ritualsModel.Refresh();
        _automationModel.Refresh();
        _settingsMenuModel.Refresh();
        _tradeModel.Refresh();
        _prestigeModel.Refresh();
        _settingsModel.Refresh();
        _titleModel.Refresh();

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
