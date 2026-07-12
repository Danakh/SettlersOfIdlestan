using System;
using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestan.Controller.Island
{
    /// <summary>
    /// Contrôle la construction des Flottes de Guerre (<see cref="WarFleet"/>) : des emplacements
    /// militaires sans bâtiment, construits sur une Balise Maritime existante (voir
    /// MaritimeBeaconController), débloqués par le Port Impérial. Une flotte a une défense et une
    /// capacité de soldats fixes et reste une cible normale de renfort et d'attaque via
    /// <see cref="IMilitaryVertex"/> — voir MilitaryController.
    /// </summary>
    public class WarFleetController
    {
        private WorldState? _state;

        internal WarFleetController() { }

        internal void Initialize(WorldState state)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
        }

        public static ResourceSet GetBuildCost() => new()
        {
            { Resource.Wood, 200 },
            { Resource.Ore, 100 },
            { Resource.Food, 200 },
            { Resource.Gold, 200 },
        };

        /// <summary>Débloqué par le Port Impérial, construit dans n'importe quelle ville de la civilisation.</summary>
        public bool IsWarFleetUnlocked(Civilization civ) => civ.GetUniqueBuilding(BuildingType.ImperialPort) != null;

        /// <summary>
        /// Retourne les vertex où la civilisation pourrait construire une flotte — un de ses propres balises
        /// maritimes, non encore occupée par une flotte — indépendamment du Port Impérial. Une ville ne peut
        /// jamais se trouver sur une balise (CityBuilderController et MaritimeBeaconController s'excluent
        /// mutuellement), donc seules les flottes sont à exclure ici.
        /// Sert à afficher l'emplacement au survol même sans le prérequis, pour informer le joueur via une
        /// infobulle (voir GetMissingPrerequisiteKey-style pattern) plutôt que de le cacher silencieusement.
        /// </summary>
        public List<Vertex> GetPotentialVertices(int civilizationIndex)
        {
            if (_state == null) throw new InvalidOperationException("WorldState has not been initialized.");

            var civ = _state.Civilizations.FirstOrDefault(c => c.Index == civilizationIndex)
                      ?? throw new ArgumentException("Civilization not found", nameof(civilizationIndex));

            var occupied = new HashSet<Vertex>(_state.GetAllFleets().Select(f => f.Position));
            return civ.MaritimeBeacons
                .Select(b => b.Position)
                .Where(v => !occupied.Contains(v))
                .ToList();
        }

        /// <summary>Sous-ensemble de <see cref="GetPotentialVertices"/> réellement constructible (Port Impérial requis).</summary>
        public List<Vertex> GetBuildableVertices(int civilizationIndex)
        {
            if (_state == null) throw new InvalidOperationException("WorldState has not been initialized.");

            var civ = _state.Civilizations.FirstOrDefault(c => c.Index == civilizationIndex)
                      ?? throw new ArgumentException("Civilization not found", nameof(civilizationIndex));

            if (!IsWarFleetUnlocked(civ)) return new List<Vertex>();

            return GetPotentialVertices(civilizationIndex);
        }

        /// <summary>
        /// Construit une flotte de guerre pour la civilisation sur une balise maritime lui appartenant.
        /// Retourne null si le Port Impérial n'est pas construit ou si les ressources sont insuffisantes.
        /// Lance une exception si le vertex n'est pas un emplacement potentiel (bug appelant).
        /// </summary>
        public WarFleet? BuildWarFleet(int civilizationIndex, Vertex vertex)
        {
            if (_state == null) throw new InvalidOperationException("WorldState has not been initialized.");
            if (vertex == null) throw new ArgumentNullException(nameof(vertex));

            var civ = _state.Civilizations.FirstOrDefault(c => c.Index == civilizationIndex)
                      ?? throw new ArgumentException("Civilization not found", nameof(civilizationIndex));

            if (!GetPotentialVertices(civilizationIndex).Any(v => v.Equals(vertex)))
                throw new InvalidOperationException("Vertex not buildable by this civilization");

            if (!IsWarFleetUnlocked(civ))
                return null;

            var cost = GetBuildCost();
            if (!civ.CanPayResourceCost(cost))
                return null;

            civ.PayResourceCost(cost);

            var fleet = new WarFleet(vertex) { CivilizationIndex = civilizationIndex };
            civ.AddFleet(fleet);
            _state.Visibility.RecalculateFor(civilizationIndex);
            return fleet;
        }

        /// <summary>
        /// Retire une flotte détruite au combat. Point d'entrée unique de suppression, à l'image de
        /// <see cref="CityBuilderController.DestroyCity"/>, appelé par CityAttackEngine.
        /// </summary>
        public void DestroyFleet(WarFleet fleet)
        {
            if (_state == null) throw new InvalidOperationException("WorldState has not been initialized.");
            if (fleet == null) throw new ArgumentNullException(nameof(fleet));

            var civ = _state.Civilizations.FirstOrDefault(c => c.Index == fleet.CivilizationIndex)
                      ?? throw new ArgumentException("Fleet's civilization not found", nameof(fleet));

            fleet.RaiseDestroyed();
            civ.RemoveFleet(fleet);
            _state.Visibility.Recalculate();
        }
    }
}
