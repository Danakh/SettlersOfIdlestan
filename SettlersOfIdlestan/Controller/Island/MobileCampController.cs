using System;
using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestan.Controller.Island
{
    /// <summary>
    /// Contrôle la construction des Camps Mobiles (<see cref="MobileCamp"/>) : des emplacements
    /// militaires terrestres sans bâtiment, analogues aux Flottes de Guerre (voir WarFleetController)
    /// mais construits sur le réseau routier de la civilisation plutôt que sur une Balise Maritime,
    /// débloqués par la recherche MobileCampConstruction.
    /// Un Camp Mobile doit être à distance >= <see cref="MinDistanceBetweenMilitaryVertices"/> (arêtes)
    /// de tout autre <see cref="IMilitaryVertex"/> de la même civilisation ; aucune restriction
    /// vis-à-vis des civilisations adverses. Il n'est proposé à la construction que là où un
    /// avant-poste classique ne peut pas être bâti (voir CityBuilderController.GetBuildableVertices),
    /// et est détruit automatiquement dès qu'une ville de la même civilisation (alliée) est construite à
    /// distance &lt;= <see cref="CityProximityDestroyDistance"/> (voir DestroyCampsNear, appelé depuis
    /// MainGameController sur CityBuilderController.OnCityBuilt). Les villes ennemies n'affectent pas
    /// les camps mobiles.
    /// </summary>
    public class MobileCampController
    {
        private WorldState? _state;
        private CityBuilderController? _cityBuilderController;

        /// <summary>Distance minimale (arêtes) entre un Camp Mobile et tout autre emplacement militaire de la même civilisation.</summary>
        public const int MinDistanceBetweenMilitaryVertices = 2;

        /// <summary>Distance (arêtes) à laquelle la construction d'une ville détruit automatiquement un Camp Mobile voisin.</summary>
        public const int CityProximityDestroyDistance = 1;

        internal MobileCampController() { }

        internal void Initialize(WorldState state, CityBuilderController cityBuilderController)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _cityBuilderController = cityBuilderController ?? throw new ArgumentNullException(nameof(cityBuilderController));
        }

        /// <summary>Coût identique à celui d'une Flotte de Guerre, sauf que le bois est remplacé par pierre + brique.</summary>
        public static ResourceSet GetBuildCost() => new()
        {
            { Resource.Stone, 100 },
            { Resource.Brick, 100 },
            { Resource.Ore, 100 },
            { Resource.Food, 200 },
            { Resource.Gold, 200 },
        };

        public bool IsMobileCampUnlocked(Civilization civ)
            => civ.TechnologyTree.CompletedTechnologies.Contains(TechnologyId.MobileCampConstruction);

        /// <summary>
        /// Retourne les vertex où la civilisation pourrait construire un Camp Mobile, indépendamment de
        /// la recherche — sert à afficher l'emplacement au survol même sans le prérequis, pour informer
        /// le joueur via une infobulle plutôt que de le cacher silencieusement (même pattern que
        /// WarFleetController.GetPotentialVertices).
        /// Règles :
        /// - touche au moins une route de la civilisation
        /// - vertex non déjà occupé par un IBuildVertex
        /// - à distance >= MinDistanceBetweenMilitaryVertices de tout IMilitaryVertex de la même
        ///   civilisation (aucune restriction envers les civilisations adverses)
        /// - non constructible en tant qu'avant-poste (sinon c'est la ville qui est proposée à cet
        ///   emplacement — voir CityBuilderController.GetBuildableVertices)
        /// </summary>
        public List<Vertex> GetPotentialVertices(int civilizationIndex)
        {
            if (_state == null || _cityBuilderController == null)
                throw new InvalidOperationException("WorldState has not been initialized.");

            var civ = _state.Civilizations.FirstOrDefault(c => c.Index == civilizationIndex)
                      ?? throw new ArgumentException("Civilization not found", nameof(civilizationIndex));

            var candidates = _cityBuilderController.GetRoadTouchingVertices(civilizationIndex);
            var occupiedVertices = new HashSet<Vertex>(_state.GetAllBuildVertices().Select(v => v.Position));
            var cityBuildableVertices = new HashSet<Vertex>(_cityBuilderController.GetBuildableVertices(civilizationIndex));

            return candidates.Where(v =>
                !occupiedVertices.Contains(v) &&
                !cityBuildableVertices.Contains(v) &&
                !civ.MilitaryVertices.Any(mv => mv.Position.Z == v.Z && mv.Position.EdgeDistanceTo(v) < MinDistanceBetweenMilitaryVertices))
                .ToList();
        }

        /// <summary>Sous-ensemble de <see cref="GetPotentialVertices"/> réellement constructible (recherche MobileCampConstruction requise).</summary>
        public List<Vertex> GetBuildableVertices(int civilizationIndex)
        {
            if (_state == null) throw new InvalidOperationException("WorldState has not been initialized.");

            var civ = _state.Civilizations.FirstOrDefault(c => c.Index == civilizationIndex)
                      ?? throw new ArgumentException("Civilization not found", nameof(civilizationIndex));

            if (!IsMobileCampUnlocked(civ)) return new List<Vertex>();

            return GetPotentialVertices(civilizationIndex);
        }

        /// <summary>
        /// Construit un Camp Mobile pour la civilisation. Retourne null si la recherche
        /// MobileCampConstruction n'est pas complétée ou si les ressources sont insuffisantes.
        /// Lance une exception si le vertex n'est pas un emplacement potentiel (bug appelant).
        /// </summary>
        public MobileCamp? BuildMobileCamp(int civilizationIndex, Vertex vertex)
        {
            if (_state == null) throw new InvalidOperationException("WorldState has not been initialized.");
            if (vertex == null) throw new ArgumentNullException(nameof(vertex));

            var civ = _state.Civilizations.FirstOrDefault(c => c.Index == civilizationIndex)
                      ?? throw new ArgumentException("Civilization not found", nameof(civilizationIndex));

            if (!GetPotentialVertices(civilizationIndex).Any(v => v.Equals(vertex)))
                throw new InvalidOperationException("Vertex not buildable by this civilization");

            if (!IsMobileCampUnlocked(civ))
                return null;

            var cost = GetBuildCost();
            if (!civ.CanPayResourceCost(cost))
                return null;

            civ.PayResourceCost(cost);

            var camp = new MobileCamp(vertex) { CivilizationIndex = civilizationIndex };
            civ.AddMobileCamp(camp);
            _state.Visibility.RecalculateFor(civilizationIndex);
            return camp;
        }

        /// <summary>
        /// Retire un camp mobile, qu'il ait été détruit au combat ou par la construction d'une ville
        /// voisine. Point d'entrée unique de suppression, à l'image de
        /// <see cref="CityBuilderController.DestroyCity"/> / <see cref="WarFleetController.DestroyFleet"/>.
        /// </summary>
        public void DestroyMobileCamp(MobileCamp camp)
        {
            if (_state == null) throw new InvalidOperationException("WorldState has not been initialized.");
            if (camp == null) throw new ArgumentNullException(nameof(camp));

            var civ = _state.Civilizations.FirstOrDefault(c => c.Index == camp.CivilizationIndex)
                      ?? throw new ArgumentException("Camp's civilization not found", nameof(camp));

            camp.RaiseDestroyed();
            civ.RemoveMobileCamp(camp);
            _state.Visibility.Recalculate();
        }

        /// <summary>
        /// Détruit automatiquement tout Camp Mobile de la même civilisation que la ville nouvellement
        /// construite, à distance &lt;= CityProximityDestroyDistance de celle-ci (les camps des autres
        /// civilisations ne sont pas affectés). Appelé depuis MainGameController sur
        /// CityBuilderController.OnCityBuilt.
        /// </summary>
        public void DestroyCampsNear(Vertex cityPosition, int civilizationIndex)
        {
            if (_state == null) return;

            var campsToDestroy = _state.GetAllMobileCamps()
                .Where(c => c.CivilizationIndex == civilizationIndex
                    && c.Position.Z == cityPosition.Z
                    && c.Position.EdgeDistanceTo(cityPosition) <= CityProximityDestroyDistance)
                .ToList();

            foreach (var camp in campsToDestroy)
                DestroyMobileCamp(camp);
        }
    }
}
