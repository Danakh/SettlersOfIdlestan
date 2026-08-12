using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using SettlersOfIdlestanUI.ViewModels;

namespace SettlersOfIdlestanUI.Controls;

/// <summary>
/// Menu deroulant de l'engrenage, ancre sous la barre du haut a droite.
///
/// Cette vue ne porte que la boite du menu. Le capteur de fermeture est expose a part
/// (<see cref="Catcher"/>) parce que les deux ne vont pas au meme etage de l'arbre visuel :
/// la boite s'ouvre a l'emplacement exact du panneau de ville et doit donc passer AU-DESSUS des
/// panneaux, alors que le capteur doit rester SOUS la barre du haut pour laisser son clic a
/// l'engrenage, qui referme le menu en le rebasculant.
/// </summary>
public sealed class SettingsMenuView : UserControl
{
    private const double MenuWidth = 250;

    private static readonly SolidColorBrush PanelBackground = new(Color.FromArgb(220, 0, 0, 0));
    private static readonly SolidColorBrush ItemBackground = new(Color.FromArgb(240, 40, 40, 40));
    private static readonly SolidColorBrush Border_ = new(Colors.Gold);
    private static readonly SolidColorBrush SeparatorText = new(Color.FromArgb(180, 150, 150, 150));

    private readonly Border _box;

    /// <summary>
    /// Capteur transparent plein ecran qui referme le menu sur un clic ailleurs, la ou le rendu
    /// Skia comparait la position du clic au rectangle du menu. A poser separement dans l'arbre,
    /// sous la barre du haut (cf. GameView).
    /// </summary>
    public Control Catcher { get; }

    public SettingsMenuView(SettingsMenuViewModel viewModel, double topOffset)
    {
        DataContext = viewModel;
        this[!IsVisibleProperty] = new Binding(nameof(SettingsMenuViewModel.IsOpen));

        // Sans alignement explicite le UserControl s'etire sur toute la fenetre : ses bornes ne
        // correspondraient plus a la boite visible et le hit-testing deviendrait imprevisible.
        HorizontalAlignment = HorizontalAlignment.Right;
        VerticalAlignment = VerticalAlignment.Top;

        var items = new ItemsControl
        {
            [!ItemsControl.ItemsSourceProperty] = new Binding(nameof(SettingsMenuViewModel.Items)),
            ItemsPanel = new FuncTemplate<Panel?>(() => new StackPanel { Orientation = Orientation.Vertical }),
            ItemTemplate = new FuncDataTemplate<SettingsMenuItemViewModel>(
                (_, _) => BuildItem(viewModel), supportsRecycling: true),
        };

        _box = new Border
        {
            Width = MenuWidth,
            Background = PanelBackground,
            BorderBrush = Border_,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, topOffset, 10, 0),
            Child = items,
        };

        // DataContext propre : le capteur ne fait pas partie des enfants de cette vue, il ne
        // l'heriterait donc pas.
        var catcher = new Border { Background = Brushes.Transparent, DataContext = viewModel };
        catcher[!IsVisibleProperty] = new Binding(nameof(SettingsMenuViewModel.IsOpen));
        catcher.PointerPressed += (_, e) =>
        {
            e.Handled = true;
            viewModel.Close();
        };
        Catcher = catcher;

        Content = _box;
    }

    /// <summary>
    /// Reancre le menu sous la barre du haut. Il s'ouvre sous l'engrenage, mais se cale sous la
    /// barre entiere : quand les ressources se replient sur une seconde ligne, s'en tenir a la
    /// ligne de l'engrenage le ferait chevaucher les pastilles.
    /// </summary>
    public void SetTopOffset(double topOffset) =>
        _box.Margin = new Thickness(0, topOffset, 10, 0);

    private static Control BuildItem(SettingsMenuViewModel owner)
    {
        SettingsMenuItemViewModel? model = null;

        var label = new TextBlock
        {
            FontSize = 12,
            FontWeight = FontWeight.Bold,
            VerticalAlignment = VerticalAlignment.Center,
            [!TextBlock.TextProperty] = new Binding(nameof(SettingsMenuItemViewModel.Label)),
        };

        var item = new Border
        {
            // Fond transparent plutot qu'absent : sans geometrie remplie, la ligne ne recevrait
            // pas le clic.
            Background = Brushes.Transparent,
            Child = label,
        };

        item.PointerPressed += (_, e) =>
        {
            if (model is not { IsClickable: true }) return;
            e.Handled = true;
            owner.Invoke(model);
        };

        item.DataContextChanged += (_, _) =>
        {
            model = item.DataContext as SettingsMenuItemViewModel;
            if (model == null) return;

            if (model.IsSeparator)
            {
                item.Height = 10;
                item.Background = Brushes.Transparent;
                item.BorderThickness = new Thickness(0);
                item.Padding = new Thickness(0);
                label.Foreground = SeparatorText;
                label.HorizontalAlignment = HorizontalAlignment.Center;
            }
            else
            {
                item.Height = 35;
                item.Background = ItemBackground;
                item.BorderBrush = Border_;
                item.BorderThickness = new Thickness(1);
                item.Padding = new Thickness(8, 0);
                label.Foreground = Brushes.White;
                label.HorizontalAlignment = HorizontalAlignment.Left;
            }
        };

        return item;
    }
}
