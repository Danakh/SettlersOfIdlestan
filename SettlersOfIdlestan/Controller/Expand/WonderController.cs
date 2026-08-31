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
    public class WonderController : MonumentControllerBase<Wonder>
    {
        public const long InvestmentIntervalTicks = MonumentInvestment.IntervalTicks;

        public event EventHandler? OnWonderPlaced;
        public event EventHandler<int>? OnWonderLevelUp;

        internal WonderController() { }

        internal void Initialize(WorldState? state, GameClock? clock = null, HarvestController? harvestController = null)
            => InitializeCore(state, clock, harvestController);

        public static ResourceSet GetLevelCost(int level) => Wonder.GetLevelCost(level);

        protected override bool IsInvestmentComplete(Wonder wonder) => wonder.IsMaxLevel;

        protected override void OnInvestmentCycleCompleted(Wonder wonder, Civilization playerCiv)
        {
            wonder.Level++;
            CompleteLevelUp(wonder, playerCiv, wonder.Level, wonder.IsMaxLevel, GameEventType.WonderLevelUp);
            OnWonderLevelUp?.Invoke(this, wonder.Level);
        }

        public bool HasWondersUnlocked(Civilization playerCiv)
            => playerCiv.ModifierAggregator.ApplyModifiers(ECategory.UNLOCK_WONDERS, "", 0) > 0;

        public bool CanPlaceWonder(Civilization playerCiv)
        {
            if (!HasWondersUnlocked(playerCiv)) return false;
            if (_state?.Features.OfType<Wonder>().Any() == true) return false;
            return true;
        }

        /// <summary>
        /// Hexes adjacent to player city vertices that have no enemy city adjacent, ordered from the
        /// cheapest to the costliest to sacrifice (see <see cref="MonumentInvestment.OrderByLeastSacrifice"/>).
        /// </summary>
        public List<HexCoord> GetPlaceableHexes() => GetPlaceableHexesAroundPlayerCities();

        /// <summary>N'importe quel terrain hors Eau.</summary>
        protected override bool IsPlacementTerrainAllowed(HexTile tile, IslandMap map, HexCoord hex)
            => tile.TerrainType != TerrainType.Water;

        protected override Wonder CreateFeature(HexCoord position) => new(position);

        protected override GameEventType PlacedEventType => GameEventType.WonderPlaced;

        protected override void RaisePlaced() => OnWonderPlaced?.Invoke(this, EventArgs.Empty);

        public Wonder? PlaceWonder(HexCoord position) => PlaceMonument(position);
    }
}
