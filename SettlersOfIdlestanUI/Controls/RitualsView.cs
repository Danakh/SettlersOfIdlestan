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
/// Onglet plein ecran des rituels : puissance disponible et cristaux en tete, puis une carte par
/// rituel connu (lancer/arreter, reglage de puissance) et par sort instantane.
///
/// Les boutons indisponibles restent cliquables, comme dans le rendu Skia : c'est le
/// MagicController qui refuse. Leur apparence seule change.
/// </summary>
public sealed class RitualsView : UserControl
{
    private static readonly SolidColorBrush PanelBackground = new(Color.FromArgb(240, 18, 18, 24));
    private static readonly SolidColorBrush Card = new(Color.FromArgb(220, 30, 30, 40));
    private static readonly SolidColorBrush CardActive = new(Color.FromArgb(230, 35, 30, 55));
    private static readonly SolidColorBrush CardBorder = new(Color.FromRgb(60, 60, 80));
    private static readonly SolidColorBrush CardActiveBorder = new(Color.FromRgb(140, 100, 220));
    private static readonly SolidColorBrush Launch = new(Color.FromRgb(90, 60, 160));
    private static readonly SolidColorBrush Stop = new(Color.FromRgb(120, 55, 55));
    private static readonly SolidColorBrush Disabled = new(Color.FromRgb(60, 60, 70));
    private static readonly SolidColorBrush Accent = new(Color.FromRgb(190, 150, 255));
    private static readonly SolidColorBrush NameText = new(Color.FromRgb(230, 230, 240));
    private static readonly SolidColorBrush Desc = new(Color.FromRgb(150, 150, 165));
    private static readonly SolidColorBrush Cost = new(Color.FromRgb(170, 150, 220));
    private static readonly SolidColorBrush Bonus = new(Color.FromRgb(120, 200, 140));
    private static readonly SolidColorBrush Warning = new(Color.FromRgb(210, 140, 90));
    private static readonly SolidColorBrush Muted = new(Color.FromRgb(110, 110, 125));
    private static readonly SolidColorBrush Summary = new(Color.FromRgb(200, 200, 215));

    public RitualsView(RitualsViewModel viewModel)
    {
        DataContext = viewModel;
        this[!IsVisibleProperty] = new Binding(nameof(RitualsViewModel.IsVisible));

        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;

        var header = new TextBlock
        {
            FontSize = 17,
            FontWeight = FontWeight.Bold,
            Foreground = Accent,
            Margin = new Thickness(0, 0, 0, 12),
            [!TextBlock.TextProperty] = new Binding(nameof(RitualsViewModel.Title)),
        };

        // Puissance a gauche, cristaux a droite, chacun avec son infobulle explicative.
        var stats = new DockPanel { LastChildFill = false, Margin = new Thickness(0, 0, 0, 12) };
        var power = SummaryText(nameof(RitualsViewModel.PowerLabel), nameof(RitualsViewModel.PowerTooltip));
        var crystals = SummaryText(nameof(RitualsViewModel.CrystalsLabel), nameof(RitualsViewModel.CrystalsTooltip));
        DockPanel.SetDock(power, Dock.Left);
        DockPanel.SetDock(crystals, Dock.Right);
        stats.Children.Add(power);
        stats.Children.Add(crystals);

        var noRituals = new TextBlock
        {
            FontSize = 11,
            Foreground = Muted,
            Margin = new Thickness(0, 0, 0, 8),
            [!TextBlock.TextProperty] = new Binding(nameof(RitualsViewModel.NoRitualsMessage)),
            [!IsVisibleProperty] = new Binding(nameof(RitualsViewModel.HasNoRituals)),
        };

        var rituals = new ItemsControl
        {
            [!ItemsControl.ItemsSourceProperty] = new Binding(nameof(RitualsViewModel.Rituals)),
            ItemsPanel = new FuncTemplate<Panel?>(() => new StackPanel { Spacing = 8 }),
            ItemTemplate = new FuncDataTemplate<RitualRowViewModel>(
                (_, _) => new RitualCard(viewModel), supportsRecycling: true),
        };

        var spellsHeader = new TextBlock
        {
            FontSize = 13,
            FontWeight = FontWeight.Bold,
            Foreground = Accent,
            Margin = new Thickness(0, 18, 0, 8),
            [!TextBlock.TextProperty] = new Binding(nameof(RitualsViewModel.SpellsHeader)),
            [!IsVisibleProperty] = new Binding(nameof(RitualsViewModel.HasSpells)),
        };

        var spells = new ItemsControl
        {
            [!ItemsControl.ItemsSourceProperty] = new Binding(nameof(RitualsViewModel.Spells)),
            ItemsPanel = new FuncTemplate<Panel?>(() => new StackPanel { Spacing = 8 }),
            ItemTemplate = new FuncDataTemplate<SpellRowViewModel>(
                (_, _) => new SpellCard(viewModel), supportsRecycling: true),
        };

        var stack = new StackPanel { Orientation = Orientation.Vertical };
        stack.Children.Add(header);
        stack.Children.Add(stats);
        stack.Children.Add(noRituals);
        stack.Children.Add(rituals);
        stack.Children.Add(spellsHeader);
        stack.Children.Add(spells);

        Content = new Border
        {
            Background = PanelBackground,
            Child = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Padding = new Thickness(20),
                Content = new Border { MaxWidth = 640, HorizontalAlignment = HorizontalAlignment.Center, Child = stack },
            },
        };
    }

    private static Control SummaryText(string textPath, string tooltipPath) => new TextBlock
    {
        FontSize = 11,
        Foreground = Summary,
        VerticalAlignment = VerticalAlignment.Center,
        // Fond transparent : sans geometrie remplie, pas de survol donc pas d'infobulle.
        Background = Brushes.Transparent,
        [!TextBlock.TextProperty] = new Binding(textPath),
        [!ToolTip.TipProperty] = new Binding(tooltipPath),
    };

    /// <summary>Texte d'une carte : nom, description, coût, et une ligne optionnelle.</summary>
    private static StackPanel CardText(string namePath, string descPath, string costPath,
        string extraPath, string hasExtraPath, IBrush extraBrush)
    {
        var stack = new StackPanel { Orientation = Orientation.Vertical, VerticalAlignment = VerticalAlignment.Center };

        stack.Children.Add(new TextBlock
        {
            FontSize = 13,
            FontWeight = FontWeight.Bold,
            Foreground = NameText,
            TextWrapping = TextWrapping.Wrap,
            [!TextBlock.TextProperty] = new Binding(namePath),
        });
        stack.Children.Add(new TextBlock
        {
            FontSize = 11,
            Foreground = Desc,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 0),
            [!TextBlock.TextProperty] = new Binding(descPath),
        });
        stack.Children.Add(new TextBlock
        {
            FontSize = 11,
            Foreground = Cost,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 6, 0, 0),
            [!TextBlock.TextProperty] = new Binding(costPath),
        });
        stack.Children.Add(new TextBlock
        {
            FontSize = 11,
            Foreground = extraBrush,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 6, 0, 0),
            [!TextBlock.TextProperty] = new Binding(extraPath),
            [!IsVisibleProperty] = new Binding(hasExtraPath),
        });

        return stack;
    }

    private static Button ActionButton(string labelPath, string enabledPath, IBrush enabledBrush)
    {
        var button = new Button
        {
            Width = 76,
            Height = 26,
            FontSize = 11,
            FontWeight = FontWeight.Bold,
            Foreground = Brushes.White,
            Padding = new Thickness(0),
            CornerRadius = new CornerRadius(5),
            BorderBrush = new SolidColorBrush(Color.FromRgb(120, 120, 140)),
            BorderThickness = new Thickness(1),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            [!ContentProperty] = new Binding(labelPath),
            [!BackgroundProperty] = new Binding(enabledPath)
            {
                Converter = new FuncValueConverter<bool, IBrush>(e => e ? enabledBrush : Disabled),
            },
        };
        button.Classes.Add(GameControlStyles.ToneButton);
        return button;
    }

    /// <summary>Une carte de rituel : texte a gauche, bouton et reglage de puissance a droite.</summary>
    private sealed class RitualCard : Border
    {
        private readonly RitualsViewModel _owner;
        private RitualRowViewModel? _row;

        public RitualCard(RitualsViewModel owner)
        {
            _owner = owner;

            Padding = new Thickness(14, 12);
            CornerRadius = new CornerRadius(6);
            BorderThickness = new Thickness(1);
            this[!BackgroundProperty] = new Binding(nameof(RitualRowViewModel.IsActive))
            {
                Converter = new FuncValueConverter<bool, IBrush>(a => a ? CardActive : Card),
            };
            this[!BorderBrushProperty] = new Binding(nameof(RitualRowViewModel.IsActive))
            {
                Converter = new FuncValueConverter<bool, IBrush>(a => a ? CardActiveBorder : CardBorder),
            };

            var toggle = ActionButton(nameof(RitualRowViewModel.ButtonLabel),
                nameof(RitualRowViewModel.IsButtonEnabled), Launch);
            // Le bouton devient rouge une fois le rituel lance : il l'arrete.
            toggle[!BackgroundProperty] = new MultiBinding
            {
                Bindings =
                {
                    new Binding(nameof(RitualRowViewModel.IsActive)),
                    new Binding(nameof(RitualRowViewModel.IsButtonEnabled)),
                },
                Converter = new FuncMultiValueConverter<object?, IBrush>(values =>
                {
                    var list = values.ToList();
                    bool active = list.Count > 0 && list[0] is true;
                    bool enabled = list.Count > 1 && list[1] is true;
                    return active ? Stop : enabled ? Launch : Disabled;
                }),
            };
            toggle.Click += (_, _) => { if (_row != null) _owner.ToggleRitual(_row); };

            var minus = PowerButton("-", Stop);
            minus.Click += (_, _) => { if (_row != null) _owner.ChangePower(_row, increase: false); };

            var powerText = new TextBlock
            {
                FontSize = 14,
                FontWeight = FontWeight.Bold,
                Foreground = Accent,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                Width = 24,
                [!TextBlock.TextProperty] = new Binding(nameof(RitualRowViewModel.PowerText)),
            };

            var plus = PowerButton("+", Launch);
            plus[!BackgroundProperty] = new Binding(nameof(RitualRowViewModel.CanIncreasePower))
            {
                Converter = new FuncValueConverter<bool, IBrush>(c => c ? Launch : Disabled),
            };
            plus.Click += (_, _) => { if (_row != null) _owner.ChangePower(_row, increase: true); };

            var powerRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 8, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Center,
                // Le reglage de puissance n'a de sens que sur un rituel en cours.
                [!IsVisibleProperty] = new Binding(nameof(RitualRowViewModel.IsActive)),
            };
            powerRow.Children.Add(minus);
            powerRow.Children.Add(powerText);
            powerRow.Children.Add(plus);

            var right = new StackPanel { Orientation = Orientation.Vertical, VerticalAlignment = VerticalAlignment.Top };
            right.Children.Add(toggle);
            right.Children.Add(powerRow);

            var layout = new DockPanel { LastChildFill = true };
            DockPanel.SetDock(right, Dock.Right);
            layout.Children.Add(right);
            layout.Children.Add(CardText(
                nameof(RitualRowViewModel.Name), nameof(RitualRowViewModel.Description),
                nameof(RitualRowViewModel.CostText), nameof(RitualRowViewModel.BonusText),
                nameof(RitualRowViewModel.HasBonus), Bonus));

            Child = layout;
        }

        private static Button PowerButton(string glyph, IBrush brush)
        {
            var button = new Button
            {
                Width = 26,
                Height = 26,
                FontSize = 14,
                FontWeight = FontWeight.Bold,
                Foreground = Brushes.White,
                Background = brush,
                Padding = new Thickness(0),
                CornerRadius = new CornerRadius(5),
                BorderBrush = new SolidColorBrush(Color.FromRgb(120, 120, 140)),
                BorderThickness = new Thickness(1),
                Content = glyph,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
            };
            button.Classes.Add(GameControlStyles.ToneButton);
            return button;
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);
            _row = DataContext as RitualRowViewModel;
        }
    }

    /// <summary>Une carte de sort : texte a gauche, bouton de lancement a droite.</summary>
    private sealed class SpellCard : Border
    {
        private readonly RitualsViewModel _owner;
        private SpellRowViewModel? _row;

        public SpellCard(RitualsViewModel owner)
        {
            _owner = owner;

            Padding = new Thickness(14, 12);
            CornerRadius = new CornerRadius(6);
            BorderThickness = new Thickness(1);
            Background = Card;
            BorderBrush = CardBorder;

            var cast = ActionButton(nameof(SpellRowViewModel.ButtonLabel), nameof(SpellRowViewModel.CanCast), Launch);
            cast.Click += (_, _) => { if (_row != null) _owner.CastSpell(_row); };

            var cooldownBar = new ProgressBar
            {
                Minimum = 0,
                Maximum = 1,
                Width = 76,
                MinWidth = 0,
                Height = 5,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 4, 0, 0),
                Foreground = Accent,
                Background = new SolidColorBrush(Color.FromArgb(160, 50, 50, 65)),
                [!RangeBase.ValueProperty] = new Binding(nameof(SpellRowViewModel.CooldownRatio)),
                [!IsVisibleProperty] = new Binding(nameof(SpellRowViewModel.HasExhaustion)),
                [!ToolTip.TipProperty] = new Binding(nameof(SpellRowViewModel.CooldownTooltip)),
            };

            var right = new StackPanel { Orientation = Orientation.Vertical, VerticalAlignment = VerticalAlignment.Center };
            right.Children.Add(cast);
            right.Children.Add(cooldownBar);

            var layout = new DockPanel { LastChildFill = true };
            DockPanel.SetDock(right, Dock.Right);
            layout.Children.Add(right);
            layout.Children.Add(CardText(
                nameof(SpellRowViewModel.Name), nameof(SpellRowViewModel.Description),
                nameof(SpellRowViewModel.CostText), nameof(SpellRowViewModel.WarningText),
                nameof(SpellRowViewModel.HasWarning), Warning));

            Child = layout;
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);
            _row = DataContext as SpellRowViewModel;
        }
    }
}
