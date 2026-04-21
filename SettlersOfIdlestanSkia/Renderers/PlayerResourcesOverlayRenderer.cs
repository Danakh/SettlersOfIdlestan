using SkiaSharp;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestanSkia.Renderers;

/// <summary>
/// Renderer affichant un overlay avec les ressources du joueur actuel en haut à droite.
/// L'overlay s'affiche par-dessus tous les autres éléments du jeu.
/// </summary>
public class PlayerResourcesOverlayRenderer : IGameRenderer
{
    private SKPaint? _backgroundPaint;
    private SKPaint? _textPaint;
    private SKPaint? _borderPaint;
    private SKTypeface? _typeface;

    private SKSize _canvasSize;
    private bool _disposed;

    // Couleurs par type de ressource
    private static readonly Dictionary<Resource, SKColor> ResourceColors = new()
    {
        { Resource.Wood, new SKColor(139, 69, 19) },    // Marron
        { Resource.Brick, new SKColor(210, 105, 30) },  // Orange-marron
        { Resource.Sheep, new SKColor(255, 192, 203) }, // Rose
        { Resource.Wheat, new SKColor(255, 215, 0) },   // Or
        { Resource.Ore, new SKColor(128, 128, 128) }    // Gris
    };

    public bool Disposed => _disposed;

    public void Initialize(SKSize canvasSize)
    {
        _canvasSize = canvasSize;

        _backgroundPaint = new SKPaint
        {
            Color = new SKColor(0, 0, 0, 200),  // Noir semi-transparent
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        _borderPaint = new SKPaint
        {
            Color = SKColors.Gold,
            StrokeWidth = 2,
            Style = SKPaintStyle.Stroke,
            IsAntialias = true
        };

        _textPaint = new SKPaint
        {
            Color = SKColors.White,
            TextSize = 14,
            IsAntialias = true,
            TextAlign = SKTextAlign.Left
        };

        _typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold);
    }

    public void Render(SKCanvas canvas, GameRenderContext context)
    {
        if (context.GameState == null || _disposed)
            return;

        if (context.GameState is not MainGameState mainGameState)
            return;

        var islandState = mainGameState.CurrentIslandState;
        if (islandState == null)
            return;

        // Récupère les ressources du joueur actuel (première civilisation pour l'instant)
        if (islandState.Civilizations.Count == 0)
            return;

        var currentCivilization = islandState.Civilizations[0];

        DrawResourcesOverlay(canvas, currentCivilization);
    }

    private void DrawResourcesOverlay(SKCanvas canvas, SettlersOfIdlestan.Model.Civilization.Civilization civilization)
    {
        // Paramètres de position et taille
        const float padding = 12;
        const float itemHeight = 24;
        const float itemSpacing = 4;
        const float cornerRadius = 8;

        // Énumère tous les types de ressources
        var resourceTypes = Enum.GetValues(typeof(Resource)).Cast<Resource>().ToList();

        // Calcule la taille de la capsule
        var numResources = resourceTypes.Count;
        var containerHeight = padding * 2 + numResources * itemHeight + (numResources - 1) * itemSpacing;
        var maxTextWidth = CalculateMaxTextWidth();
        var containerWidth = padding * 2 + 20 + 8 + maxTextWidth;  // 20 pour la couleur, 8 d'espacement

        // Position en haut à droite
        var xStart = _canvasSize.Width - containerWidth - padding;
        var yStart = padding;

        // Dessine le fond avec bordure
        var rect = new SKRect(xStart, yStart, xStart + containerWidth, yStart + containerHeight);

        // Fond arrondi
        canvas.DrawRoundRect(rect, cornerRadius, cornerRadius, _backgroundPaint);

        // Bordure arrondie
        canvas.DrawRoundRect(rect, cornerRadius, cornerRadius, _borderPaint);

        // Dessine chaque ressource
        float currentY = yStart + padding;
        foreach (var resource in resourceTypes)
        {
            var quantity = civilization.GetResourceQuantity(resource);
            DrawResourceItem(canvas, resource, quantity, xStart + padding, currentY);
            currentY += itemHeight + itemSpacing;
        }
    }

    private void DrawResourceItem(SKCanvas canvas, Resource resource, int quantity, float x, float y)
    {
        // Carré de couleur de la ressource
        var colorRect = new SKRect(x, y + 2, x + 16, y + 18);
        using (var colorPaint = new SKPaint { Color = ResourceColors[resource], Style = SKPaintStyle.Fill, IsAntialias = true })
        {
            canvas.DrawRect(colorRect, colorPaint);
        }

        // Texte du nom et de la quantité
        var resourceName = resource.ToString();
        var text = $"{resourceName}: {quantity}";

        if (_textPaint != null && _typeface != null)
        {
            _textPaint.Typeface = _typeface;
            canvas.DrawText(text, x + 20, y + 16, _textPaint);
        }
    }

    private float CalculateMaxTextWidth()
    {
        float maxWidth = 0;

        if (_textPaint == null)
            return 120;

        foreach (var resource in Enum.GetValues(typeof(Resource)).Cast<Resource>())
        {
            var text = $"{resource}: 999";
            var width = _textPaint.MeasureText(text);
            maxWidth = Math.Max(maxWidth, width);
        }

        return maxWidth;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _backgroundPaint?.Dispose();
        _textPaint?.Dispose();
        _borderPaint?.Dispose();
        _typeface?.Dispose();
        _disposed = true;
    }
}
