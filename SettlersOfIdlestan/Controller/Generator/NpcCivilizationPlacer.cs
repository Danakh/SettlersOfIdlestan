using System;
using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestan.Controller.Generator;

/// <summary>
/// Place les villes initiales des civilisations NPC sur la carte de l'île.
/// L'algorithme glouton maximise la distance minimale entre toutes les villes.
/// </summary>
public class NpcCivilizationPlacer
{
    /// <summary>
    /// Place les civilisations NPC de l'état d'île fourni.
    /// La ville du joueur doit déjà être placée avant l'appel.
    /// Retourne false si un placement est impossible.
    /// </summary>
    public bool PlaceNpcCivilizations(IslandState state)
    {
        if (state.PlayerCivilization.Cities.Count == 0) return false;

        var npcCivs = state.Civilizations.Where(c => c.IsNpc).ToList();
        if (npcCivs.Count == 0) return true;

        var placedVertices = new List<Vertex> { state.PlayerCivilization.Cities[0].Position };
        var validVertices = FindValidCityVertices(state.Map);

        foreach (var civ in npcCivs)
        {
            var vertex = FindBestVertex(validVertices, placedVertices);
            if (vertex == null) return false;

            PopulateNpcCivilization(state.Map, civ, vertex);
            placedVertices.Add(vertex);
        }

        return true;
    }

    /// <summary>
    /// Retourne tous les vertex valides pour y fonder une ville :
    /// au moins 2 des 3 hexagones adjacents sont des terres présentes dans la carte.
    /// </summary>
    public List<Vertex> FindValidCityVertices(IslandMap map)
    {
        var vertices = new HashSet<Vertex>();

        foreach (var tile in map.Tiles.Values)
        {
            if (tile.TerrainType == TerrainType.Water) continue;

            foreach (SecondaryHexDirection dir in Enum.GetValues<SecondaryHexDirection>())
            {
                var vertex = tile.Coord.Vertex(dir);
                if (vertices.Contains(vertex)) continue;

                var hexes = vertex.GetHexes();
                var landCount = hexes.Count(h =>
                    map.HasTile(h) && map.GetTile(h)!.TerrainType != TerrainType.Water);

                if (landCount >= 2)
                    vertices.Add(vertex);
            }
        }

        return vertices.ToList();
    }

    /// <summary>
    /// Sélectionne le vertex candidat qui maximise la distance minimale à tous les vertex déjà occupés.
    /// </summary>
    private static Vertex? FindBestVertex(List<Vertex> candidates, List<Vertex> occupied)
    {
        Vertex? best = null;
        int bestMinDist = -1;

        foreach (var v in candidates)
        {
            if (occupied.Contains(v)) continue;
            int minDist = occupied.Min(o => o.EdgeDistanceTo(v));
            if (minDist > bestMinDist)
            {
                bestMinDist = minDist;
                best = v;
            }
        }

        return best;
    }

    private static void PopulateNpcCivilization(IslandMap map, Civilization civ, Vertex vertex)
    {
        switch (civ.NpcParameters?.EvolutionLevel ?? NpcEvolutionLevel.Minimum)
        {
            case NpcEvolutionLevel.Minimum:
                PopulateMinimumNpc(map, civ, vertex);
                break;
            // Low, Medium, Strong : phase d'expansion initiale — non implémentée
            default:
                PopulateMinimumNpc(map, civ, vertex);
                break;
        }
    }

    /// <summary>
    /// Peuple une ville NPC au niveau minimum :
    /// TownHall niveau 2, tous les bâtiments de production disponibles selon le terrain adjacent,
    /// un Marché, un Entrepôt niveau 1, et toutes les ressources au maximum.
    /// </summary>
    private static void PopulateMinimumNpc(IslandMap map, Civilization civ, Vertex vertex)
    {
        var city = new City(vertex) { CivilizationIndex = civ.Index };

        city.Buildings.Add(new TownHall { Level = 2 });

        foreach (var hex in vertex.GetHexes())
        {
            var tile = map.GetTile(hex);
            if (tile == null) continue;

            switch (tile.TerrainType)
            {
                case TerrainType.Forest:
                    city.Buildings.Add(new Sawmill());
                    break;
                case TerrainType.Plain:
                    city.Buildings.Add(new Mill());
                    break;
                case TerrainType.Hill:
                    city.Buildings.Add(new Brickworks());
                    break;
                case TerrainType.Mountain:
                    city.Buildings.Add(new Quarry());
                    break;
                case TerrainType.Water:
                    city.Buildings.Add(new Seaport());
                    break;
            }
        }

        city.Buildings.Add(new Market());
        city.Buildings.Add(new Warehouse { Level = 1 });

        civ.Cities.Add(city);

        foreach (Resource resource in Enum.GetValues<Resource>())
        {
            int max = civ.GetResourceMaxQuantity(resource);
            if (max > 0)
                civ.AddResource(resource, max);
        }
    }
}
