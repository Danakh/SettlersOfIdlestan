using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using SettlersOfIdlestanUI.ViewModels;

namespace SettlersOfIdlestanUI.Controls;

/// <summary>
/// Popup de reglages en jeu : un chrome bloquant autour du panneau partage avec l'ecran-titre.
/// </summary>
public sealed class SettingsPopupView : UserControl
{
    private static readonly SolidColorBrush Veil = new(Color.FromArgb(120, 0, 0, 0));
    private static readonly SolidColorBrush Background = new(Color.FromArgb(245, 24, 24, 30));
    private static readonly SolidColorBrush Border_ = new(Colors.Gold);
    private static readonly SolidColorBrush CloseButton = new(Color.FromArgb(230, 90, 50, 50));

    public SettingsPopupView(SettingsPopupViewModel viewModel)
    {
        DataContext = viewModel;
        this[!IsVisibleProperty] = new Binding(nameof(SettingsPopupViewModel.IsOpen));

        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;

        var title = new TextBlock
        {
            FontSize = 20,
            FontWeight = FontWeight.Bold,
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 16),
            [!TextBlock.TextProperty] = new Binding(nameof(SettingsPopupViewModel.Title)),
        };

        var panel = new SettingsPanelView(viewModel.Panel);

        var scroll = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MaxHeight = 420,
            Content = panel,
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
        stack.Children.Add(scroll);

        var body = new Panel();
        body.Children.Add(stack);
        body.Children.Add(close);

        var box = new Border
        {
            Width = 560,
            Background = Background,
            BorderBrush = Border_,
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(24),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Child = body,
        };

        Content = new Border { Background = Veil, Child = box };
    }
}
