using SkiaSharp;
using Svg.Skia;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestan.Controller.Military;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestanSkia.Services;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestanSkia.Renderers.Debug;
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

    // Couleurs pour les civilisations (noir réservé)
    private static readonly SKColor[] CivilizationColors = new[]
    {
        new SKColor(220, 50,  50),  // Rouge    - Civ 0
        new SKColor(60,  100, 220), // Bleu     - Civ 1
        new SKColor(50,  180, 50),  // Vert     - Civ 2
        new SKColor(230, 180, 0),   // Jaune    - Civ 3
        new SKColor(180, 60,  220), // Violet   - Civ 4
        new SKColor(220, 130, 40),  // Orange   - Civ 5
        new SKColor(0,   190, 190), // Cyan     - Civ 6
        new SKColor(220, 100, 160), // Rose     - Civ 7
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

        _attackSvg = _resourceManager.LoadImage("Resources.icons.military.attack.svg");
        _defenseSvg = _resourceManager.LoadImage("Resources.icons.military.defense.svg");
    }

    public void Render(SKCanvas canvas, GameRenderContext context)
    {
        if (context.GameState == null || _settlementPaint == null || _cityPaint == null || _borderPaint == null)
            return;

        if (context.GameState is MainGameState mainGameState)
        {
            var worldState = mainGameState.CurrentWorldState;
            if (worldState == null) return;

            if (worldState.CurrentViewedLayer == LayerState.UnderworldZ && worldState.Layers.TryGetValue(LayerState.UnderworldZ, out var underworldLayer))
            {
                var playerIdx = worldState.PlayerCivilization.Index;
                var visibilityMap = DebugSettings.ShowFullMap
                    ? (IslandMap)underworldLayer.Map
                    : worldState.Visibility.GetForZ(LayerState.UnderworldZ).TryGetValue(playerIdx, out var uvm) ? uvm : underworldLayer.Map;
                foreach (var civ in worldState.Civilizations)
                {
                    bool isPlayerCiv = (civ == worldState.PlayerCivilization);
                    DrawCities(canvas, civ.Cities.Where(c => c.Position.Z == LayerState.UnderworldZ).ToList(), civ, visibilityMap, isPlayerCiv, mainGameState.Settings.ShowCityMilitaryStats);
                }
                return;
            }

            IslandMap? mapForVisibility;
            if (DebugSettings.ShowFullMap)
                mapForVisibility = worldState.GetMapForZ(IslandMap.SurfaceLayer);
            else if (!worldState.Visibility.GetForZ(worldState.CurrentViewedLayer).TryGetValue(worldState.PlayerCivilization.Index, out var vm))
                return;
            else
                mapForVisibility = vm;

            if (mapForVisibility != null)
            {
                // Dessine les villes de chaque civilisation
                foreach (var civilization in worldState.Civilizations)
                {
                    bool isPlayerCiv = (civilization == worldState.PlayerCivilization);
                    DrawCities(canvas, civilization.Cities, civilization, mapForVisibility, isPlayerCiv, mainGameState.Settings.ShowCityMilitaryStats);
                }
            }
        }
    }

    internal void RenderConstructionHighlights(SKCanvas canvas, ConstructionHoverState state, GameRenderContext context)
    {
        foreach (var vertex in state.BuildableVertices.Where(v => v.Z == context.CurrentLayer))
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

            if (context.GameState is MainGameState mgs && mgs.CurrentWorldState != null)
            {
                var worldState = mgs.CurrentWorldState;
                var city = worldState.FindCityAt(state.HoveredCityVertex);
                if (city != null)
                {
                    var civ = worldState.Civilizations.FirstOrDefault(c => c.Index == city.CivilizationIndex);
                    bool isPlayer = city.CivilizationIndex == worldState.PlayerCivilization.Index;
                    _tooltipRenderer.SetCityTooltip(city, civ, isPlayer, _militaryController, state.HoveredCityVertex);
                }
            }
        }

        if (state.HoveredEnemyCityVertex != null)
        {
            var pt = VertexToIsland(state.HoveredEnemyCityVertex);
            canvas.DrawCircle(pt, 9f, _hoverCityPaint);

            if (context.GameState is MainGameState mgs2 && mgs2.CurrentWorldState != null)
            {
                var worldState2 = mgs2.CurrentWorldState;
                var city = worldState2.FindCityAt(state.HoveredEnemyCityVertex);
                if (city != null)
                {
                    var civ = worldState2.Civilizations.FirstOrDefault(c => c.Index == city.CivilizationIndex);
                    _tooltipRenderer.SetCityTooltip(city, civ, isPlayerCity: false, _militaryController, state.HoveredEnemyCityVertex);
                }
            }
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
    private void DrawCities(SKCanvas canvas, IEnumerable<City> cities, Civilization civilization, IslandMap visibleMap, bool isPlayerCiv, bool showMilitaryStats)
    {
        if (!cities.Any() || _settlementPaint == null || _cityPaint == null || _borderPaint == null)
            return;

        // Sélectionne la couleur de la civilisation
        var color = CivilizationColors[civilization.Index % CivilizationColors.Length];

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
            {
                bool shouldShowUnique = isPlayerCiv && city.Buildings.Any(b => b.IsUnique);
                string label = shouldShowUnique ? $"{city.Level}!" : city.Level.ToString();
                SkiaTextUtils.DrawText(canvas, label, pixelPos.X, pixelPos.Y + 4, SKTextAlign.Center, _cityLevelFont, _cityLevelTextPaint);
            }

            if (showMilitaryStats)
                DrawMilitaryScores(canvas, city, civilization, pixelPos, radius);
        }
    }

    private void DrawMilitaryScores(SKCanvas canvas, City city, Civilization civilization, SKPoint cityPos, float cityRadius)
    {
        if (_militaryTextPaint == null || _militaryTextFont == null || _iconColorPaint == null)
            return;

        int attack = _militaryController.GetAttackScore(city);
        int maxDefense = _militaryController.GetDefenseScore(city);
        int currentDefense = city.CurrentDefense;

        bool showAttack = attack > 0;
        bool showDefense = maxDefense > 0;

        if (!showAttack && !showDefense)
            return;

        float yBase = cityPos.Y + cityRadius + 3f + MilitaryIconSize;
        float spacing = MilitaryIconSize + 16f;
        float totalWidth = 0f;
        if (showAttack) totalWidth += spacing;
        if (showDefense) totalWidth += spacing;
        float xStart = cityPos.X - totalWidth / 2f + spacing / 2f;

        float x = xStart;
        if (showAttack)
        {
            DrawMilitaryIcon(canvas, _attackSvg, new SKPoint(x, yBase), new SKColor(220, 80, 60));
            SkiaTextUtils.DrawText(canvas, attack.ToString(), x + MilitaryIconSize / 2f + 2f, yBase + 3f, SKTextAlign.Left, _militaryTextFont, _militaryTextPaint);
            x += spacing;
        }
        if (showDefense)
        {
            var defColor = currentDefense == 0 ? new SKColor(200, 60, 60) : new SKColor(80, 160, 220);
            DrawMilitaryIcon(canvas, _defenseSvg, new SKPoint(x, yBase), defColor);
            string defText = $"{currentDefense}/{maxDefense}";
            SkiaTextUtils.DrawText(canvas, defText, x + MilitaryIconSize / 2f + 2f, yBase + 3f, SKTextAlign.Left, _militaryTextFont, _militaryTextPaint);
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
        return (city.Position.Z == visibleMap.Z) && city.Position.GetHexes().Any(visibleMap.HasTile);
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
