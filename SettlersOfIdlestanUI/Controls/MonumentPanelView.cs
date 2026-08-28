using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Layout;
using Avalonia.Media;
using SettlersOfIdlestanUI.ViewModels;

namespace SettlersOfIdlestanUI.Controls;

/// <summary>
/// Panneau lateral du monument selectionne : lignes d'investissement avec leur progression,
/// bonus, messages d'etat et boutons d'action.
/// </summary>
public sealed class MonumentPanelView : UserControl
{
    private static readonly SolidColorBrush DimText = new(Color.FromArgb(200, 160, 160, 170));
    private static readonly SolidColorBrush Accent = new(Color.FromArgb(230, 180, 140, 30));
    private static readonly SolidColorBrush Warning = new(Color.FromArgb(230, 220, 90, 90));
    private static readonly SolidColorBrush Corrupted = new(Color.FromArgb(230, 190, 110, 230));

    public MonumentPanelView(MonumentPanelViewModel viewModel, SvgIconCache icons)
    {
        DataContext = viewModel;
        this[!IsVisibleProperty] = new Binding(nameof(MonumentPanelViewModel.IsVisible));

        // Sans cela le UserControl s'etire sur toute la fenetre : ses bornes ne correspondraient
        // plus au panneau visible, et il capterait des clics dans le vide a cote de lui.
        HorizontalAlignment = HorizontalAlignment.Right;
        VerticalAlignment = VerticalAlignment.Top;

        var panel = new GamePanelView(title: "", onClose: null);
        panel.TitleBlock[!TextBlock.TextProperty] = new Binding(nameof(MonumentPanelViewModel.Title));

        var rows = new ItemsControl
        {
            [!ItemsControl.ItemsSourceProperty] = new Binding(nameof(MonumentPanelViewModel.Rows)),
            ItemTemplate = new FuncDataTemplate<InvestmentRowViewModel>(
                (_, _) => BuildRow(viewModel, icons), supportsRecycling: true),
        };

        var bonuses = new ItemsControl
        {
            Margin = new Thickness(10, 4),
            [!ItemsControl.ItemsSourceProperty] = new Binding(nameof(MonumentPanelViewModel.BonusLines)),
            ItemTemplate = new FuncDataTemplate<BonusLineViewModel>((_, _) => new TextBlock
            {
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 3),
                [!TextBlock.TextProperty] = new Binding(nameof(BonusLineViewModel.Text)),
                [!TextBlock.ForegroundProperty] = new Binding(nameof(BonusLineViewModel.IsActive))
                {
                    Converter = new FuncValueConverter<bool, IBrush>(a => a ? Accent : DimText),
                },
            }, supportsRecycling: true),
        };

        var content = new StackPanel { Orientation = Orientation.Vertical };
        // En premiere ligne du panneau, avant les lignes d'investissement : quand l'investissement
        // est bloque (pas de ville adjacente), le joueur doit le voir sans avoir a chercher plus bas
        // pourquoi les montants investis ne bougent plus (voir MonumentInvestment.HasAdjacentCity).
        content.Children.Add(Message(nameof(MonumentPanelViewModel.NoCityWarning), Warning));
        content.Children.Add(rows);
        content.Children.Add(bonuses);
        content.Children.Add(Message(nameof(MonumentPanelViewModel.WonderMaxedMessage), DimText));
        content.Children.Add(Message(nameof(MonumentPanelViewModel.PurifiedMessage), Accent));
        content.Children.Add(Message(nameof(MonumentPanelViewModel.CorruptedPrestigeMessage), Corrupted));
        content.Children.Add(ActionButton(
            nameof(MonumentPanelViewModel.EvolveButtonLabel),
            Color.FromArgb(230, 125, 63, 209),
            viewModel.Evolve,
            enabledPath: null));
        content.Children.Add(ActionButton(
            nameof(MonumentPanelViewModel.WonderSkipButtonLabel),
            Color.FromArgb(230, 60, 140, 220),
            viewModel.SkipWonder,
            enabledPath: nameof(MonumentPanelViewModel.CanSkipWonder)));
        // Destruction de la Spire, en bas du panneau : le libelle bascule sur une confirmation au
        // premier clic, le second seulement detruit.
        content.Children.Add(ActionButton(
            nameof(MonumentPanelViewModel.DestroyButtonLabel),
            Color.FromArgb(230, 140, 45, 45),
            viewModel.Destroy,
            enabledPath: null));

        panel.Content = content;
        _panel = panel;
        Content = panel;
    }

    private readonly GamePanelView _panel;

    /// <summary>Borne la hauteur défilante du panneau (cf. GamePanelView.SetMaxContentHeight).</summary>
    public void SetMaxContentHeight(double height) => _panel.SetMaxContentHeight(height);

    /// Message de pied de panneau, masque tant que le texte est null.
    private static TextBlock Message(string path, IBrush brush) => new()
    {
        FontSize = 12,
        Foreground = brush,
        TextWrapping = TextWrapping.Wrap,
        TextAlignment = TextAlignment.Center,
        Margin = new Thickness(10, 6),
        [!TextBlock.TextProperty] = new Binding(path),
        [!IsVisibleProperty] = new Binding(path)
        {
            Converter = new FuncValueConverter<string?, bool>(t => !string.IsNullOrEmpty(t)),
        },
    };

    /// Bouton d'action, masque tant que son libelle est null.
    private static Button ActionButton(string labelPath, Color background, Action onClick, string? enabledPath)
    {
        var button = new Button
        {
            Margin = new Thickness(10, 4),
            Height = 32,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            Background = new SolidColorBrush(background),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(6),
            FontSize = 12,
            [!ContentProperty] = new Binding(labelPath),
            [!IsVisibleProperty] = new Binding(labelPath)
            {
                Converter = new FuncValueConverter<string?, bool>(t => !string.IsNullOrEmpty(t)),
            },
        };
        button.Classes.Add(GameControlStyles.ToneButton);

        if (enabledPath != null) button[!IsEnabledProperty] = new Binding(enabledPath);
        button.Click += (_, _) => onClick();
        return button;
    }

    private static Control BuildRow(MonumentPanelViewModel viewModel, SvgIconCache icons)
    {
        var checkbox = new CheckBox { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) };
        checkbox[!ToggleButton.IsCheckedProperty] = new Binding(nameof(InvestmentRowViewModel.IsEnabled));
        // Une ligne complete n'est plus decochable : c'est un etat verrouille, distinct de
        // "desactivee", pour ne pas laisser croire que la case est bloquee sans raison.
        checkbox[!IsEnabledProperty] = new Binding(nameof(InvestmentRowViewModel.IsDone))
        {
            Converter = new FuncValueConverter<bool, bool>(done => !done),
        };
        checkbox.Click += (sender, _) =>
        {
            if (((Control)sender!).DataContext is InvestmentRowViewModel row)
                viewModel.ToggleInvestment(row.Key);
        };

        var icon = new Image { Width = 16, Height = 16, VerticalAlignment = VerticalAlignment.Center };

        var label = new TextBlock
        {
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4, 0, 0, 0),
            [!TextBlock.TextProperty] = new Binding(nameof(InvestmentRowViewModel.Label)),
            [!TextBlock.ForegroundProperty] = new Binding(nameof(InvestmentRowViewModel.IsDone))
            {
                Converter = new FuncValueConverter<bool, IBrush>(d => d ? DimText : Brushes.White),
            },
        };

        var amount = new TextBlock
        {
            FontSize = 10,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
            [!TextBlock.TextProperty] = new Binding(nameof(InvestmentRowViewModel.AmountLabel)),
            [!TextBlock.ForegroundProperty] = new Binding(nameof(InvestmentRowViewModel.IsDone))
            {
                Converter = new FuncValueConverter<bool, IBrush>(d => d ? Accent : DimText),
            },
        };

        var top = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(checkbox, Dock.Left);
        DockPanel.SetDock(icon, Dock.Left);
        DockPanel.SetDock(amount, Dock.Right);
        top.Children.Add(checkbox);
        top.Children.Add(icon);
        top.Children.Add(amount);
        top.Children.Add(label);

        var bar = new ProgressBar
        {
            Minimum = 0,
            Maximum = 1,
            Height = 10,
            Margin = new Thickness(0, 4, 0, 0),
            Foreground = Accent,
            Background = new SolidColorBrush(Color.FromArgb(200, 50, 50, 65)),
            [!RangeBase.ValueProperty] = new Binding(nameof(InvestmentRowViewModel.Progress)),
        };

        var row = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(10, 6) };
        row.Children.Add(top);
        row.Children.Add(bar);

        // L'icone depend de la ligne : elle est resolue au rattachement du DataContext.
        row.DataContextChanged += (sender, _) =>
        {
            if (((Control)sender!).DataContext is InvestmentRowViewModel vm)
                icon.Source = vm.IconName != null ? icons.GetResourceIcon(vm.IconName, 16) : null;
        };

        return row;
    }
}
