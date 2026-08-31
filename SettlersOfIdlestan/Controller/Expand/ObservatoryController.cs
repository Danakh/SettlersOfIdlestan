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
    /// Monument débloqué par la recherche Cartes des Étoiles : bâti sur une Montagne de la surface,
    /// il monte de niveau par investissement progressif (Verre/Acier/Mithril + points de recherche,
    /// voir <see cref="Observatory"/>). Chaque niveau abaisse le multiplicateur exponentiel du coût
    /// en points de recherche des routes du Vide, de ×3 à ×2.4 une fois l'Observatoire complet
    /// (voir RoadController.GetVoidRouteResearchCost).
    /// </summary>
    public class ObservatoryController : MonumentControllerBase<Observatory>
    {
        public const long InvestmentIntervalTicks = MonumentInvestment.IntervalTicks;

        public event EventHandler? OnObservatoryPlaced;
        public event EventHandler<int>? OnObservatoryLevelUp;

        internal ObservatoryController() { }

        internal void Initialize(WorldState? state, GameClock? clock = null, HarvestController? harvestController = null)
            => InitializeCore(state, clock, harvestController);

        public static ResourceSet GetLevelCost(int level) => Observatory.GetLevelCost(level);

        public static long GetLevelResearchCost(int level) => Observatory.GetLevelResearchCost(level);

        protected override bool IsInvestmentComplete(Observatory observatory) => observatory.IsMaxLevel;

        /// <summary>
        /// Second axe d'investissement de l'Observatoire : les points de recherche, prélevés sur le
        /// pool de la civilisation à leur propre rythme (voir MonumentInvestment.ProcessResearchTick).
        /// </summary>
        protected override bool ProcessExtraInvestmentAxes(Observatory observatory, Civilization playerCiv, long now)
            => MonumentInvestment.ProcessResearchTick(observatory, observatory.GetRequiredResearch(playerCiv), playerCiv, now);

        protected override void ResetExtraInvestmentAxes(Observatory observatory)
        {
            observatory.InvestedResearch = 0;
            observatory.ResearchInvestmentEnabled = false;
        }

        protected override void OnInvestmentCycleCompleted(Observatory observatory, Civilization playerCiv)
        {
            observatory.Level++;
            CompleteLevelUp(observatory, playerCiv, observatory.Level, observatory.IsMaxLevel, GameEventType.ObservatoryLevelUp);
            OnObservatoryLevelUp?.Invoke(this, observatory.Level);
        }

        public bool HasObservatoryUnlocked(Civilization playerCiv)
            => playerCiv.ModifierAggregator.HasModifier(ECategory.UNLOCK_OBSERVATORY);

        public int GetObservatoryLevel()
            => _state?.Features.OfType<Observatory>().FirstOrDefault()?.Level ?? 0;

        public bool CanPlaceObservatory(Civilization playerCiv)
        {
            if (!HasObservatoryUnlocked(playerCiv)) return false;
            if (_state?.Features.OfType<Observatory>().Any() == true) return false;
            return true;
        }

        /// <summary>
        /// Hexes de Montagne en surface, adjacents à une ville du joueur, sans ville ennemie
        /// adjacente et sans autre feature — mêmes règles que la Mine Profonde, ordre du moins au
        /// plus coûteux à sacrifier compris (voir <see cref="MonumentInvestment.OrderByLeastSacrifice"/>).
        /// </summary>
        public List<HexCoord> GetPlaceableHexes() => GetPlaceableHexesAroundPlayerCities();

        protected override bool IsPlacementLayerAllowed(HexCoord hex) => hex.Z == IslandMap.SurfaceLayer;

        protected override bool IsPlacementTerrainAllowed(HexTile tile, IslandMap map, HexCoord hex)
            => tile.TerrainType == TerrainType.Mountain;

        protected override Observatory CreateFeature(HexCoord position) => new(position);

        protected override GameEventType PlacedEventType => GameEventType.ObservatoryPlaced;

        /// <summary>
        /// Amorce aussi le cooldown de l'investissement en recherche, pour la même raison que
        /// LastInvestmentTick (voir MonumentControllerBase.PlaceMonument).
        /// </summary>
        protected override void PrimeExtraInvestmentAxesOnPlacement(Observatory observatory)
            => observatory.LastResearchInvestmentTick = _clock?.CurrentTick ?? 0;

        protected override void RaisePlaced() => OnObservatoryPlaced?.Invoke(this, EventArgs.Empty);

        public Observatory? PlaceObservatory(HexCoord position) => PlaceMonument(position);
    }
}
