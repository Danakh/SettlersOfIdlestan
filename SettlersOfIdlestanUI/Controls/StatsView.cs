using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Layout;
using Avalonia.Media;
using SettlersOfIdlestanUI.ViewModels;
using SkiaLayer = SettlersOfIdlestanSkia.Services;

namespace SettlersOfIdlestanUI.Controls;

/// <summary>
/// Onglet plein ecran des statistiques : une barre de sous-onglets, puis des sections de cartes,
/// chaque carte etant une grille de cellules « libelle + valeur ».
///
/// Le defilement est un ScrollViewer natif : l'ancien rendu Skia calculait lui-meme la hauteur
/// totale du contenu, la piste et le curseur de l'ascenseur, et gerait le glisser du curseur.
/// </summary>
public sealed class StatsView : UserControl
{
    private static readonly SolidColorBrush Background = new(Color.FromArgb(240, 18, 18, 24));
    private static readonly SolidColorBrush CardBackground = new(Color.FromArgb(220, 30, 30, 40));
    private static readonly SolidColorBrush CardBorder = new(Color.FromRgb(80, 80, 100));
    private static readonly SolidColorBrush CurrentBorder = new(Colors.Gold);
    private static readonly SolidColorBrush Accent = new(Color.FromRgb(255, 215, 0));
    private static readonly SolidColorBrush Muted = new(Color.FromRgb(180, 180, 190));
    private static readonly SolidColorBrush Label = new(Color.FromRgb(140, 180, 220));
    private static readonly SolidColorBrush SubTabActive = new(Color.FromRgb(60, 100, 160));

    public StatsView(StatsViewModel viewModel)
    {
        DataContext = viewModel;
        this[!IsVisibleProperty] = new Binding(nameof(StatsViewModel.IsVisible));

        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;

        var subTabs = new ItemsControl
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 14),
            [!ItemsControl.ItemsSourceProperty] = new Binding(nameof(StatsViewModel.SubTabs)),
            ItemsPanel = new FuncTemplate<Panel?>(() => new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
            }),
            ItemTemplate = new FuncDataTemplate<StatsSubTabViewModel>(
                (_, _) => BuildSubTab(viewModel), supportsRecycling: true),
        };

        var sections = new ItemsControl
        {
            [!ItemsControl.ItemsSourceProperty] = new Binding(nameof(StatsViewModel.Sections)),
            ItemsPanel = new FuncTemplate<Panel?>(() => new StackPanel { Spacing = 18 }),
            ItemTemplate = new FuncDataTemplate<SkiaLayer.StatSectionSnapshot>(
                (_, _) => BuildSection(), supportsRecycling: true),
        };

        var stack = new StackPanel { Orientation = Orientation.Vertical };
        stack.Children.Add(subTabs);
        stack.Children.Add(sections);

        Content = new Border
        {
            Background = Background,
            Child = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Padding = new Thickness(20),
                Content = new Border { MaxWidth = 700, HorizontalAlignment = HorizontalAlignment.Center, Child = stack },
            },
        };
    }

    private static Control BuildSubTab(StatsViewModel owner)
    {
        StatsSubTabViewModel? model = null;

        var button = new Button
        {
            Width = 140,
            Height = 28,
            FontSize = 13,
            FontWeight = FontWeight.Bold,
            Foreground = Brushes.White,
            CornerRadius = new CornerRadius(5),
            BorderThickness = new Thickness(1),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            [!ContentProperty] = new Binding(nameof(StatsSubTabViewModel.Label)),
            [!BackgroundProperty] = new Binding(nameof(StatsSubTabViewModel.IsActive))
            {
                Converter = new FuncValueConverter<bool, IBrush>(a => a ? SubTabActive : CardBackground),
            },
            [!BorderBrushProperty] = new Binding(nameof(StatsSubTabViewModel.IsActive))
            {
                Converter = new FuncValueConverter<bool, IBrush>(a => a ? CurrentBorder : CardBorder),
            },
        };

        // Le sous-onglet actif ne se distingue que par son fond.
        button.Classes.Add(GameControlStyles.ToneButton);
        button.DataContextChanged += (_, _) => model = button.DataContext as StatsSubTabViewModel;
        button.Click += (_, _) => { if (model != null) owner.SelectSubTab(model); };

        return button;
    }

    private static Control BuildSection()
    {
        var title = new TextBlock
        {
            FontSize = 17,
            FontWeight = FontWeight.Bold,
            Margin = new Thickness(0, 0, 0, 10),
            [!TextBlock.TextProperty] = new Binding(nameof(SkiaLayer.StatSectionSnapshot.Title)),
            [!TextBlock.ForegroundProperty] = new Binding(nameof(SkiaLayer.StatSectionSnapshot.IsAccent))
            {
                Converter = new FuncValueConverter<bool, IBrush>(a => a ? Accent : Brushes.White),
            },
        };

        var empty = new TextBlock
        {
            FontSize = 13,
            Foreground = Muted,
            [!TextBlock.TextProperty] = new Binding(nameof(SkiaLayer.StatSectionSnapshot.EmptyMessage)),
            [!IsVisibleProperty] = new Binding(nameof(SkiaLayer.StatSectionSnapshot.EmptyMessage))
            {
                Converter = new FuncValueConverter<string?, bool>(m => !string.IsNullOrEmpty(m)),
            },
        };

        var cards = new ItemsControl
        {
            [!ItemsControl.ItemsSourceProperty] = new Binding(nameof(SkiaLayer.StatSectionSnapshot.Cards)),
            ItemsPanel = new FuncTemplate<Panel?>(() => new StackPanel { Spacing = 8 }),
            ItemTemplate = new FuncDataTemplate<SkiaLayer.StatCardSnapshot>(
                (_, _) => new StatCard(), supportsRecycling: true),
        };

        var stack = new StackPanel { Orientation = Orientation.Vertical };
        stack.Children.Add(title);
        stack.Children.Add(empty);
        stack.Children.Add(cards);
        return stack;
    }

    /// <summary>
    /// Une carte. La grille de cellules et les lignes de texte simple s'excluent : une carte est
    /// soit un tableau de statistiques, soit une liste (les races jouees).
    /// </summary>
    private sealed class StatCard : Border
    {
        private readonly UniformGrid _cells;
        private readonly StackPanel _rows;

        public StatCard()
        {
            Padding = new Thickness(12);
            CornerRadius = new CornerRadius(8);
            BorderThickness = new Thickness(1);
            Background = CardBackground;

            _cells = new UniformGrid();
            _rows = new StackPanel { Orientation = Orientation.Vertical, Spacing = 4 };

            var content = new StackPanel { Orientation = Orientation.Vertical };
            content.Children.Add(_cells);
            content.Children.Add(_rows);
            Child = content;
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);
            if (DataContext is not SkiaLayer.StatCardSnapshot card) return;

            BorderBrush = card.IsCurrent ? CurrentBorder : CardBorder;

            _cells.Columns = Math.Max(1, card.Columns);
            _cells.Children.Clear();
            foreach (var cell in card.Cells) _cells.Children.Add(BuildCell(cell));
            _cells.IsVisible = card.Cells.Count > 0;

            _rows.Children.Clear();
            foreach (var row in card.TextRows)
                _rows.Children.Add(new TextBlock { FontSize = 13, Foreground = Brushes.White, Text = row });
            _rows.IsVisible = card.TextRows.Count > 0;
        }

        private static Control BuildCell(SkiaLayer.StatCellSnapshot cell)
        {
            var panel = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(0, 0, 8, 6) };
            panel.Children.Add(new TextBlock { FontSize = 11, Foreground = Label, Text = cell.Label });
            panel.Children.Add(new TextBlock
            {
                FontSize = 13,
                FontWeight = FontWeight.Bold,
                Foreground = Brushes.White,
                Text = cell.Value,
            });
            return panel;
        }
    }
}
