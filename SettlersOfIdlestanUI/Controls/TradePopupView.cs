using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Layout;
using Avalonia.Media;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestanUI.ViewModels;

namespace SettlersOfIdlestanUI.Controls;

/// <summary>
/// Popup de commerce : deux colonnes vendre/acheter, un historique, et un multiplicateur de
/// paquet en pied.
///
/// Comme les modales, un voile plein ecran rend le popup bloquant — c'est lui qui empeche les
/// clics d'atteindre la carte, la ou le rendu Skia comparait le point au rectangle du popup.
///
/// Les boutons indisponibles restent cliquables, seule leur apparence change : c'est ce que
/// faisait le rendu Skia, et c'est ce qui leur permet de garder l'infobulle qui explique le
/// blocage — un controle desactive ne recoit plus le pointeur.
/// </summary>
public sealed class TradePopupView : UserControl
{
    private static readonly SolidColorBrush Veil = new(Color.FromArgb(120, 0, 0, 0));
    private static readonly SolidColorBrush PanelBackground = new(Color.FromArgb(245, 24, 24, 30));
    private static readonly SolidColorBrush Border_ = new(Colors.Gold);
    private static readonly SolidColorBrush RowFill = new(Color.FromArgb(245, 55, 55, 65));
    private static readonly SolidColorBrush RowDisabled = new(Color.FromArgb(220, 70, 70, 76));
    private static readonly SolidColorBrush RowBorder = new(Color.FromArgb(100, 255, 255, 255));
    private static readonly SolidColorBrush ActionEnabled = new(Color.FromRgb(46, 125, 50));
    private static readonly SolidColorBrush ActionDisabled = new(Color.FromRgb(90, 90, 96));
    private static readonly SolidColorBrush Subtle = new(Color.FromRgb(180, 180, 190));
    private static readonly SolidColorBrush MaxStock = new(Color.FromRgb(220, 60, 60));
    private static readonly SolidColorBrush Gain = new(Color.FromRgb(90, 200, 90));
    private static readonly SolidColorBrush Loss = new(Color.FromRgb(220, 130, 90));
    private static readonly SolidColorBrush TabActive = new(Color.FromRgb(60, 100, 160));
    private static readonly SolidColorBrush TabInactive = new(Color.FromRgb(38, 38, 50));
    private static readonly SolidColorBrush MultTemporary = new(Color.FromRgb(140, 80, 0));
    private static readonly SolidColorBrush CloseButton = new(Color.FromArgb(230, 90, 50, 50));

    public TradePopupView(TradePopupViewModel viewModel, SvgIconCache icons)
    {
        DataContext = viewModel;
        this[!IsVisibleProperty] = new Binding(nameof(TradePopupViewModel.IsOpen));

        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;

        var title = new TextBlock
        {
            FontSize = 20,
            FontWeight = FontWeight.Bold,
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            [!TextBlock.TextProperty] = new Binding(nameof(TradePopupViewModel.Title)),
        };

        var tabs = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 10, 0, 10),
        };
        tabs.Children.Add(TabButton(nameof(TradePopupViewModel.TradeTabLabel),
            nameof(TradePopupViewModel.ShowingTrade), viewModel.ShowTrade));
        tabs.Children.Add(TabButton(nameof(TradePopupViewModel.HistoryTabLabel),
            nameof(TradePopupViewModel.ShowingHistory), viewModel.ShowHistory));

        // ── Onglet d'echange : deux colonnes ──
        var columns = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,16,*"),
            [!IsVisibleProperty] = new Binding(nameof(TradePopupViewModel.ShowingTrade)),
        };
        var sell = BuildColumn(viewModel, icons, nameof(TradePopupViewModel.SellHeader),
            nameof(TradePopupViewModel.SellRows), isSell: true);
        var buy = BuildColumn(viewModel, icons, nameof(TradePopupViewModel.BuyHeader),
            nameof(TradePopupViewModel.BuyRows), isSell: false);
        Grid.SetColumn(sell, 0);
        Grid.SetColumn(buy, 2);
        columns.Children.Add(sell);
        columns.Children.Add(buy);

        // ── Onglet historique ──
        var historyEmpty = new TextBlock
        {
            FontSize = 13,
            Foreground = Subtle,
            [!TextBlock.TextProperty] = new Binding(nameof(TradePopupViewModel.HistoryEmptyMessage)),
            [!IsVisibleProperty] = new Binding(nameof(TradePopupViewModel.IsHistoryEmpty)),
        };

        var historyList = new ItemsControl
        {
            [!ItemsControl.ItemsSourceProperty] = new Binding(nameof(TradePopupViewModel.HistoryEntries)),
            ItemsPanel = new FuncTemplate<Panel?>(() => new StackPanel { Spacing = 4 }),
            ItemTemplate = new FuncDataTemplate<TradeHistoryEntryViewModel>(
                (_, _) => BuildHistoryRow(icons), supportsRecycling: true),
        };

        var history = new StackPanel
        {
            Orientation = Orientation.Vertical,
            [!IsVisibleProperty] = new Binding(nameof(TradePopupViewModel.ShowingHistory)),
        };
        history.Children.Add(historyEmpty);
        history.Children.Add(historyList);

        var content = new StackPanel { Orientation = Orientation.Vertical };
        content.Children.Add(columns);
        content.Children.Add(history);

        var scroll = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MaxHeight = 380,
            Content = content,
        };

        // ── Pied : or disponible et multiplicateurs ──
        var goldIcon = new Image
        {
            Width = 18,
            Height = 18,
            VerticalAlignment = VerticalAlignment.Center,
            Source = icons.GetResourceIcon(nameof(Resource.Gold), 18),
        };
        var goldText = new TextBlock
        {
            FontSize = 13,
            FontWeight = FontWeight.Bold,
            Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center,
            [!TextBlock.TextProperty] = new Binding(nameof(TradePopupViewModel.GoldLabel)),
        };
        var gold = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 5,
            VerticalAlignment = VerticalAlignment.Center,
        };
        gold.Children.Add(goldIcon);
        gold.Children.Add(goldText);

        var multipliers = new ItemsControl
        {
            VerticalAlignment = VerticalAlignment.Center,
            [!ItemsControl.ItemsSourceProperty] = new Binding(nameof(TradePopupViewModel.Multipliers)),
            ItemsPanel = new FuncTemplate<Panel?>(() => new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 5,
            }),
            ItemTemplate = new FuncDataTemplate<TradeMultiplierViewModel>(
                (_, _) => BuildMultiplier(viewModel), supportsRecycling: true),
            [!IsVisibleProperty] = new Binding(nameof(TradePopupViewModel.ShowingTrade)),
        };

        var footer = new DockPanel { LastChildFill = false, Margin = new Thickness(0, 12, 0, 0) };
        DockPanel.SetDock(gold, Dock.Left);
        DockPanel.SetDock(multipliers, Dock.Right);
        footer.Children.Add(gold);
        footer.Children.Add(multipliers);

        var close = new Button
        {
            Width = 28,
            Height = 28,
            Padding = new Thickness(0),
            Background = CloseButton,
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(5),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Content = new TextBlock
            {
                Text = "X",
                FontSize = 14,
                FontWeight = FontWeight.Bold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
            },
        };
        close.Classes.Add(GameControlStyles.ToneButton);
        close.Click += (_, _) => viewModel.Close();

        var stack = new StackPanel { Orientation = Orientation.Vertical };
        stack.Children.Add(title);
        stack.Children.Add(tabs);
        stack.Children.Add(scroll);
        stack.Children.Add(footer);

        var body = new Panel();
        body.Children.Add(stack);
        body.Children.Add(close);

        var box = new Border
        {
            Width = 700,
            Background = PanelBackground,
            BorderBrush = Border_,
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(18),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Child = body,
        };

        Content = new Border { Background = Veil, Child = box };
    }

    private static Button TabButton(string labelPath, string activePath, Action onClick)
    {
        var button = new Button
        {
            Width = 130,
            Height = 26,
            FontSize = 12,
            Foreground = Brushes.White,
            Padding = new Thickness(0),
            CornerRadius = new CornerRadius(5),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Color.FromRgb(80, 80, 95)),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            [!ContentProperty] = new Binding(labelPath),
            [!BackgroundProperty] = new Binding(activePath)
            {
                Converter = new FuncValueConverter<bool, IBrush>(a => a ? TabActive : TabInactive),
            },
        };
        button.Classes.Add(GameControlStyles.ToneButton);
        button.Click += (_, _) => onClick();
        return button;
    }

    private static Control BuildColumn(
        TradePopupViewModel viewModel, SvgIconCache icons, string headerPath, string rowsPath, bool isSell)
    {
        var header = new TextBlock
        {
            FontSize = 13,
            FontWeight = FontWeight.Bold,
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 8),
            [!TextBlock.TextProperty] = new Binding(headerPath),
        };

        var rows = new ItemsControl
        {
            [!ItemsControl.ItemsSourceProperty] = new Binding(rowsPath),
            ItemsPanel = new FuncTemplate<Panel?>(() => new StackPanel { Spacing = 4 }),
            ItemTemplate = new FuncDataTemplate<TradeRowViewModel>(
                (_, _) => new TradeRow(viewModel, icons, isSell), supportsRecycling: true),
        };

        var stack = new StackPanel { Orientation = Orientation.Vertical };
        stack.Children.Add(header);
        stack.Children.Add(rows);
        return stack;
    }

    private static Control BuildMultiplier(TradePopupViewModel owner)
    {
        TradeMultiplierViewModel? model = null;

        var button = new Button
        {
            Width = 42,
            Height = 24,
            FontSize = 11,
            Foreground = Brushes.White,
            Padding = new Thickness(0),
            CornerRadius = new CornerRadius(4),
            BorderThickness = new Thickness(1),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            [!ContentProperty] = new Binding(nameof(TradeMultiplierViewModel.Label)),
        };

        // Le multiplicateur temporaire (Ctrl/Maj) se distingue de l'actif permanent.
        void ApplyLook()
        {
            bool temporary = model?.IsTemporary == true;
            bool active = model?.IsActive == true;
            button.Background = temporary ? MultTemporary : active ? TabActive : TabInactive;
            button.BorderBrush = new SolidColorBrush(temporary
                ? Color.FromRgb(220, 140, 30)
                : active ? Color.FromRgb(100, 150, 220) : Color.FromRgb(80, 80, 95));
        }

        button.Classes.Add(GameControlStyles.ToneButton);
        button.DataContextChanged += (_, _) =>
        {
            if (model != null) model.PropertyChanged -= OnModelChanged;
            model = button.DataContext as TradeMultiplierViewModel;
            if (model != null) model.PropertyChanged += OnModelChanged;
            ApplyLook();
        };
        void OnModelChanged(object? _, System.ComponentModel.PropertyChangedEventArgs __) => ApplyLook();

        button.Click += (_, _) => { if (model != null) owner.SetMultiplier(model); };
        return button;
    }

    private static Control BuildHistoryRow(SvgIconCache icons)
    {
        var icon = new Image { Width = 18, Height = 18, VerticalAlignment = VerticalAlignment.Center };
        var label = new TextBlock
        {
            FontSize = 13,
            Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(6, 0, 0, 0),
            [!TextBlock.TextProperty] = new Binding(nameof(TradeHistoryEntryViewModel.Label)),
        };

        var goldText = new TextBlock
        {
            FontSize = 11,
            TextAlignment = TextAlignment.Right,
            [!TextBlock.TextProperty] = new Binding(nameof(TradeHistoryEntryViewModel.GoldText)),
            [!TextBlock.ForegroundProperty] = new Binding(nameof(TradeHistoryEntryViewModel.IsGain))
            {
                Converter = new FuncValueConverter<bool, IBrush>(g => g ? Gain : Loss),
            },
        };
        var timeText = new TextBlock
        {
            FontSize = 11,
            Foreground = Subtle,
            TextAlignment = TextAlignment.Right,
            [!TextBlock.TextProperty] = new Binding(nameof(TradeHistoryEntryViewModel.TimeText)),
        };
        var right = new StackPanel { Orientation = Orientation.Vertical, VerticalAlignment = VerticalAlignment.Center };
        right.Children.Add(goldText);
        right.Children.Add(timeText);

        var layout = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(icon, Dock.Left);
        DockPanel.SetDock(right, Dock.Right);
        layout.Children.Add(icon);
        layout.Children.Add(right);
        layout.Children.Add(label);

        var border = new Border
        {
            Height = 40,
            Padding = new Thickness(8, 0),
            CornerRadius = new CornerRadius(5),
            Background = RowFill,
            BorderBrush = RowBorder,
            BorderThickness = new Thickness(1),
            Child = layout,
        };

        border.DataContextChanged += (_, _) =>
        {
            if (border.DataContext is TradeHistoryEntryViewModel vm)
                icon.Source = icons.GetResourceIcon(vm.IconName, 18);
        };

        return border;
    }

    /// <summary>Une ligne : icone, nom, stock, et bouton d'echange.</summary>
    private sealed class TradeRow : Border
    {
        private readonly TradePopupViewModel _owner;
        private readonly SvgIconCache _icons;
        private readonly bool _isSell;
        private readonly Image _icon = new() { Width = 18, Height = 18, VerticalAlignment = VerticalAlignment.Center };
        private TradeRowViewModel? _row;

        public TradeRow(TradePopupViewModel owner, SvgIconCache icons, bool isSell)
        {
            _owner = owner;
            _icons = icons;
            _isSell = isSell;

            Height = 44;
            Padding = new Thickness(8, 0);
            CornerRadius = new CornerRadius(5);
            BorderBrush = RowBorder;
            BorderThickness = new Thickness(1);
            this[!BackgroundProperty] = new Binding(nameof(TradeRowViewModel.IsEnabled))
            {
                Converter = new FuncValueConverter<bool, IBrush>(e => e ? RowFill : RowDisabled),
            };

            var name = new TextBlock
            {
                FontSize = 13,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 0, 0),
                TextTrimming = TextTrimming.CharacterEllipsis,
                [!TextBlock.TextProperty] = new Binding(nameof(TradeRowViewModel.Name)),
                [!TextBlock.ForegroundProperty] = new Binding(nameof(TradeRowViewModel.IsEnabled))
                {
                    Converter = new FuncValueConverter<bool, IBrush>(e => e ? Brushes.White : Subtle),
                },
            };

            var stock = new TextBlock
            {
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                Width = 70,
                [!TextBlock.TextProperty] = new Binding(nameof(TradeRowViewModel.StockLabel)),
                [!TextBlock.ForegroundProperty] = new Binding(nameof(TradeRowViewModel.IsAtMax))
                {
                    Converter = new FuncValueConverter<bool, IBrush>(m => m ? MaxStock : Subtle),
                },
            };

            var action = new Button
            {
                Width = 110,
                Height = 28,
                FontSize = 12,
                Padding = new Thickness(0),
                CornerRadius = new CornerRadius(5),
                BorderThickness = new Thickness(0),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                [!ContentProperty] = new Binding(nameof(TradeRowViewModel.ButtonLabel)),
                [!ToolTip.TipProperty] = new Binding(nameof(TradeRowViewModel.DisabledTooltip)),
                [!BackgroundProperty] = new Binding(nameof(TradeRowViewModel.IsEnabled))
                {
                    Converter = new FuncValueConverter<bool, IBrush>(e => e ? ActionEnabled : ActionDisabled),
                },
                [!ForegroundProperty] = new Binding(nameof(TradeRowViewModel.IsEnabled))
                {
                    Converter = new FuncValueConverter<bool, IBrush>(e => e ? Brushes.White : Subtle),
                },
            };
            action.Classes.Add(GameControlStyles.ToneButton);
            action.Click += (_, _) =>
            {
                if (_row == null) return;
                if (_isSell) _owner.Sell(_row);
                else _owner.Buy(_row);
            };

            var layout = new DockPanel { LastChildFill = true };
            DockPanel.SetDock(_icon, Dock.Left);
            DockPanel.SetDock(action, Dock.Right);
            DockPanel.SetDock(stock, Dock.Right);
            layout.Children.Add(_icon);
            layout.Children.Add(action);
            layout.Children.Add(stock);
            layout.Children.Add(name);

            Child = layout;
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);
            _row = DataContext as TradeRowViewModel;
            if (_row != null) _icon.Source = _icons.GetResourceIcon(_row.IconName, 18);
        }
    }
}
