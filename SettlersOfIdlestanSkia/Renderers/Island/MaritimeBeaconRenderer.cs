using SkiaSharp;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Renderers.Debug;

namespace SettlersOfIdlestanSkia.Renderers.Island;

/// <summary>
/// Renderer pour afficher les balises maritimes (MaritimeBeacon) sur la carte.
/// </summary>
public class MaritimeBeaconRenderer : HexBasedRenderer, IGameRenderer
{
    private bool _disposed;

    private const float BeaconRadius = 6f;

    private SKPaint? _beaconPaint;
    private SKPaint? _borderPaint;

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

            foreach (var beacon in civilization.MaritimeBeacons)
            {
                if (!IsBeaconVisible(beacon, mapForVisibility)) continue;

                var pixelPos = VertexToIsland(beacon.Position);
                DrawDiamond(canvas, pixelPos, BeaconRadius, _beaconPaint);
                DrawDiamond(canvas, pixelPos, BeaconRadius, _borderPaint);
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

    public void Dispose()
    {
        if (_disposed) return;
        _beaconPaint?.Dispose();
        _borderPaint?.Dispose();
        _disposed = true;
    }
}
