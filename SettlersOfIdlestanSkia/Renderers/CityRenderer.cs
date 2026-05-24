using SkiaSharp;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestanSkia.Services;
using SettlersOfIdlestan.Model.HexGrid;

namespace SettlersOfIdlestanSkia.Renderers;

/// <summary>
/// Renderer pour afficher les villes (settlements et cities).
/// </summary>
public class CityRenderer : HexBasedRenderer, IGameRenderer
{
    private bool _disposed;

    private const float CityRadius = 8f;
    private const float SettlementRadius = 6f;

    private readonly TooltipRenderer _tooltipRenderer;

    private SKPaint? _settlementPaint;
    private SKPaint? _cityPaint;
    private SKPaint? _borderPaint;
    private SKPaint? _cityLevelTextPaint;
    private SKFont? _cityLevelFont;

    private readonly SKPaint _buildableVertexPaint = new()
    {
        Color = new SKColor(60, 160, 255, 120),
        Style = SKPaintStyle.Fill,
        IsAntialias = true
    };
    private readonly SKPaint _hoverVertexPaint = new()
    {
        Color = new SKColor(255, 235, 59, 220),
        Style = SKPaintStyle.Fill,
        IsAntialias = true
    };
    private readonly SKPaint _hoverCityPaint = new()
    {
        Color = new SKColor(255, 255, 255, 220),
        Style = SKPaintStyle.Stroke,
        StrokeWidth = 2,
        IsAntialias = true
    };
    private readonly SKPaint _selectedCityPaint = new()
    {
        Color = new SKColor(255, 215, 0, 230),
        Style = SKPaintStyle.Stroke,
        StrokeWidth = 3,
        IsAntialias = true
    };

    // Couleurs pour les civilisations
    private static readonly SKColor[] CivilizationColors = new[]
    {
        new SKColor(255, 0, 0),     // Rouge - Civ 0
        new SKColor(0, 0, 255),     // Bleu - Civ 1
        new SKColor(0, 200, 0),     // Vert - Civ 2
        new SKColor(255, 200, 0),   // Orange - Civ 3
    };

    public CityRenderer(TooltipRenderer tooltipRenderer)
    {
        _tooltipRenderer = tooltipRenderer;
    }

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

        _cityLevelTextPaint = new SKPaint { Color = SKColors.White, IsAntialias = true };
        _cityLevelFont = new SKFont { Size = 10 };
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
                if (!islandState.VisibleIslandMaps.TryGetValue(islandState.PlayerCivilization.Index, out var visibleMap))
                    return;

                // Dessine les villes de chaque civilisation
                foreach (var civilization in islandState.Civilizations)
                {
                    DrawCities(canvas, civilization.Cities, civilization.Index, visibleMap);
                }
            }
        }
    }

    internal void RenderConstructionHighlights(SKCanvas canvas, ConstructionHoverState state)
    {
        foreach (var vertex in state.BuildableVertices)
        {
            var pt = VertexToIsland(vertex);
            canvas.DrawCircle(pt, 5f, _buildableVertexPaint);
        }

        if (state.HoveredVertex != null)
        {
            var pt = VertexToIsland(state.HoveredVertex);
            canvas.DrawCircle(pt, 7f, _hoverVertexPaint);

            _tooltipRenderer.SetOutpostConstructionTooltip(state.HoveredVertex);
        }

        if (state.HoveredCityVertex != null)
        {
            var pt = VertexToIsland(state.HoveredCityVertex);
            canvas.DrawCircle(pt, 9f, _hoverCityPaint);
        }

        if (state.SelectedCityVertex != null)
        {
            var pt = VertexToIsland(state.SelectedCityVertex);
            canvas.DrawCircle(pt, 12f, _selectedCityPaint);
        }
    }

    /// <summary>
    /// Dessine les villes d'une civilisation.
    /// </summary>
    private void DrawCities(SKCanvas canvas, List<City> cities, int civilizationIndex, IslandMap visibleMap)
    {
        if (cities.Count == 0 || _settlementPaint == null || _cityPaint == null || _borderPaint == null)
            return;

        // Sélectionne la couleur de la civilisation
        var color = CivilizationColors[civilizationIndex % CivilizationColors.Length];

        foreach (var city in cities)
        {
            if (!IsCityVisible(city, visibleMap))
                continue;

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

            if (city.Level >= 2)
                canvas.DrawText(city.Level.ToString(), pixelPos.X, pixelPos.Y + 4, SKTextAlign.Center, _cityLevelFont, _cityLevelTextPaint);
        }
    }

    private static bool IsCityVisible(City city, IslandMap visibleMap)
    {
        return city.Position.GetHexes().Any(visibleMap.HasTile);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _settlementPaint?.Dispose();
        _cityPaint?.Dispose();
        _borderPaint?.Dispose();
        _cityLevelTextPaint?.Dispose();
        _cityLevelFont?.Dispose();
        _buildableVertexPaint.Dispose();
        _hoverVertexPaint.Dispose();
        _hoverCityPaint.Dispose();
        _selectedCityPaint.Dispose();

        _disposed = true;
    }
}
