using System;
using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Controller.Island;
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

        var map = state.GetMapForZ(IslandMap.SurfaceLayer);
        foreach (var civ in npcCivs)
        {
            var level = civ.NpcParameters?.EvolutionLevel ?? NpcEvolutionLevel.Minimum;
            if (level == NpcEvolutionLevel.Minimum) continue;

            var aggressivity = civ.NpcParameters?.AggressivityLevel ?? NpcAggressivityLevel.Cautious;
            var autoplayer = new NpcCivilizationAutoplayer(civ, map, mainController, aggressivity);
            ExpandNpcWithAutoplayer(autoplayer, civ, map, level);
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
        city.InvalidateLevelCache();
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
        NpcCivilizationAutoplayer autoplayer, Civilization civ, IslandMap map, NpcEvolutionLevel level)
    {
        int target = civ.NpcParameters?.CityCount ?? TargetCityCount(level);

        for (int i = 0; i < MaxExpandIterations; i++)
        {
            if (civ.Cities.Count >= target) break;
            FillMaxResources(civ);
            autoplayer.Inner.TryStep0Once();
        }

        AddDefaultBuildingsForLevel(map, civ, level);
        FillMaxResources(civ);
    }

    private static readonly BuildingType[] StrongNonUniqueBuildings =
    {
        BuildingType.TownHall, BuildingType.Market, BuildingType.Mine, BuildingType.Warehouse,
        BuildingType.Forge, BuildingType.Library, BuildingType.Temple, BuildingType.BuildersGuild,
        BuildingType.Laboratory, BuildingType.Barracks, BuildingType.Palisade, BuildingType.Watchtower,
        BuildingType.Academy, BuildingType.MilitaryAcademy,
    };

    private static void AddDefaultBuildingsForLevel(IslandMap map, Civilization civ, NpcEvolutionLevel level)
    {
        foreach (var city in civ.Cities)
            ApplyBuildingsForLevel(map, city, level);
    }

    private static void ApplyBuildingsForLevel(IslandMap map, City city, NpcEvolutionLevel level)
    {
        // Terrain-based production buildings
        foreach (var hex in city.Position.GetHexes())
        {
            var tile = map.GetTile(hex);
            if (tile == null) continue;
            var bt = GetProductionBuildingType(tile.TerrainType);
            if (bt == null) continue;

            if (level == NpcEvolutionLevel.Strong)
            {
                var proto = BuildingController.CreateBuilding(bt.Value);
                if (proto != null) EnsureBuilding(city, bt.Value, proto.GetDefaultMaxLevel());
            }
            else
            {
                int prodLevel = level == NpcEvolutionLevel.Medium ? 3 : 2;
                EnsureBuilding(city, bt.Value, prodLevel);
            }
        }

        switch (level)
        {
            case NpcEvolutionLevel.Minimum:
                EnsureBuilding(city, BuildingType.TownHall, 1);
                EnsureBuilding(city, BuildingType.Market, 1);
                break;

            case NpcEvolutionLevel.Low:
                EnsureBuilding(city, BuildingType.TownHall, 2);
                EnsureBuilding(city, BuildingType.Market, 1);
                EnsureBuilding(city, BuildingType.Warehouse, 1);
                break;

            case NpcEvolutionLevel.Medium:
                EnsureBuilding(city, BuildingType.TownHall, 3);
                EnsureBuilding(city, BuildingType.Market, 1);
                EnsureBuilding(city, BuildingType.Warehouse, 3);
                EnsureBuilding(city, BuildingType.Palisade);
                EnsureBuilding(city, BuildingType.Barracks);
                break;

            case NpcEvolutionLevel.Strong:
                foreach (var bt in StrongNonUniqueBuildings)
                {
                    var proto = BuildingController.CreateBuilding(bt);
                    if (proto != null) EnsureBuilding(city, bt, proto.GetDefaultMaxLevel());
                }
                break;
        }
    }

    private static BuildingType? GetProductionBuildingType(TerrainType terrain) => terrain switch
    {
        TerrainType.Forest   => BuildingType.Sawmill,
        TerrainType.Plain    => BuildingType.Mill,
        TerrainType.Hill     => BuildingType.Brickworks,
        TerrainType.Mountain => BuildingType.Quarry,
        TerrainType.Water    => BuildingType.Seaport,
        TerrainType.Desert   => BuildingType.GlassWorks,
        _                    => null,
    };

    private static void EnsureBuilding(City city, BuildingType type, int targetLevel = 1)
    {
        var existing = city.Buildings.FirstOrDefault(b => b.Type == type);
        if (existing == null)
        {
            var building = BuildingController.CreateBuilding(type);
            if (building == null) return;
            building.Level = targetLevel;
            city.Buildings.Add(building);
            if (type == BuildingType.TownHall) city.InvalidateLevelCache();
        }
        else if (existing.Level < targetLevel)
        {
            existing.Level = targetLevel;
        }
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
