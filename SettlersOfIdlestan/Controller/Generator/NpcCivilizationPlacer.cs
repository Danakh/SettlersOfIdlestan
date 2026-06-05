using System;
using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestan.Controller.Generator;

/// <summary>
/// Place les villes initiales des civilisations NPC sur la carte de l'île.
/// L'algorithme glouton maximise la distance minimale entre toutes les villes pour le
/// placement initial, puis délègue l'expansion au CivilizationAutoplayer afin que
/// les règles de distance intra-civilisation soient respectées.
/// </summary>
public class NpcCivilizationPlacer
{
    private const int MaxExpandIterations = 500;

    /// <summary>
    /// Place les civilisations NPC de l'état d'île fourni.
    /// La ville du joueur doit déjà être placée avant l'appel.
    /// Retourne false si un placement initial est impossible.
    /// </summary>
    public bool PlaceNpcCivilizations(WorldState state)
    {
        if (state.PlayerCivilization.Cities.Count == 0) return false;

        var npcCivs = state.Civilizations.Where(c => c.IsNpc).ToList();
        if (npcCivs.Count == 0) return true;

        var allOccupied = new List<Vertex> { state.PlayerCivilization.Cities[0].Position };
        var validVertices = FindValidCityVertices(state.GetMapForZ(IslandMap.SurfaceLayer));

        var npcModifiers = NpcModifierSetMaker.Create(maxTechTier: 3, maxPrestigeDistance: 2);

        foreach (var civ in npcCivs)
        {
            var initialVertex = FindBestVertex(validVertices, allOccupied);
            if (initialVertex == null) return false;

            allOccupied.Add(initialVertex);
            civ.AddCustomAggregator(npcModifiers);
            PopulateMinimumNpc(state.GetMapForZ(IslandMap.SurfaceLayer), civ, initialVertex);
        }

        bool needsExpansion = npcCivs.Any(c =>
            (c.NpcParameters?.EvolutionLevel ?? NpcEvolutionLevel.Minimum) != NpcEvolutionLevel.Minimum);

        if (!needsExpansion) return true;

        var clock = new GameClock();
        clock.Start();
        var mainController = new MainGameController();
        mainController.SetGame(new MainGameState(state, clock));

        foreach (var civ in npcCivs)
        {
            var level = civ.NpcParameters?.EvolutionLevel ?? NpcEvolutionLevel.Minimum;
            if (level == NpcEvolutionLevel.Minimum) continue;

            var aggressivity = civ.NpcParameters?.AggressivityLevel ?? NpcAggressivityLevel.Cautious;
            var autoplayer = new NpcCivilizationAutoplayer(civ, state.GetMapForZ(IslandMap.SurfaceLayer), mainController, aggressivity);
            ExpandNpcWithAutoplayer(autoplayer, civ, level);
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

    // ── Helpers de placement initial ─────────────────────────────────────

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
        civ.AddCity(city);
        FillMaxResources(civ);
    }

    private static void AddStep1ProductionBuildings(IslandMap map, City city)
    {
        foreach (var hex in city.Position.GetHexes())
        {
            var tile = map.GetTile(hex);
            if (tile == null) continue;

            switch (tile.TerrainType)
            {
                case TerrainType.Forest:   city.Buildings.Add(new Sawmill());    break;
                case TerrainType.Plain:    city.Buildings.Add(new Mill());       break;
                case TerrainType.Hill:     city.Buildings.Add(new Brickworks()); break;
                case TerrainType.Mountain: city.Buildings.Add(new Quarry());     break;
                case TerrainType.Water:    city.Buildings.Add(new Seaport());    break;
            }
        }
    }

    // ── Expansion via autoplayer ──────────────────────────────────────────

    private static int TargetCityCount(NpcEvolutionLevel level) => level switch
    {
        NpcEvolutionLevel.Low    => 3,
        NpcEvolutionLevel.Medium => 5,
        NpcEvolutionLevel.Strong => 7,
        _                        => 1,
    };

    private static void ExpandNpcWithAutoplayer(
        NpcCivilizationAutoplayer autoplayer, Civilization civ, NpcEvolutionLevel level)
    {
        int target = TargetCityCount(level);

        for (int i = 0; i < MaxExpandIterations; i++)
        {
            FillMaxResources(civ);
            autoplayer.TryStepOnce(shouldExpand: civ.Cities.Count < target);
        }

        if (level >= NpcEvolutionLevel.Medium)
        {
            for (int i = 0; i < MaxExpandIterations; i++)
            {
                FillMaxResources(civ);
                autoplayer.Inner.TryStep2Once(shouldExpand: false);
            }
        }

        FillMaxResources(civ);
    }

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
