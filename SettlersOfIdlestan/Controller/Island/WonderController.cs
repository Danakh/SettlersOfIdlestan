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

        internal WonderController() { }

        internal void Initialize(IslandState? state)
        {
            _state = state;
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
                if (_state.Features.OfType<Wonder>().Any(w => w.Position.Equals(hex))) continue;
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
