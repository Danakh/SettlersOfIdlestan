using System;
using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Controller.Expand;
using SettlersOfIdlestan.Controller.Island;

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
        private readonly MainGameController _mainController;

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
            Step2Buildings.Concat(new[] { BuildingType.Library, BuildingType.Temple }).ToArray();

        // When prestige points are sufficient but ImperialPort is not yet built, focus on
        // the buildings that push city level toward 4 (required for ImperialPort).
        private static readonly BuildingType[] Step3PortFocusBuildings =
        {
            BuildingType.TownHall, BuildingType.Seaport, BuildingType.Warehouse,
        };

        private static readonly Resource[] Step3TradeTargets = { Resource.Gold };

        private static readonly BuildingType[] MilitaryBuildings = { BuildingType.Palisade, BuildingType.Barracks };
        public const int MilitaryThreatEdges = 5;

        public CivilizationAutoplayer(Civilization civ, IslandMap map, MainGameController mainController)
        {
            _civ = civ ?? throw new ArgumentNullException(nameof(civ));
            _map = map ?? throw new ArgumentNullException(nameof(map));
            _mainController = mainController ?? throw new ArgumentNullException(nameof(mainController));

            _roadController = mainController.RoadController;
            _harvestController = mainController.HarvestController;
            _cityBuilderController = mainController.CityBuilderController;
            _buildingController = mainController.BuildingController;
            _tradeController = mainController.TradeController;
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
                try { _tradeController.TryAutoTradeForPurchase(_civ.Index, requiredResources); }
                catch { }
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
        public bool TryBuildBuildingOnce(Vertex cityVertex, BuildingType buildingType, bool withGrind = true)
        {
            if (cityVertex == null) throw new ArgumentNullException(nameof(cityVertex));

            var buildables = _buildingController.GetBuildingsAndBuildables(_civ.Index, cityVertex);
            var target = buildables.FirstOrDefault(b => b.Type == buildingType);
            if (target == null) return false;

            // Skip already-maxed buildings
            if (target.Level > 0 && target.Level >= _buildingController.GetMaxLevel(target, _civ.Index))
                return false;

            if (_buildingController.BuildBuilding(_civ.Index, cityVertex, buildingType))
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
            var buildings = _mainController.ResearchController.IsResearchUnlocked()
                ? Step2WithLibraryBuildings
                : Step2Buildings;
            return TryStepOnce(buildings, shouldExpand);
        }

        /// <summary>Step 3: step 2 + victory-point buildings (Library, Temple, TownHall upgrades) + unique buildings.
        /// When prestige points are already sufficient but ImperialPort is missing, switches to a focused
        /// building list (TownHall, Seaport, Warehouse) to reach city level 4 faster.</summary>
        public bool TryStep3Once(bool shouldExpand = true)
        {
            var prestigeCtrl = _mainController.PrestigeController;
            bool readyForPort = prestigeCtrl.CalculatePrestigePoints() >= PrestigeController.PrestigeRequiredPoints
                                && !prestigeCtrl.HasImperialPort();

            var buildings = readyForPort ? Step3PortFocusBuildings : Step3Buildings;
            bool didSomething = TryStepOnce(buildings, shouldExpand, Step3TradeTargets);

            foreach (var city in _civ.Cities.ToList().Where(c => c.Level >= 4))
            {
                try
                {
                    if (TryBuildUniqueBuildingOnce(city.Position, BuildingType.ImperialPort, withGrind: true))
                        didSomething = true;
                }
                catch { }
            }

            return didSomething;
        }

        /// <summary>Step militaire : construit Palissade et Caserne dans les villes proches d'une civilisation ennemie (&lt; 5 edges).</summary>
        public bool TryMilitaryStepOnce()
        {
            var islandState = _mainController.CurrentMainState?.CurrentIslandState;
            if (islandState == null) return false;

            bool didSomething = false;
            TryGrindOnce(null);

            foreach (var city in _civ.Cities.ToList())
            {
                var nearestEnemy = _mainController.MilitaryController
                    .FindNearestEnemyCityForDefense(city, _civ, islandState, MilitaryThreatEdges);
                if (nearestEnemy == null) continue;

                foreach (var bt in MilitaryBuildings)
                {
                    try { if (TryBuildBuildingOnce(city.Position, bt, withGrind: false)) didSomething = true; }
                    catch { }
                }
            }

            return didSomething;
        }

        /// <summary>
        /// Attempts to build a unique building in the specified city.
        /// Uses GetUniqueBuildingsAndBuildables to check availability.
        /// </summary>
        public bool TryBuildUniqueBuildingOnce(Vertex cityVertex, BuildingType buildingType, bool withGrind = true)
        {
            if (cityVertex == null) throw new ArgumentNullException(nameof(cityVertex));

            var buildables = _buildingController.GetUniqueBuildingsAndBuildables(_civ.Index, cityVertex);
            var target = buildables.FirstOrDefault(b => b.Type == buildingType && b.Level == 0);
            if (target == null) return false;

            if (_buildingController.BuildBuilding(_civ.Index, cityVertex, buildingType))
                return true;

            if (withGrind)
                TryGrindOnce(target.GetBuildCost());

            return false;
        }

        /// <summary>
        /// Attempts to trade the most abundant surplus basic resource for one unit (or pack) of <paramref name="target"/>.
        /// Uses TradeMultiForSingle so that non-basic targets such as Gold are supported.
        /// </summary>
        public bool TryTradeForResourceOnce(Resource target)
        {
            if (!_tradeController.IsTradeAvailable(_civ.Index)) return false;

            var receiveQty = _tradeController.ReceiveRate(target);
            if (!_tradeController.CanRecieveTrade(_civ, target, receiveQty)) return false;

            Resource? bestSource = null;
            int bestQty = 0;
            foreach (var r in ResourceUtils.BasicResources)
            {
                if (r == target) continue;
                var rate = _tradeController.TradeRate(_civ.Index, r);
                var qty = _civ.GetResourceQuantity(r);
                if (qty >= rate && qty > bestQty)
                {
                    bestSource = r;
                    bestQty = qty;
                }
            }

            if (bestSource == null) return false;

            return _tradeController.TradeMultiForSingle(
                _civ.Index,
                new Dictionary<Resource, int> { [bestSource.Value] = _tradeController.TradeRate(_civ.Index, bestSource.Value) },
                target,
                receiveQty);
        }

        /// <summary>
        /// Performs the prestige transition and greedily distributes all available prestige points.
        /// Returns false if prestige is not yet available.
        /// The autoplayer's civ/map references become stale after this call — do not reuse them.
        /// </summary>
        public bool TryPrestigeOnce()
        {
            if (!_mainController.PrestigeController.PrestigeIsAvailable()) return false;

            _mainController.PerformPrestige();

            var prestigeState = _mainController.CurrentMainState?.PrestigeState;
            if (prestigeState != null)
            {
                var ctrl = _mainController.PrestigeMapController;
                bool purchased;
                do
                {
                    purchased = false;
                    foreach (var vertex in PrestigeMapController.DefaultMap.Vertices.OrderBy(v => v.Cost))
                    {
                        if (ctrl.PurchaseVertex(prestigeState, vertex.Coord))
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
            var researchCtrl = _mainController.ResearchController;
            if (!researchCtrl.IsResearchUnlocked()) return false;

            bool didSomething = false;

            bool isAnyInProgress = TechnologyDefinitions.All
                .Any(t => researchCtrl.GetStatus(t.Id) == TechnologyStatus.InProgress);

            if (!isAnyInProgress)
            {
                var next = TechnologyDefinitions.All
                    .Where(t => researchCtrl.GetStatus(t.Id) == TechnologyStatus.Available)
                    .OrderBy(t => t.Cost)
                    .FirstOrDefault();
                if (next != null && researchCtrl.StartResearch(next.Id))
                    didSomething = true;
            }

            if (researchCtrl.IsResearchQueueUnlocked() && researchCtrl.GetQueuedResearch() == null)
            {
                var queued = TechnologyDefinitions.All
                    .Where(t => researchCtrl.CanBeQueued(t.Id))
                    .OrderBy(t => t.Cost)
                    .FirstOrDefault();
                if (queued != null && researchCtrl.SetQueuedResearch(queued.Id))
                    didSomething = true;
            }

            return didSomething;
        }

        private bool TryStepOnce(BuildingType[] targetBuildings, bool shouldExpand, Resource[]? tradeTargets = null)
        {
            bool didSomething = false;

            TryGrindOnce(null);

            try { if (TryResearchOnce()) didSomething = true; } catch { }

            if (tradeTargets != null)
            {
                foreach (var target in tradeTargets)
                {
                    try { if (TryTradeForResourceOnce(target)) didSomething = true; }
                    catch { }
                }
            }

            if (shouldExpand)
            {
                // Try outpost if a buildable vertex is accessible
                try
                {
                    var newVert = _cityBuilderController.GetBuildableVertices(_civ.Index).FirstOrDefault();
                    if (newVert != null && TryBuildOutpostOnce(newVert, withGrind: false)) didSomething = true;
                }
                catch { }
            }

            // Build production/support buildings (no per-building grind to avoid trade interference)
            foreach (var city in _civ.Cities.ToList())
            {
                foreach (var bt in targetBuildings)
                {
                    try { if (TryBuildBuildingOnce(city.Position, bt, withGrind: false)) didSomething = true; }
                    catch { }
                }
            }

            if (shouldExpand)
            {
                // Expand road network: target distance 3 first (unlocks new city slots), fall back to nearest
                try
                {
                    var d3 = _roadController.GetBuildableRoadsAtDistance(_civ.Index, 3);
                    var nextRoad = (d3 != null && d3.Any())
                        ? d3[0]
                        : _roadController.GetBuildableRoads(_civ.Index).OrderBy(r => r.DistanceToNearestCity).FirstOrDefault();
                    if (nextRoad != null && TryBuildRoadOnce(nextRoad.Position, withGrind: false)) didSomething = true;
                }
                catch { }
            }

            return didSomething;
        }
    }
}
