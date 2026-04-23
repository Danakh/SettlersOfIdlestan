using SkiaSharp;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestanSkia.Core;

namespace SettlersOfIdlestanSkia.Renderers;

/// <summary>
/// Classe mère utilitaire pour les renderers basés sur la grille hexagonale.
/// Elle travaille en coordonnées Island (espace monde), sans connaissance de l'écran.
/// </summary>
public abstract class HexBasedRenderer : IHexConverter
{
    /// <summary>
    /// Taille des hexagones en pixels.
    /// </summary>
    protected const float HexSize = 40f;

    /// <summary>
    /// Convertit des coordonnées hexagonales (Q, R) en coordonnées Island (x, y).
    /// </summary>
    public (float x, float y) AxialToIsland(int q, int r)
    {
        float x = HexSize * (3f / 2 * q);
        float y = HexSize * (float)System.Math.Sqrt(3) / 2 * q + HexSize * (float)System.Math.Sqrt(3) * r;

        return (x, y);
    }

    /// <summary>
    /// Convertit des coordonnées Island (x, y) en coordonnées hexagonales axiales (q, r).
    /// Utilise l'inverse de la transformation AxialToIsland.
    /// </summary>
    public (int q, int r) IslandToAxial(float x, float y)
    {
        // Applique l'offset d'origine inverse
        float q = (2f / 3 * x) / HexSize;
        float r = (-1f / 3 * x + (float)System.Math.Sqrt(3) / 3 * y) / HexSize;

        // Arrondit aux coordonnées hexagonales les plus proches
        return RoundAxialCoordinates(q, r);
    }

    /// <summary>
    /// Arrondit des coordonnées axiales floatantes aux coordonnées entières les plus proches.
    /// </summary>
    private (int q, int r) RoundAxialCoordinates(float q, float r)
    {
        float s = -q - r;
        
        float rq = (float)System.Math.Round(q);
        float rr = (float)System.Math.Round(r);
        float rs = (float)System.Math.Round(s);
        
        float qDiff = System.Math.Abs(rq - q);
        float rDiff = System.Math.Abs(rr - r);
        float sDiff = System.Math.Abs(rs - s);
        
        if (qDiff > rDiff && qDiff > sDiff)
        {
            rq = -rr - rs;
        }
        else if (rDiff > sDiff)
        {
            rr = -rq - rs;
        }
        
        return ((int)rq, (int)rr);
    }

    /// <summary>
    /// Vérifie si un point (x, y) en coordonnées Island se trouve à l'intérieur d'un hexagone.
    /// Utilise l'algorithme "point in polygon" pour les hexagones réguliers.
    /// </summary>
    public bool IsPointInHexagon(float px, float py, float hexCenterX, float hexCenterY, float size = HexSize)
    {
        var points = GetHexagonPoints(hexCenterX, hexCenterY, size);
        return IsPointInPolygon(px, py, points);
    }

    /// <summary>
    /// Vérifie si un point se trouve à l'intérieur d'un polygone en utilisant l'algorithme ray casting.
    /// </summary>
    private bool IsPointInPolygon(float x, float y, SKPoint[] polygon)
    {
        int n = polygon.Length;
        bool inside = false;

        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            float xi = polygon[i].X, yi = polygon[i].Y;
            float xj = polygon[j].X, yj = polygon[j].Y;

            bool intersect = ((yi > y) != (yj > y)) && (x < (xj - xi) * (y - yi) / (yj - yi) + xi);
            if (intersect)
                inside = !inside;
        }

        return inside;
    }

    /// <summary>
    /// Convertit un Vertex (défini par 3 hexagones) en coordonnées Island.
    /// Position du vertex = moyenne des centres des 3 hexagones.
    /// </summary>
    protected SKPoint VertexToIsland(Vertex vertex)
    {
        var (x1, y1) = AxialToIsland(vertex.Hex1.Q, vertex.Hex1.R);
        var (x2, y2) = AxialToIsland(vertex.Hex2.Q, vertex.Hex2.R);
        var (x3, y3) = AxialToIsland(vertex.Hex3.Q, vertex.Hex3.R);

        return new SKPoint((x1 + x2 + x3) / 3, (y1 + y2 + y3) / 3);
    }

    /// <summary>
    /// Crée une liste de points pour un hexagone régulier centré à (centerX, centerY).
    /// </summary>
    protected SKPoint[] GetHexagonPoints(float centerX, float centerY, float size = HexSize)
    {
        var points = new SKPoint[6];

        for (int i = 0; i < 6; i++)
        {
            float angle = (float)System.Math.PI / 3 * i;
            points[i] = new SKPoint(
                centerX + size * (float)System.Math.Cos(angle),
                centerY + size * (float)System.Math.Sin(angle)
            );
        }

        return points;
    }

    /// <summary>
    /// Convertit une liste de points en chemin SKPath.
    /// </summary>
    protected SKPath PointsToPath(SKPoint[] points)
    {
        var path = new SKPath();
        if (points.Length > 0)
        {
            path.MoveTo(points[0]);
            for (int i = 1; i < points.Length; i++)
                path.LineTo(points[i]);
            path.Close();
        }
        return path;
    }

    /// <summary>
    /// Calcule le point milieu entre deux hexagones adjacents en coordonnées Island.
    /// </summary>
    protected SKPoint EdgeToIsland(int q1, int r1, int q2, int r2)
    {
        var (x1, y1) = AxialToIsland(q1, r1);
        var (x2, y2) = AxialToIsland(q2, r2);

        return new SKPoint((x1 + x2) / 2, (y1 + y2) / 2);
    }

    /// <summary>
    /// Convertit un point Island en coordonnée d'hexagone.
    /// </summary>
    public HexCoord IslandToHexCoord(SKPoint islandPoint)
    {
        var (q, r) = IslandToAxial(islandPoint.X, islandPoint.Y);
        return new HexCoord(q, r);
    }

    /// <summary>
    /// Trouve le vertex le plus proche d'un point Island.
    /// </summary>
    public Vertex IslandToNearestVertex(SKPoint islandPoint)
    {
        var centerHex = IslandToHexCoord(islandPoint);
        var candidates = new HashSet<Vertex>();
        var neighborDirections = new[]
        {
            HexDirection.W, HexDirection.E, HexDirection.NE,
            HexDirection.SE, HexDirection.NW, HexDirection.SW
        };
        var vertexDirections = new[]
        {
            SecondaryHexDirection.N, SecondaryHexDirection.EN, SecondaryHexDirection.ES,
            SecondaryHexDirection.S, SecondaryHexDirection.WS, SecondaryHexDirection.WN
        };

        void AddVertexCandidates(HexCoord origin)
        {
            foreach (var vertexDirection in vertexDirections)
            {
                candidates.Add(origin.Vertex(vertexDirection));
            }
        }

        AddVertexCandidates(centerHex);
        foreach (var direction in neighborDirections)
        {
            AddVertexCandidates(centerHex.Neighbor(direction));
        }

        Vertex? closest = null;
        float minDistanceSquared = float.MaxValue;
        foreach (var vertex in candidates)
        {
            var vertexPoint = VertexToIsland(vertex);
            float dx = vertexPoint.X - islandPoint.X;
            float dy = vertexPoint.Y - islandPoint.Y;
            float distanceSquared = dx * dx + dy * dy;
            if (distanceSquared < minDistanceSquared)
            {
                minDistanceSquared = distanceSquared;
                closest = vertex;
            }
        }

        return closest ?? centerHex.Vertex(SecondaryHexDirection.N);
    }

    /// <summary>
    /// Trouve l'edge la plus proche d'un point Island.
    /// </summary>
    public Edge IslandToNearestEdge(SKPoint islandPoint)
    {
        var centerHex = IslandToHexCoord(islandPoint);
        var candidates = new HashSet<Edge>();
        var neighborDirections = new[]
        {
            HexDirection.W, HexDirection.E, HexDirection.NE,
            HexDirection.SE, HexDirection.NW, HexDirection.SW
        };
        var edgeDirections = neighborDirections;

        void AddEdgeCandidates(HexCoord origin)
        {
            foreach (var edgeDirection in edgeDirections)
            {
                candidates.Add(origin.Edge(edgeDirection));
            }
        }

        AddEdgeCandidates(centerHex);
        foreach (var direction in neighborDirections)
        {
            AddEdgeCandidates(centerHex.Neighbor(direction));
        }

        Edge? closest = null;
        float minDistanceSquared = float.MaxValue;
        foreach (var edge in candidates)
        {
            var (hex1, hex2) = edge.GetHexes();
            var edgeCenter = EdgeToIsland(hex1.Q, hex1.R, hex2.Q, hex2.R);
            float dx = edgeCenter.X - islandPoint.X;
            float dy = edgeCenter.Y - islandPoint.Y;
            float distanceSquared = dx * dx + dy * dy;
            if (distanceSquared < minDistanceSquared)
            {
                minDistanceSquared = distanceSquared;
                closest = edge;
            }
        }

        return closest ?? centerHex.Edge(HexDirection.E);
    }
}
