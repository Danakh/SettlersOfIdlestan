using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using SettlersOfIdlestanUI.ViewModels;

namespace SettlersOfIdlestanUI.Controls;

/// <summary>
/// Popup de progression d'un saut de temps : voile plein ecran, boite centree, barre de
/// progression. Aucun bouton — le saut n'est pas annulable une fois lance.
///
/// Le voile n'est pas decoratif : pendant le saut la boucle de jeu ne traite plus le temps reel,
/// et un clic qui atteindrait la carte ou un panneau agirait sur un etat en train de defiler.
/// Comme pour <see cref="ModalPopupView"/>, c'est le Border plein qui bloque.
/// </summary>
public sealed class TimeJumpView : UserControl
{
    private static readonly SolidColorBrush Veil = new(Color.FromArgb(160, 0, 0, 0));
    private static readonly SolidColorBrush Background = new(Color.FromArgb(245, 24, 24, 30));
    private static readonly SolidColorBrush BorderGold = new(Colors.Gold);
    private static readonly SolidColorBrush TitleText = new(Color.FromRgb(255, 200, 50));
    private static readonly SolidColorBrush BodyText = new(Color.FromRgb(180, 180, 190));
    private static readonly SolidColorBrush BarTrack = new(Color.FromArgb(200, 50, 50, 65));
    private static readonly SolidColorBrush BarFill = new(Color.FromRgb(255, 200, 50));

    public TimeJumpView(TimeJumpViewModel viewModel)
    {
        DataContext = viewModel;
        this[!IsVisibleProperty] = new Binding(nameof(TimeJumpViewModel.IsActive));

        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;

        var title = new TextBlock
        {
            FontSize = 20,
            FontWeight = FontWeight.Bold,
            Foreground = TitleText,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            [!TextBlock.TextProperty] = new Binding(nameof(TimeJumpViewModel.Title)),
        };

        var reason = new TextBlock
        {
            FontSize = 13,
            Margin = new Thickness(0, 14, 0, 0),
            Foreground = BodyText,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            [!TextBlock.TextProperty] = new Binding(nameof(TimeJumpViewModel.Reason)),
        };

        var bar = new ProgressBar
        {
            Minimum = 0,
            Maximum = 1,
            Height = 14,
            Margin = new Thickness(0, 20, 0, 0),
            Foreground = BarFill,
            Background = BarTrack,
            [!RangeBase.ValueProperty] = new Binding(nameof(TimeJumpViewModel.Progress)),
        };

        var percent = new TextBlock
        {
            FontSize = 13,
            FontWeight = FontWeight.Bold,
            Margin = new Thickness(0, 8, 0, 0),
            Foreground = BodyText,
            TextAlignment = TextAlignment.Center,
            [!TextBlock.TextProperty] = new Binding(nameof(TimeJumpViewModel.PercentLabel)),
        };

        var stack = new StackPanel { Orientation = Orientation.Vertical };
        stack.Children.Add(title);
        stack.Children.Add(reason);
        stack.Children.Add(bar);
        stack.Children.Add(percent);

        var box = new Border
        {
            MinWidth = 420,
            MaxWidth = 520,
            Background = Background,
            BorderBrush = BorderGold,
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(28, 26),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Child = stack,
        };

        Content = new Border { Background = Veil, Child = box };
    }
}
