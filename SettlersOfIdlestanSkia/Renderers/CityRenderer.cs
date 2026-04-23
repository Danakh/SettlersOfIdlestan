using SkiaSharp;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestan.Model.Game;

namespace SettlersOfIdlestanSkia.Renderers;

/// <summary>
/// Renderer pour afficher les villes (settlements et cities).
/// </summary>
public class CityRenderer : HexBasedRenderer, IGameRenderer
{
    private bool _disposed;

    private const float CityRadius = 8f;
    private const float SettlementRadius = 6f;

    private SKPaint? _settlementPaint;
    private SKPaint? _cityPaint;
    private SKPaint? _borderPaint;

    // Couleurs pour les civilisations
    private static readonly SKColor[] CivilizationColors = new[]
    {
        new SKColor(255, 0, 0),     // Rouge - Civ 0
        new SKColor(0, 0, 255),     // Bleu - Civ 1
        new SKColor(0, 200, 0),     // Vert - Civ 2
        new SKColor(255, 200, 0),   // Orange - Civ 3
    };

    public void Initialize(SKSize canvasSize)
    {
        _settlementPaint = new SKPaint
        {
            Style = SKPaintStyle.Fill,
            IsAntialias = true,
        };

        _cityPaint = new SKPaint
        {
            Style = SKPaintStyle.Fill,
            IsAntialias = true,
        };

        _borderPaint = new SKPaint
        {
            Color = SKColors.Black,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2,
            IsAntialias = true,
        };
    }

    public void Render(SKCanvas canvas, GameRenderContext context)
    {
        if (context.GameState == null || _settlementPaint == null || _cityPaint == null || _borderPaint == null)
            return;

        if (context.GameState is MainGameState mainGameState)
        {
            var islandState = mainGameState.CurrentIslandState;
            if (islandState != null)
            {
                // Dessine les villes de chaque civilisation
                foreach (var civilization in islandState.Civilizations)
                {
                    DrawCities(canvas, civilization.Cities, civilization.Index);
                }
            }
        }
    }

    /// <summary>
    /// Dessine les villes d'une civilisation.
    /// </summary>
    private void DrawCities(SKCanvas canvas, List<SettlersOfIdlestan.Model.City.City> cities, int civilizationIndex)
    {
        if (cities.Count == 0 || _settlementPaint == null || _cityPaint == null || _borderPaint == null)
            return;

        // Sélectionne la couleur de la civilisation
        var color = CivilizationColors[civilizationIndex % CivilizationColors.Length];

        foreach (var city in cities)
        {
            // Calcule la position du sommet (vertex)
            var pixelPos = VertexToIsland(city.Position);

            // Sélectionne la couleur en fonction du niveau de la ville
            var fillColor = city.Level >= 2 ? color : new SKColor(color.Red, color.Green, color.Blue, 150);
            _cityPaint.Color = fillColor;

            // Dessine la ville (cercle rempli)
            float radius = city.Level >= 2 ? CityRadius : SettlementRadius;
            canvas.DrawCircle(pixelPos.X, pixelPos.Y, radius, _cityPaint);

            // Dessine la bordure
            canvas.DrawCircle(pixelPos.X, pixelPos.Y, radius, _borderPaint);

            // Affiche le niveau de la ville si c'est une vraie ville
            if (city.Level >= 2)
            {
                var textPaint = new SKPaint { Color = SKColors.White, IsAntialias = true };
                var font = new SKFont { Size = 10 };
                canvas.DrawText(city.Level.ToString(), pixelPos.X, pixelPos.Y + 4, SKTextAlign.Center, font, textPaint);
                textPaint.Dispose();
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _settlementPaint?.Dispose();
        _cityPaint?.Dispose();
        _borderPaint?.Dispose();
        _disposed = true;
    }
}
