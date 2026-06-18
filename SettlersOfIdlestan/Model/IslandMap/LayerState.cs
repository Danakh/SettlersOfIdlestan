using System.Collections.Generic;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.HexGrid;

namespace SettlersOfIdlestan.Model.IslandMap;

/// <summary>
/// State of a single map layer (Z-index). Contains the map tiles and layer metadata.
/// </summary>
public class LayerState
{
    public const int UnderworldZ = 1;

    public IslandMap Map { get; set; }

    /// <summary>
    /// Quand true, la construction d'une route génère automatiquement les hexagones manquants adjacents.
    /// </summary>
    public bool AutoExtend { get; set; }

    /// <summary>
    /// Vertex d'arrivée du joueur sur cette couche (premier avant-poste).
    /// Null pour les couches sans point d'entrée fixe.
    /// </summary>
    public Vertex? ArrivalVertex { get; set; }

    /// <summary>
    /// Hexagones (absolus) formant un motif de base de la rivière de cette couche, généré une
    /// seule fois. La rivière est de longueur infinie : elle se répète indéfiniment en se
    /// translatant de <see cref="RiverCycleDisplacementQ"/>/<see cref="RiverCycleDisplacementR"/>
    /// à chaque répétition, ce qui permet de tester l'appartenance de n'importe quel hexagone
    /// (même découvert hors-ordre) sans dépendre de l'ordre d'exploration. Vide si pas encore générée.
    /// </summary>
    public List<HexCoord> RiverCycleHexes { get; set; } = new();

    /// <summary>Décalage (q, r) appliqué à <see cref="RiverCycleHexes"/> à chaque répétition du motif.</summary>
    public int RiverCycleDisplacementQ { get; set; }

    /// <summary>Décalage (q, r) appliqué à <see cref="RiverCycleHexes"/> à chaque répétition du motif.</summary>
    public int RiverCycleDisplacementR { get; set; }

    [System.Text.Json.Serialization.JsonConstructor]
    public LayerState()
    {
        Map = new IslandMap(System.Array.Empty<HexTile>());
    }

    public LayerState(IslandMap map)
    {
        Map = map;
    }

    /// <summary>
    /// Creates the default 3-hex underworld map with an outpost at the shared vertex.
    /// Hexes (0,0), (1,0), (0,1) form a triangle sharing one vertex.
    /// The returned City must be added to the owning civilization by the caller.
    /// </summary>
    public static LayerState EstablishOupostInNewAutoExpandLayer(Civilization.Civilization playerCiv)
    {
        var tiles = new[]
        {
            new HexTile(new HexCoord(0, 0, UnderworldZ), TerrainType.Mountain),
            new HexTile(new HexCoord(1, 0, UnderworldZ), TerrainType.Mountain),
            new HexTile(new HexCoord(0, 1, UnderworldZ), TerrainType.Mountain),
        };

        var map = new IslandMap(tiles);

        var outpostVertex = Vertex.Create(
            new HexCoord(0, 0, UnderworldZ),
            new HexCoord(1, 0, UnderworldZ),
            new HexCoord(0, 1, UnderworldZ));

        var outpost = new City(outpostVertex) { CivilizationIndex = playerCiv.Index };
        playerCiv.AddCity(outpost);

        var layer = new LayerState
        {
            Map = map,
            AutoExtend = true,
            ArrivalVertex = outpostVertex,
        };

        return layer;
    }
}
