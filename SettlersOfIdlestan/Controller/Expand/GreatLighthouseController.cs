using SettlersOfIdlestan.Controller.Expand;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandFeatures;
using SettlersOfIdlestan.Model.IslandMap;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SettlersOfIdlestan.Controller.Island
{
    /// <summary>
    /// Monument débloqué par la recherche Grand Phare (même palier que les Tours de Guet) : bâti
    /// sur un hex côtier (terre adjacente à de l'eau), il fournit un bonus de prestige par niveau
    /// et des effets de portée liés aux Tours de Guet / routes maritimes une fois ces branches de
    /// prestige débloquées.
    /// </summary>
    public class GreatLighthouseController : MonumentControllerBase<GreatLighthouse>
    {
        public const long InvestmentIntervalTicks = MonumentInvestment.IntervalTicks;

        public event EventHandler? OnGreatLighthousePlaced;
        public event EventHandler<int>? OnGreatLighthouseLevelUp;

        internal GreatLighthouseController() { }

        internal void Initialize(WorldState? state, GameClock? clock = null, HarvestController? harvestController = null)
            => InitializeCore(state, clock, harvestController);

        public static ResourceSet GetLevelCost(int level) => GreatLighthouse.GetLevelCost(level);

        protected override bool IsInvestmentComplete(GreatLighthouse greatLighthouse) => greatLighthouse.IsMaxLevel;

        protected override void OnInvestmentCycleCompleted(GreatLighthouse greatLighthouse, Civilization playerCiv)
        {
            greatLighthouse.Level++;
            // Niveau 1 active le bonus de portée des Tours de Guet (WorldVisibility.WatchtowerVisionBonus),
            // qui s'applique à toutes les civilisations : il faut donc un Recalculate() global, pas
            // seulement RecalculateFor(playerCiv). Sans cet appel, le cache de visibilité reste figé
            // à l'ancien rayon jusqu'à la prochaine mutation route/ville/bâtiment.
            _state!.Visibility.Recalculate();
            CompleteLevelUp(greatLighthouse, playerCiv, greatLighthouse.Level, greatLighthouse.IsMaxLevel, GameEventType.GreatLighthouseLevelUp);
            OnGreatLighthouseLevelUp?.Invoke(this, greatLighthouse.Level);
        }

        public bool HasGreatLighthouseUnlocked(Civilization playerCiv)
            => playerCiv.ModifierAggregator.ApplyModifiers(ECategory.UNLOCK_GREAT_LIGHTHOUSE, "", 0) > 0;

        public int GetGreatLighthouseLevel()
            => _state?.Features.OfType<GreatLighthouse>().FirstOrDefault()?.Level ?? 0;

        /// <summary>
        /// Grand Phare niveau 2 : débloque la construction de Balises Maritimes
        /// (voir MaritimeBeaconController), qui servent d'ancrage côtier artificiel pour prolonger
        /// les routes maritimes en pleine mer une fois routes maritimes débloquées (UNLOCK_MARITIME_ROUTES).
        /// </summary>
        public bool AreMaritimeBeaconsUnlocked() => GetGreatLighthouseLevel() >= 2;

        public bool CanPlaceGreatLighthouse(Civilization playerCiv)
        {
            if (!HasGreatLighthouseUnlocked(playerCiv)) return false;
            if (_state?.Features.OfType<GreatLighthouse>().Any() == true) return false;
            return true;
        }

        /// <summary>
        /// Hexes côtiers (terre adjacente à de l'eau) adjacents aux villes du joueur, sans ville
        /// ennemie adjacente — du moins au plus coûteux à sacrifier (voir
        /// <see cref="MonumentInvestment.OrderByLeastSacrifice"/>).
        /// </summary>
        public List<HexCoord> GetPlaceableHexes() => GetPlaceableHexesAroundPlayerCities();

        /// <summary>Terre côtière : hex non aquatique ayant au moins un voisin aquatique.</summary>
        protected override bool IsPlacementTerrainAllowed(HexTile tile, IslandMap map, HexCoord hex)
        {
            if (tile.TerrainType.IsWater()) return false;
            return hex.Neighbors().Any(n => map.GetTile(n)?.TerrainType.IsWater() == true);
        }

        protected override GreatLighthouse CreateFeature(HexCoord position) => new(position);

        protected override GameEventType PlacedEventType => GameEventType.GreatLighthousePlaced;

        protected override void RaisePlaced() => OnGreatLighthousePlaced?.Invoke(this, EventArgs.Empty);

        public GreatLighthouse? PlaceGreatLighthouse(HexCoord position) => PlaceMonument(position);
    }
}
