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

    /// <summary>
    /// Layer de l'Abysse. Point d'entrée : <see cref="SettlersOfIdlestan.Controller.Expand.AbyssGateController"/>,
    /// une fois la Faille des Abysses bâtie (voir OnAbyssGateBuilt).
    /// </summary>
    public const int AbyssZ = 2;

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

    /// <summary>Dernier tick où la chance d'apparition d'un monstre en bordure de carte a été testée.</summary>
    public long LastBorderMonsterSpawnTick { get; set; }

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
    /// Creates the default 3-hex layer map (Inframonde ou Abysse) with an outpost at the shared vertex.
    /// Hexes (0,0), (1,0), (0,1) form a triangle sharing one vertex, on layer <paramref name="z"/>.
    /// The returned City must be added to the owning civilization by the caller.
    /// </summary>
    /// <param name="surroundWithVoid">
    /// Abysse uniquement : entoure le triangle d'un anneau de <see cref="TerrainType.Void"/>, pour que
    /// <see cref="SettlersOfIdlestan.Controller.Island.AutoExtendController"/> puisse faire pousser une
    /// première île dès qu'un de ces hexes de Void devient visible (voir AbyssIslandGenerator).
    /// </param>
    public static LayerState EstablishOupostInNewAutoExpandLayer(
        Civilization.Civilization playerCiv, int z = UnderworldZ, bool surroundWithVoid = false)
    {
        var h1 = new HexCoord(0, 0, z);
        var h2 = new HexCoord(1, 0, z);
        var h3 = new HexCoord(0, 1, z);

        var tiles = new List<HexTile>
        {
            new HexTile(h1, TerrainType.Mountain),
            new HexTile(h2, TerrainType.Mountain),
            new HexTile(h3, TerrainType.Mountain),
        };

        if (surroundWithVoid)
        {
            var islandSet = new HashSet<HexCoord> { h1, h2, h3 };
            var ringHexes = new HashSet<HexCoord>();
            foreach (var hex in islandSet)
                foreach (var n in hex.Neighbors())
                    if (!islandSet.Contains(n))
                        ringHexes.Add(n);

            foreach (var hex in ringHexes)
                tiles.Add(new HexTile(hex, TerrainType.Void));
        }

        var map = new IslandMap(tiles);
        var outpostVertex = Vertex.Create(h1, h2, h3);

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
