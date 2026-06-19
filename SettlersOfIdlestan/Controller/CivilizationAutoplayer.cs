using System;
using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Prestige;
using SettlersOfIdlestan.Controller.Expand;
using SettlersOfIdlestan.Controller.Island;
using System.Diagnostics;

namespace SettlersOfIdlestan.Controller
{
    /// <summary>
    /// Utility autoplayer for a civilization: provides single-attempt methods to build roads,
    /// outposts and buildings, plus step-based strategies for automated civilisation growth.
    /// None of these methods advance the game clock — use CivilizationAutoplayerRunner in
    /// SOITests for time-advancing loops.
    /// </summary>
    public class CivilizationAutoplayer
    {
        private readonly Civilization _civ;
        private readonly IslandMap _map;
        private readonly RoadController _roadController;
        private readonly HarvestController _harvestController;
        private readonly BuildingController _buildingController;
        private readonly CityBuilderController _cityBuilderController;
        private readonly TradeController _tradeController;
        private readonly ResearchController _researchController;
        private readonly PrestigeController _prestigeController;
        private readonly PrestigeMapController _prestigeMapController;
        private readonly WorldState? _worldState;
        private readonly PrestigeState? _prestigeState;
        private readonly Action? _performPrestige;

        private static readonly BuildingType[] Step1Buildings =
        {
            BuildingType.TownHall, BuildingType.Seaport, BuildingType.Market,
            BuildingType.Sawmill, BuildingType.Brickworks, BuildingType.Quarry, BuildingType.Mill,
        };

        private static readonly BuildingType[] Step2Buildings =
            Step1Buildings.Concat(new[] { BuildingType.Warehouse, BuildingType.Mine, BuildingType.Forge }).ToArray();

        private static readonly BuildingType[] Step2WithLibraryBuildings =
            Step2Buildings.Concat(new[] { BuildingType.Library }).ToArray();

        private static readonly BuildingType[] Step3Buildings =
            Step2Buildings.Concat(new[] { BuildingType.Temple }).ToArray();

        private static readonly BuildingType[] MilitaryBuildings = { BuildingType.Palisade, BuildingType.Barracks };

        public Civilization Civilization => _civ;

        public CivilizationAutoplayer(
            Civilization civ,
            IslandMap map,
            RoadController roadController,
            HarvestController harvestController,
            BuildingController buildingController,
            CityBuilderController cityBuilderController,
            TradeController tradeController,
            ResearchController researchController,
            PrestigeController prestigeController,
            PrestigeMapController prestigeMapController,
            WorldState? worldState,
            PrestigeState? prestigeState = null,
            Action? performPrestige = null)
        {
            _civ = civ ?? throw new ArgumentNullException(nameof(civ));
            _map = map ?? throw new ArgumentNullException(nameof(map));
            _roadController = roadController ?? throw new ArgumentNullException(nameof(roadController));
            _harvestController = harvestController ?? throw new ArgumentNullException(nameof(harvestController));
            _buildingController = buildingController ?? throw new ArgumentNullException(nameof(buildingController));
            _cityBuilderController = cityBuilderController ?? throw new ArgumentNullException(nameof(cityBuilderController));
            _tradeController = tradeController ?? throw new ArgumentNullException(nameof(tradeController));
            _researchController = researchController ?? throw new ArgumentNullException(nameof(researchController));
            _prestigeController = prestigeController ?? throw new ArgumentNullException(nameof(prestigeController));
            _prestigeMapController = prestigeMapController ?? throw new ArgumentNullException(nameof(prestigeMapController));
            _worldState = worldState;
            _prestigeState = prestigeState;
            _performPrestige = performPrestige;
        }

        // ── Primitive utilities ──────────────────────────────────────────────────

        public void TryGrindOnce(ResourceSet? requiredResources)
        {
            var toHarvest = new HashSet<HexCoord>();
            foreach (var city in _civ.Cities)
            {
                foreach (var h in city.Position.GetHexes())
                    if (h != null) toHarvest.Add(h);
            }

            foreach (var hex in toHarvest)
                _harvestController.ManualHarvest(_civ.Index, hex);

            if (requiredResources != null && requiredResources.Any())
            {
                if (_tradeController.TryAutoTradeForPurchase(_civ.Index, requiredResources))
                    return;
            }
        }

        public bool TryBuildRoadOnce(Edge edge, bool withGrind = true)
        {
            if (edge == null) throw new ArgumentNullException(nameof(edge));

            var buildableEdges = _roadController.GetBuildableRoads(_civ.Index).Select(r => r.Position);
            if (!buildableEdges.Any(e => e.Equals(edge))) return false;

            if (_roadController.BuildRoad(_civ.Index, edge) != null)
                return true;

            if (withGrind)
            {
                var road = _roadController.GetBuildableRoads(_civ.Index).FirstOrDefault(r => r.Position.Equals(edge));
                if (road != null) TryGrindOnce(_roadController.GetRoadCost(road.DistanceToNearestCity));
            }
            return false;
        }

        public bool TryBuildOutpostOnce(Vertex vertex, bool withGrind = true)
        {
            if (vertex == null) throw new ArgumentNullException(nameof(vertex));

            if (!_cityBuilderController.GetBuildableVertices(_civ.Index).Any(v => v.Equals(vertex)))
                return false;

            if (_cityBuilderController.BuildCity(_civ.Index, vertex) != null)
                return true;

            if (withGrind) TryGrindOnce(_cityBuilderController.NewCityBuildingCost());
            return false;
        }

        /// <summary>
        /// Attempts to build or upgrade the specified building. When <paramref name="withGrind"/> is
        /// true (default) and resources are insufficient, calls TryGrindOnce to harvest/trade.
        /// Pass false when calling from TryStepOnce to avoid cross-building trade interference.
        /// </summary>
        public bool TryBuildBuildingOnce(City city, BuildingType buildingType, bool withGrind = true)
        {
            if (city == null) throw new ArgumentNullException(nameof(city));

            var buildables = _buildingController.GetBuildingsAndBuildables(city);
            var target = buildables.FirstOrDefault(b => b.Type == buildingType);
            if (target == null) return false;

            // Skip already-maxed buildings
            if (target.Level > 0 && target.Level >= _buildingController.GetMaxLevel(target, _civ.Index))
                return false;

            if (_buildingController.BuildBuilding(city, buildingType))
                return true;

            if (withGrind)
            {
                // Use correct upgrade cost: Level=0 → build cost; Level≥1 → upgrade to next level
                var required = target.Level == 0
                    ? target.GetBuildCost()
                    : target.GetUpgradeCost(target.Level + 1);
                TryGrindOnce(required);
            }

            return false;
        }

        // ── Step strategies ──────────────────────────────────────────────────────

        /// <summary>Step 1: level-1 production buildings, roads and new cities.</summary>
        public bool TryStep1Once(bool shouldExpand = true) => TryStepOnce(Step1Buildings, shouldExpand);

        /// <summary>Step 2: step 1 + upgraded production, Warehouse and Forge.
        /// Also builds Libraries once research is unlocked via prestige.</summary>
        public bool TryStep2Once(bool shouldExpand = true)
        {
            var buildings = _researchController.IsResearchUnlocked()
                ? Step2WithLibraryBuildings
                : Step2Buildings;
            return TryStepOnce(buildings, shouldExpand);
        }

        /// <summary>Step 3: step 2 + prestige-point buildings (Temple, TownHall upgrades) + unique buildings.
        /// When prestige points are sufficient but ImperialPort is missing, focuses exclusively on the
        /// first coastal city: Seaport 4, Warehouse 4, TownHall 4, then ImperialPort.</summary>
        public bool TryStep3Once(bool shouldExpand = true)
        {
            bool readyForPort = _prestigeController.CalculatePrestigePoints() >= PrestigeController.PrestigeRequiredPoints
                                && !_prestigeController.HasImperialPort();

            if (readyForPort)
                return TryStep3PortFocusOnce();
            else
                return TryStepOnce(Step3Buildings, shouldExpand);
        }

        private bool TryStep3PortFocusOnce()
        {
            var coastalCity = _civ.Cities.FirstOrDefault(c =>
                _map.IsOnSameLayer(c.Position) &&
                _map.VertexHasTerrainType(c.Position, TerrainType.Water));
            if (coastalCity == null) return false;

            bool didSomething = false;
            TryGrindOnce(null);

            if (TryResearchOnce())
                didSomething = true;

            bool shouldGrind = true;
            foreach (var bt in new[] { BuildingType.Seaport, BuildingType.Warehouse, BuildingType.TownHall })
            {
                Building? existing = coastalCity.Buildings.FirstOrDefault(b => b.Type == bt);
                if ((existing == null) || existing.Level < _buildingController.GetMaxLevel(existing, _civ.Index))
                {
                    if (TryBuildBuildingOnce(coastalCity, bt, withGrind: shouldGrind))
                        didSomething = true;
                    shouldGrind = false;
                }
            }

            if (TryBuildUniqueBuildingOnce(coastalCity, BuildingType.ImperialPort, withGrind: shouldGrind))
                didSomething = true;

            return didSomething;
        }

        /// <summary>Step 0 : expansion seule — outpost si un vertex est disponible, sinon route vers un vertex prospectif.
        /// N'effectue aucune construction de bâtiments.</summary>
        public bool TryStep0Once()
        {
            bool didSomething = false;

            var possibleConstructionVertex = _cityBuilderController.GetBuildableVertices(_civ.Index).FirstOrDefault();
            if (possibleConstructionVertex != null)
            {
                if (TryBuildOutpostOnce(possibleConstructionVertex, withGrind: true))
                    didSomething = true;
                return didSomething;
            }

            bool buildableRoadFound = false;
            var candidates = GetProspectiveVertices();
            if (candidates.Count > 0)
            {
                var networkVertices = new HashSet<Vertex>(_civ.Cities
                    .Select(c => c.Position)
                    .Where(v => candidates.Any(candidate => candidate.Z == v.Z)));
                foreach (var road in _civ.Roads)
                    foreach (var v in road.Position.GetVertices())
                        if (candidates.Any(candidate => candidate.Z == v.Z))
                            networkVertices.Add(v);

                Vertex? bestTarget = null;
                Vertex? bestFrom = null;
                int bestDist = int.MaxValue;
                foreach (var candidate in candidates)
                {
                    foreach (var nv in networkVertices)
                    {
                        if (nv.Z != candidate.Z) continue;
                        int dist = nv.EdgeDistanceTo(candidate);
                        if (dist < bestDist)
                        {
                            bestDist = dist;
                            bestTarget = candidate;
                            bestFrom = nv;
                        }
                    }
                }

                if (bestTarget != null && bestFrom != null)
                {
                    var buildableRoads = _roadController.GetBuildableRoads(_civ.Index);
                    var path = HexGridPathfinder.FindVertexPath(bestFrom, bestTarget);
                    var shared = path[0].GetHexes().Intersect(path[1].GetHexes()).ToArray();
                    Debug.Assert(shared.Length == 2);
                    var edge = Edge.Create(shared[0], shared[1]);
                    if (buildableRoads.Any(r => r.Position.Equals(edge)))
                    {
                        buildableRoadFound = true;
                        if (TryBuildRoadOnce(edge, withGrind: true))
                            didSomething = true;
                    }
                }
            }

            if (!buildableRoadFound)
            {
                var nextRoad = _roadController.GetBuildableRoads(_civ.Index)
                    .OrderByDescending(r => r.DistanceToNearestCity)
                    .FirstOrDefault();
                if (nextRoad != null && TryBuildRoadOnce(nextRoad.Position, withGrind: true))
                    didSomething = true;
            }

            return didSomething;
        }

        /// <summary>Step militaire : construit Palissade et Caserne dans les villes proches d'une civilisation ennemie (&lt; 5 edges).</summary>
        public bool TryMilitaryStepOnce()
        {
            if (_worldState == null) return false;

            bool didSomething = false;
            TryGrindOnce(null);

            foreach (var city in _civ.Cities.ToList())
            {
                foreach (var bt in MilitaryBuildings)
                {
                    if (TryBuildBuildingOnce(city, bt, withGrind: false))
                        didSomething = true;
                }
            }

            return didSomething;
        }

        /// <summary>
        /// Attempts to build a unique building in the specified city.
        /// Uses GetUniqueBuildingsAndBuildables to check availability.
        /// </summary>
        public bool TryBuildUniqueBuildingOnce(City city, BuildingType buildingType, bool withGrind = true)
        {
            if (city == null) throw new ArgumentNullException(nameof(city));

            var buildables = _buildingController.GetUniqueBuildingsAndBuildables(city);
            var target = buildables.FirstOrDefault(b => b.Type == buildingType && b.Level == 0);
            if (target == null) return false;

            if (_buildingController.BuildBuilding(city, buildingType))
                return true;

            if (withGrind)
                TryGrindOnce(target.GetBuildCost());

            return false;
        }

        /// <summary>
        /// Attempts one trade step to accumulate <paramref name="target"/>.
        /// For Gold: sells the most abundant basic resource.
        /// For basic/advanced resources: buys with gold if available, otherwise sells first.
        /// </summary>
        public bool TryTradeForResourceOnce(Resource target)
        {
            if (!_tradeController.IsTradeAvailable(_civ.Index)) return false;

            if (!ResourceUtils.BasicResources.Contains(target) && target != Resource.Gold)
            {
                if (_tradeController.CanBuyResource(_civ.Index, target))
                {
                    _tradeController.BuyResource(_civ.Index, target);
                    return true;
                }
                return TryTradeForResourceOnce(Resource.Gold);
            }

            if (target == Resource.Gold)
            {
                if (!_tradeController.CanRecieveTrade(_civ, Resource.Gold)) return false;
                Resource? bestSource = null;
                int bestQty = 0;
                foreach (var r in ResourceUtils.BasicResources)
                {
                    var rate = _tradeController.GetSellRate(_civ.Index, r);
                    var qty = _civ.GetResourceQuantity(r);
                    if (qty >= rate && qty > bestQty)
                    {
                        bestSource = r;
                        bestQty = qty;
                    }
                }
                if (bestSource == null) return false;
                return _tradeController.SellResource(_civ.Index, bestSource.Value);
            }

            // Basic resource target: buy with gold or accumulate gold first
            if (_tradeController.CanBuyResource(_civ.Index, target))
            {
                _tradeController.BuyResource(_civ.Index, target);
                return true;
            }
            return TryTradeForResourceOnce(Resource.Gold);
        }

        /// <summary>
        /// Performs the prestige transition and greedily distributes all available prestige points.
        /// <paramref name="priorityVertices"/>, if given, are purchased first (in order, still subject to
        /// the normal cost/adjacency rules) before the remaining balance is spent on the cheapest
        /// reachable vertices — useful to deterministically unlock a specific building.
        /// Returns false if prestige is not yet available or performPrestige was not provided.
        /// The autoplayer's civ/map references become stale after this call — do not reuse them.
        /// </summary>
        public bool TryPrestigeOnce(IReadOnlyList<Vertex>? priorityVertices = null)
        {
            if (_performPrestige == null || !_prestigeController.PrestigeIsAvailable()) return false;

            _performPrestige();

            if (_prestigeState != null)
            {
                if (priorityVertices != null)
                    foreach (var vertex in priorityVertices)
                        _prestigeMapController.PurchaseVertex(_prestigeState, vertex);

                bool purchased;
                do
                {
                    purchased = false;
                    foreach (var vertex in PrestigeMapController.DefaultMap.Vertices.OrderBy(v => v.Cost))
                    {
                        if (_prestigeMapController.PurchaseVertex(_prestigeState, vertex.Coord))
                        {
                            purchased = true;
                            break;
                        }
                    }
                }
                while (purchased);
            }

            return true;
        }

        /// <summary>
        /// Starts the cheapest available research if none is active, and queues the next cheapest
        /// if the research queue prestige perk is unlocked. No-ops when research is not unlocked.
        /// </summary>
        public bool TryResearchOnce()
        {
            if (!_researchController.IsResearchUnlocked()) return false;

            bool didSomething = false;

            bool isAnyInProgress = TechnologyDefinitions.All
                .Any(t => _researchController.GetStatus(t.Id) == TechnologyStatus.InProgress);

            if (!isAnyInProgress)
            {
                var next = TechnologyDefinitions.All
                    .Where(t => _researchController.GetStatus(t.Id) == TechnologyStatus.Available)
                    .OrderBy(t => t.Cost)
                    .FirstOrDefault();
                if (next != null && _researchController.StartResearch(next.Id))
                    didSomething = true;
            }

            if (_researchController.IsResearchQueueUnlocked() && _researchController.GetQueuedResearch() == null)
            {
                var queued = TechnologyDefinitions.All
                    .Where(t => _researchController.CanBeQueued(t.Id))
                    .OrderBy(t => t.Cost)
                    .FirstOrDefault();
                if (queued != null && _researchController.SetQueuedResearch(queued.Id))
                    didSomething = true;
            }

            return didSomething;
        }

        private bool TryStepOnce(BuildingType[] targetBuildings, bool shouldExpand)
        {
            bool didSomething = false;
            bool hasGrindedThisStep = false;

            if (TryResearchOnce())
                didSomething = true;

            Vertex? possibleConstructionVertex = null;

            if (shouldExpand)
            {
                // Try outpost if a buildable vertex is accessible
                possibleConstructionVertex = _cityBuilderController.GetBuildableVertices(_civ.Index).FirstOrDefault();
                if (possibleConstructionVertex != null)
                {
                    if (TryBuildOutpostOnce(possibleConstructionVertex, withGrind: !hasGrindedThisStep))
                        didSomething = true;
                    hasGrindedThisStep = true;
                }

            }

            // Build production/support buildings (no per-building grind to avoid trade interference)
            foreach (var city in _civ.Cities.ToList())
            {
                foreach (var bt in targetBuildings)
                {
                    var shouldGrind = !hasGrindedThisStep && !shouldExpand;
                    if (TryBuildBuildingOnce(city, bt, withGrind: shouldGrind))
                        didSomething = true;
                    if (shouldGrind)
                        hasGrindedThisStep = true;
                }
            }

            if (shouldExpand && (possibleConstructionVertex == null)) // in case we don't have possible outpost vertex, build more roads !
            {
                bool buildableRoadFound = false;
                var candidates = GetProspectiveVertices();
                if (candidates.Count > 0)
                {
                    var networkVertices = new HashSet<Vertex>(_civ.Cities
                        .Select(c => c.Position)
                        .Where(v => candidates.Any(candidate => candidate.Z == v.Z)));
                    foreach (var road in _civ.Roads)
                        foreach (var v in road.Position.GetVertices())
                            if (candidates.Any(candidate => candidate.Z == v.Z))
                                networkVertices.Add(v);

                    Vertex? bestTarget = null;
                    Vertex? bestFrom = null;
                    int bestDist = int.MaxValue;
                    foreach (var candidate in candidates)
                    {
                        foreach (var nv in networkVertices)
                        {
                            if (nv.Z != candidate.Z) continue;
                            int dist = nv.EdgeDistanceTo(candidate);
                            if (dist < bestDist)
                            {
                                bestDist = dist;
                                bestTarget = candidate;
                                bestFrom = nv;
                            }
                        }
                    }

                    if (bestTarget != null && bestFrom != null)
                    {
                        var buildableRoads = _roadController.GetBuildableRoads(_civ.Index);
                        var path = HexGridPathfinder.FindVertexPath(bestFrom, bestTarget);
                        var shared = path[0].GetHexes().Intersect(path[1].GetHexes()).ToArray();
                        Debug.Assert(shared.Length == 2);
                        var edge = Edge.Create(shared[0], shared[1]);
                        if (buildableRoads.Any(r => r.Position.Equals(edge))) // can fail if the path needs martime road
                        {
                            if (TryBuildRoadOnce(edge, withGrind: !hasGrindedThisStep))
                            {
                                didSomething = true;
                            }
                            buildableRoadFound = true;
                            hasGrindedThisStep = true;
                        }
                    }
                }

                if (!buildableRoadFound)
                {
                    // Fallback: push frontier outward when no prospective vertex is reachable
                    var nextRoad = _roadController.GetBuildableRoads(_civ.Index)
                        .OrderByDescending(r => r.DistanceToNearestCity)
                        .FirstOrDefault();
                    if (nextRoad != null && TryBuildRoadOnce(nextRoad.Position, withGrind: !hasGrindedThisStep))
                        didSomething = true;
                    hasGrindedThisStep = true;
                }
            }

            Debug.Assert(hasGrindedThisStep);

            return didSomething;
        }

        /// <summary>
        /// Returns visible vertices that are not yet in our road network and respect city-distance
        /// constraints — good candidates for a future outpost.
        /// </summary>
        private List<Vertex> GetProspectiveVertices()
        {
            var worldState = _worldState;
            if (worldState == null || !worldState.Visibility.GetForZ(worldState.CurrentViewedLayer).TryGetValue(_civ.Index, out var visibleMap))
                return new List<Vertex>();

            int z = visibleMap.Z;
            var visibleVertices = new HashSet<Vertex>();
            foreach (var hex in visibleMap.Tiles.Keys)
                foreach (var dir in SecondaryHexDirectionUtils.AllSecondaryDirections)
                    visibleVertices.Add(hex.Vertex(dir));

            var networkVertices = new HashSet<Vertex>(_civ.Cities
                .Select(c => c.Position)
                .Where(v => v.Z == z));
            foreach (var road in _civ.Roads)
                foreach (var v in road.Position.GetVertices())
                    if (v.Z == z)
                        networkVertices.Add(v);

            var visibleEnemyCities = worldState.Civilizations
                .Where(c => c.Index != _civ.Index)
                .SelectMany(c => c.Cities)
                .Where(city => city.Position.Z == z)
                .Where(city => city.Position.GetHexes().Any(h => visibleMap.GetTile(h) != null))
                .Select(city => city.Position)
                .ToList();

            int minOwn = _cityBuilderController.MinDistanceBetweenCivilizationCities;
            int minEnemy = _cityBuilderController.MinDistanceBetweenCities;

            return visibleVertices
                .Where(v => !networkVertices.Contains(v))
                .Where(v => v.GetHexes().Any(h => visibleMap.GetTile(h) is { TerrainType: not TerrainType.Water }))
                .Where(v => _civ.Cities.Where(c => c.Position.Z == v.Z).All(c => c.Position.EdgeDistanceTo(v) >= minOwn))
                .Where(v => visibleEnemyCities.All(ec => ec.EdgeDistanceTo(v) >= minEnemy))
                .ToList();
        }
    }
}
