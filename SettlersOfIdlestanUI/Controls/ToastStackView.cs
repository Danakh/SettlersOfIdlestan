using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Layout;
using Avalonia.Media;
using SettlersOfIdlestanUI.ViewModels;
using SkiaOverlay = SettlersOfIdlestanSkia.Renderers.Overlay;

namespace SettlersOfIdlestanUI.Controls;

/// <summary>
/// Pile de toasts de notification, ancree en bas a droite.
///
/// Le fondu vient de l'instantane, calcule par le renderer. La synchronisation etant cadencee a
/// 100 ms, une transition d'opacite de meme duree interpole entre deux echantillons : sans elle
/// le fondu avancerait par paliers visibles.
/// </summary>
public sealed class ToastStackView : UserControl
{
    private const double Margin_ = 12;

    /// Hauteur de la barre d'onglets mobile, sous laquelle les toasts ne doivent pas passer.
    private const double MobileTabBarHeight = 56;

    private static readonly SolidColorBrush Background = new(Color.FromArgb(245, 20, 20, 28));
    private static readonly SolidColorBrush Border_ = new(Color.FromRgb(180, 150, 60));
    private static readonly SolidColorBrush MessageText = new(Color.FromRgb(180, 180, 195));

    public ToastStackView(ToastViewModel viewModel)
    {
        DataContext = viewModel;

        // Sans alignement explicite, le UserControl s'etire sur toute la fenetre et avalerait
        // les clics destines a la carte.
        HorizontalAlignment = HorizontalAlignment.Right;
        VerticalAlignment = VerticalAlignment.Bottom;

        this[!MarginProperty] = new Binding(nameof(ToastViewModel.TabsAtBottom))
        {
            Converter = new FuncValueConverter<bool, Thickness>(atBottom => new Thickness(
                0, 0, Margin_, atBottom ? MobileTabBarHeight + Margin_ : Margin_)),
        };

        Content = new ItemsControl
        {
            [!ItemsControl.ItemsSourceProperty] = new Binding(nameof(ToastViewModel.Toasts)),
            ItemsPanel = new FuncTemplate<Panel?>(() => new StackPanel
            {
                Orientation = Orientation.Vertical,
                Spacing = 6,
            }),
            ItemTemplate = new FuncDataTemplate<ToastItemViewModel>(
                (_, _) => new ToastCard(viewModel), supportsRecycling: true),
        };
    }

    /// <summary>Un toast : bande d'icone coloree a gauche, titre et message a droite.</summary>
    private sealed class ToastCard : Border
    {
        private readonly ToastViewModel _owner;
        private readonly TextBlock _icon;
        private readonly TextBlock _title;
        private readonly TextBlock _message;
        private readonly Border _separator;
        private ToastItemViewModel? _toast;

        public ToastCard(ToastViewModel owner)
        {
            _owner = owner;

            Width = 274;
            Height = 64;
            Background = ToastStackView.Background;
            BorderBrush = Border_;
            BorderThickness = new Thickness(1.5);
            CornerRadius = new CornerRadius(8);
            Transitions = [new Avalonia.Animation.DoubleTransition
            {
                Property = OpacityProperty,
                Duration = TimeSpan.FromMilliseconds(100),
            }];
            this[!OpacityProperty] = new Binding(nameof(ToastItemViewModel.Opacity));

            _icon = new TextBlock
            {
                FontSize = 18,
                FontWeight = FontWeight.Bold,
                Width = 44,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };

            _separator = new Border { Width = 1, Margin = new Thickness(0, 8) };

            _title = new TextBlock
            {
                FontSize = 12,
                FontWeight = FontWeight.Bold,
                Foreground = Brushes.White,
                TextTrimming = TextTrimming.CharacterEllipsis,
            };

            _message = new TextBlock
            {
                FontSize = 11,
                Foreground = MessageText,
                Margin = new Thickness(0, 4, 0, 0),
                TextTrimming = TextTrimming.CharacterEllipsis,
            };

            var text = new StackPanel
            {
                Orientation = Orientation.Vertical,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0),
            };
            text.Children.Add(_title);
            text.Children.Add(_message);

            var layout = new DockPanel { LastChildFill = true };
            DockPanel.SetDock(_icon, Dock.Left);
            DockPanel.SetDock(_separator, Dock.Left);
            layout.Children.Add(_icon);
            layout.Children.Add(_separator);
            layout.Children.Add(text);

            Child = layout;

            // Un clic ferme le toast, comme dans le rendu Skia.
            PointerPressed += (_, e) =>
            {
                if (_toast == null) return;
                e.Handled = true;
                _owner.Dismiss(_toast);
            };
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);
            _toast = DataContext as ToastItemViewModel;
            if (_toast == null) return;

            _title.Text = _toast.Title;
            _message.Text = _toast.Message;

            var (color, glyph) = IconStyle(_toast.Icon);
            _icon.Text = glyph;
            _icon.Foreground = new SolidColorBrush(color);
            _separator.Background = new SolidColorBrush(color, 0.31);
        }

        /// Memes couleurs et glyphes que le rendu Skia.
        private static (Color Color, string Glyph) IconStyle(SkiaOverlay.NotificationIcon icon) => icon switch
        {
            SkiaOverlay.NotificationIcon.Achievement => (Color.FromRgb(255, 215, 0), "★"),
            SkiaOverlay.NotificationIcon.StoreOk => (Color.FromRgb(80, 200, 100), "✓"),
            SkiaOverlay.NotificationIcon.StoreFail => (Color.FromRgb(220, 70, 70), "✗"),
            _ => (Color.FromRgb(80, 150, 220), "ℹ"),
        };
    }
}
