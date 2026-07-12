using SkiaSharp;
using SettlersOfIdlestan.Controller.Military;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Renderers.Debug;
using SettlersOfIdlestanSkia.Renderers.Overlay;
using SettlersOfIdlestanSkia.Services;
using System.Linq;

namespace SettlersOfIdlestanSkia.Renderers.Island;

/// <summary>
/// Renderer pour afficher les balises maritimes (MaritimeBeacon) et les Flottes de Guerre (WarFleet)
/// sur la carte — les deux vivent sur le même type de vertex marin.
/// </summary>
public class MaritimeBeaconRenderer : HexBasedRenderer, IGameRenderer
{
    private bool _disposed;

    private const float BeaconRadius = 6f;
    private const float FleetRadius = 9f;

    private readonly TooltipRenderer _tooltipRenderer;
    private readonly MilitaryController _militaryController;
    private readonly MilitaryScoreOverlay _militaryScoreOverlay;

    private SKPaint? _beaconPaint;
    private SKPaint? _borderPaint;
    private SKPaint? _fleetPaint;

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
    private readonly SKPaint _buildableFleetPaint = new()
    {
        Color = new SKColor(220, 50, 50, 120),
        Style = SKPaintStyle.Stroke,
        StrokeWidth = 2f,
        IsAntialias = true
    };

    public MaritimeBeaconRenderer(TooltipRenderer tooltipRenderer, MilitaryController militaryController, MilitaryScoreOverlay militaryScoreOverlay)
    {
        _tooltipRenderer = tooltipRenderer;
        _militaryController = militaryController;
        _militaryScoreOverlay = militaryScoreOverlay;
    }

    // Mêmes couleurs par civilisation que CityRenderer (noir réservé).
    private static readonly SKColor[] CivilizationColors = new[]
    {
        new SKColor(220, 50,  50),
        new SKColor(60,  100, 220),
        new SKColor(50,  180, 50),
        new SKColor(230, 180, 0),
        new SKColor(180, 60,  220),
        new SKColor(220, 130, 40),
        new SKColor(0,   190, 190),
        new SKColor(220, 100, 160),
    };

    public void Initialize(SKSize canvasSize)
    {
        _beaconPaint = new SKPaint { Style = SKPaintStyle.Fill, IsAntialias = true };
        _borderPaint = new SKPaint
        {
            Color = SKColors.White,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.5f,
            IsAntialias = true,
        };
        _fleetPaint = new SKPaint { Style = SKPaintStyle.Fill, IsAntialias = true };
    }

    public void Render(SKCanvas canvas, GameRenderContext context)
    {
        if (context.GameState is not MainGameState mainGameState || _beaconPaint == null || _borderPaint == null)
            return;

        var worldState = mainGameState.CurrentWorldState;
        if (worldState == null) return;

        IslandMap? mapForVisibility;
        if (DebugSettings.ShowFullMap || mainGameState.GodState.AscensionState.IsEyeOfGodActive)
            mapForVisibility = worldState.GetMapForZ(worldState.CurrentViewedLayer);
        else if (!worldState.Visibility.GetForZ(worldState.CurrentViewedLayer).TryGetValue(worldState.PlayerCivilization.Index, out var vm))
            return;
        else
            mapForVisibility = vm;

        if (mapForVisibility == null) return;

        foreach (var civilization in worldState.Civilizations)
        {
            var color = CivilizationColors[civilization.Index % CivilizationColors.Length];
            _beaconPaint.Color = color;
            _fleetPaint!.Color = color;

            foreach (var beacon in civilization.MaritimeBeacons)
            {
                if (!IsBeaconVisible(beacon, mapForVisibility)) continue;

                var pixelPos = VertexToIsland(beacon.Position);
                DrawDiamond(canvas, pixelPos, BeaconRadius, _beaconPaint);
                DrawDiamond(canvas, pixelPos, BeaconRadius, _borderPaint);
            }

            foreach (var fleet in civilization.Fleets)
            {
                if (!IsFleetVisible(fleet, mapForVisibility)) continue;

                var pixelPos = VertexToIsland(fleet.Position);
                canvas.DrawCircle(pixelPos, FleetRadius, _fleetPaint);
                canvas.DrawCircle(pixelPos, FleetRadius, _borderPaint);

                if (mainGameState.Settings.ShowCityMilitaryStats)
                    _militaryScoreOverlay.Draw(canvas, fleet, _militaryController, pixelPos, FleetRadius);
            }
        }
    }

    internal void RenderConstructionHighlights(SKCanvas canvas, ConstructionHoverState state, GameRenderContext context)
    {
        foreach (var vertex in state.BuildableBeaconVertices.Where(v => v.Z == context.CurrentLayer))
        {
            var pt = VertexToIsland(vertex);
            DrawDiamond(canvas, pt, BeaconRadius, _buildableVertexPaint);
        }

        foreach (var vertex in state.BuildableFleetVertices.Where(v => v.Z == context.CurrentLayer))
        {
            var pt = VertexToIsland(vertex);
            DrawDiamond(canvas, pt, BeaconRadius + 4f, _buildableFleetPaint);
        }

        if (state.HoveredBeaconVertex != null)
        {
            var pt = VertexToIsland(state.HoveredBeaconVertex);
            DrawDiamond(canvas, pt, BeaconRadius + 2f, _hoverVertexPaint);

            _tooltipRenderer.SetMaritimeBeaconConstructionTooltip(state.HoveredBeaconVertex);
        }
        else if (state.HoveredFleetVertex != null)
        {
            var pt = VertexToIsland(state.HoveredFleetVertex);
            DrawDiamond(canvas, pt, BeaconRadius + 4f, _hoverVertexPaint);

            _tooltipRenderer.SetWarFleetConstructionTooltip(state.HoveredFleetVertex);
        }
        else if (context.GameState is MainGameState mgs && mgs.CurrentWorldState != null)
        {
            var vertex = state.HoveredOwnFleetVertex ?? state.HoveredEnemyFleetVertex;
            if (vertex != null)
            {
                var fleet = mgs.CurrentWorldState.FindFleetAt(vertex);
                if (fleet != null)
                    _tooltipRenderer.SetFleetTooltip(fleet, state.HoveredOwnFleetVertex != null, _militaryController);
            }
        }
    }

    private static void DrawDiamond(SKCanvas canvas, SKPoint center, float radius, SKPaint paint)
    {
        using var path = new SKPath();
        path.MoveTo(center.X, center.Y - radius);
        path.LineTo(center.X + radius, center.Y);
        path.LineTo(center.X, center.Y + radius);
        path.LineTo(center.X - radius, center.Y);
        path.Close();
        canvas.DrawPath(path, paint);
    }

    private static bool IsBeaconVisible(MaritimeBeacon beacon, IslandMap visibleMap)
    {
        return beacon.Position.Z == visibleMap.Z && beacon.Position.GetHexes().Any(visibleMap.HasTile);
    }

    private static bool IsFleetVisible(WarFleet fleet, IslandMap visibleMap)
    {
        return fleet.Position.Z == visibleMap.Z && fleet.Position.GetHexes().Any(visibleMap.HasTile);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _beaconPaint?.Dispose();
        _borderPaint?.Dispose();
        _fleetPaint?.Dispose();
        _buildableVertexPaint.Dispose();
        _hoverVertexPaint.Dispose();
        _buildableFleetPaint.Dispose();
        _disposed = true;
    }
}
