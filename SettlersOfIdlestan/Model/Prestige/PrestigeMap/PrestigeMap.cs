using SettlersOfIdlestan.Model.HexGrid;

namespace SettlersOfIdlestan.Model.Prestige.PrestigeMap;

public class PrestigeMap
{
    // ── Hex coordinates ──────────────────────────────────────────────────────
    // Inner hexes (each adjacent to Central vertex)
    public static readonly HexCoord StartingResourcesCoord     = new(0,  0, 0);
    public static readonly HexCoord ResearchSpeedCoord         = new(1,  0, 0);
    public static readonly HexCoord HarvestSpeedCoord          = new(0,  1, 0);
    // Outer hexes (each adjacent to exactly one outer vertex)
    public static readonly HexCoord UnitProductionSpeedCoord   = new(1, -1, 0);
    public static readonly HexCoord FortifiedOutpostCoord      = new(0, -1, 0);
    public static readonly HexCoord StorageCapacityCoord       = new(-1,  1, 0);
    public static readonly HexCoord ResearchCostReductionCoord = new(1,  1, 0);
    public static readonly HexCoord GoldTradeCoord             = new(-1,  2, 0);
    public static readonly HexCoord ArtisansProductionCoord   = new(0,   2, 0);
    public static readonly HexCoord ExperimentalScienceCoord  = new(2,   0, 0);
    public static readonly HexCoord DefenseRegenCoord         = new(2,  -1, 0);
    public static readonly HexCoord WarehouseMaxLevelCoord    = new(-1,  0, 0);
    // Branche de l'Acier (nord-est)
    public static readonly HexCoord SteelworksCoord           = new( 3, -1, 0);
    // Branche de l'Inframonde (nord-ouest)
    public static readonly HexCoord UnderworldCoord           = new(-1, -1, 0);
    // Branche de la Magie (sud)
    public static readonly HexCoord LeyLinesCoord             = new(-1,  3, 0);

    // ── Prestige vertices (HexGrid Vertex objects) ────────────────────────────
    // Layout: pointy-top, R=60, Central vertex at screen center.

    public static readonly Vertex CentralVertex          = Vertex.Create(new(0, 0, 0), new(1, 0, 0), new(0, 1, 0));
    public static readonly Vertex BarracksVertex              = Vertex.Create(new(0, 0, 0), new(1, 0, 0), new(1, -1, 0));
    public static readonly Vertex FortifiedOutpostVertex      = Vertex.Create(new(0, 0, 0), new(0, -1, 0), new(1, -1, 0));
    public static readonly Vertex SeaportMarketVertex    = Vertex.Create(new(0, 0, 0), new(0, 1, 0), new(-1, 1, 0));
    public static readonly Vertex LaboratoryVertex       = Vertex.Create(new(1, 0, 0), new(0, 1, 0), new(1, 1, 0));
    public static readonly Vertex AppliedResearchVertex  = Vertex.Create(new(0, 1, 0), new(1, 1, 0), new(0, 2, 0));
    public static readonly Vertex AcademyVertex          = Vertex.Create(new(1, 0, 0), new(2, 0, 0), new(1, 1, 0));
    public static readonly Vertex HarvestGuildVertex     = Vertex.Create(new(-1, 1, 0), new(-1, 2, 0), new(0, 1, 0));
    public static readonly Vertex ArtisansGuildVertex    = Vertex.Create(new(-1, 2, 0), new(0, 1, 0), new(0, 2, 0));
    public static readonly Vertex MilitaryStrategyVertex  = Vertex.Create(new(1, 0, 0), new(2,  0, 0), new(2, -1, 0));
    public static readonly Vertex ConscriptionVertex      = Vertex.Create(new(1, 0, 0), new(1, -1, 0), new(2, -1, 0));
    public static readonly Vertex MilitaryAcademyVertex   = Vertex.Create(new(1,  1, 0), new(2,  0, 0), new(2,  1, 0));
    public static readonly Vertex SteelSecretVertex       = Vertex.Create(new(2,  0, 0), new(2, -1, 0), new(3, -1, 0));
    // Branche de l'Acier (nord-est) — autour de l'hex Forges (3,-1)
    public static readonly Vertex BlastFurnaceVertex          = Vertex.Create(new(2, -1, 0), new(3, -2, 0), new(3, -1, 0));
    public static readonly Vertex MilitaryEngineeringVertex   = Vertex.Create(new(2,  0, 0), new(3, -1, 0), new(3,  0, 0));
    public static readonly Vertex ImperialRoadsVertex         = Vertex.Create(new(3, -1, 0), new(4, -1, 0), new(3,  0, 0));
    public static readonly Vertex SteelLegionVertex           = Vertex.Create(new(3, -1, 0), new(3, -2, 0), new(4, -2, 0));
    public static readonly Vertex MasterSmithsVertex          = Vertex.Create(new(3, -1, 0), new(4, -2, 0), new(4, -1, 0));
    public static readonly Vertex KnowledgeMasteryVertex  = Vertex.Create(new(1, 1, 0), new(0,  2, 0), new(1,  2, 0));
    public static readonly Vertex WatchtowerVertex           = Vertex.Create(new(0, 0, 0), new(-1, 0, 0), new(-1, 1, 0));
    public static readonly Vertex MaritimeRoutesVertex       = Vertex.Create(new(-1, 0, 0), new(-1, 1, 0), new(-2, 1, 0));
    public static readonly Vertex TraderGuildVertex          = Vertex.Create(new(-1, 1, 0), new(-1, 2, 0), new(-2, 2, 0));
    public static readonly Vertex WarehouseNewCitiesVertex   = Vertex.Create(new(0, 0, 0), new(-1, 0, 0), new(0, -1, 0));

    // ── Placeholder vertices — fill all open corners around mapped hexes ──────
    // Around FortifiedOutpost (0,-1) / UnitProductionSpeed (1,-1) north edge
    public static readonly Vertex PlaceholderA1Vertex = Vertex.Create(new(0, -1, 0), new(1, -2, 0), new(1, -1, 0));
    public static readonly Vertex PlaceholderA2Vertex = Vertex.Create(new(0, -1, 0), new(0, -2, 0), new(1, -2, 0));
    public static readonly Vertex PlaceholderA3Vertex = Vertex.Create(new(1, -1, 0), new(1, -2, 0), new(2, -2, 0));
    // Around DefenseRegen (2,-1) / UnitProductionSpeed (1,-1) outer east
    public static readonly Vertex PlaceholderB1Vertex = Vertex.Create(new(1, -1, 0), new(2, -2, 0), new(2, -1, 0));
    public static readonly Vertex PlaceholderB2Vertex = Vertex.Create(new(2, -1, 0), new(2, -2, 0), new(3, -2, 0));
    // Outer NE — east corner of ExperimentalScience (2,0)
    public static readonly Vertex PlaceholderC3Vertex = Vertex.Create(new(2,  0, 0), new(3,  0, 0), new(2,  1, 0));
    // Around ResearchCostReduction (1,1) / ExperimentalScience (2,0) outer east
    public static readonly Vertex PlaceholderD2Vertex = Vertex.Create(new(1,  1, 0), new(2,  1, 0), new(1,  2, 0));
    // Outer W connecting WarehouseMaxLevel → new NW hex (-2,0)
    public static readonly Vertex PlaceholderE3Vertex = Vertex.Create(new(-1,  0, 0), new(-2,  1, 0), new(-2,  0, 0));
    // Around StorageCapacity (-1,1) outer west
    public static readonly Vertex PlaceholderF1Vertex = Vertex.Create(new(-1,  1, 0), new(-2,  2, 0), new(-2,  1, 0));
    // ── Branche de la Magie (sud) — autour de l'hex Lignes Telluriques (-1,3)
    // Deux entrées : Cercles de Fées (depuis la Guilde des Artisans) et Dolmens (depuis la Guilde des Marchands)
    public static readonly Vertex FairyCirclesVertex  = Vertex.Create(new(-1,  2, 0), new( 0,  2, 0), new(-1,  3, 0));
    public static readonly Vertex PlaceholderG2Vertex = Vertex.Create(new( 0,  2, 0), new( 1,  2, 0), new( 0,  3, 0));
    // Porte d'entrée : déverrouille la magie (au moins aussi chère que la porte de l'Inframonde)
    public static readonly Vertex MagicSecretVertex   = Vertex.Create(new( 0,  2, 0), new( 0,  3, 0), new(-1,  3, 0));
    public static readonly Vertex FocalizationVertex  = Vertex.Create(new(-1,  2, 0), new(-1,  3, 0), new(-2,  3, 0));
    public static readonly Vertex DolmensVertex       = Vertex.Create(new(-1,  2, 0), new(-2,  3, 0), new(-2,  2, 0));
    // ── Branche de l'Inframonde (nord-ouest) — autour des hexes Excavations (-1,-1) et Inframonde (-2,-1)
    // Porte d'entrée : déverrouille la Mine Profonde (plus chère que le Secret de l'Acier)
    public static readonly Vertex DeepestMineVertex     = Vertex.Create(new(-1,  0, 0), new(-1, -1, 0), new( 0, -1, 0));
    public static readonly Vertex MushroomCultureVertex = Vertex.Create(new( 0, -1, 0), new(-1, -1, 0), new( 0, -2, 0));
    public static readonly Vertex UnderworldWatchVertex = Vertex.Create(new(-1, -1, 0), new(-1, -2, 0), new( 0, -2, 0));
    public static readonly Vertex DeepProspectorsVertex = Vertex.Create(new(-1,  0, 0), new(-2,  0, 0), new(-1, -1, 0));
    public static readonly Vertex TreasureHuntersVertex = Vertex.Create(new(-2,  0, 0), new(-2, -1, 0), new(-1, -1, 0));
    public static readonly Vertex MithrilVertex         = Vertex.Create(new(-2, -1, 0), new(-1, -2, 0), new(-1, -1, 0));
    // Sommets profonds de la branche de la Magie (coins sud de l'hex Lignes Telluriques)
    public static readonly Vertex ArchmageVertex      = Vertex.Create(new( 0,  3, 0), new(-1,  4, 0), new(-1,  3, 0));
    public static readonly Vertex CrystalomancyVertex = Vertex.Create(new(-1,  3, 0), new(-1,  4, 0), new(-2,  4, 0));
    public static readonly Vertex InnerCircleVertex   = Vertex.Create(new(-1,  3, 0), new(-2,  4, 0), new(-2,  3, 0));

    public IReadOnlyList<PrestigeVertex> Vertices { get; }
    public IReadOnlyList<PrestigeHex> Hexes { get; }

    public event Action<Vertex>? VertexPurchased;

    internal void RaiseVertexPurchased(Vertex vertex) => VertexPurchased?.Invoke(vertex);

    public PrestigeMap(IEnumerable<PrestigeVertex> vertices, IEnumerable<PrestigeHex> hexes)
    {
        Vertices = vertices.ToList();
        Hexes = hexes.ToList();
    }

    public PrestigeVertex? GetVertex(Vertex coord) => Vertices.FirstOrDefault(v => v.Coord.Equals(coord));
    public PrestigeHex?    GetHex(HexCoord coord)  => Hexes.FirstOrDefault(h => h.Coord.Equals(coord));

    public IReadOnlyList<PrestigeVertex> GetNeighbors(Vertex coord)
        => Vertices.Where(v => !v.Coord.Equals(coord) && coord.IsAdjacentTo(v.Coord)).ToList();

    // Default cost formula: central = 10, others = 10 + distance² × 5.
    public static int DefaultCost(int distanceFromCenter)
    {
        int[] costPerDistance = new int[] { 10, 25, 100, 400, 2000, 10000 };
        int len = costPerDistance.Length;
        return distanceFromCenter < len
            ? costPerDistance[distanceFromCenter]
            : costPerDistance[len - 1] * (int)Math.Pow(10, distanceFromCenter + 1 - len);
    }


}
