using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Layout;
using Avalonia.Media;
using SettlersOfIdlestanUI.ViewModels;
using SkiaSharp;

namespace SettlersOfIdlestanUI.Controls;

/// <summary>
/// Panneau lateral de la civilisation du joueur, ancre a gauche : actions disponibles (prestige,
/// merveille, pillage...) et bascules epinglees depuis la page Automatisation.
///
/// Les infobulles sont du texte simple : elles passent en ToolTip Avalonia natif, contrairement
/// a celle du panneau ville qui reste dessinee en Skia. C'est pourquoi les boutons indisponibles
/// restent actifs au sens Avalonia — desactives, ils cesseraient de recevoir le pointeur et
/// n'afficheraient plus l'infobulle qui explique justement le blocage. Le refus de l'action est
/// porte par le renderer, qui garde chacune de ses actions.
/// </summary>
public sealed class CivPanelView : UserControl
{
    private static readonly SolidColorBrush Normal = new(Color.FromRgb(46, 125, 50));
    private static readonly SolidColorBrush Highlighted = new(Color.FromRgb(170, 40, 40));
    private static readonly SolidColorBrush Disabled = new(Color.FromRgb(70, 70, 78));
    private static readonly SolidColorBrush DisabledText = new(Color.FromRgb(160, 160, 165));
    private static readonly SolidColorBrush SectionTitle = new(Color.FromRgb(160, 160, 175));
    private static readonly SolidColorBrush Separator = new(Color.FromRgb(60, 60, 80));

    private const int IconButtonSize = 24;

    private readonly GamePanelView _panel;
    private readonly CivPanelViewModel _viewModel;

    public CivPanelView(CivPanelViewModel viewModel, SvgIconCache icons)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        this[!IsVisibleProperty] = new Binding(nameof(CivPanelViewModel.IsVisible));

        // Sans alignement explicite le UserControl s'etire sur toute la fenetre : ses bornes ne
        // correspondraient plus au panneau visible et le hit-testing deviendrait imprevisible.
        HorizontalAlignment = HorizontalAlignment.Left;
        VerticalAlignment = VerticalAlignment.Top;

        _panel = new GamePanelView(title: "", onClose: null, side: GamePanelSide.Left);

        // L'en-tete porte le titre Actions et les boutons icone, hors du defilement : ils
        // doivent rester atteignables quand la liste des bascules fait defiler le panneau.
        _panel.TitleBlock[!TextBlock.TextProperty] = new Binding(nameof(CivPanelViewModel.ActionsTitle));
        _panel.TitleBlock.FontSize = 11;
        _panel.TitleBlock.FontWeight = FontWeight.Normal;
        _panel.TitleBlock.Foreground = SectionTitle;
        _panel.HeaderHost[!IsVisibleProperty] = new Binding(nameof(CivPanelViewModel.HasActions));
        _panel.HeaderHost.Children.Add(BuildIconActions(viewModel, icons));

        var content = new StackPanel { Orientation = Orientation.Vertical };
        content.Children.Add(BuildActionGrid(viewModel));
        content.Children.Add(BuildSeparator());
        content.Children.Add(BuildControlsTitle());
        content.Children.Add(BuildToggles(viewModel));

        _panel.Content = content;
        Content = _panel;

        // Le repli est detenu par le renderer (la disposition mobile le replie d'autorite quand
        // un panneau lateral droit s'ouvre) : la vue le lui renvoie et le relit de l'instantane.
        // La comparaison evite qu'un repli venu du renderer ne lui soit renvoye en echo.
        _panel.CollapsedChanged = collapsed =>
        {
            if (collapsed != _viewModel.IsCollapsed) _viewModel.SetCollapsed(collapsed);
        };
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(CivPanelViewModel.IsCollapsed))
                _panel.SetCollapsed(_viewModel.IsCollapsed);
        };
    }

    /// <summary>Borne la hauteur defilante du panneau (cf. GamePanelView.SetMaxContentHeight).</summary>
    public void SetMaxContentHeight(double height) => _panel.SetMaxContentHeight(height);

    private static Control BuildIconActions(CivPanelViewModel viewModel, SvgIconCache icons) =>
        new ItemsControl
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            [!ItemsControl.ItemsSourceProperty] = new Binding(nameof(CivPanelViewModel.IconActions)),
            ItemsPanel = new FuncTemplate<Panel?>(() => new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
            }),
            ItemTemplate = new FuncDataTemplate<CivActionViewModel>(
                (_, _) => new IconActionButton(viewModel, icons), supportsRecycling: true),
        };

    private static Control BuildActionGrid(CivPanelViewModel viewModel) => new ItemsControl
    {
        Margin = new Thickness(10, 4),
        [!IsVisibleProperty] = new Binding(nameof(CivPanelViewModel.HasActions)),
        [!ItemsControl.ItemsSourceProperty] = new Binding(nameof(CivPanelViewModel.Actions)),
        // Deux colonnes, comme le rendu Skia : les libelles sont courts et le panneau etroit.
        ItemsPanel = new FuncTemplate<Panel?>(() => new UniformGrid { Columns = 2 }),
        ItemTemplate = new FuncDataTemplate<CivActionViewModel>(
            (_, _) => new GridActionButton(viewModel), supportsRecycling: true),
    };

    private static Control BuildSeparator() => new Border
    {
        Height = 1,
        Margin = new Thickness(10, 8),
        Background = Separator,
        [!IsVisibleProperty] = new Binding(nameof(CivPanelViewModel.HasSeparator)),
    };

    private static Control BuildControlsTitle() => new TextBlock
    {
        FontSize = 11,
        Foreground = SectionTitle,
        Margin = new Thickness(10, 0, 10, 4),
        [!TextBlock.TextProperty] = new Binding(nameof(CivPanelViewModel.ControlsTitle)),
        [!IsVisibleProperty] = new Binding(nameof(CivPanelViewModel.HasToggles)),
    };

    private static Control BuildToggles(CivPanelViewModel viewModel) => new ItemsControl
    {
        [!ItemsControl.ItemsSourceProperty] = new Binding(nameof(CivPanelViewModel.Toggles)),
        ItemTemplate = new FuncDataTemplate<CivToggleViewModel>(
            (_, _) => new ToggleRow(viewModel), supportsRecycling: true),
    };

    private static IBrush BackgroundFor(CivActionVisual visual) => visual switch
    {
        CivActionVisual.Highlighted => Highlighted,
        CivActionVisual.Disabled => Disabled,
        _ => Normal,
    };

    private static IBrush ForegroundFor(CivActionVisual visual) =>
        visual == CivActionVisual.Disabled ? DisabledText : Brushes.White;

    /// <summary>Petit bouton carre de l'en-tete : icone SVG teintee en blanc, ou glyphe.</summary>
    private sealed class IconActionButton : Button
    {
        private readonly CivPanelViewModel _owner;
        private readonly SvgIconCache _icons;
        private readonly Image _icon = new() { Width = 14, Height = 14 };
        private readonly TextBlock _glyph = new() { FontSize = 13, IsVisible = false };
        private CivActionViewModel? _action;

        public IconActionButton(CivPanelViewModel owner, SvgIconCache icons)
        {
            _owner = owner;
            _icons = icons;

            Width = IconButtonSize;
            Height = IconButtonSize;
            Padding = new Thickness(0);
            BorderThickness = new Thickness(0);
            CornerRadius = new CornerRadius(5);
            HorizontalContentAlignment = HorizontalAlignment.Center;
            VerticalContentAlignment = VerticalAlignment.Center;

            var content = new Panel();
            content.Children.Add(_icon);
            content.Children.Add(_glyph);
            Content = content;

            Classes.Add(GameControlStyles.ToneButton);
            this[!BackgroundProperty] = new Binding(nameof(CivActionViewModel.Visual))
            {
                Converter = new FuncValueConverter<CivActionVisual, IBrush>(BackgroundFor),
            };
            this[!ToolTip.TipProperty] = new Binding(nameof(CivActionViewModel.Tooltip));

            Click += (_, _) => { if (_action != null) _owner.Execute(_action); };
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);
            _action = DataContext as CivActionViewModel;
            if (_action == null) return;

            _glyph.Text = _action.Glyph ?? "";
            _glyph.IsVisible = _action.HasGlyph;
            _icon.IsVisible = !_action.HasGlyph;
            _icon.Source = _action.IconName != null
                ? _icons.Get(_action.IconName, 14, SKColors.White)
                : null;
        }
    }

    /// <summary>Bouton pleine largeur de colonne : libelle sur une ou deux lignes.</summary>
    private sealed class GridActionButton : Button
    {
        private readonly CivPanelViewModel _owner;
        private CivActionViewModel? _action;

        public GridActionButton(CivPanelViewModel owner)
        {
            _owner = owner;

            Height = 38;
            Margin = new Thickness(3);
            Padding = new Thickness(4, 0);
            BorderThickness = new Thickness(0);
            CornerRadius = new CornerRadius(6);
            HorizontalAlignment = HorizontalAlignment.Stretch;
            HorizontalContentAlignment = HorizontalAlignment.Center;
            VerticalContentAlignment = VerticalAlignment.Center;

            var label = new TextBlock
            {
                FontSize = 11,
                FontWeight = FontWeight.Bold,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                [!TextBlock.TextProperty] = new Binding(nameof(CivActionViewModel.Label)),
                [!TextBlock.ForegroundProperty] = new Binding(nameof(CivActionViewModel.Visual))
                {
                    Converter = new FuncValueConverter<CivActionVisual, IBrush>(ForegroundFor),
                },
            };
            Content = label;

            Classes.Add(GameControlStyles.ToneButton);
            this[!BackgroundProperty] = new Binding(nameof(CivActionViewModel.Visual))
            {
                Converter = new FuncValueConverter<CivActionVisual, IBrush>(BackgroundFor),
            };
            this[!ToolTip.TipProperty] = new Binding(nameof(CivActionViewModel.Tooltip));

            Click += (_, _) => { if (_action != null) _owner.Execute(_action); };
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);
            _action = DataContext as CivActionViewModel;
        }
    }

    /// <summary>Une bascule epinglee. Trois etats : l'etat mixte est celui du renderer.</summary>
    private sealed class ToggleRow : Border
    {
        private readonly CivPanelViewModel _owner;
        private CivToggleViewModel? _toggle;

        public ToggleRow(CivPanelViewModel owner)
        {
            _owner = owner;

            // Fond transparent plutot qu'absent : sans geometrie remplie la ligne ne recevrait
            // pas les evenements de survol, donc pas d'infobulle.
            Background = Brushes.Transparent;
            Padding = new Thickness(10, 2);
            this[!ToolTip.TipProperty] = new Binding(nameof(CivToggleViewModel.Tooltip));

            var checkbox = new CheckBox
            {
                IsThreeState = true,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0),
                [!ToggleButton.IsCheckedProperty] = new Binding(nameof(CivToggleViewModel.IsOn)),
            };
            checkbox.Click += (_, _) => { if (_toggle != null) _owner.Toggle(_toggle); };

            var label = new TextBlock
            {
                FontSize = 13,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                [!TextBlock.TextProperty] = new Binding(nameof(CivToggleViewModel.Label)),
            };

            var layout = new DockPanel { LastChildFill = true };
            DockPanel.SetDock(checkbox, Dock.Left);
            layout.Children.Add(checkbox);
            layout.Children.Add(label);
            Child = layout;
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);
            _toggle = DataContext as CivToggleViewModel;
        }
    }
}
