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
    public class WonderController
    {
        private WorldState? _state;
        private GameClock? _clock;
        private HarvestController? _harvestController;

        public const long InvestmentIntervalTicks = MonumentInvestment.IntervalTicks;

        public event EventHandler? OnWonderPlaced;
        public event EventHandler<int>? OnWonderLevelUp;

        internal WonderController() { }

        internal void Initialize(WorldState? state, GameClock? clock = null, HarvestController? harvestController = null)
        {
            if (_clock != null)
                _clock.Advanced -= OnClockAdvanced;

            _state = state;
            _clock = clock;
            _harvestController = harvestController;

            if (_clock != null)
                _clock.Advanced += OnClockAdvanced;
        }

        private void OnClockAdvanced(object? sender, GameClockAdvancedEventArgs e)
        {
            try { ProcessInvestment(); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[WonderController] {nameof(ProcessInvestment)}: {ex}"); }
        }

        public static ResourceSet GetLevelCost(int level) => Wonder.GetLevelCost(level);

        private void ProcessInvestment()
        {
            if (_state == null || _clock == null) return;
            var wonder = _state.Features.OfType<Wonder>().FirstOrDefault();
            if (wonder == null || wonder.IsMaxLevel || wonder.InvestmentEnabled.Count == 0) return;
            if (_clock.CurrentTick - wonder.LastInvestmentTick < InvestmentIntervalTicks) return;

            var playerCiv = _state.PlayerCivilization;
            var cost = wonder.GetInvestmentCost(playerCiv);
            if (!MonumentInvestment.ProcessTick(wonder, cost, playerCiv, _clock.CurrentTick)) return;

            wonder.Level++;
            wonder.InvestedResources.Clear();
            wonder.CompletedInvestmentCost.Clear();
            wonder.InvestmentEnabled.Clear();
            _state.EventLog.Add(GameEventType.WonderLevelUp, wonder.Level.ToString(), toast: true);
            if (_harvestController != null && !wonder.IsMaxLevel)
                MonumentInvestment.TryAutoStartInvestment(wonder, wonder.GetInvestmentCost(playerCiv), playerCiv, _harvestController, _state);
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
        public List<HexCoord> GetPlaceableHexes()
        {
            if (_state == null) return new List<HexCoord>();

            var playerCiv = _state.PlayerCivilization;

            // Hexes that are part of a player city vertex
            var playerCityHexes = new HashSet<HexCoord>();
            foreach (var city in playerCiv.Cities)
                foreach (var hex in city.Position.GetHexes())
                    playerCityHexes.Add(hex);

            // Hexes that are part of or adjacent to NPC city vertices
            var enemyZone = new HashSet<HexCoord>();
            foreach (var civ in _state.Civilizations.Where(c => c.Index != playerCiv.Index))
                foreach (var city in civ.Cities)
                    foreach (var hex in city.Position.GetHexes())
                    {
                        enemyZone.Add(hex);
                        foreach (HexDirection dir in Enum.GetValues<HexDirection>())
                            enemyZone.Add(hex.Neighbor(dir));
                    }

            var result = new List<HexCoord>();
            foreach (var hex in playerCityHexes)
            {
                var tile = _state.GetMapFor(hex)?.GetTile(hex);
                if (tile == null) continue;
                if (tile.TerrainType == TerrainType.Water) continue;
                if (enemyZone.Contains(hex)) continue;
                if (_state.HasMonumentBlockingFeaturesAt(hex)) continue;
                result.Add(hex);
            }

            return MonumentInvestment.OrderByLeastSacrifice(result, playerCiv, _state);
        }

        public Wonder? PlaceWonder(HexCoord position)
        {
            if (_state == null) return null;
            if (_state.GetMapFor(position) == null) return null;
            var wonder = new Wonder(position);
            // Amorce le cooldown d'investissement sur le tick de pose plutôt que de laisser la valeur
            // par défaut à 0 : sans ça, ProcessTick voit un écart énorme dès le premier cycle
            // (now - 0) et rattrape d'un coup tous les cycles "manqués" depuis le tick 0 de la partie,
            // ce qui vide le stock de ressources d'un coup au lieu de démarrer progressivement (bug
            // vécu après un prestige, où le tick courant est déjà élevé au moment de la pose).
            wonder.LastInvestmentTick = _clock?.CurrentTick ?? 0;
            _state.AddFeature(wonder);
            _state.EventLog.Add(GameEventType.WonderPlaced);
            if (_harvestController != null)
                MonumentInvestment.TryAutoStartInvestment(wonder, wonder.GetInvestmentCost(_state.PlayerCivilization), _state.PlayerCivilization, _harvestController, _state);
            OnWonderPlaced?.Invoke(this, EventArgs.Empty);
            return wonder;
        }
    }
}
