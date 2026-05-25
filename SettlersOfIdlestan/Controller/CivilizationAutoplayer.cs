using System;
using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.Buildings;
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
            BuildingType.Sawmill, BuildingType.Brickworks, BuildingType.Mine, BuildingType.Mill,
        };

        private static readonly BuildingType[] Step2Buildings =
            Step1Buildings.Concat(new[] { BuildingType.Warehouse, BuildingType.Forge }).ToArray();

        private static readonly BuildingType[] Step3Buildings =
            Step2Buildings.Concat(new[] { BuildingType.Library, BuildingType.Temple }).ToArray();

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

        public void TryGrindOnce(ResourceCost? requiredResources)
        {
            var toHarvest = new HashSet<HexCoord>();
            foreach (var city in _civ.Cities)
            {
                foreach (var h in city.Position.GetHexes())
                    if (h != null) toHarvest.Add(h);
            }

            foreach (var hex in toHarvest)
            {
                try { _harvestController.ManualHarvest(_civ.Index, hex); }
                catch { }
            }

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

            try
            {
                _roadController.BuildRoad(_civ.Index, edge);
                return true;
            }
            catch (InvalidOperationException)
            {
                if (withGrind)
                {
                    try
                    {
                        var road = _roadController.GetBuildableRoads(_civ.Index).FirstOrDefault(r => r.Position.Equals(edge));
                        if (road != null) TryGrindOnce(_roadController.GetRoadCost(road.DistanceToNearestCity));
                    }
                    catch { }
                }
                return false;
            }
        }

        public bool TryBuildOutpostOnce(Vertex vertex, bool withGrind = true)
        {
            if (vertex == null) throw new ArgumentNullException(nameof(vertex));

            if (!_cityBuilderController.GetBuildableVertices(_civ.Index).Any(v => v.Equals(vertex)))
                return false;

            try
            {
                _cityBuilderController.BuildCity(_civ.Index, vertex);
                return true;
            }
            catch (InvalidOperationException)
            {
                if (withGrind) TryGrindOnce(_cityBuilderController.NewCityBuildingCost());
                return false;
            }
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

        /// <summary>Step 2: step 1 + upgraded production, Warehouse and Forge.</summary>
        public bool TryStep2Once(bool shouldExpand = true) => TryStepOnce(Step2Buildings, shouldExpand);

        /// <summary>Step 3: step 2 + victory-point buildings (Library, Temple, TownHall upgrades).</summary>
        public bool TryStep3Once(bool shouldExpand = true) => TryStepOnce(Step3Buildings, shouldExpand);

        private bool TryStepOnce(BuildingType[] targetBuildings, bool shouldExpand)
        {
            bool didSomething = false;

            TryGrindOnce(null);

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
