using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using SettlersOfIdlestanUI.ViewModels;

namespace SettlersOfIdlestanUI.Controls;

/// <summary>
/// Panneau de reglages, partage par le popup en jeu et l'ecran-titre — un seul gabarit pour les
/// deux, la ou le rendu Skia avait deja un SettingsContentPanel commun.
///
/// Une ligne n'affiche qu'un controle selon sa nature : bascule, choix exclusif, curseur ou
/// champ de texte.
/// </summary>
public sealed class SettingsPanelView : UserControl
{
    private static readonly SolidColorBrush Label = new(Color.FromRgb(220, 220, 230));
    private static readonly SolidColorBrush Muted = new(Color.FromRgb(120, 120, 132));
    private static readonly SolidColorBrush ChoiceActive = new(Color.FromRgb(60, 100, 160));
    private static readonly SolidColorBrush ChoiceInactive = new(Color.FromRgb(45, 45, 58));
    private static readonly SolidColorBrush TabActive = new(Color.FromRgb(45, 90, 145));
    private static readonly SolidColorBrush TabInactive = new(Color.FromArgb(120, 40, 40, 52));

    /// <summary>
    /// Largeur du panneau, fixe et commune aux deux hotes (popup en jeu, ecran-titre).
    ///
    /// Sans largeur imposee, le panneau se dimensionne sur sa ligne la plus longue : changer de
    /// langue change la largeur, et tous les debuts de ligne se decalent. Elle est choisie assez
    /// large pour qu'il reste un blanc entre le libelle le plus long et ses boutons.
    /// </summary>
    public const double ContentWidth = 500;

    /// <summary>
    /// Hauteur de la zone des lignes, la meme pour tous les onglets. Fixe et non pas ajustee au
    /// contenu : sans cela le panneau — donc le popup qui l'entoure — changeait de taille a
    /// chaque changement d'onglet, et la barre d'onglets sautait sous le curseur.
    ///
    /// <para>Mesuree sur l'onglet le plus charge (Affichage, 8 lignes) : 370 px. La marge est
    /// mince a dessein, un onglet ne doit pas s'ouvrir sur une moitie de vide. Les deux lignes du
    /// mode debogage depassent et defilent — elles n'existent pas dans le jeu livre.
    /// <c>SettingsPanelSizeTests</c> echoue si une ligne ajoutee fait deborder un onglet.</para>
    /// </summary>
    public const double RowsHeight = 380;

    private const double TabButtonHeight = 30;
    private const double TabBarGap = 14;

    /// <summary>Hauteur de la barre d'onglets, sa marge basse comprise.</summary>
    public const double TabBarHeight = TabButtonHeight + TabBarGap;

    /// <summary>
    /// Bande reservee a l'ascenseur, a droite des lignes. L'ascenseur de Fluent se pose
    /// <b>par-dessus</b> le contenu, au bord droit de sa zone : sans cette bande il recouvrait
    /// les controles des reglages, tous alignes a droite.
    ///
    /// <para>Sa valeur est la largeur de l'ascenseur de Fluent ; en deca, il mordrait encore sur
    /// les controles. Elle s'ajoute a la largeur du panneau au lieu de la deborder : un enfant
    /// qui sort des limites du panneau n'est pas dessine — l'ascenseur, pousse dehors, avait
    /// purement disparu alors que la molette marchait toujours.</para>
    /// </summary>
    private const double ScrollbarGutter = 16;

    /// <summary>
    /// Largeur totale du panneau, bande de l'ascenseur comprise. C'est elle que doit suivre
    /// l'hote qui encadre le panneau (voir <see cref="SettingsPopupView"/>).
    /// </summary>
    public const double TotalWidth = ContentWidth + ScrollbarGutter;

    public SettingsPanelView(SettingsPanelViewModel viewModel)
    {
        DataContext = viewModel;
        Width = TotalWidth;
        HorizontalAlignment = HorizontalAlignment.Center;

        var rows = new ItemsControl
        {
            // Les lignes s'arretent avant la bande de l'ascenseur : elles gardent ContentWidth,
            // et lui occupe seul ce qui reste a droite.
            Margin = new Thickness(0, 0, ScrollbarGutter, 0),
            [!ItemsControl.ItemsSourceProperty] = new Binding(nameof(SettingsPanelViewModel.Rows)),
            ItemsPanel = new FuncTemplate<Panel?>(() => new StackPanel { Spacing = 10 }),
            ItemTemplate = new FuncDataTemplate<SettingRowViewModel>(
                (_, _) => new SettingRow(viewModel), supportsRecycling: true),
        };

        // Seules les lignes defilent : la barre d'onglets est ancree au-dessus de la zone de
        // defilement, donc immobile, et reste atteignable meme si un onglet deborde.
        var scroll = new ScrollViewer
        {
            Height = RowsHeight,
            VerticalAlignment = VerticalAlignment.Top,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = rows,
        };

        var tabBar = BuildTabBar(viewModel);
        DockPanel.SetDock(tabBar, Dock.Top);

        var layout = new DockPanel { LastChildFill = true };
        layout.Children.Add(tabBar);
        layout.Children.Add(scroll);

        Content = layout;
    }

    /// <summary>
    /// Barre d'onglets. Absente d'un panneau d'un seul tenant (instantane sans onglets) : la
    /// place qu'elle prend ne se justifie que s'il y a un choix a faire.
    /// </summary>
    private static Control BuildTabBar(SettingsPanelViewModel owner) => new ItemsControl
    {
        Height = TabButtonHeight,
        Margin = new Thickness(0, 0, 0, TabBarGap),
        HorizontalAlignment = HorizontalAlignment.Center,
        [!IsVisibleProperty] = new Binding(nameof(SettingsPanelViewModel.HasTabs)),
        [!ItemsControl.ItemsSourceProperty] = new Binding(nameof(SettingsPanelViewModel.Tabs)),
        ItemsPanel = new FuncTemplate<Panel?>(() => new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
        }),
        ItemTemplate = new FuncDataTemplate<SettingsTabViewModel>((_, _) => BuildTabButton(owner), true),
    };

    private static Control BuildTabButton(SettingsPanelViewModel owner)
    {
        SettingsTabViewModel? tab = null;

        var button = new Button
        {
            MinWidth = 110,
            Height = TabButtonHeight,
            FontSize = 13,
            Padding = new Thickness(10, 0),
            Foreground = Brushes.White,
            CornerRadius = new CornerRadius(5),
            BorderThickness = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            [!ContentProperty] = new Binding(nameof(SettingsTabViewModel.Label)),
            [!BackgroundProperty] = new Binding(nameof(SettingsTabViewModel.IsActive))
            {
                Converter = new FuncValueConverter<bool, IBrush>(a => a ? TabActive : TabInactive),
            },
        };
        button.Classes.Add(GameControlStyles.ToneButton);
        button.DataContextChanged += (_, _) => tab = button.DataContext as SettingsTabViewModel;
        button.Click += (_, _) => { if (tab != null) owner.SelectTab(tab); };
        return button;
    }

    /// <summary>Une ligne : libelle a gauche, controle a droite selon la nature du reglage.</summary>
    private sealed class SettingRow : Border
    {
        private readonly SettingsPanelViewModel _owner;
        private SettingRowViewModel? _row;

        public SettingRow(SettingsPanelViewModel owner)
        {
            _owner = owner;

            Background = Brushes.Transparent;
            Padding = new Thickness(0, 2);

            var label = new TextBlock
            {
                FontSize = 13,
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                // Gouttiere : le libelle occupe toute la place restante, sans cette marge il
                // viendrait toucher le controle des qu'il est un peu long.
                Margin = new Thickness(0, 0, 16, 0),
                [!TextBlock.TextProperty] = new Binding(nameof(SettingRowViewModel.Label)),
                [!TextBlock.ForegroundProperty] = new Binding(nameof(SettingRowViewModel.IsEnabled))
                {
                    Converter = new FuncValueConverter<bool, IBrush>(e => e ? Label : Muted),
                },
            };

            var controls = new Panel { HorizontalAlignment = HorizontalAlignment.Right };
            controls.Children.Add(BuildToggle());
            controls.Children.Add(BuildChoices());
            controls.Children.Add(BuildSlider());
            controls.Children.Add(BuildTextInput());

            var layout = new DockPanel { LastChildFill = true };
            DockPanel.SetDock(controls, Dock.Right);
            layout.Children.Add(controls);
            layout.Children.Add(label);

            Child = layout;
        }

        private Control BuildToggle()
        {
            var toggle = new CheckBox
            {
                VerticalAlignment = VerticalAlignment.Center,
                [!IsVisibleProperty] = new Binding(nameof(SettingRowViewModel.IsToggle)),
                [!IsEnabledProperty] = new Binding(nameof(SettingRowViewModel.IsEnabled)),
                [!ToggleButton.IsCheckedProperty] = new Binding(nameof(SettingRowViewModel.ToggleValue)),
            };
            toggle.Click += (_, _) => { if (_row != null) _owner.Toggle(_row); };
            return toggle;
        }

        private Control BuildChoices() => new ItemsControl
        {
            VerticalAlignment = VerticalAlignment.Center,
            [!IsVisibleProperty] = new Binding(nameof(SettingRowViewModel.IsChoice)),
            [!ItemsControl.ItemsSourceProperty] = new Binding(nameof(SettingRowViewModel.Choices)),
            ItemsPanel = new FuncTemplate<Panel?>(() => new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
            }),
            ItemTemplate = new FuncDataTemplate<SettingChoiceViewModel>((_, _) => BuildChoiceButton(), true),
        };

        private Control BuildChoiceButton()
        {
            SettingChoiceViewModel? choice = null;

            var button = new Button
            {
                MinWidth = 90,
                Height = 26,
                FontSize = 12,
                Padding = new Thickness(8, 0),
                Foreground = Brushes.White,
                CornerRadius = new CornerRadius(4),
                BorderThickness = new Thickness(0),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                [!ContentProperty] = new Binding(nameof(SettingChoiceViewModel.Label)),
                [!BackgroundProperty] = new Binding(nameof(SettingChoiceViewModel.IsSelected))
                {
                    Converter = new FuncValueConverter<bool, IBrush>(s => s ? ChoiceActive : ChoiceInactive),
                },
            };
            // L'option retenue ne se distingue que par son fond.
            button.Classes.Add(GameControlStyles.ToneButton);
            button.DataContextChanged += (_, _) => choice = button.DataContext as SettingChoiceViewModel;
            button.Click += (_, _) => { if (_row != null && choice != null) _owner.SelectChoice(_row, choice); };
            return button;
        }

        private Control BuildSlider()
        {
            var slider = new Slider
            {
                Width = 160,
                VerticalAlignment = VerticalAlignment.Center,
                [!Slider.MinimumProperty] = new Binding(nameof(SettingRowViewModel.SliderMin)),
                [!Slider.MaximumProperty] = new Binding(nameof(SettingRowViewModel.SliderMax)),
                [!Slider.ValueProperty] = new Binding(nameof(SettingRowViewModel.SliderValue)),
                // Comme la bascule : un curseur sans objet (le volume quand le son est coupe) se
                // grise et ne bouge plus. Sans cela il se laissait tirer, puis revenait a sa
                // valeur au tick suivant — l'instantane, lui, refuse deja la nouvelle valeur.
                [!IsEnabledProperty] = new Binding(nameof(SettingRowViewModel.IsEnabled)),
                TickFrequency = 0.1,
                IsSnapToTickEnabled = true,
            };

            // Applique au relachement plutot qu'a chaque pas : l'echelle d'interface fait
            // relayouter tout le jeu, la suivre en continu pendant le glissement serait saccade.
            slider.AddHandler(PointerReleasedEvent, (_, _) =>
            {
                if (_row != null) _owner.SetSlider(_row, slider.Value);
            }, RoutingStrategies.Tunnel);

            var text = new TextBlock
            {
                FontSize = 12,
                Foreground = Label,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0),
                Width = 40,
                [!TextBlock.TextProperty] = new Binding(nameof(SettingRowViewModel.SliderText)),
            };

            var stack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                [!IsVisibleProperty] = new Binding(nameof(SettingRowViewModel.IsSlider)),
            };
            stack.Children.Add(slider);
            stack.Children.Add(text);
            return stack;
        }

        private Control BuildTextInput()
        {
            var box = new TextBox
            {
                Width = 160,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                [!IsVisibleProperty] = new Binding(nameof(SettingRowViewModel.IsTextInput)),
                [!TextBox.TextProperty] = new Binding(nameof(SettingRowViewModel.TextValue)),
            };

            // Applique a la validation, comme le champ Skia qui attendait Entree.
            box.KeyDown += (_, e) =>
            {
                if (e.Key != Key.Enter || _row == null) return;
                e.Handled = true;
                _owner.SetText(_row, box.Text ?? "");
            };
            return box;
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);
            _row = DataContext as SettingRowViewModel;
        }
    }
}
