using SkiaSharp;
using Svg.Skia;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestan.Controller.Military;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestanSkia.Services;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestanSkia.Renderers.Overlay;

namespace SettlersOfIdlestanSkia.Renderers.Island;

/// <summary>
/// Renderer pour afficher les villes (settlements et cities).
/// </summary>
public class CityRenderer : HexBasedRenderer, IGameRenderer
{
    private bool _disposed;

    private const float CityRadius = 8f;
    private const float SettlementRadius = 6f;
    private const float MilitaryIconSize = 10f;
    private const float MilitaryIconSvgSize = 64f;

    private readonly TooltipRenderer _tooltipRenderer;
    private readonly ResourceManager _resourceManager;
    private readonly MilitaryController _militaryController;
    private SKSvg? _attackSvg;
    private SKSvg? _defenseSvg;
    private SKPaint? _militaryTextPaint;
    private SKFont? _militaryTextFont;
    private SKPaint? _iconColorPaint;

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

    public CityRenderer(TooltipRenderer tooltipRenderer, ResourceManager resourceManager, MilitaryController militaryController)
    {
        _tooltipRenderer = tooltipRenderer;
        _resourceManager = resourceManager;
        _militaryController = militaryController;
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

        _militaryTextPaint = new SKPaint { Color = SKColors.White, IsAntialias = true };
        _militaryTextFont = new SKFont { Size = 8 };
        _iconColorPaint = new SKPaint { IsAntialias = true };

        try { _attackSvg = _resourceManager.LoadImage("Resources.icons.military.attack.svg"); } catch { }
        try { _defenseSvg = _resourceManager.LoadImage("Resources.icons.military.defense.svg"); } catch { }
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

            DrawMilitaryScores(canvas, city, pixelPos, radius);
        }
    }

    private void DrawMilitaryScores(SKCanvas canvas, City city, SKPoint cityPos, float cityRadius)
    {
        if (_militaryTextPaint == null || _militaryTextFont == null || _iconColorPaint == null)
            return;

        int attack = _militaryController.GetAttackScore(city);
        int defense = _militaryController.GetDefenseScore(city);

        if (attack == 0 && defense == 0)
            return;

        float yBase = cityPos.Y + cityRadius + 3f + MilitaryIconSize;
        float spacing = MilitaryIconSize + 16f;
        float totalWidth = 0f;
        if (attack > 0) totalWidth += spacing;
        if (defense > 0) totalWidth += spacing;
        float xStart = cityPos.X - totalWidth / 2f + spacing / 2f;

        float x = xStart;
        if (attack > 0)
        {
            DrawMilitaryIcon(canvas, _attackSvg, new SKPoint(x, yBase), new SKColor(220, 80, 60));
            canvas.DrawText(attack.ToString(), x + MilitaryIconSize / 2f + 2f, yBase + 3f, SKTextAlign.Left, _militaryTextFont, _militaryTextPaint);
            x += spacing;
        }
        if (defense > 0)
        {
            DrawMilitaryIcon(canvas, _defenseSvg, new SKPoint(x, yBase), new SKColor(80, 160, 220));
            canvas.DrawText(defense.ToString(), x + MilitaryIconSize / 2f + 2f, yBase + 3f, SKTextAlign.Left, _militaryTextFont, _militaryTextPaint);
        }
    }

    private void DrawMilitaryIcon(SKCanvas canvas, SKSvg? svg, SKPoint center, SKColor tint)
    {
        var picture = svg?.Picture;
        if (picture == null || _iconColorPaint == null) return;

        float scale = MilitaryIconSize / MilitaryIconSvgSize;
        _iconColorPaint.ColorFilter = SKColorFilter.CreateBlendMode(tint, SKBlendMode.SrcIn);
        canvas.Save();
        canvas.Translate(center.X - MilitaryIconSize / 2f, center.Y - MilitaryIconSize / 2f);
        canvas.Scale(scale);
        canvas.SaveLayer(new SKRect(0, 0, MilitaryIconSvgSize, MilitaryIconSvgSize), _iconColorPaint);
        canvas.DrawPicture(picture);
        canvas.Restore();
        canvas.Restore();
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
        _militaryTextPaint?.Dispose();
        _militaryTextFont?.Dispose();
        _iconColorPaint?.Dispose();

        _disposed = true;
    }
}
