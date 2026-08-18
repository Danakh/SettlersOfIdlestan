using System.Collections.Generic;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Monsters;

namespace SettlersOfIdlestan.Controller.Generator;

/// <summary>
/// Génère le Pandémonium : une île hexagonale unique de <see cref="IslandHexCount"/> hexagones
/// (rayon <see cref="IslandRadius"/> autour de l'origine) cernée d'un anneau de Void, avec un
/// <see cref="DemonGod"/> en son centre et <see cref="TentacleCount"/> Tentacules serrées autour de
/// lui, dans un rayon de <see cref="TentacleRadius"/> hexes. Le joueur y arrive au bord : l'anneau
/// extérieur lui est laissé pour prendre pied, et c'est en avançant vers le centre qu'il passe sous
/// la garde des Tentacules.
///
/// Contrairement à l'Abysse (îles générées à la volée derrière chaque hex de Void, voir
/// <see cref="AbyssIslandGenerator"/>), cette couche est entièrement posée d'un coup et n'est
/// jamais étendue : <see cref="LayerState.AutoExtend"/> reste false.
/// </summary>
public static class PandemoniumGenerator
{
    /// <summary>
    /// Distance maximale au centre pour un hex de terre — 4 donne 1 + 6 + 12 + 18 + 24 = 61 hexes.
    ///
    /// <para>Le rayon 3 (37 hexes) ne laissait pas de quoi assiéger : le dieu démon frappant lui-même
    /// à 2 hexes, seul le dernier anneau échappait à sa garde, et les Tentacules en couvraient
    /// l'essentiel. Mesuré sur 7 seeds, 2 à 9 emplacements de ville hors de portée sur toute l'île —
    /// le siège les occupait tous en quatre villes puis se figeait. À rayon 4 il y en a 14 à 29, en
    /// grappes connexes : la bande côtière devient colonisable, l'approche du centre reste gardée.</para>
    /// </summary>
    public const int IslandRadius = 4;

    /// <summary>Nombre d'hexagones de terre de l'île (hexagone plein de rayon <see cref="IslandRadius"/>).</summary>
    public const int IslandHexCount = 61;

    public const int TentacleCount = 8;

    /// <summary>
    /// Distance maximale au centre pour une Tentacule : elles gardent le dieu démon de près au lieu
    /// d'être éparpillées sur toute l'île. Les deux premiers anneaux offrent 6 + 12 = 18 hexes pour
    /// 8 Tentacules, et jamais moins de 16 une fois exclus les abords du joueur (voir
    /// <see cref="PickTentacleHexes"/> : arrivé sur le dernier anneau, il n'en exclut au plus que les
    /// deux hexes d'anneau 2 voisins du seul de ses hexes qui soit d'anneau 3) — le compte est donc
    /// toujours tenu.
    ///
    /// <para>C'est ce qui rend l'arène assiégeable : réparties sur toute l'île, neuf monstres de
    /// portée 2 ne laissaient aucun emplacement de ville hors de portée (mesuré au Pandémonium du
    /// SOIStrategyTester : 0 vertex sûr sur 25 constructibles, siège figé). Ramenées au centre, et
    /// avec le rayon d'île porté à <see cref="IslandRadius"/>, elles laissent les deux anneaux
    /// extérieurs libres pour bâtir.</para>
    /// </summary>
    public const int TentacleRadius = 2;

    /// <summary>Résultat de la génération : la couche prête à être ajoutée au monde, et ses monstres.</summary>
    /// <remarks>
    /// Les monstres sont retournés plutôt qu'ajoutés : les features vivent sur le
    /// <see cref="WorldState"/>, pas sur la couche, et doivent passer par
    /// <see cref="WorldState.AddFeature"/> pour alimenter le cache par hex et lever les events.
    /// </remarks>
    public sealed record PandemoniumLayout(LayerState Layer, IReadOnlyList<MonsterFeature> Monsters);

    /// <summary>
    /// Terrains de l'île, repris de ceux des îles de l'Abysse : le Pandémonium doit rester
    /// exploitable (bois, nourriture, pierre) pour que le joueur puisse y bâtir de quoi assiéger.
    /// </summary>
    private static readonly TerrainType[] TerrainPool = AbyssIslandGenerator.TerrainPool;

    /// <summary>
    /// Construit la couche et y installe l'avant-poste du joueur (ajouté à <paramref name="playerCiv"/>).
    /// <paramref name="monsterLevel"/> est appliqué au dieu démon comme aux Tentacules.
    /// </summary>
    public static PandemoniumLayout Create(Civilization playerCiv, GamePRNG prng, int monsterLevel = 1)
    {
        const int z = LayerState.PandemoniumZ;
        var center = new HexCoord(0, 0, z);

        var islandHexes = HexesWithinRadius(center, IslandRadius);
        var islandSet = new HashSet<HexCoord>(islandHexes);

        var tiles = new List<HexTile>(islandHexes.Count);
        foreach (var hex in islandHexes)
            tiles.Add(new HexTile(hex, TerrainPool[prng.Next(TerrainPool.Length)]));

        // Anneau de Void : la couche ne s'étend jamais, il ne sert qu'à fermer visuellement l'île
        // (un hex de Void n'est pas rendu) et à empêcher route et ville de sortir de l'arène.
        foreach (var hex in HexesWithinRadius(center, IslandRadius + 1))
            if (!islandSet.Contains(hex))
                tiles.Add(new HexTile(hex, TerrainType.Void));

        var map = new IslandMap(tiles, z);
        var arrivalVertex = PickBorderVertex(center, islandSet, prng);

        var outpost = new City(arrivalVertex) { CivilizationIndex = playerCiv.Index };
        playerCiv.AddCity(outpost);

        var layer = new LayerState
        {
            Map = map,
            AutoExtend = false,
            ArrivalVertex = arrivalVertex,
        };

        var monsters = new List<MonsterFeature>(TentacleCount + 1) { new DemonGod(center, monsterLevel) };
        foreach (var hex in PickTentacleHexes(center, islandHexes, arrivalVertex, prng))
            monsters.Add(new Tentacle(hex, monsterLevel));

        return new PandemoniumLayout(layer, monsters);
    }

    /// <summary>Tous les hexagones à distance &lt;= <paramref name="radius"/> du centre, dans un ordre déterministe.</summary>
    private static List<HexCoord> HexesWithinRadius(HexCoord center, int radius)
    {
        var result = new List<HexCoord>();
        for (int q = -radius; q <= radius; q++)
        {
            int rMin = System.Math.Max(-radius, -q - radius);
            int rMax = System.Math.Min(radius, -q + radius);
            for (int r = rMin; r <= rMax; r++)
                result.Add(new HexCoord(center.Q + q, center.R + r, center.Z));
        }
        return result;
    }

    /// <summary>
    /// Vertex d'arrivée du joueur : sur le pourtour de l'île (un de ses hexes au moins est à la
    /// distance maximale du centre), mais dont les trois hexes sont bien de la terre — un avant-poste
    /// posé sur du Void n'aurait ni récolte ni extension possible. Il en existe toujours : chaque hex
    /// du dernier anneau partage au moins un vertex avec deux autres hexes de l'île.
    /// </summary>
    private static Vertex PickBorderVertex(HexCoord center, HashSet<HexCoord> islandSet, GamePRNG prng)
    {
        var candidates = new List<Vertex>();
        foreach (var hex in islandSet)
        {
            if (hex.DistanceTo(center) != IslandRadius) continue;
            foreach (var dir in SecondaryHexDirectionUtils.AllSecondaryDirections)
            {
                var vertex = hex.Vertex(dir);
                bool allOnIsland = true;
                foreach (var h in vertex.GetHexes())
                    if (!islandSet.Contains(h)) { allOnIsland = false; break; }
                if (allOnIsland) candidates.Add(vertex);
            }
        }

        // Tri avant tirage : l'ordre d'énumération d'un HashSet n'est pas un contrat, alors que la
        // génération doit rester reproductible à seed égal (voir GamePRNG).
        candidates.Sort(CompareVertices);
        return candidates[prng.Next(candidates.Count)];
    }

    private static int CompareVertices(Vertex a, Vertex b)
    {
        int c = a.Hex1.Q.CompareTo(b.Hex1.Q);
        if (c != 0) return c;
        c = a.Hex1.R.CompareTo(b.Hex1.R);
        if (c != 0) return c;
        c = a.Hex2.Q.CompareTo(b.Hex2.Q);
        if (c != 0) return c;
        return a.Hex2.R.CompareTo(b.Hex2.R);
    }

    /// <summary>
    /// Hexes des Tentacules : dans un rayon de <see cref="TentacleRadius"/> autour du centre, sauf le
    /// centre lui-même (occupé par le dieu démon) et sauf les hexes collés au joueur — les trois hexes
    /// de son vertex d'arrivée et tous leurs voisins — pour qu'il ne débarque pas au corps-à-corps
    /// d'une Tentacule.
    /// </summary>
    private static List<HexCoord> PickTentacleHexes(
        HexCoord center, List<HexCoord> islandHexes, Vertex arrivalVertex, GamePRNG prng)
    {
        var excluded = new HashSet<HexCoord> { center };
        foreach (var hex in arrivalVertex.GetHexes())
        {
            excluded.Add(hex);
            foreach (var neighbor in hex.Neighbors())
                excluded.Add(neighbor);
        }

        var candidates = new List<HexCoord>(islandHexes.Count);
        foreach (var hex in islandHexes)
            if (hex.DistanceTo(center) <= TentacleRadius && !excluded.Contains(hex))
                candidates.Add(hex);

        // Tirage sans remise : une Tentacule par hex.
        var chosen = new List<HexCoord>(TentacleCount);
        int count = System.Math.Min(TentacleCount, candidates.Count);
        for (int i = 0; i < count; i++)
        {
            int index = prng.Next(candidates.Count);
            chosen.Add(candidates[index]);
            candidates.RemoveAt(index);
        }
        return chosen;
    }
}
