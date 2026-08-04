using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using SettlersOfIdlestanUI.ViewModels;

namespace SettlersOfIdlestanUI.Controls;

/// <summary>
/// Panneau de reglages, partage par le popup en jeu et l'ecran-titre — un seul gabarit pour les
/// deux, la ou le rendu Skia avait deja un SettingsContentPanel commun.
///
/// Une ligne n'affiche qu'un controle selon sa nature : bascule, choix exclusif, curseur ou
/// champ de texte.
/// </summary>
public sealed class SettingsPanelView : UserControl
{
    private static readonly SolidColorBrush Label = new(Color.FromRgb(220, 220, 230));
    private static readonly SolidColorBrush Muted = new(Color.FromRgb(120, 120, 132));
    private static readonly SolidColorBrush ChoiceActive = new(Color.FromRgb(60, 100, 160));
    private static readonly SolidColorBrush ChoiceInactive = new(Color.FromRgb(45, 45, 58));

    public SettingsPanelView(SettingsPanelViewModel viewModel)
    {
        DataContext = viewModel;
        HorizontalAlignment = HorizontalAlignment.Stretch;

        Content = new ItemsControl
        {
            [!ItemsControl.ItemsSourceProperty] = new Binding(nameof(SettingsPanelViewModel.Rows)),
            ItemsPanel = new FuncTemplate<Panel?>(() => new StackPanel { Spacing = 10 }),
            ItemTemplate = new FuncDataTemplate<SettingRowViewModel>(
                (_, _) => new SettingRow(viewModel), supportsRecycling: true),
        };
    }

    /// <summary>Une ligne : libelle a gauche, controle a droite selon la nature du reglage.</summary>
    private sealed class SettingRow : Border
    {
        private readonly SettingsPanelViewModel _owner;
        private SettingRowViewModel? _row;

        public SettingRow(SettingsPanelViewModel owner)
        {
            _owner = owner;

            Background = Brushes.Transparent;
            Padding = new Thickness(0, 2);

            var label = new TextBlock
            {
                FontSize = 13,
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                [!TextBlock.TextProperty] = new Binding(nameof(SettingRowViewModel.Label)),
                [!TextBlock.ForegroundProperty] = new Binding(nameof(SettingRowViewModel.IsEnabled))
                {
                    Converter = new FuncValueConverter<bool, IBrush>(e => e ? Label : Muted),
                },
            };

            var controls = new Panel { HorizontalAlignment = HorizontalAlignment.Right };
            controls.Children.Add(BuildToggle());
            controls.Children.Add(BuildChoices());
            controls.Children.Add(BuildSlider());
            controls.Children.Add(BuildTextInput());

            var layout = new DockPanel { LastChildFill = true };
            DockPanel.SetDock(controls, Dock.Right);
            layout.Children.Add(controls);
            layout.Children.Add(label);

            Child = layout;
        }

        private Control BuildToggle()
        {
            var toggle = new CheckBox
            {
                VerticalAlignment = VerticalAlignment.Center,
                [!IsVisibleProperty] = new Binding(nameof(SettingRowViewModel.IsToggle)),
                [!IsEnabledProperty] = new Binding(nameof(SettingRowViewModel.IsEnabled)),
                [!ToggleButton.IsCheckedProperty] = new Binding(nameof(SettingRowViewModel.ToggleValue)),
            };
            toggle.Click += (_, _) => { if (_row != null) _owner.Toggle(_row); };
            return toggle;
        }

        private Control BuildChoices() => new ItemsControl
        {
            VerticalAlignment = VerticalAlignment.Center,
            [!IsVisibleProperty] = new Binding(nameof(SettingRowViewModel.IsChoice)),
            [!ItemsControl.ItemsSourceProperty] = new Binding(nameof(SettingRowViewModel.Choices)),
            ItemsPanel = new FuncTemplate<Panel?>(() => new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
            }),
            ItemTemplate = new FuncDataTemplate<SettingChoiceViewModel>((_, _) => BuildChoiceButton(), true),
        };

        private Control BuildChoiceButton()
        {
            SettingChoiceViewModel? choice = null;

            var button = new Button
            {
                MinWidth = 90,
                Height = 26,
                FontSize = 12,
                Padding = new Thickness(8, 0),
                Foreground = Brushes.White,
                CornerRadius = new CornerRadius(4),
                BorderThickness = new Thickness(0),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                [!ContentProperty] = new Binding(nameof(SettingChoiceViewModel.Label)),
                [!BackgroundProperty] = new Binding(nameof(SettingChoiceViewModel.IsSelected))
                {
                    Converter = new FuncValueConverter<bool, IBrush>(s => s ? ChoiceActive : ChoiceInactive),
                },
            };
            // L'option retenue ne se distingue que par son fond.
            button.Classes.Add(GameControlStyles.ToneButton);
            button.DataContextChanged += (_, _) => choice = button.DataContext as SettingChoiceViewModel;
            button.Click += (_, _) => { if (_row != null && choice != null) _owner.SelectChoice(_row, choice); };
            return button;
        }

        private Control BuildSlider()
        {
            var slider = new Slider
            {
                Width = 160,
                VerticalAlignment = VerticalAlignment.Center,
                [!Slider.MinimumProperty] = new Binding(nameof(SettingRowViewModel.SliderMin)),
                [!Slider.MaximumProperty] = new Binding(nameof(SettingRowViewModel.SliderMax)),
                [!Slider.ValueProperty] = new Binding(nameof(SettingRowViewModel.SliderValue)),
                TickFrequency = 0.1,
                IsSnapToTickEnabled = true,
            };

            // Applique au relachement plutot qu'a chaque pas : l'echelle d'interface fait
            // relayouter tout le jeu, la suivre en continu pendant le glissement serait saccade.
            slider.AddHandler(PointerReleasedEvent, (_, _) =>
            {
                if (_row != null) _owner.SetSlider(_row, slider.Value);
            }, RoutingStrategies.Tunnel);

            var text = new TextBlock
            {
                FontSize = 12,
                Foreground = Label,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0),
                Width = 40,
                [!TextBlock.TextProperty] = new Binding(nameof(SettingRowViewModel.SliderText)),
            };

            var stack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                [!IsVisibleProperty] = new Binding(nameof(SettingRowViewModel.IsSlider)),
            };
            stack.Children.Add(slider);
            stack.Children.Add(text);
            return stack;
        }

        private Control BuildTextInput()
        {
            var box = new TextBox
            {
                Width = 160,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                [!IsVisibleProperty] = new Binding(nameof(SettingRowViewModel.IsTextInput)),
                [!TextBox.TextProperty] = new Binding(nameof(SettingRowViewModel.TextValue)),
            };

            // Applique a la validation, comme le champ Skia qui attendait Entree.
            box.KeyDown += (_, e) =>
            {
                if (e.Key != Key.Enter || _row == null) return;
                e.Handled = true;
                _owner.SetText(_row, box.Text ?? "");
            };
            return box;
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);
            _row = DataContext as SettingRowViewModel;
        }
    }
}
