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
    private const int MinEdgeDist = 7;

    private static readonly HashSet<BuildingType> ProductionBuildingTypes =
    [
        BuildingType.Sawmill,
        BuildingType.Mill,
        BuildingType.Brickworks,
        BuildingType.Quarry,
        BuildingType.Mine,
        BuildingType.Seaport,
    ];

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

        var allOccupied = new List<Vertex> { state.PlayerCivilization.Cities[0].Position };
        var validVertices = FindValidCityVertices(state.Map);

        foreach (var civ in npcCivs)
        {
            var initialVertex = FindBestVertex(validVertices, allOccupied);
            if (initialVertex == null) return false;

            allOccupied.Add(initialVertex);
            PopulateNpcCivilization(state.Map, civ, initialVertex, allOccupied, validVertices);
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

    private static void PopulateNpcCivilization(
        IslandMap map, Civilization civ, Vertex initialVertex,
        List<Vertex> allOccupied, List<Vertex> validVertices)
    {
        switch (civ.NpcParameters?.EvolutionLevel ?? NpcEvolutionLevel.Minimum)
        {
            case NpcEvolutionLevel.Minimum:
                PopulateMinimumNpc(map, civ, initialVertex);
                break;
            case NpcEvolutionLevel.Low:
                PopulateLowNpc(map, civ, initialVertex, allOccupied, validVertices);
                break;
            case NpcEvolutionLevel.Medium:
                PopulateMediumNpc(map, civ, initialVertex, allOccupied, validVertices);
                break;
            case NpcEvolutionLevel.Strong:
                PopulateStrongNpc(map, civ, initialVertex, allOccupied, validVertices);
                break;
        }
    }

    // ── Niveaux d'évolution ───────────────────────────────────────────────

    /// <summary>
    /// Minimum : 1 ville, TownHall niveau 2, bâtiments de production step-1 selon le terrain,
    /// Market, Warehouse niveau 1, toutes les ressources au maximum.
    /// </summary>
    private static void PopulateMinimumNpc(IslandMap map, Civilization civ, Vertex vertex)
    {
        var city = new City(vertex) { CivilizationIndex = civ.Index };
        city.Buildings.Add(new TownHall { Level = 2 });
        AddStep1ProductionBuildings(map, city);
        city.Buildings.Add(new Market());
        city.Buildings.Add(new Warehouse { Level = 1 });
        civ.Cities.Add(city);
        FillMaxResources(civ);
    }

    /// <summary>
    /// Low : Minimum + step 1 jusqu'à 3 villes (ressources au max à chaque étape et à la fin).
    /// </summary>
    private static void PopulateLowNpc(
        IslandMap map, Civilization civ, Vertex initialVertex,
        List<Vertex> allOccupied, List<Vertex> validVertices)
    {
        PopulateMinimumNpc(map, civ, initialVertex);

        ExpandWithStep1Cities(map, civ, targetCount: 3, allOccupied, validVertices);

        FillMaxResources(civ);
    }

    /// <summary>
    /// Medium : Low (3 villes) + step 1 jusqu'à 5 villes
    /// puis step 2 sans expansion (Mine, Forge, Warehouse, moitié des bâtiments de production niveau 3+).
    /// </summary>
    private static void PopulateMediumNpc(
        IslandMap map, Civilization civ, Vertex initialVertex,
        List<Vertex> allOccupied, List<Vertex> validVertices)
    {
        PopulateLowNpc(map, civ, initialVertex, allOccupied, validVertices);

        ExpandWithStep1Cities(map, civ, targetCount: 5, allOccupied, validVertices);

        ApplyStep2Upgrades(map, civ);
        FillMaxResources(civ);
    }

    /// <summary>
    /// Strong : Medium (5 villes avec step 2) + step 1 jusqu'à 7 villes.
    /// </summary>
    private static void PopulateStrongNpc(
        IslandMap map, Civilization civ, Vertex initialVertex,
        List<Vertex> allOccupied, List<Vertex> validVertices)
    {
        PopulateMediumNpc(map, civ, initialVertex, allOccupied, validVertices);

        ExpandWithStep1Cities(map, civ, targetCount: 7, allOccupied, validVertices);

        FillMaxResources(civ);
    }

    // ── Helpers d'expansion ───────────────────────────────────────────────

    private static void ExpandWithStep1Cities(
        IslandMap map, Civilization civ, int targetCount,
        List<Vertex> allOccupied, List<Vertex> validVertices)
    {
        while (civ.Cities.Count < targetCount)
        {
            var ownCities = civ.Cities.Select(c => c.Position).ToList();
            var expansion = FindExpansionVertex(validVertices, allOccupied, ownCities);
            if (expansion == null) break;

            AddStep1City(map, civ, expansion);
            allOccupied.Add(expansion);
            FillMaxResources(civ);
        }
    }

    private static void AddStep1City(IslandMap map, Civilization civ, Vertex vertex)
    {
        var city = new City(vertex) { CivilizationIndex = civ.Index };
        city.Buildings.Add(new TownHall { Level = 1 });
        AddStep1ProductionBuildings(map, city);
        city.Buildings.Add(new Market());
        civ.Cities.Add(city);
    }

    /// <summary>
    /// Bâtiments de production step-1 selon le terrain : Sawmill, Mill, Brickworks, Quarry, Seaport.
    /// Mine et Forge sont des bâtiments step-2 et ne sont pas inclus ici.
    /// </summary>
    private static void AddStep1ProductionBuildings(IslandMap map, City city)
    {
        foreach (var hex in city.Position.GetHexes())
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
    }

    /// <summary>
    /// Step 2 sans expansion :
    /// - TownHall porté au niveau 2 dans toutes les villes
    /// - Mine dans les villes adjacentes à une Montagne
    /// - Forge dans les villes avec des bâtiments de production
    /// - Warehouse si absente
    /// - La moitié (arrondie au supérieur) des bâtiments de production montée au niveau 3
    /// </summary>
    private static void ApplyStep2Upgrades(IslandMap map, Civilization civ)
    {
        foreach (var city in civ.Cities)
        {
            var townHall = city.Buildings.First(b => b.Type == BuildingType.TownHall);
            if (townHall.Level < 2) townHall.Level = 2;

            if (!city.Buildings.Any(b => b.Type == BuildingType.Mine)
                && city.Position.GetHexes().Any(h => map.GetTile(h)?.TerrainType == TerrainType.Mountain))
            {
                city.Buildings.Add(new Mine());
            }

            if (!city.Buildings.Any(b => b.Type == BuildingType.Forge)
                && city.Buildings.Any(b => ProductionBuildingTypes.Contains(b.Type)))
            {
                city.Buildings.Add(new Forge());
            }

            if (!city.Buildings.Any(b => b.Type == BuildingType.Warehouse))
                city.Buildings.Add(new Warehouse { Level = 1 });
        }

        var allProdBuildings = civ.Cities
            .SelectMany(c => c.Buildings)
            .Where(b => ProductionBuildingTypes.Contains(b.Type))
            .ToList();

        int halfCount = (allProdBuildings.Count + 1) / 2;
        foreach (var b in allProdBuildings.Take(halfCount))
            b.Level = 3;
    }

    /// <summary>
    /// Sélectionne le meilleur vertex d'expansion : le plus proche des villes existantes de la civ,
    /// à au moins MinEdgeDist des villes ennemies, avec une portée croissante si nécessaire.
    /// </summary>
    private static Vertex? FindExpansionVertex(
        List<Vertex> validVertices,
        List<Vertex> allOccupied,
        List<Vertex> ownCities)
    {
        var otherOccupied = allOccupied.Where(v => !ownCities.Contains(v)).ToList();

        for (int maxDist = 3; maxDist <= 16; maxDist++)
        {
            var candidate = validVertices
                .Where(v => !allOccupied.Contains(v))
                .Where(v => ownCities.Any(c => c.EdgeDistanceTo(v) <= maxDist))
                .Where(v => otherOccupied.Count == 0
                         || otherOccupied.Min(o => o.EdgeDistanceTo(v)) >= MinEdgeDist)
                .OrderBy(v => ownCities.Min(c => c.EdgeDistanceTo(v)))
                .FirstOrDefault();

            if (candidate != null) return candidate;
        }

        return null;
    }

    /// <summary>Remplit toutes les ressources au maximum pour la civilisation.</summary>
    private static void FillMaxResources(Civilization civ)
    {
        foreach (Resource resource in Enum.GetValues<Resource>())
        {
            int max = civ.GetResourceMaxQuantity(resource);
            if (max > 0)
                civ.AddResource(resource, max);
        }
    }
}
