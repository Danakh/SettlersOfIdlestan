using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using SettlersOfIdlestanUI.ViewModels;

namespace SettlersOfIdlestanUI.Controls;

/// <summary>
/// Popup d'edition des presets d'automatisation de construction (voir
/// TechnologyId.AutomationPreset) : un tableau, une ligne par batiment automatisable, une colonne
/// par preset (menu deroulant de plafond de niveau, de 0 au niveau max theorique du batiment —
/// voir AutomationPresetRowViewModel.MaxLevel / Building.GetAbsoluteMaxLevel).
///
/// Meme voile bloquant + boite centree que TradePopupView/PrestigePopupView.
/// </summary>
public sealed class AutomationPresetPopupView : UserControl
{
    private static readonly SolidColorBrush Veil = new(Color.FromArgb(120, 0, 0, 0));
    private static readonly SolidColorBrush PanelBackground = new(Color.FromArgb(245, 24, 24, 30));
    private static readonly SolidColorBrush Border_ = new(Colors.Gold);
    private static readonly SolidColorBrush RowFill = new(Color.FromArgb(245, 55, 55, 65));
    private static readonly SolidColorBrush RowBorder = new(Color.FromArgb(100, 255, 255, 255));
    private static readonly SolidColorBrush Subtle = new(Color.FromRgb(180, 180, 190));
    private static readonly SolidColorBrush CloseButton = new(Color.FromArgb(230, 90, 50, 50));

    public AutomationPresetPopupView(AutomationPresetPopupViewModel viewModel)
    {
        DataContext = viewModel;
        this[!IsVisibleProperty] = new Binding(nameof(AutomationPresetPopupViewModel.IsOpen));

        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;

        var title = new TextBlock
        {
            FontSize = 20,
            FontWeight = FontWeight.Bold,
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            [!TextBlock.TextProperty] = new Binding(nameof(AutomationPresetPopupViewModel.Title)),
            Margin = new Thickness(0, 0, 0, 10),
        };

        // Marge droite de ScrollbarGutter : l'ascenseur de la ScrollViewer ci-dessous est un
        // ascenseur flottant (theme Fluent) qui se superpose au contenu plutot que de lui reserver
        // une colonne — sans cette marge il recouvrait la derniere colonne de presets.
        const double ScrollbarGutter = 16;

        var columnHeaders = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,90,90,90"),
            Margin = new Thickness(8, 0, 8 + ScrollbarGutter, 6),
        };
        var buildingHeader = new TextBlock { FontSize = 12, FontWeight = FontWeight.Bold, Foreground = Subtle };
        buildingHeader[!TextBlock.TextProperty] = new Binding(nameof(AutomationPresetPopupViewModel.BuildingColumnHeader));
        Grid.SetColumn(buildingHeader, 0);
        columnHeaders.Children.Add(buildingHeader);

        var preset1Header = BuildPresetColumnHeader(viewModel, "1", 1);
        var preset2Header = BuildPresetColumnHeader(viewModel, "2", 2);
        var preset3Header = BuildPresetColumnHeader(viewModel, "3", 3);
        Grid.SetColumn(preset1Header, 1);
        Grid.SetColumn(preset2Header, 2);
        Grid.SetColumn(preset3Header, 3);
        columnHeaders.Children.Add(preset1Header);
        columnHeaders.Children.Add(preset2Header);
        columnHeaders.Children.Add(preset3Header);

        var rows = new ItemsControl
        {
            Margin = new Thickness(0, 0, ScrollbarGutter, 0),
            [!ItemsControl.ItemsSourceProperty] = new Binding(nameof(AutomationPresetPopupViewModel.Rows)),
            ItemsPanel = new FuncTemplate<Panel?>(() => new StackPanel { Spacing = 4 }),
            ItemTemplate = new FuncDataTemplate<AutomationPresetRowViewModel>(
                (_, _) => new PresetRow(viewModel), supportsRecycling: true),
        };

        var scroll = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Height = 420,
            Width = 520,
            Content = rows,
        };

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
        stack.Children.Add(columnHeaders);
        stack.Children.Add(scroll);

        var body = new Panel();
        body.Children.Add(stack);
        body.Children.Add(close);

        var box = new Border
        {
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

    /// <summary>En-tete d'une colonne de preset : son numero, plus deux petits boutons "0"/"M" qui
    /// mettent tout de suite toute la colonne a 0 (jamais construit) ou au max atteignable de
    /// chaque batiment (voir AutomationPresetPopupViewModel.SetColumnToZero/SetColumnToMax).</summary>
    private static Control BuildPresetColumnHeader(AutomationPresetPopupViewModel viewModel, string label, int preset)
    {
        var numberText = new TextBlock
        {
            FontSize = 12,
            FontWeight = FontWeight.Bold,
            Foreground = Subtle,
            HorizontalAlignment = HorizontalAlignment.Center,
            Text = label,
        };

        Button SmallButton(string content, string tooltipPath, Action onClick)
        {
            var button = new Button
            {
                Width = 20,
                Height = 16,
                Padding = new Thickness(0),
                FontSize = 9,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Content = content,
                [!ToolTip.TipProperty] = new Binding(tooltipPath),
            };
            button.Classes.Add(GameControlStyles.ToneButton);
            button.Click += (_, _) => onClick();
            return button;
        }

        var zeroButton = SmallButton("0", nameof(AutomationPresetPopupViewModel.ZeroColumnTooltip),
            () => viewModel.SetColumnToZero(preset));
        var maxButton = SmallButton("M", nameof(AutomationPresetPopupViewModel.MaxColumnTooltip),
            () => viewModel.SetColumnToMax(preset));

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Spacing = 4,
        };
        buttons.Children.Add(zeroButton);
        buttons.Children.Add(maxButton);

        var stack = new StackPanel { Orientation = Orientation.Vertical, Spacing = 2 };
        stack.Children.Add(numberText);
        stack.Children.Add(buttons);
        return stack;
    }

    /// <summary>Une ligne : nom du batiment, 3 menus deroulants de plafond (0 au niveau max
    /// theorique du batiment, voir Building.GetAbsoluteMaxLevel / AutomationPresetRowViewModel.MaxLevel —
    /// pas la peine de proposer des paliers qu'aucune recherche/prestige ne peut jamais debloquer).</summary>
    private sealed class PresetRow : Border
    {
        private readonly AutomationPresetPopupViewModel _owner;
        private readonly ComboBox[] _combos = new ComboBox[3];
        private AutomationPresetRowViewModel? _row;

        public PresetRow(AutomationPresetPopupViewModel owner)
        {
            _owner = owner;

            Height = 34;
            Padding = new Thickness(8, 0);
            CornerRadius = new CornerRadius(5);
            Background = RowFill;
            BorderBrush = RowBorder;
            BorderThickness = new Thickness(1);

            var name = new TextBlock
            {
                FontSize = 12,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                [!TextBlock.TextProperty] = new Binding(nameof(AutomationPresetRowViewModel.Name)),
            };

            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,90,90,90") };
            Grid.SetColumn(name, 0);
            grid.Children.Add(name);

            _combos[0] = BuildCapCell(nameof(AutomationPresetRowViewModel.Preset1), 1, 1);
            _combos[1] = BuildCapCell(nameof(AutomationPresetRowViewModel.Preset2), 2, 2);
            _combos[2] = BuildCapCell(nameof(AutomationPresetRowViewModel.Preset3), 3, 3);
            grid.Children.Add(_combos[0]);
            grid.Children.Add(_combos[1]);
            grid.Children.Add(_combos[2]);

            Child = grid;
        }

        private ComboBox BuildCapCell(string bindingPath, int column, int preset)
        {
            var combo = new ComboBox
            {
                Width = 64,
                Height = 26,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                [!SelectingItemsControl.SelectedItemProperty] = new Binding(bindingPath),
            };
            combo.SelectionChanged += (_, _) =>
            {
                if (_row != null && combo.SelectedItem is int value) _owner.SetCap(_row, preset, value);
            };

            Grid.SetColumn(combo, column);
            return combo;
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);
            _row = DataContext as AutomationPresetRowViewModel;
            if (_row == null) return;

            // Les valeurs stockees ne depassent jamais MaxLevel : une sauvegarde plus ancienne en
            // desaccord avec le max theorique courant est corrigee au chargement, voir
            // AutomationPresetSettings.ClampToTheoreticalMax.
            var options = Enumerable.Range(0, _row.MaxLevel + 1).Cast<object>().ToList();
            foreach (var combo in _combos) combo.ItemsSource = options;
        }
    }
}
