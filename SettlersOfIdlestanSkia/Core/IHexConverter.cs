using SkiaSharp;
using SettlersOfIdlestan.Model.HexGrid;

namespace SettlersOfIdlestanSkia.Core;

/// <summary>
/// Interface pour les conversions de coordonnées hexagonales.
/// </summary>
public interface IHexConverter
{
    /// <summary>
    /// Convertit des coordonnées Island (monde) en coordonnées hexagonales axiales (q, r).
    /// </summary>
    (int q, int r) IslandToAxial(float x, float y);

    /// <summary>
    /// Convertit des coordonnées hexagonales (Q, R) en coordonnées Island (x, y).
    /// </summary>
    (float x, float y) AxialToIsland(int q, int r);

    /// <summary>
    /// Vérifie si un point (x, y) en coordonnées Island se trouve à l'intérieur d'un hexagone.
    /// </summary>
    bool IsPointInHexagon(float px, float py, float hexCenterX, float hexCenterY, float size = 40f);
}
