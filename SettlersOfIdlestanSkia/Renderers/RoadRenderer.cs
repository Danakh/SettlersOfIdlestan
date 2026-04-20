using SkiaSharp;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestan.Model.Game;

namespace SettlersOfIdlestanSkia.Renderers;

/// <summary>
/// Renderer pour afficher les routes entre les villes.
/// </summary>
public class RoadRenderer : HexBasedRenderer
{
    private SKPaint? _roadPaint;

    // Couleurs pour les civilisations (à étendre selon le nombre de civs)
    private static readonly SKColor[] CivilizationColors = new[]
    {
        new SKColor(255, 0, 0),     // Rouge - Civ 0
        new SKColor(0, 0, 255),     // Bleu - Civ 1
        new SKColor(0, 200, 0),     // Vert - Civ 2
        new SKColor(255, 200, 0),   // Orange - Civ 3
    };

    public override void Initialize(SKSize canvasSize)
    {
        CanvasSize = canvasSize;

        _roadPaint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 3,
            IsAntialias = true,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round,
        };
    }

    public override void Render(SKCanvas canvas, GameRenderContext context)
    {
        if (context.GameState == null || _roadPaint == null)
            return;

        using (ApplyCameraTransform(canvas, context))
        {
            if (context.GameState is MainGameState mainGameState)
            {
                var islandState = mainGameState.CurrentIslandState;
                if (islandState != null)
                {
                    // Dessine les routes de chaque civilisation
                    foreach (var civilization in islandState.Civilizations)
                    {
                        DrawRoads(canvas, islandState.Map, civilization.Roads, civilization.Index);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Dessine les routes d'une civilisation.
    /// </summary>
    private void DrawRoads(SKCanvas canvas, SettlersOfIdlestan.Model.IslandMap.IslandMap map, 
        List<SettlersOfIdlestan.Model.Road.Road> roads, int civilizationIndex)
    {
        if (_roadPaint == null || roads.Count == 0)
            return;

        // Sélectionne la couleur de la civilisation
        var color = CivilizationColors[civilizationIndex % CivilizationColors.Length];
        _roadPaint.Color = color;

        foreach (var road in roads)
        {
            // Récupère les deux hexagones de l'arête
            var (hex1, hex2) = road.Position.GetHexes();
            var (x1, y1) = AxialToPixel(hex1.Q, hex1.R);
            var (x2, y2) = AxialToPixel(hex2.Q, hex2.R);

            // Dessine une ligne entre les centres des deux hexagones
            canvas.DrawLine(x1, y1, x2, y2, _roadPaint);
        }
    }

    public override void Dispose()
    {
        if (Disposed)
            return;

        _roadPaint?.Dispose();
        Disposed = true;
    }
}
