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
        private IslandState? _state;
        private GameClock? _clock;

        public const long InvestmentIntervalTicks = 100L;

        internal WonderController() { }

        internal void Initialize(IslandState? state, GameClock? clock = null)
        {
            if (_clock != null)
                _clock.Advanced -= OnClockAdvanced;

            _state = state;
            _clock = clock;

            if (_clock != null)
                _clock.Advanced += OnClockAdvanced;
        }

        private void OnClockAdvanced(object? sender, GameClockAdvancedEventArgs e)
        {
            try { ProcessInvestment(); }
            catch { }
        }

        public static ResourceSet GetLevelCost(int level)
        {
            return new ResourceSet
            {
                { Resource.Food,  5000 * level * level },
                { Resource.Wood,  5000 * level * level },
                { Resource.Brick, 5000 * level * level },
                { Resource.Stone, 5000 * level * level },
                { Resource.Ore,   1000 * level * level },
                { Resource.Gold,  500  * level * level },
            };
        }

        private void ProcessInvestment()
        {
            if (_state == null || _clock == null) return;
            var wonder = _state.Features.OfType<Wonder>().FirstOrDefault();
            if (wonder == null || wonder.InvestmentEnabled.Count == 0) return;

            long now = _clock.CurrentTick;
            if (now - wonder.LastInvestmentTick < InvestmentIntervalTicks) return;
            wonder.LastInvestmentTick = now;

            var playerCiv = _state.PlayerCivilization;
            var cost = GetLevelCost(wonder.Level + 1);
            var toDeselect = new List<Resource>();

            foreach (var resource in wonder.InvestmentEnabled)
            {
                if (!cost.Contains(resource)) continue;
                long invested = wonder.InvestedResources.TryGetValue(resource, out var inv) ? inv : 0;
                long required = cost[resource];
                if (invested >= required) { toDeselect.Add(resource); continue; }

                int stock = playerCiv.GetResourceQuantity(resource);
                int amount = Math.Max(1, stock / 100);
                if (amount <= 0) continue;

                long remaining = required - invested;
                if (amount > remaining) amount = (int)remaining;

                playerCiv.RemoveResource(resource, amount);
                long newInvested = invested + amount;
                wonder.InvestedResources[resource] = newInvested;
                if (newInvested >= required)
                    toDeselect.Add(resource);
            }

            foreach (var r in toDeselect)
                wonder.InvestmentEnabled.Remove(r);

            if (cost.Keys.All(r => (wonder.InvestedResources.TryGetValue(r, out var inv) ? inv : 0) >= cost[r]))
            {
                wonder.Level++;
                wonder.InvestedResources.Clear();
                wonder.InvestmentEnabled.Clear();
            }
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
        /// Hexes adjacent to player city vertices that have no enemy city adjacent.
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
                var tile = _state.Map.GetTile(hex);
                if (tile == null) continue;
                if (tile.TerrainType == TerrainType.Water) continue;
                if (enemyZone.Contains(hex)) continue;
                if (_state.Features.Any(f => f.Position.Equals(hex))) continue;
                result.Add(hex);
            }

            return result;
        }

        public Wonder? PlaceWonder(HexCoord position)
        {
            if (_state == null) return null;
            var wonder = new Wonder(position);
            _state.AddFeature(wonder);
            _state.EventLog.Add(GameEventType.WonderPlaced);
            return wonder;
        }
    }
}
