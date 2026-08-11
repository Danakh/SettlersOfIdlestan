using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using SettlersOfIdlestanUI.ViewModels;

namespace SettlersOfIdlestanUI.Controls;

/// <summary>
/// Banque de temps, bouton pause/lecture et selecteur de vitesse.
///
/// Le menu de vitesse est un Flyout : sa fermeture au clic exterieur et son positionnement
/// sont natifs, la ou l'ancien renderer devait suivre l'etat _speedMenuOpen a la main et
/// declarer ses rectangles dans ContainsPoint pour ne pas laisser fuir les clics.
///
/// Le bouton de temps affiche l'etat COURANT, pas l'action a venir : triangle quand la partie
/// tourne, double barre quand elle est en pause. Une partie en pause est le seul etat ou le
/// joueur peut se demander pourquoi plus rien ne bouge — d'ou le fond qui pulse entre le dore
/// et le noir, visible du coin de l'oeil sans avoir a lire le bouton.
/// </summary>
public sealed class TimeControlView : UserControl
{
    /// Classe posee sur le bouton de temps tant que la partie est en pause : c'est elle qui
    /// declenche l'animation de fond.
    private const string PausedClass = "paused";

    private static readonly SolidColorBrush PausedGold = new(Color.FromRgb(212, 175, 55));
    private static readonly SolidColorBrush PausedBlack = new(Color.FromRgb(18, 18, 24));

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
        toggle.Content = CreateToggleIcons();
        Styles.Add(BuildPausePulse());
        toggle.Click += (_, _) => _viewModel.TogglePause();

        // L'etat de pause est repousse par sondage (Refresh), pas par evenement du runtime :
        // on suit donc le ViewModel, seul endroit ou le changement est deja detecte. Le faire
        // ici et non dans le Click couvre aussi les pauses venues d'ailleurs (raccourci,
        // reprise de partie), qui laissaient l'infobulle a l'envers.
        ApplyPausedState(toggle, localize, localizeFormat);
        _viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is null or nameof(TimeControlViewModel.IsPaused))
                ApplyPausedState(toggle, localize, localizeFormat);
        };

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

    /// <summary>
    /// Repercute l'etat de pause sur le bouton : classe du fond pulsant, et infobulle qui —
    /// elle — annonce l'ACTION, complement de l'icone qui montre l'etat.
    /// </summary>
    private void ApplyPausedState(Button toggle, Func<string, string> localize,
                                  Func<string, object[], string> localizeFormat)
    {
        toggle.Classes.Set(PausedClass, _viewModel.IsPaused);
        ToolTip.SetTip(toggle, _viewModel.IsPaused
            ? localizeFormat("timecontrol_toggle_play_tooltip", [_viewModel.ActiveSpeed])
            : localize("timecontrol_toggle_pause_tooltip"));
    }

    /// <summary>
    /// Les deux icones cohabitent dans le bouton, seule leur visibilite change. Reconstruire
    /// le contenu a chaque bascule couperait l'animation de fond au moment ou elle demarre.
    /// </summary>
    private static Control CreateToggleIcons()
    {
        var play = PlayPauseIcon.CreatePlay();
        play.Bind(IsVisibleProperty, new Binding(nameof(TimeControlViewModel.IsPaused))
        {
            Converter = BoolConverters.Not,
        });

        var pause = PlayPauseIcon.CreatePause();
        pause.Bind(IsVisibleProperty, new Binding(nameof(TimeControlViewModel.IsPaused)));

        return new Panel { Children = { play, pause } };
    }

    /// <summary>
    /// Fond pulsant du bouton en pause. Porte par un Style plutot que par un timer : Avalonia
    /// demarre et arrete l'animation avec la classe, et rend la couleur locale du bouton des
    /// que la partie repart.
    /// </summary>
    private static Style BuildPausePulse() =>
        new(x => x.OfType<Button>().Class(GameControlStyles.ToneButton).Class(PausedClass))
        {
            Animations =
            {
                new Animation
                {
                    Duration = TimeSpan.FromSeconds(0.8),
                    IterationCount = IterationCount.Infinite,
                    PlaybackDirection = PlaybackDirection.Alternate,
                    Easing = new SineEaseInOut(),
                    Children =
                    {
                        new KeyFrame
                        {
                            Cue = new Cue(0d),
                            Setters = { new Setter(BackgroundProperty, PausedGold) },
                        },
                        new KeyFrame
                        {
                            Cue = new Cue(1d),
                            Setters = { new Setter(BackgroundProperty, PausedBlack) },
                        },
                    },
                },
            },
        };

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
