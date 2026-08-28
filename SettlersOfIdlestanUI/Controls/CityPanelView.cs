using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using SettlersOfIdlestanUI.ViewModels;

namespace SettlersOfIdlestanUI.Controls;

/// <summary>
/// Panneau de la ville selectionnee : liste des batiments avec leur cout et leur action,
/// onglets Batiments / Uniques, et pied militaire.
///
/// Le survol d'une ligne est renvoye au runtime : le tooltip detaille (~450 lignes de regles
/// de jeu) continue d'etre dessine en Skia par-dessus la carte plutot que d'etre reecrit ici.
/// </summary>
public sealed class CityPanelView : UserControl
{
    private static readonly SolidColorBrush Affordable = new(Color.FromRgb(110, 220, 110));
    private static readonly SolidColorBrush TooExpensive = new(Color.FromArgb(200, 200, 200, 200));
    private static readonly SolidColorBrush DimText = new(Color.FromArgb(200, 130, 130, 140));
    private static readonly SolidColorBrush BuildBrush = new(Color.FromRgb(21, 101, 192));
    private static readonly SolidColorBrush UpgradeBrush = new(Color.FromRgb(46, 125, 50));
    // Or du niveau max : le badge doit se lire comme une recompense, pas comme un bouton eteint.
    private static readonly SolidColorBrush MaxLevelBrush = new(Color.FromRgb(224, 179, 60));
    private static readonly SolidColorBrush MaxLevelBorder = new(Color.FromRgb(255, 226, 150));
    private static readonly SolidColorBrush MaxLevelText = new(Color.FromRgb(59, 44, 5));
    private static readonly SolidColorBrush OtherCityBrush = new(Color.FromArgb(200, 60, 55, 80));
    private static readonly SolidColorBrush TabActive = new(Color.FromArgb(240, 60, 60, 85));
    private static readonly SolidColorBrush TabInactive = new(Color.FromArgb(180, 20, 20, 30));

    public CityPanelView(CityPanelViewModel viewModel, SvgIconCache icons)
    {
        DataContext = viewModel;
        this[!IsVisibleProperty] = new Binding(nameof(CityPanelViewModel.IsVisible));

        // Sans alignement explicite le UserControl s'etire sur toute la fenetre : ses bornes
        // ne correspondraient plus au panneau visible et le hit-testing deviendrait imprevisible.
        HorizontalAlignment = HorizontalAlignment.Right;
        VerticalAlignment = VerticalAlignment.Top;

        // Ni titre ni bouton de fermeture : le panneau se replie par son onglet lateral et se
        // ferme en deselectionnant la ville. Sa barre de titre n'aurait plus rien a montrer, on
        // la masque entierement plutot que de laisser 32 px de vide au-dessus des batiments.
        var panel = new GamePanelView(title: "", onClose: null);
        panel.HeaderHost.IsVisible = false;
        // La barre de titre servait aussi de respiration sous le coin arrondi du panneau.
        panel.ContentHost.Margin = new Thickness(0, 10, 0, 0);

        var rows = new ItemsControl
        {
            [!ItemsControl.ItemsSourceProperty] = new Binding(nameof(CityPanelViewModel.Rows)),
            ItemTemplate = new FuncDataTemplate<CityBuildingRowViewModel>(
                (_, _) => new CityBuildingRow(viewModel, icons), supportsRecycling: true),
        };

        // Onglets et pied militaire restent ancres : ils ne doivent pas defiler avec la liste.
        panel.FooterHost.Children.Add(BuildTabs(viewModel));
        panel.FooterHost.Children.Add(BuildFooter());

        panel.Content = rows;
        _panel = panel;
        Content = panel;
    }

    private readonly GamePanelView _panel;

    /// <summary>Borne la hauteur défilante du panneau (cf. GamePanelView.SetMaxContentHeight).</summary>
    public void SetMaxContentHeight(double height) => _panel.SetMaxContentHeight(height);

    private static Control BuildTabs(CityPanelViewModel viewModel)
    {
        var regular = TabButton(nameof(CityPanelViewModel.RegularTabLabel), viewModel.ShowRegular);
        regular[!Button.BackgroundProperty] = new Binding(nameof(CityPanelViewModel.ShowingUnique))
        {
            Converter = new FuncValueConverter<bool, IBrush>(u => u ? TabInactive : TabActive),
        };

        var unique = TabButton(nameof(CityPanelViewModel.UniqueTabLabel), viewModel.ShowUnique);
        unique[!Button.BackgroundProperty] = new Binding(nameof(CityPanelViewModel.ShowingUnique))
        {
            Converter = new FuncValueConverter<bool, IBrush>(u => u ? TabActive : TabInactive),
        };

        var grid = new Grid
        {
            Margin = new Thickness(10, 6),
            ColumnDefinitions = new ColumnDefinitions("*,4,*"),
            [!IsVisibleProperty] = new Binding(nameof(CityPanelViewModel.HasUniqueTab)),
        };
        Grid.SetColumn(regular, 0);
        Grid.SetColumn(unique, 2);
        grid.Children.Add(regular);
        grid.Children.Add(unique);
        return grid;
    }

    private static Button TabButton(string labelPath, Action onClick)
    {
        var button = new Button
        {
            Height = 28,
            FontSize = 12,
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            BorderBrush = new SolidColorBrush(Color.FromArgb(180, 200, 200, 220)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(5),
            [!ContentProperty] = new Binding(labelPath),
        };
        // L'onglet selectionne ne se distingue que par son fond.
        button.Classes.Add(GameControlStyles.ToneButton);
        button.Click += (_, _) => onClick();
        return button;
    }

    private static Control BuildFooter() => new TextBlock
    {
        FontSize = 10,
        Foreground = TooExpensive,
        TextAlignment = TextAlignment.Center,
        Margin = new Thickness(10, 2, 10, 0),
        [!TextBlock.TextProperty] = new Binding(nameof(CityPanelViewModel.MilitaryFooter)),
    };

    /// <summary>
    /// Une ligne : case d'activation optionnelle, nom + niveau, cout, et bouton d'action.
    /// </summary>
    private sealed class CityBuildingRow : Border
    {
        private readonly CityPanelViewModel _owner;
        private readonly SvgIconCache _icons;
        private readonly ItemsControl _cost;
        private CityBuildingRowViewModel? _row;

        public CityBuildingRow(CityPanelViewModel owner, SvgIconCache icons)
        {
            _owner = owner;
            _icons = icons;

            // Fond transparent plutot qu'absent : sans geometrie remplie la ligne ne recevrait
            // pas les evenements de survol, donc pas de tooltip.
            Background = Brushes.Transparent;
            Padding = new Thickness(10, 3);

            var checkbox = new CheckBox { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) };
            checkbox[!IsVisibleProperty] = new Binding(nameof(CityBuildingRowViewModel.HasActivation));
            checkbox[!ToggleButton.IsCheckedProperty] = new Binding(nameof(CityBuildingRowViewModel.IsActivated));
            checkbox.Click += (_, _) => { if (_row != null) _owner.ToggleActivation(_row.Key); };

            var label = new TextBlock
            {
                FontSize = 15,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                [!TextBlock.TextProperty] = new Binding(nameof(CityBuildingRowViewModel.Label)),
                [!TextBlock.ForegroundProperty] = new MultiBinding
                {
                    Bindings =
                    {
                        new Binding(nameof(CityBuildingRowViewModel.IsGoToOtherCity)),
                        new Binding(nameof(CityBuildingRowViewModel.IsPermanent)),
                    },
                    Converter = new FuncMultiValueConverter<object?, IBrush>(values =>
                    {
                        var list = values.ToList();
                        bool dim = (list.Count > 0 && list[0] is true) || (list.Count > 1 && list[1] is true);
                        return dim ? DimText : Brushes.White;
                    }),
                },
            };

            _cost = new ItemsControl
            {
                Margin = new Thickness(0, 2, 0, 0),
                [!ItemsControl.ItemsSourceProperty] = new Binding(nameof(CityBuildingRowViewModel.Cost)),
                ItemsPanel = new FuncTemplate<Panel?>(() => new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 6,
                }),
                ItemTemplate = new FuncDataTemplate<CostItemViewModel>((_, _) => BuildCostItem(icons), true),
            };

            var action = new Button
            {
                MinWidth = 26,
                Height = 26,
                FontSize = 12,
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                CornerRadius = new CornerRadius(7),
                // Marge verticale a zero : le defaut Fluent (11,5,11,6) ne laisserait que 15 px
                // de haut utile dans les 26 px imposes, moins que la ligne d'un texte en 12 px —
                // les jambages de « Upgrade » etaient rognes en bas.
                Padding = new Thickness(10, 0),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                [!ContentProperty] = new Binding(nameof(CityBuildingRowViewModel.ActionLabel)),
                [!IsVisibleProperty] = new Binding(nameof(CityBuildingRowViewModel.HasActionButton)),
                [!IsEnabledProperty] = new Binding(nameof(CityBuildingRowViewModel.IsActionEnabled)),
                [!Button.BackgroundProperty] = new Binding(nameof(CityBuildingRowViewModel.Action))
                {
                    Converter = new FuncValueConverter<SettlersOfIdlestanSkia.Services.CityBuildingAction, IBrush>(a => a switch
                    {
                        SettlersOfIdlestanSkia.Services.CityBuildingAction.Upgrade => UpgradeBrush,
                        SettlersOfIdlestanSkia.Services.CityBuildingAction.GoToOtherCity => OtherCityBrush,
                        _ => BuildBrush,
                    }),
                },
            };
            // Les quatre etats du bouton se distinguent par leur couleur : sans cela, le survol
            // la remplacerait par celle du theme.
            action.Classes.Add(GameControlStyles.ToneButton);
            action.Click += (_, _) => { if (_row != null) _owner.ExecuteAction(_row); };

            // Niveau max : un badge, pas un bouton. Un bouton desactive serait repeint en gris par
            // le style « indisponible » partage, exactement comme un batiment trop cher — alors
            // qu'ici il n'y a plus rien a cliquer et que l'etat est un aboutissement.
            var maxLevel = new Border
            {
                MinWidth = 26,
                Height = 26,
                Padding = new Thickness(10, 0),
                CornerRadius = new CornerRadius(7),
                Background = MaxLevelBrush,
                BorderBrush = MaxLevelBorder,
                BorderThickness = new Thickness(1),
                VerticalAlignment = VerticalAlignment.Center,
                [!IsVisibleProperty] = new Binding(nameof(CityBuildingRowViewModel.IsMaxLevel)),
                Child = new TextBlock
                {
                    FontSize = 12,
                    FontWeight = FontWeight.SemiBold,
                    Foreground = MaxLevelText,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    [!TextBlock.TextProperty] = new Binding(nameof(CityBuildingRowViewModel.ActionLabel)),
                },
            };

            // Batiment permanent (Ascension) : meme traitement que le niveau max — un badge dore,
            // pas une fleche "aller a la ville" qui ne menerait nulle part (le batiment ne vit
            // dans aucune ville).
            var permanent = new Border
            {
                MinWidth = 26,
                Height = 26,
                Padding = new Thickness(10, 0),
                CornerRadius = new CornerRadius(7),
                Background = MaxLevelBrush,
                BorderBrush = MaxLevelBorder,
                BorderThickness = new Thickness(1),
                VerticalAlignment = VerticalAlignment.Center,
                [!IsVisibleProperty] = new Binding(nameof(CityBuildingRowViewModel.IsPermanent)),
                Child = new TextBlock
                {
                    FontSize = 12,
                    FontWeight = FontWeight.SemiBold,
                    Foreground = MaxLevelText,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    [!TextBlock.TextProperty] = new Binding(nameof(CityBuildingRowViewModel.ActionLabel)),
                },
            };

            var left = new StackPanel { Orientation = Orientation.Vertical, VerticalAlignment = VerticalAlignment.Center };
            left.Children.Add(label);
            left.Children.Add(_cost);

            var layout = new DockPanel { LastChildFill = true };
            DockPanel.SetDock(checkbox, Dock.Left);
            DockPanel.SetDock(action, Dock.Right);
            DockPanel.SetDock(maxLevel, Dock.Right);
            DockPanel.SetDock(permanent, Dock.Right);
            layout.Children.Add(checkbox);
            layout.Children.Add(action);
            layout.Children.Add(maxLevel);
            layout.Children.Add(permanent);
            layout.Children.Add(left);

            Child = layout;
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);
            _row = DataContext as CityBuildingRowViewModel;
        }

        protected override void OnPointerMoved(Avalonia.Input.PointerEventArgs e)
        {
            base.OnPointerMoved(e);
            if (_row == null) return;

            // Position relative a la fenetre : c'est le repere dans lequel la couche Skia
            // dessine, le controle carte occupant toute la surface.
            var root = TopLevel.GetTopLevel(this);
            var p = root != null ? e.GetPosition(root) : e.GetPosition(this);
            _owner.SetHovered(_row.Key, (float)p.X, (float)p.Y);
        }

        protected override void OnPointerExited(Avalonia.Input.PointerEventArgs e)
        {
            base.OnPointerExited(e);
            _owner.SetHovered(null, 0f, 0f);
        }

        private static Control BuildCostItem(SvgIconCache icons)
        {
            var icon = new Image { Width = 11, Height = 11, VerticalAlignment = VerticalAlignment.Center };
            var amount = new TextBlock
            {
                FontSize = 10,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(2, 0, 0, 0),
                [!TextBlock.TextProperty] = new Binding(nameof(CostItemViewModel.Amount)),
                [!TextBlock.ForegroundProperty] = new Binding(nameof(CostItemViewModel.CanAfford))
                {
                    Converter = new FuncValueConverter<bool, IBrush>(a => a ? Affordable : TooExpensive),
                },
            };

            var item = new StackPanel { Orientation = Orientation.Horizontal };
            item.Children.Add(icon);
            item.Children.Add(amount);

            item.DataContextChanged += (sender, _) =>
            {
                if (((Control)sender!).DataContext is CostItemViewModel vm)
                    icon.Source = icons.GetResourceIcon(vm.IconName, 11);
            };

            return item;
        }
    }
}
