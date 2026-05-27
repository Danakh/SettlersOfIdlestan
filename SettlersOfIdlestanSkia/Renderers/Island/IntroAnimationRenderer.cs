using SkiaSharp;
using Svg.Skia;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Services;

namespace SettlersOfIdlestanSkia.Renderers.Island;

public class IntroAnimationRenderer : HexBasedRenderer, IGameRenderer
{
    private const float IntroDuration = 3f;

    // Phase boundaries (0..1 over IntroDuration)
    private const float PhFadeIn  = 0.12f;  // écran noir → bateau visible sur hex1, commence à bouger
    private const float PhArrival = 0.83f;  // bateau arrive au vertex, overlay commence à disparaître
    // PhArrival..1.0 : fondu de sortie

    private const float IconSize   = 48f;
    private const float SvgViewBox = 64f;

    private static readonly SKColor WaterColor = new(30, 144, 255);

    private readonly ResourceManager _resourceManager;
    private SKSvg? _tradeshipSvg;
    private SKPaint? _overlayPaint;
    private SKPaint? _waterHexPaint;

    private float _elapsed;

    /// <summary>True pendant que l'animation tourne ; false avant StartIntro et après la fin.</summary>
    public bool IsActive { get; private set; }

    // Points de la trajectoire (espace Island / monde)
    private SKPoint _hex1Center;
    private SKPoint _hex2Center;
    private SKPoint _cityVertex;
    private HexCoord _hex1Coord = new(0, 0);
    private HexCoord _hex2Coord = new(0, 0);

    public IntroAnimationRenderer(ResourceManager resourceManager)
    {
        _resourceManager = resourceManager;
    }

    public void Initialize(SKSize canvasSize)
    {
        try { _tradeshipSvg = _resourceManager.LoadImage("Resources.icons.military.tradeship.svg"); }
        catch { }

        _overlayPaint  = new SKPaint { Style = SKPaintStyle.Fill };
        _waterHexPaint = new SKPaint { Style = SKPaintStyle.Fill, IsAntialias = true };
    }

    /// <summary>
    /// Démarre (ou redémarre) l'animation d'intro pour une nouvelle partie.
    /// </summary>
    public void StartIntro(MainGameState gameState)
    {
        IsActive = false;
        _elapsed = 0f;

        var islandState = gameState.CurrentIslandState;
        if (islandState == null) return;

        var playerCiv = islandState.PlayerCivilization;
        if (playerCiv.Cities.Count == 0) return;

        _cityVertex = VertexToIsland(playerCiv.Cities[0].Position);

        var map = islandState.Map;
        if (map?.Tiles == null) return;

        var waterHexes = map.Tiles
            .Where(kvp => kvp.Value.TerrainType == TerrainType.Water)
            .Select(kvp =>
            {
                var (wx, wy) = AxialToIsland(kvp.Key.Q, kvp.Key.R);
                var pt = new SKPoint(wx, wy);
                float dx = pt.X - _cityVertex.X;
                float dy = pt.Y - _cityVertex.Y;
                return (coord: kvp.Key, center: pt, distSq: dx * dx + dy * dy);
            })
            .OrderBy(x => x.distSq)
            .ToList();

        if (waterHexes.Count == 0) return;

        _hex2Coord  = waterHexes[0].coord;
        _hex2Center = waterHexes[0].center;

        var waterNeighbors = GetHexNeighborCoords(_hex2Coord)
            .Where(n => map.Tiles.TryGetValue(n, out var t) && t.TerrainType == TerrainType.Water)
            .Select(n =>
            {
                var (wx, wy) = AxialToIsland(n.Q, n.R);
                var pt = new SKPoint(wx, wy);
                float dx = pt.X - _cityVertex.X;
                float dy = pt.Y - _cityVertex.Y;
                return (coord: n, center: pt, distSq: dx * dx + dy * dy);
            })
            .OrderByDescending(x => x.distSq)
            .ToList();

        if (waterNeighbors.Count > 0)
        {
            _hex1Coord  = waterNeighbors[0].coord;
            _hex1Center = waterNeighbors[0].center;
        }
        else if (waterHexes.Count > 1)
        {
            _hex1Coord  = waterHexes[1].coord;
            _hex1Center = waterHexes[1].center;
        }
        else
        {
            _hex1Coord  = _hex2Coord;
            _hex1Center = _hex2Center;
        }

        IsActive = true;
    }

    public void Render(SKCanvas canvas, GameRenderContext context)
    {
        if (!IsActive) return;

        _elapsed += context.DeltaTime;
        float t = Math.Clamp(_elapsed / IntroDuration, 0f, 1f);

        if (t >= 1f)
        {
            IsActive = false;
            return;
        }

        // ---- Position du bateau sur la spline Catmull-Rom ----
        SKPoint boatPos;
        float splineS;   // 0..1 sur la courbe hex1→hex2→vertex
        bool showHex2;

        if (t < PhFadeIn)
        {
            splineS  = 0f;
            boatPos  = _hex1Center;
            showHex2 = false;
        }
        else if (t < PhArrival)
        {
            splineS  = EaseSine((t - PhFadeIn) / (PhArrival - PhFadeIn));
            boatPos  = SplineAt(splineS);
            showHex2 = splineS > 0.25f;
        }
        else
        {
            splineS  = 1f;
            boatPos  = _cityVertex;
            showHex2 = true;
        }

        // ---- Alpha overlay ----
        float overlayAlpha = t < PhArrival
            ? 1f
            : 1f - EaseSine((t - PhArrival) / (1f - PhArrival));

        // ---- Overlay noir ----
        if (overlayAlpha > 0.004f)
        {
            var bounds = new SKRect(0, 0, context.CanvasSize.Width, context.CanvasSize.Height);
            _overlayPaint!.Color = new SKColor(0, 0, 0, (byte)(255f * overlayAlpha));
            canvas.DrawRect(bounds, _overlayPaint);
        }

        // ---- Hexes water dessinés par-dessus le noir ----
        if (t >= PhFadeIn && overlayAlpha > 0.004f)
        {
            _waterHexPaint!.Color = WaterColor.WithAlpha((byte)(255f * overlayAlpha));
            DrawHexFill(canvas, _hex1Coord, context);
            if (showHex2)
                DrawHexFill(canvas, _hex2Coord, context);
        }

        // ---- Rendu du bateau ----
        if (_tradeshipSvg?.Picture != null)
        {
            var screenPos = IslandToScreen(boatPos, context.ZoomLevel, context.CameraPosition);
            float scale = IconSize / SvgViewBox;

            canvas.Save();
            canvas.Translate(screenPos.X - IconSize / 2f, screenPos.Y - IconSize / 2f);
            canvas.Scale(scale);
            canvas.DrawPicture(_tradeshipSvg.Picture);
            canvas.Restore();
        }
    }

    /// <summary>
    /// Spline Catmull-Rom passant par hex1Center (s=0), hex2Center (s=0.5) et cityVertex (s=1).
    /// Des points fantômes sont calculés par symétrie aux extrémités.
    /// </summary>
    private SKPoint SplineAt(float s)
    {
        var p0 = _hex1Center;
        var p1 = _hex2Center;
        var p2 = _cityVertex;

        // Points fantômes : symétrie aux extrémités pour une tangente naturelle
        var pPre  = new SKPoint(2 * p0.X - p1.X, 2 * p0.Y - p1.Y);
        var pPost = new SKPoint(2 * p2.X - p1.X, 2 * p2.Y - p1.Y);

        if (s <= 0.5f)
            return CatmullRomSegment(pPre, p0, p1, p2, s * 2f);
        else
            return CatmullRomSegment(p0, p1, p2, pPost, (s - 0.5f) * 2f);
    }

    private static SKPoint CatmullRomSegment(SKPoint p0, SKPoint p1, SKPoint p2, SKPoint p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;
        float x = 0.5f * (2*p1.X + (-p0.X + p2.X)*t + (2*p0.X - 5*p1.X + 4*p2.X - p3.X)*t2 + (-p0.X + 3*p1.X - 3*p2.X + p3.X)*t3);
        float y = 0.5f * (2*p1.Y + (-p0.Y + p2.Y)*t + (2*p0.Y - 5*p1.Y + 4*p2.Y - p3.Y)*t2 + (-p0.Y + 3*p1.Y - 3*p2.Y + p3.Y)*t3);
        return new SKPoint(x, y);
    }

    private void DrawHexFill(SKCanvas canvas, HexCoord coord, GameRenderContext context)
    {
        var (wx, wy) = AxialToIsland(coord.Q, coord.R);
        var islandPoints = GetHexagonPoints(wx, wy);

        var screenPoints = new SKPoint[islandPoints.Length];
        for (int i = 0; i < islandPoints.Length; i++)
            screenPoints[i] = IslandToScreen(islandPoints[i], context.ZoomLevel, context.CameraPosition);

        using var path = PointsToPath(screenPoints);
        canvas.DrawPath(path, _waterHexPaint!);
    }

    private static SKPoint IslandToScreen(SKPoint p, float zoom, SKPoint cam)
        => new((p.X - cam.X) * zoom, (p.Y - cam.Y) * zoom);

    private static float EaseSine(float t)
        => (float)(-(Math.Cos(Math.PI * t) - 1.0) / 2.0);

    private static HexCoord[] GetHexNeighborCoords(HexCoord c) =>
    [
        new(c.Q + 1, c.R    ),
        new(c.Q - 1, c.R    ),
        new(c.Q,     c.R + 1),
        new(c.Q,     c.R - 1),
        new(c.Q + 1, c.R - 1),
        new(c.Q - 1, c.R + 1),
    ];

    public void Dispose()
    {
        _overlayPaint?.Dispose();
        _waterHexPaint?.Dispose();
        _tradeshipSvg = null;
    }
}
