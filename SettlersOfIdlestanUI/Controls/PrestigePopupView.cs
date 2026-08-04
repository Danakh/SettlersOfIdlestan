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
/// Popup de prestige : le detail des points gagnes dans une zone defilante, puis un pied fixe
/// avec le bonus de merveille, le total, le choix de palier et les actions.
///
/// Le pied reste hors du defilement, comme dans le rendu Skia : le total et le bouton d'action
/// doivent rester visibles quelle que soit la longueur du decompte.
/// </summary>
public sealed class PrestigePopupView : UserControl
{
    private static readonly SolidColorBrush Veil = new(Color.FromArgb(120, 0, 0, 0));
    private static readonly SolidColorBrush Background = new(Color.FromArgb(245, 24, 24, 30));
    private static readonly SolidColorBrush Border_ = new(Colors.Gold);
    private static readonly SolidColorBrush Separator = new(Color.FromArgb(120, 120, 120, 140));
    private static readonly SolidColorBrush Subtle = new(Color.FromRgb(180, 180, 190));
    private static readonly SolidColorBrush Warning = new(Color.FromRgb(230, 150, 70));
    private static readonly SolidColorBrush ActionEnabled = new(Color.FromRgb(60, 110, 180));
    private static readonly SolidColorBrush ActionCorrupted = new(Color.FromRgb(120, 60, 140));
    private static readonly SolidColorBrush ActionDisabled = new(Color.FromRgb(70, 70, 78));
    private static readonly SolidColorBrush CloseButton = new(Color.FromArgb(230, 90, 50, 50));

    public PrestigePopupView(PrestigePopupViewModel viewModel)
    {
        DataContext = viewModel;
        this[!IsVisibleProperty] = new Binding(nameof(PrestigePopupViewModel.IsOpen));

        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;

        var title = new TextBlock
        {
            FontSize = 20,
            FontWeight = FontWeight.Bold,
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 12),
            [!TextBlock.TextProperty] = new Binding(nameof(PrestigePopupViewModel.Title)),
        };

        var rows = new ItemsControl
        {
            [!ItemsControl.ItemsSourceProperty] = new Binding(nameof(PrestigePopupViewModel.Rows)),
            ItemsPanel = new FuncTemplate<Panel?>(() => new StackPanel { Spacing = 2 }),
            ItemTemplate = new FuncDataTemplate<SkiaLayer.PrestigeRowSnapshot>(
                (_, _) => BuildRow(), supportsRecycling: true),
        };

        var scroll = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MaxHeight = 300,
            Content = rows,
        };

        var footer = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(0, 10, 0, 0) };
        footer.Children.Add(BuildWonderRow(viewModel));
        footer.Children.Add(BuildTotalRow());
        footer.Children.Add(BuildTierPicker(viewModel));
        footer.Children.Add(BuildActions(viewModel));
        footer.Children.Add(new TextBlock
        {
            FontSize = 13,
            Foreground = Subtle,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 10, 0, 0),
            [!TextBlock.TextProperty] = new Binding(nameof(PrestigePopupViewModel.ImperialPortWarning)),
            [!IsVisibleProperty] = new Binding(nameof(PrestigePopupViewModel.HasImperialPortWarning)),
        });

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
        stack.Children.Add(scroll);
        stack.Children.Add(footer);

        var body = new Panel();
        body.Children.Add(stack);
        body.Children.Add(close);

        var box = new Border
        {
            Width = 460,
            Background = Background,
            BorderBrush = Border_,
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(20),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Child = body,
        };

        Content = new Border { Background = Veil, Child = box };
    }

    /// <summary>Une ligne du decompte : libelle a gauche, valeur a droite.</summary>
    private static Control BuildRow()
    {
        var label = new TextBlock
        {
            FontSize = 13,
            Foreground = Subtle,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            [!TextBlock.TextProperty] = new Binding(nameof(SkiaLayer.PrestigeRowSnapshot.Label)),
        };

        var value = new TextBlock
        {
            FontSize = 13,
            FontWeight = FontWeight.Bold,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10, 0, 0, 0),
            [!TextBlock.TextProperty] = new Binding(nameof(SkiaLayer.PrestigeRowSnapshot.Value)),
            [!TextBlock.ForegroundProperty] = new Binding(nameof(SkiaLayer.PrestigeRowSnapshot.IsWarning))
            {
                Converter = new FuncValueConverter<bool, IBrush>(w => w ? Warning : Brushes.White),
            },
        };

        var layout = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(value, Dock.Right);
        layout.Children.Add(value);
        layout.Children.Add(label);

        var row = new Border
        {
            // Fond transparent : sans geometrie remplie, pas de survol donc pas d'infobulle.
            Background = Brushes.Transparent,
            Padding = new Thickness(0, 5),
            Child = layout,
        };
        row.DataContextChanged += (_, _) =>
        {
            if (row.DataContext is SkiaLayer.PrestigeRowSnapshot snapshot)
                ToolTip.SetTip(row, snapshot.Tooltip.Count > 0 ? string.Join('\n', snapshot.Tooltip) : null);
        };
        return row;
    }

    /// <summary>Bonus de merveille : seul bonus exprime en multiplicateur, avec son bouton d'avance.</summary>
    private static Control BuildWonderRow(PrestigePopupViewModel viewModel)
    {
        var label = new TextBlock
        {
            FontSize = 13,
            Foreground = Subtle,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            [!TextBlock.TextProperty] = new Binding(nameof(PrestigePopupViewModel.WonderLabel)),
        };

        var value = new TextBlock
        {
            FontSize = 13,
            FontWeight = FontWeight.Bold,
            Foreground = Subtle,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10, 0, 6, 0),
            [!TextBlock.TextProperty] = new Binding(nameof(PrestigePopupViewModel.WonderValue)),
        };

        var skip = new Button
        {
            Width = 24,
            Height = 24,
            FontSize = 12,
            Padding = new Thickness(0),
            CornerRadius = new CornerRadius(4),
            BorderThickness = new Thickness(0),
            Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Content = "⏩",
            [!BackgroundProperty] = new Binding(nameof(PrestigePopupViewModel.CanSkipWonderTime))
            {
                Converter = new FuncValueConverter<bool, IBrush>(c => c ? ActionEnabled : ActionDisabled),
            },
            [!ToolTip.TipProperty] = new Binding(nameof(PrestigePopupViewModel.WonderSkipTooltip)),
        };
        skip.Classes.Add(GameControlStyles.ToneButton);
        skip.Click += (_, _) => viewModel.SkipWonderTime();

        var layout = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(skip, Dock.Right);
        DockPanel.SetDock(value, Dock.Right);
        layout.Children.Add(skip);
        layout.Children.Add(value);
        layout.Children.Add(label);

        return new Border
        {
            Background = Brushes.Transparent,
            Padding = new Thickness(0, 6),
            BorderBrush = Separator,
            BorderThickness = new Thickness(0, 1, 0, 0),
            Child = layout,
            [!IsVisibleProperty] = new Binding(nameof(PrestigePopupViewModel.HasWonderRow)),
            [!ToolTip.TipProperty] = new Binding(nameof(PrestigePopupViewModel.WonderTooltip)),
        };
    }

    private static Control BuildTotalRow()
    {
        var label = new TextBlock
        {
            FontSize = 14,
            FontWeight = FontWeight.Bold,
            Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center,
            [!TextBlock.TextProperty] = new Binding(nameof(PrestigePopupViewModel.TotalLabel)),
        };

        var value = new TextBlock
        {
            FontSize = 14,
            FontWeight = FontWeight.Bold,
            Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center,
            [!TextBlock.TextProperty] = new Binding(nameof(PrestigePopupViewModel.TotalValue)),
        };

        var layout = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(value, Dock.Right);
        layout.Children.Add(value);
        layout.Children.Add(label);

        return new Border
        {
            Padding = new Thickness(0, 8),
            BorderBrush = Separator,
            BorderThickness = new Thickness(0, 1, 0, 0),
            Child = layout,
        };
    }

    /// <summary>Choix du palier de la prochaine ile, debloque par le Grand Phare niveau 3.</summary>
    private static Control BuildTierPicker(PrestigePopupViewModel viewModel)
    {
        var minus = TierButton("-", nameof(PrestigePopupViewModel.CanDecreaseTier));
        minus.Click += (_, _) => viewModel.ChangeTier(increase: false);

        var plus = TierButton("+", nameof(PrestigePopupViewModel.CanIncreaseTier));
        plus.Click += (_, _) => viewModel.ChangeTier(increase: true);

        var label = new TextBlock
        {
            FontSize = 13,
            Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            [!TextBlock.TextProperty] = new Binding(nameof(PrestigePopupViewModel.TierPickerLabel)),
        };

        var layout = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(minus, Dock.Left);
        DockPanel.SetDock(plus, Dock.Right);
        layout.Children.Add(minus);
        layout.Children.Add(plus);
        layout.Children.Add(label);

        return new Border
        {
            Padding = new Thickness(0, 6),
            Child = layout,
            [!IsVisibleProperty] = new Binding(nameof(PrestigePopupViewModel.HasTierPicker)),
            [!ToolTip.TipProperty] = new Binding(nameof(PrestigePopupViewModel.TierPickerTooltip)),
        };
    }

    private static Button TierButton(string glyph, string enabledPath)
    {
        var button = new Button
        {
            Width = 24,
            Height = 24,
            FontSize = 13,
            FontWeight = FontWeight.Bold,
            Padding = new Thickness(0),
            CornerRadius = new CornerRadius(4),
            BorderThickness = new Thickness(0),
            Foreground = Brushes.White,
            Content = glyph,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            [!BackgroundProperty] = new Binding(enabledPath)
            {
                Converter = new FuncValueConverter<bool, IBrush>(e => e ? ActionEnabled : ActionDisabled),
            },
        };
        button.Classes.Add(GameControlStyles.ToneButton);
        return button;
    }

    private static Control BuildActions(PrestigePopupViewModel viewModel) => new ItemsControl
    {
        Margin = new Thickness(0, 8, 0, 0),
        HorizontalAlignment = HorizontalAlignment.Center,
        [!ItemsControl.ItemsSourceProperty] = new Binding(nameof(PrestigePopupViewModel.Actions)),
        ItemsPanel = new FuncTemplate<Panel?>(() => new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            HorizontalAlignment = HorizontalAlignment.Center,
        }),
        ItemTemplate = new FuncDataTemplate<PrestigeActionViewModel>(
            (_, _) => BuildAction(viewModel), supportsRecycling: true),
    };

    private static Control BuildAction(PrestigePopupViewModel owner)
    {
        PrestigeActionViewModel? model = null;

        var label = new TextBlock
        {
            FontSize = 13,
            FontWeight = FontWeight.Bold,
            Foreground = Brushes.White,
            TextAlignment = TextAlignment.Center,
            [!TextBlock.TextProperty] = new Binding(nameof(PrestigeActionViewModel.Label)),
        };

        var subLabel = new TextBlock
        {
            FontSize = 11,
            Foreground = Brushes.White,
            TextAlignment = TextAlignment.Center,
            [!TextBlock.TextProperty] = new Binding(nameof(PrestigeActionViewModel.SubLabel)),
            [!IsVisibleProperty] = new Binding(nameof(PrestigeActionViewModel.HasSubLabel)),
        };

        var content = new StackPanel { Orientation = Orientation.Vertical };
        content.Children.Add(label);
        content.Children.Add(subLabel);

        var button = new Button
        {
            Width = 150,
            Height = 42,
            Padding = new Thickness(0),
            CornerRadius = new CornerRadius(7),
            BorderThickness = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Content = content,
            [!ToolTip.TipProperty] = new Binding(nameof(PrestigeActionViewModel.Tooltip)),
        };

        // Le prestige corrompu se distingue du normal, et l'indisponibilite des deux.
        button[!BackgroundProperty] = new MultiBinding
        {
            Bindings =
            {
                new Binding(nameof(PrestigeActionViewModel.IsEnabled)),
                new Binding(nameof(PrestigeActionViewModel.IsCorrupted)),
            },
            Converter = new FuncMultiValueConverter<object?, IBrush>(values =>
            {
                var list = values.ToList();
                bool enabled = list.Count > 0 && list[0] is true;
                bool corrupted = list.Count > 1 && list[1] is true;
                return !enabled ? ActionDisabled : corrupted ? ActionCorrupted : ActionEnabled;
            }),
        };

        button.Classes.Add(GameControlStyles.ToneButton);
        button.DataContextChanged += (_, _) => model = button.DataContext as PrestigeActionViewModel;
        button.Click += (_, _) => { if (model != null) owner.Invoke(model); };

        return button;
    }
}
