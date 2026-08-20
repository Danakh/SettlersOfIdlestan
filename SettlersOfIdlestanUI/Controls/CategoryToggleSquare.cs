using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using SkiaLayer = SettlersOfIdlestanSkia.Services;

namespace SettlersOfIdlestanUI.Controls;

/// <summary>
/// Case carree remplacant le CheckBox Avalonia pour une bascule d'automatisme : sa couleur porte
/// la famille de l'automatisme (voir AutomationCategory) plutot qu'une teinte neutre du theme —
/// pleine quand actif, contour seul quand inactif, demi-remplie en etat mixte. Un Border plutot
/// qu'un CheckBox restyle : le template CheckBox du theme ne permet de recolorer que le glyphe de
/// coche, jamais le fond de la case elle-meme.
///
/// Se lie a "Category" et "IsOn" du DataContext par leur nom plutot que via nameof : partagee par
/// CivToggleViewModel (panneau civilisation) et AutomationRowViewModel (page Automatisation), qui
/// exposent toutes les deux ces proprietes sous ce nom.
/// </summary>
public sealed class CategoryToggleSquare : Border
{
    private static readonly SolidColorBrush ConstructionColor = new(Color.FromRgb(90, 175, 145));
    private static readonly SolidColorBrush BehaviorColor = new(Color.FromRgb(170, 130, 220));
    private static readonly SolidColorBrush ActivationColor = new(Color.FromRgb(220, 160, 80));
    private static readonly SolidColorBrush OffFill = new(Color.FromRgb(45, 45, 58));

    public event EventHandler? Click;

    public CategoryToggleSquare()
    {
        Width = 16;
        Height = 16;
        CornerRadius = new CornerRadius(3);
        BorderThickness = new Thickness(1.5);
        VerticalAlignment = VerticalAlignment.Center;
        Cursor = new Cursor(StandardCursorType.Hand);

        this[!BorderBrushProperty] = new Binding("Category")
        {
            Converter = new FuncValueConverter<SkiaLayer.AutomationCategory, IBrush>(AccentFor),
        };

        this[!BackgroundProperty] = new MultiBinding
        {
            Bindings = { new Binding("Category"), new Binding("IsOn") },
            Converter = new FillConverter(),
        };

        PointerPressed += (_, e) =>
        {
            e.Handled = true;
            Click?.Invoke(this, EventArgs.Empty);
        };
    }

    private static IBrush AccentFor(SkiaLayer.AutomationCategory category) => category switch
    {
        SkiaLayer.AutomationCategory.Behavior => BehaviorColor,
        SkiaLayer.AutomationCategory.Activation => ActivationColor,
        _ => ConstructionColor,
    };

    private sealed class FillConverter : IMultiValueConverter
    {
        public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            var category = values.Count > 0 && values[0] is SkiaLayer.AutomationCategory c
                ? c : SkiaLayer.AutomationCategory.Construction;
            bool? isOn = values.Count > 1 ? values[1] as bool? : null;
            var accent = AccentFor(category);

            return isOn switch
            {
                true => accent,
                null => new SolidColorBrush(((SolidColorBrush)accent).Color, 0.5),
                _ => OffFill,
            };
        }
    }
}
