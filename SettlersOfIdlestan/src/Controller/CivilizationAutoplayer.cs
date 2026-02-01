using System;
using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.HexGrid;

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

        public CivilizationAutoplayer(Civilization civ, IslandMap map, RoadController roadController, HarvestController harvestController)
        {
            _civ = civ ?? throw new ArgumentNullException(nameof(civ));
            _map = map ?? throw new ArgumentNullException(nameof(map));
            _roadController = roadController ?? throw new ArgumentNullException(nameof(roadController));
            _harvestController = harvestController ?? throw new ArgumentNullException(nameof(harvestController));
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
                        // Ignorer toute erreur locale lors de l'obtention des hex d'une ville
                    }
                }

                foreach (var hex in toHarvest)
                {
                    try
                    {
                        // Lancer la récolte et ignorer le résultat (cooldown ou échec possible)
                        _harvestController.ManualHarvest(_civ.Index, hex);
                    }
                    catch
                    {
                        // ignorer les exceptions individuelles
                    }
                }

                return false;
            }
        }
    }
}
