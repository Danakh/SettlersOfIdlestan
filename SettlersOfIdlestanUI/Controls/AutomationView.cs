using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Layout;
using Avalonia.Media;
using SettlersOfIdlestanUI.ViewModels;

namespace SettlersOfIdlestanUI.Controls;

/// <summary>
/// Onglet plein ecran de l'automatisation : deux colonnes de cartes, chacune avec sa bascule, sa
/// description, une note en infobulle et une case a cocher qui l'epingle au panneau civilisation.
///
/// Une ligne verrouillee n'a pas de bascule : sa description porte la condition de deblocage.
/// </summary>
public sealed class AutomationView : UserControl
{
    private static readonly SolidColorBrush PanelBackground = new(Color.FromArgb(240, 18, 18, 24));
    private static readonly SolidColorBrush Card = new(Color.FromArgb(220, 30, 30, 40));
    private static readonly SolidColorBrush CardBorder = new(Color.FromRgb(60, 60, 80));
    private static readonly SolidColorBrush Accent = new(Color.FromRgb(255, 215, 0));
    private static readonly SolidColorBrush NameText = new(Color.FromRgb(230, 230, 240));
    private static readonly SolidColorBrush Desc = new(Color.FromRgb(150, 150, 165));
    private static readonly SolidColorBrush Muted = new(Color.FromRgb(110, 110, 125));
    private static readonly SolidColorBrush Summary = new(Color.FromRgb(120, 175, 120));

    public AutomationView(AutomationViewModel viewModel)
    {
        DataContext = viewModel;
        this[!IsVisibleProperty] = new Binding(nameof(AutomationViewModel.IsVisible));

        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;

        var title = new TextBlock
        {
            FontSize = 17,
            FontWeight = FontWeight.Bold,
            Foreground = Accent,
            VerticalAlignment = VerticalAlignment.Center,
            [!TextBlock.TextProperty] = new Binding(nameof(AutomationViewModel.Title)),
        };

        // Interrupteur global : les reglages par ligne restent stockes tels quels et reprennent
        // effet des sa reactivation.
        var globalToggle = new CheckBox
        {
            VerticalAlignment = VerticalAlignment.Center,
            // Seule case a cocher de l'onglet qui porte un libelle : sans couleur explicite elle
            // herite du noir du theme, illisible sur le fond sombre. Meme blanc que le nom des
            // lignes ("Routes (Surface)").
            Foreground = NameText,
            [!ToggleButton.IsCheckedProperty] = new Binding(nameof(AutomationViewModel.GlobalToggleOn)),
            [!ContentProperty] = new Binding(nameof(AutomationViewModel.GlobalToggleLabel)),
        };
        globalToggle.Click += (_, _) => viewModel.ToggleGlobal();

        var header = new DockPanel { LastChildFill = false, Margin = new Thickness(0, 0, 0, 14) };
        DockPanel.SetDock(title, Dock.Left);
        DockPanel.SetDock(globalToggle, Dock.Right);
        header.Children.Add(title);
        header.Children.Add(globalToggle);

        var columns = new Grid { ColumnDefinitions = new ColumnDefinitions("*,12,*") };
        // La barre de presets n'a de sens qu'a cote de "Constructions", seule section de la
        // colonne gauche : pas besoin de la retrouver par cle, ce template n'est jamais instancie
        // pour la colonne droite.
        var left = BuildColumn(viewModel, nameof(AutomationViewModel.LeftColumn), showPresetBar: true);
        var right = BuildColumn(viewModel, nameof(AutomationViewModel.RightColumn), showPresetBar: false);
        Grid.SetColumn(left, 0);
        Grid.SetColumn(right, 2);
        columns.Children.Add(left);
        columns.Children.Add(right);

        var stack = new StackPanel { Orientation = Orientation.Vertical };
        stack.Children.Add(header);
        stack.Children.Add(columns);

        Content = new Border
        {
            Background = PanelBackground,
            Child = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Padding = new Thickness(20),
                Content = new Border { MaxWidth = 660, HorizontalAlignment = HorizontalAlignment.Center, Child = stack },
            },
        };
    }

    private static Control BuildColumn(AutomationViewModel viewModel, string path, bool showPresetBar) => new ItemsControl
    {
        [!ItemsControl.ItemsSourceProperty] = new Binding(path),
        ItemsPanel = new FuncTemplate<Panel?>(() => new StackPanel { Spacing = 12 }),
        ItemTemplate = new FuncDataTemplate<AutomationSectionViewModel>(
            (_, _) => BuildSection(viewModel, showPresetBar), supportsRecycling: true),
    };

    private static Control BuildSection(AutomationViewModel viewModel, bool showPresetBar)
    {
        var header = new TextBlock
        {
            FontSize = 13,
            FontWeight = FontWeight.Bold,
            Foreground = Accent,
            VerticalAlignment = VerticalAlignment.Center,
            [!TextBlock.TextProperty] = new Binding(nameof(AutomationSectionViewModel.Header)),
        };

        var headerRow = new DockPanel { LastChildFill = false, Margin = new Thickness(0, 0, 0, 8) };
        DockPanel.SetDock(header, Dock.Left);
        headerRow.Children.Add(header);
        if (showPresetBar)
        {
            var presetBar = BuildPresetBar(viewModel);
            presetBar[!IsVisibleProperty] = new Binding(nameof(AutomationViewModel.ShowPresetBar)) { Source = viewModel };
            DockPanel.SetDock(presetBar, Dock.Right);
            headerRow.Children.Add(presetBar);
        }

        var rows = new ItemsControl
        {
            [!ItemsControl.ItemsSourceProperty] = new Binding(nameof(AutomationSectionViewModel.Rows)),
            ItemsPanel = new FuncTemplate<Panel?>(() => new StackPanel { Spacing = 8 }),
            ItemTemplate = new FuncDataTemplate<AutomationRowViewModel>(
                (_, _) => new AutomationCard(viewModel), supportsRecycling: true),
        };

        var stack = new StackPanel { Orientation = Orientation.Vertical };
        stack.Children.Add(headerRow);
        stack.Children.Add(rows);
        return stack;
    }

    /// <summary>Boutons 1/2/3 (preset actif en surbrillance) + bouton "Changer" ouvrant le popup
    /// d'edition. Visible uniquement une fois TechnologyId.AutomationPreset debloquee. Lie
    /// directement au AutomationViewModel du panneau (pas a la section) : une seule barre existe,
    /// toujours a cote de "Constructions".</summary>
    private static Control BuildPresetBar(AutomationViewModel viewModel)
    {
        static Button PresetButton(int n, AutomationViewModel owner)
        {
            var button = new Button
            {
                Width = 22,
                Height = 22,
                Padding = new Thickness(0),
                FontSize = 12,
                FontWeight = FontWeight.Bold,
                Content = n.ToString(),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                CornerRadius = new CornerRadius(4),
                [!BackgroundProperty] = new Binding(nameof(AutomationViewModel.ActivePreset))
                {
                    Source = owner,
                    Converter = new FuncValueConverter<int, IBrush>(active => active == n ? Accent : Card),
                },
                [!ForegroundProperty] = new Binding(nameof(AutomationViewModel.ActivePreset))
                {
                    Source = owner,
                    Converter = new FuncValueConverter<int, IBrush>(active => active == n ? new SolidColorBrush(Colors.Black) : NameText),
                },
            };
            button.Classes.Add(GameControlStyles.ToneButton);
            button.Click += (_, _) => owner.SelectPreset(n);
            return button;
        }

        var changeButton = new Button
        {
            Height = 22,
            Padding = new Thickness(8, 0),
            FontSize = 11,
            [!ContentProperty] = new Binding(nameof(AutomationViewModel.PresetChangeButtonLabel)) { Source = viewModel },
            CornerRadius = new CornerRadius(4),
        };
        changeButton.Classes.Add(GameControlStyles.ToneButton);
        changeButton.Click += (_, _) => viewModel.OpenPresetPopup();

        var bar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
        bar.Children.Add(PresetButton(1, viewModel));
        bar.Children.Add(PresetButton(2, viewModel));
        bar.Children.Add(PresetButton(3, viewModel));
        bar.Children.Add(changeButton);
        return bar;
    }

    /// <summary>Une carte : bascule, nom, description, resume de construction, case d'epinglage.</summary>
    private sealed class AutomationCard : Border
    {
        private readonly AutomationViewModel _owner;
        private AutomationRowViewModel? _row;

        public AutomationCard(AutomationViewModel owner)
        {
            _owner = owner;

            Padding = new Thickness(12, 10);
            CornerRadius = new CornerRadius(6);
            BorderThickness = new Thickness(1);
            Background = Card;
            BorderBrush = CardBorder;
            // La note s'affiche au survol de toute la carte, comme dans le rendu Skia.
            this[!ToolTip.TipProperty] = new Binding(nameof(AutomationRowViewModel.Note));

            // Case coloree par famille plutot qu'un CheckBox neutre du theme (voir
            // CategoryToggleSquare) : construction / comportement / activation se distinguent
            // d'un coup d'oeil, meme dans cette liste qui les regroupe deja par section.
            var toggle = new CategoryToggleSquare
            {
                Margin = new Thickness(0, 0, 10, 0),
                [!IsVisibleProperty] = new Binding(nameof(AutomationRowViewModel.HasToggle)),
            };
            toggle.Click += (_, _) => { if (_row != null) _owner.Toggle(_row); };

            // Case discrete, calee dans le coin haut-droit de la carte. Fluent impose une taille
            // fixe a la boite : on la reduit par une mise a l'echelle ancree en haut a droite,
            // apres avoir annule les tailles minimales du theme qui decalaient la boite dans son
            // emplacement. Les marges negatives ramenent la case au tiers du rembourrage de la
            // carte (12 a droite, 10 en haut).
            var pin = new CheckBox
            {
                VerticalAlignment = VerticalAlignment.Top,
                MinWidth = 0,
                MinHeight = 0,
                Padding = new Thickness(0),
                RenderTransform = new ScaleTransform(0.8, 0.8),
                RenderTransformOrigin = new RelativePoint(1, 0, RelativeUnit.Relative),
                Margin = new Thickness(6, -7, -8, 0),
                [!IsVisibleProperty] = new Binding(nameof(AutomationRowViewModel.CanPin)),
                [!ToggleButton.IsCheckedProperty] = new Binding(nameof(AutomationRowViewModel.IsPinned)),
                [!ToolTip.TipProperty] = new Binding(nameof(AutomationViewModel.PinTooltip))
                {
                    Source = owner,
                },
            };
            pin.Click += (_, _) => { if (_row != null) _owner.TogglePin(_row); };

            var name = new TextBlock
            {
                FontSize = 13,
                FontWeight = FontWeight.Bold,
                TextWrapping = TextWrapping.Wrap,
                [!TextBlock.TextProperty] = new Binding(nameof(AutomationRowViewModel.Name)),
                [!TextBlock.ForegroundProperty] = new Binding(nameof(AutomationRowViewModel.IsLocked))
                {
                    Converter = new FuncValueConverter<bool, IBrush>(l => l ? Muted : NameText),
                },
            };

            var desc = new TextBlock
            {
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 3, 0, 0),
                [!TextBlock.TextProperty] = new Binding(nameof(AutomationRowViewModel.Description)),
                [!TextBlock.ForegroundProperty] = new Binding(nameof(AutomationRowViewModel.IsLocked))
                {
                    Converter = new FuncValueConverter<bool, IBrush>(l => l ? Muted : Desc),
                },
            };

            // Etat de construction par type concerne, sous un filet separateur.
            var summary = new ItemsControl
            {
                Margin = new Thickness(0, 8, 0, 0),
                [!ItemsControl.ItemsSourceProperty] = new Binding(nameof(AutomationRowViewModel.SummaryLines)),
                ItemTemplate = new FuncDataTemplate<string>((_, _) => new TextBlock
                {
                    FontSize = 10,
                    Foreground = Summary,
                    [!TextBlock.TextProperty] = new Binding(),
                }, supportsRecycling: true),
            };

            var text = new StackPanel { Orientation = Orientation.Vertical, VerticalAlignment = VerticalAlignment.Center };
            text.Children.Add(name);
            text.Children.Add(desc);
            text.Children.Add(summary);

            var layout = new DockPanel { LastChildFill = true };
            DockPanel.SetDock(toggle, Dock.Left);
            DockPanel.SetDock(pin, Dock.Right);
            layout.Children.Add(toggle);
            layout.Children.Add(pin);
            layout.Children.Add(text);

            Child = layout;
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);
            _row = DataContext as AutomationRowViewModel;
        }
    }
}
