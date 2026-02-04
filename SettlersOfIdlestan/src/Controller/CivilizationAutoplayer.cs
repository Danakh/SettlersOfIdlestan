using System;
using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.Buildings;

namespace SettlersOfIdlestan.Controller
{
    /// <summary>
    /// Autoplayer basique pour une civilisation : tente de construire une route
    /// puis, si la construction est impossible pour des raisons de ressources,
    /// récolte manuellement tous les hexagones autour des villes de la civilisation.
    /// L'autoplayer échoue immédiatement si l'arête n'est pas constructible par la civilisation.
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

        // Optional reference to a MainGameController so the autoplayer can use the
        // controllers exposed by it. When provided the other controller parameters
        // passed to the older constructor are ignored.
        private readonly MainGameController? _mainController;

        /// <summary>
        /// Constructor that accepts a MainGameController and uses its exposed sub-controllers.
        /// </summary>
        public CivilizationAutoplayer(Civilization civ, IslandMap map, MainGameController mainController)
        {
            _civ = civ ?? throw new ArgumentNullException(nameof(civ));
            _map = map ?? throw new ArgumentNullException(nameof(map));
            _mainController = mainController ?? throw new ArgumentNullException(nameof(mainController));

            // Use controllers from the main controller so the autoplayer operates on the same state
            _roadController = mainController.RoadController;
            _harvestController = mainController.HarvestController;
            _cityBuilderController = mainController.CityBuilderController;
            _buildingController = mainController.BuildingController;
            _tradeController = mainController.TradeController;
        }

        public void AutoGrind(Dictionary<Resource, int>? requiredResources)
        {
            // Récolter manuellement tous les hexagones autour des villes de la civilisation.
            var toHarvest = new HashSet<HexCoord>();
            foreach (var city in _civ.Cities)
            {
                try
                {
                    var hexes = city.Position.GetHexes();
                    foreach (var h in hexes)
                    {
                        if (h != null)
                            toHarvest.Add(h);
                    }
                }
                catch
                {
                    // ignore local errors when retrieving city hexes
                }
            }

            foreach (var hex in toHarvest)
            {
                try
                {
                    _harvestController.ManualHarvest(_civ.Index, hex);
                }
                catch
                {
                    // ignore individual harvest failures
                }
            }

            // If a required resource set was provided, attempt a single trade to fulfill it
            if (requiredResources != null && requiredResources.Any())
            {
                try
                {
                    _tradeController.TryAutoTradeForPurchase(_civ.Index, requiredResources);
                }
                catch
                {
                    // ignore trade failures
                }
            }
        }

        /// <summary>
        /// Tente de construire une route sur l'arête spécifiée. Si l'arête n'est pas
        /// constructible par la civilisation, la méthode échoue (retourne false).
        /// Si l'arête est constructible mais que la construction échoue (par ex. pas
        /// assez de ressources), alors l'autoplayer effectue une récolte manuelle de
        /// tous les hexagones autour des villes de la civilisation et retourne false.
        /// Retourne true si la route a été construite avec succès.
        /// </summary>
        public bool AutoBuildRoad(Edge edge)
        {
            if (edge == null) throw new ArgumentNullException(nameof(edge));

            // Vérifier que l'arête est listée comme constructible par le controller
            var buildableEdges = _roadController.GetBuildableRoads(_civ.Index).Select(r => r.Position);
            var isConstructible = buildableEdges.Any(e => e.Equals(edge));
            if (!isConstructible)
            {
                // échoue si pas constructible
                return false;
            }

            try
            {
                _roadController.BuildRoad(_civ.Index, edge);
                return true;
            }
            catch (InvalidOperationException)
            {
                // Construction impossible (probablement pas assez de ressources).
                // Effectuer un grind automatique (récolte + tentative de trade) pour obtenir les ressources
                try
                {
                    // Determine cost for this road using the Road entry returned by controller
                    var buildableRoads = _roadController.GetBuildableRoads(_civ.Index);
                    var road = buildableRoads.FirstOrDefault(r => r.Position.Equals(edge));
                    if (road != null)
                    {
                        var distance = road.DistanceToNearestCity;
                        if (distance != int.MaxValue)
                        {
                            var cost = 2 * (distance * distance);
                            var required = new Dictionary<Resource, int>
                            {
                                { Resource.Wood, cost },
                                { Resource.Brick, cost }
                            };
                            AutoGrind(required);
                        }
                        else
                        {
                            // no determinable cost - still attempt basic grind (harvest only)
                            AutoGrind(null);
                        }
                    }
                    else
                    {
                        // no matching road found - just harvest
                        AutoGrind(null);
                    }
                }
                catch
                {
                    // ignore trade/grind failures
                }

                return false;
            }
        }

        /// <summary>
        /// Tente de construire une ville (outpost) sur le vertex spécifié. Si la construction est impossible
        /// pour des raisons de ressources, alors l'autoplayer effectue une récolte manuelle de tous les
        /// hexagones autour des villes de la civilisation et retourne false. Retourne true si la ville a
        /// été construite avec succès.
        /// </summary>
        public bool AutoBuildOutpost(Vertex vertex)
        {
            if (vertex == null) throw new ArgumentNullException(nameof(vertex));

            var buildable = _cityBuilderController.GetBuildableVertices(_civ.Index);
            var isConstructible = buildable.Any(v => v.Equals(vertex));
            if (!isConstructible)
            {
                return false;
            }

            try
            {
                _cityBuilderController.BuildCity(_civ.Index, vertex);
                return true;
            }
            catch (InvalidOperationException)
            {
                // Not enough resources: attempt grind (harvest + one trade attempt)
                try
                {
                    var required = new Dictionary<Resource, int>
                    {
                        { Resource.Brick, 10 },
                        { Resource.Wood, 10 },
                        { Resource.Wheat, 10 },
                        { Resource.Sheep, 10 }
                    };
                    AutoGrind(required);
                }
                catch
                {
                    // ignore
                }

                return false;
            }
        }

        /// <summary>
        /// Tente de construire ou améliorer un bâtiment dans la ville spécifiée.
        /// Si pas constructible retourne false. Si pas assez de ressources, effectue des récoltes autour des villes puis retourne false.
        /// Retourne true si la construction/rénovation a réussi.
        /// </summary>
        public bool AutoBuildBuilding(Vertex cityVertex, BuildingType buildingType)
        {
            if (cityVertex == null) throw new ArgumentNullException(nameof(cityVertex));

            var buildable = _buildingController.GetBuildableBuildings(_civ.Index, cityVertex);
            var isConstructible = buildable.Any(b => b.Type == buildingType);
            if (!isConstructible) return false;

            try
            {
                _buildingController.BuildBuilding(_civ.Index, cityVertex, buildingType);
                return true;
            }
            catch (InvalidOperationException)
            {
                var toHarvest = new HashSet<HexCoord>();
                foreach (var city in _civ.Cities)
                {
                    try
                    {
                        var hexes = city.Position.GetHexes();
                        foreach (var h in hexes)
                        {
                            if (h != null)
                                toHarvest.Add(h);
                        }
                    }
                    catch
                    {
                    }
                }

                foreach (var hex in toHarvest)
                {
                    try
                    {
                        _harvestController.ManualHarvest(_civ.Index, hex);
                    }
                    catch
                    {
                    }
                }

                // Après la récolte, tenter un trade unique qui pourrait aider la construction
                try
                {
                    // Attempt to get the buildable entry for this building to compute cost
                    var buildables = _buildingController.GetBuildableBuildings(_civ.Index, cityVertex);
                    var target = buildables.FirstOrDefault(b => b.Type == buildingType);
                    if (target != null)
                    {
                        Dictionary<Resource, int> required;
                        if (target.Level <= 1)
                        {
                            required = target.GetBuildCost();
                        }
                        else
                        {
                            required = target.GetUpgradeCost(target.Level);
                        }

                        if (required != null && required.Any())
                        {
                            _tradeController.TryAutoTradeForPurchase(_civ.Index, required);
                        }
                    }
                }
                catch
                {
                    // ignore trade errors
                }

                return false;
            }
        }
    }
}
