using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using SettlersOfIdlestanUI.ViewModels;

namespace SettlersOfIdlestanUI.Controls;

/// <summary>
/// Banque de temps, bouton pause/lecture et selecteur de vitesse.
///
/// Le menu de vitesse est un Flyout : sa fermeture au clic exterieur et son positionnement
/// sont natifs, la ou l'ancien renderer devait suivre l'etat _speedMenuOpen a la main et
/// declarer ses rectangles dans ContainsPoint pour ne pas laisser fuir les clics.
/// </summary>
public sealed class TimeControlView : UserControl
{
    private readonly TimeControlViewModel _viewModel;

    public TimeControlView(TimeControlViewModel viewModel, Func<string, string> localize,
                           Func<string, object[], string> localizeFormat)
    {
        _viewModel = viewModel;
        DataContext = viewModel;

        var bank = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(35, 35, 50)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(100, 100, 130)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Width = 72,
            Height = 26,
            Child = new TextBlock
            {
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(200, 240, 255)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                [!TextBlock.TextProperty] = new Binding(nameof(TimeControlViewModel.BankLabel)),
            },
        };
        ToolTip.SetTip(bank, localize("timecontrol_bank_tooltip"));

        var toggle = CreateSquareButton();
        toggle.Bind(ContentProperty, new Binding(nameof(TimeControlViewModel.ToggleLabel)));
        toggle.Click += (_, _) =>
        {
            _viewModel.TogglePause();
            UpdateToggleTooltip(toggle, localize, localizeFormat);
        };
        UpdateToggleTooltip(toggle, localize, localizeFormat);

        var speed = CreateSquareButton();
        speed.Bind(ContentProperty, new Binding(nameof(TimeControlViewModel.ActiveSpeed)) { StringFormat = "x{0}" });
        speed.Flyout = BuildSpeedFlyout();
        ToolTip.SetTip(speed, localize("timecontrol_speed_tooltip"));

        Content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 3,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { bank, toggle, speed },
        };

        Bind(IsVisibleProperty, new Binding(nameof(TimeControlViewModel.IsAvailable)));
    }

    private void UpdateToggleTooltip(Button toggle, Func<string, string> localize,
                                     Func<string, object[], string> localizeFormat) =>
        ToolTip.SetTip(toggle, _viewModel.IsPaused
            ? localizeFormat("timecontrol_toggle_play_tooltip", [_viewModel.ActiveSpeed])
            : localize("timecontrol_toggle_pause_tooltip"));

    private Flyout BuildSpeedFlyout()
    {
        var options = new StackPanel { Orientation = Orientation.Vertical, Spacing = 3 };
        var flyout = new Flyout { Content = options, Placement = PlacementMode.Bottom };

        foreach (var value in TimeControlViewModel.SpeedOptions)
        {
            int captured = value;
            var option = CreateSquareButton();
            option.Content = $"x{captured}";
            option.Click += (_, _) =>
            {
                _viewModel.SetSpeed(captured);
                flyout.Hide();
            };
            options.Children.Add(option);
        }

        return flyout;
    }

    private static Button CreateSquareButton()
    {
        var button = new Button
        {
            Width = 26,
            Height = 26,
            Padding = new Thickness(0),
            FontSize = 13,
            FontWeight = FontWeight.Bold,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Foreground = Brushes.White,
            Background = new SolidColorBrush(Color.FromRgb(60, 140, 220)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(100, 100, 130)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
        };
        // Pause et vitesse signalent leur etat par leur fond.
        button.Classes.Add(GameControlStyles.ToneButton);
        return button;
    }
}
