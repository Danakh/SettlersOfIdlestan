using System;
using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SettlersOfIdlestan.Controller.Island;

/// <summary>
/// Étend automatiquement la carte de l'underworld quand une route touche un hexagone manquant.
/// </summary>
public class AutoExtendController
{
    private WorldState? _state;
    private GamePRNG _prng = new();

    // 20 entrées : 10x Mountain=50%, 8x Desert=40%, 1x MithrilVein=5%, 1x CrystalCave=5%
    private static readonly TerrainType[] TerrainPool = new[]
    {
        TerrainType.Mountain, TerrainType.Mountain, TerrainType.Mountain, TerrainType.Mountain, TerrainType.Mountain,
        TerrainType.Mountain, TerrainType.Mountain, TerrainType.Mountain, TerrainType.Mountain, TerrainType.Mountain,
        TerrainType.Desert,   TerrainType.Desert,   TerrainType.Desert,   TerrainType.Desert,   TerrainType.Desert,
        TerrainType.Desert,   TerrainType.Desert,   TerrainType.Desert,
        TerrainType.MithrilVein,
        TerrainType.CrystalCave,
    };

    private const int MaxTotalCivilizations = 8;
    private const int AggressiveCivSpawnChancePercent = 10;
    private const int ExtraHexCount = 10;
    private const int AggressiveCivCityCount = 3;
    private const int MinHexDistanceFromArrival = 2;

    internal AutoExtendController() { }

    internal void Initialize(WorldState state, GamePRNG prng)
    {
        _state = state;
        _prng = prng;
    }

    /// <summary>
    /// À appeler après la construction d'une route. Génère les hexagones manquants
    /// aux deux vertex de l'arête sur les cartes marquées AutoExtend.
    /// Peut déclencher l'apparition d'une civilisation agressive si les conditions sont réunies.
    /// </summary>
    public void TryExtendMapAfterRoad(int civIndex, Edge roadEdge)
    {
        if (_state == null) return;

        int z = roadEdge.Z;
        if (!_state.Layers.TryGetValue(z, out var layerState) || !layerState.AutoExtend)
            return;

        var map = layerState.Map;

        // Snapshot des hexagones visibles par le joueur AVANT l'ajout des nouvelles tuiles
        var playerVisibleHexesBefore = GetPlayerVisibleHexCoords(layerState);

        var newHexes = new List<HexCoord>();
        foreach (var vertex in roadEdge.GetVertices())
        {
            foreach (var hex in vertex.GetHexes())
            {
                if (!map.HasTile(hex))
                {
                    map.AddTile(new HexTile(hex, RollTerrain()));
                    newHexes.Add(hex);
                }
            }
        }

        if (newHexes.Count > 0)
            _state.Visibility.RecalculateFor(civIndex);

        if (layerState.ArrivalVertex == null) return;
        if (civIndex != _state.PlayerCivilization.Index) return;

        foreach (var newHex in newHexes)
            TrySpawnAggressiveCivilization(newHex, layerState, playerVisibleHexesBefore, z);
    }

    // ── Helpers visibilité ────────────────────────────────────────────────────

    private HashSet<HexCoord> GetPlayerVisibleHexCoords(LayerState layerState)
    {
        if (_state == null) return new HashSet<HexCoord>();

        var visibleMaps = _state.Visibility.GetForZ(layerState.Map.Z);
        if (!visibleMaps.TryGetValue(_state.PlayerCivilization.Index, out var visibleMap))
            return new HashSet<HexCoord>();

        return new HashSet<HexCoord>(visibleMap.Tiles.Keys);
    }

    // ── Spawn civilisation agressive ─────────────────────────────────────────

    private void TrySpawnAggressiveCivilization(
        HexCoord newHex,
        LayerState layerState,
        HashSet<HexCoord> playerVisibleHexesBefore,
        int z)
    {
        if (_state == null) return;

        // Cap total civilisations
        if (_state.Civilizations.Count >= MaxTotalCivilizations) return;

        // Distance minimale depuis le vertex d'arrivée
        var arrivalHexes = layerState.ArrivalVertex!.GetHexes();
        int minDist = int.MaxValue;
        foreach (var h in arrivalHexes)
        {
            if (!newHex.HasSameZ(h)) continue;
            int d = newHex.DistanceTo(h);
            if (d < minDist) minDist = d;
        }
        if (minDist < MinHexDistanceFromArrival) return;

        // Au moins un vertex du nouvel hexagone n'était pas visible avant
        bool hasNewVertex = false;
        foreach (var dir in SecondaryHexDirectionUtils.AllSecondaryDirections)
        {
            var v = newHex.Vertex(dir);
            if (!v.GetHexes().Any(h => playerVisibleHexesBefore.Contains(h)))
            {
                hasNewVertex = true;
                break;
            }
        }
        if (!hasNewVertex) return;

        // 10% de chance
        if (_prng.Next(100) >= AggressiveCivSpawnChancePercent) return;

        SpawnAggressiveCivilization(newHex, layerState, playerVisibleHexesBefore, z);
    }

    private void SpawnAggressiveCivilization(
        HexCoord originHex,
        LayerState layerState,
        HashSet<HexCoord> playerVisibleHexesBefore,
        int z)
    {
        if (_state == null) return;

        var map = layerState.Map;

        // Ajout de jusqu'à ExtraHexCount hexagones autour de l'hexagone d'origine (non visibles)
        var extraHexes = new List<HexCoord>();
        var frontier = new Queue<HexCoord>();
        var visited = new HashSet<HexCoord> { originHex };
        frontier.Enqueue(originHex);

        while (frontier.Count > 0 && extraHexes.Count < ExtraHexCount)
        {
            var current = frontier.Dequeue();
            foreach (var neighbor in current.Neighbors())
            {
                if (visited.Contains(neighbor)) continue;
                visited.Add(neighbor);
                if (playerVisibleHexesBefore.Contains(neighbor)) continue;

                if (!map.HasTile(neighbor))
                {
                    map.AddTile(new HexTile(neighbor, RollTerrain()));
                    extraHexes.Add(neighbor);
                }

                if (extraHexes.Count < ExtraHexCount)
                    frontier.Enqueue(neighbor);
            }
        }

        if (extraHexes.Count == 0) return;

        // Cherche les vertex valides pour les villes (≥2 hexes sur la carte, non visibles)
        var candidateVertices = FindCandidateCityVertices(extraHexes, map, playerVisibleHexesBefore, z);
        if (candidateVertices.Count == 0) return;

        // Création de la civilisation agressive
        int newCivIndex = _state.Civilizations.Max(c => c.Index) + 1;
        var extraModifiers = BuildAggressiveModifiers();

        var npcCiv = new Civilization
        {
            Index = newCivIndex,
            IsNpc = true,
            NpcParameters = new NpcParameters
            {
                AggressivityLevel = NpcAggressivityLevel.Warlike,
                EvolutionLevel = NpcEvolutionLevel.Strong,
                ExtraModifiers = extraModifiers,
            },
        };
        npcCiv.AddCustomAggregator(new StaticModifierProvider(extraModifiers));

        // Placement des villes (jusqu'à AggressiveCivCityCount)
        int citiesPlaced = 0;
        foreach (var vertex in candidateVertices)
        {
            if (citiesPlaced >= AggressiveCivCityCount) break;

            // Distance avec les villes existantes (autres civs : ≥2, même civ : ≥3)
            bool tooCloseToOther = _state.GetAllCities()
                .Any(c => c.Position.Z == z && c.Position.EdgeDistanceTo(vertex) < 2);
            if (tooCloseToOther) continue;

            bool tooCloseToOwn = npcCiv.Cities
                .Any(c => c.Position.EdgeDistanceTo(vertex) < 3);
            if (tooCloseToOwn) continue;

            var city = new City(vertex) { CivilizationIndex = newCivIndex };
            PopulateAggressiveCity(city, map);
            city.Soldiers = city.MaxSoldiers + npcCiv.CityMaxSoldiersBonus;
            npcCiv.AddCity(city);
            citiesPlaced++;
        }

        if (citiesPlaced == 0) return;

        // Remplissage des ressources initiales
        FillMaxResources(npcCiv);

        _state.Civilizations.Add(npcCiv);
        _state.Visibility.Recalculate();
    }

    private List<Vertex> FindCandidateCityVertices(
        List<HexCoord> extraHexes,
        IslandMap map,
        HashSet<HexCoord> playerVisibleHexesBefore,
        int z)
    {
        var seen = new HashSet<Vertex>();
        var candidates = new List<Vertex>();

        foreach (var hex in extraHexes)
        {
            foreach (var dir in SecondaryHexDirectionUtils.AllSecondaryDirections)
            {
                var vertex = hex.Vertex(dir);
                if (seen.Contains(vertex)) continue;
                seen.Add(vertex);

                // Non visible par le joueur avant l'extension
                if (vertex.GetHexes().Any(h => playerVisibleHexesBefore.Contains(h)))
                    continue;

                // Au moins 2 hexes du vertex sont sur la carte
                var hexes = vertex.GetHexes();
                int onMap = hexes.Count(h => map.HasTile(h));
                if (onMap < 2) continue;

                // Aucun hex d'eau
                if (hexes.Any(h => map.HasTile(h) && map.GetTile(h)!.TerrainType == TerrainType.Water))
                    continue;

                candidates.Add(vertex);
            }
        }

        return candidates;
    }

    // Niveau appliqué aux bâtiments dont le max de base est 0 (verrouillés par prestige)
    private const int NpcPrestigeLevelOverride = 3;

    private static void PopulateAggressiveCity(City city, IslandMap map)
    {
        // TownHall en premier — son level détermine city.Level pour les checks AvailableAtLevel
        var townHall = new TownHall { Level = new TownHall().GetDefaultMaxLevel() };
        city.Buildings.Add(townHall);
        city.InvalidateLevelCache();

        foreach (BuildingType type in Enum.GetValues<BuildingType>())
        {
            if (type == BuildingType.TownHall) continue;

            var building = BuildingController.CreateBuilding(type);
            if (building == null) continue;
            if (!building.IsBuildingAvailableForCity(map, city)) continue;

            int maxLevel = building.GetDefaultMaxLevel() > 0
                ? building.GetDefaultMaxLevel()
                : NpcPrestigeLevelOverride;
            building.Level = maxLevel;

            if (building.ActivationStatus != ActivationStatus.NON_ACTIVABLE)
                building.ActivationStatus = ActivationStatus.ACTIVE;

            city.Buildings.Add(building);
        }
    }

    private static List<Modifier> BuildAggressiveModifiers() =>
    [
        // Grand stockage de ressources de base
        new(ECategory.STORAGE_CAPACITY_BASIC, EType.ADDITIVE, 500),
        // Capacité 100 soldats par ville (Caserne niv.3 = 15, + 85 de bonus = 100)
        new(ECategory.CITY_MAX_SOLDIERS_BONUS, EType.ADDITIVE, 85),
        // Génération passive de nourriture : 20/s (100 ticks) pour couvrir 50 soldats × 3 villes + marge
        new(ECategory.PASSIVE_RESOURCE_GENERATION, "Food", EType.ADDITIVE, 20),
        // Génération passive de minerai pour produire des soldats : 2/s
        new(ECategory.PASSIVE_RESOURCE_GENERATION, "Ore", EType.ADDITIVE, 2),
    ];

    private static void FillMaxResources(Civilization civ)
    {
        foreach (Resource resource in Enum.GetValues<Resource>())
        {
            int max = civ.GetResourceMaxQuantity(resource);
            if (max > 0)
            {
                try { civ.AddResource(resource, max); }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[AutoExtendController] AddResource {resource}: {ex.Message}"); }
            }
        }
    }

    private TerrainType RollTerrain() => TerrainPool[_prng.Next(TerrainPool.Length)];
}
