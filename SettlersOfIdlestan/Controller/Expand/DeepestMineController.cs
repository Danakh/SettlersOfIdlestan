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
    /// Gère la Mine Profonde : placement (comme une Merveille, uniquement sur Montagne),
    /// creusement par investissement progressif (1000 Acier entre autres), puis ouverture
    /// de l'avant-poste dans l'Inframonde.
    /// </summary>
    public class DeepestMineController
    {
        private WorldState? _state;
        private GameClock? _clock;

        public const long InvestmentIntervalTicks = 100L;

        public event EventHandler? OnDeepestMinePlaced;
        public event EventHandler? OnDeepestMineDug;

        internal DeepestMineController() { }

        internal void Initialize(WorldState? state, GameClock? clock = null)
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
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[DeepestMineController] {nameof(ProcessInvestment)}: {ex}"); }
            try { TryInitializeUnderworld(); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[DeepestMineController] {nameof(TryInitializeUnderworld)}: {ex}"); }
        }

        private void ProcessInvestment()
        {
            if (_state == null || _clock == null) return;
            var mine = _state.Features.OfType<DeepestMine>().FirstOrDefault();
            if (mine == null || mine.Dug || mine.InvestmentEnabled.Count == 0) return;

            long now = _clock.CurrentTick;
            if (now - mine.LastInvestmentTick < InvestmentIntervalTicks) return;
            mine.LastInvestmentTick = now;

            var playerCiv = _state.PlayerCivilization;
            var cost = mine.GetInvestmentCost();
            var toDeselect = new List<Resource>();

            foreach (var resource in mine.InvestmentEnabled)
            {
                if (!cost.Contains(resource)) continue;
                long invested = mine.InvestedResources.TryGetValue(resource, out var inv) ? inv : 0;
                long required = cost[resource];
                if (invested >= required) { toDeselect.Add(resource); continue; }

                int stock = playerCiv.GetResourceQuantity(resource);
                if (stock < 1) continue;
                int amount = Math.Max(1, stock / 100);

                long remaining = required - invested;
                if (amount > remaining) amount = (int)remaining;

                playerCiv.RemoveResource(resource, amount);
                long newInvested = invested + amount;
                mine.InvestedResources[resource] = newInvested;
                if (newInvested >= required)
                    toDeselect.Add(resource);
            }

            foreach (var r in toDeselect)
                mine.InvestmentEnabled.Remove(r);

            if (cost.Keys.All(r => (mine.InvestedResources.TryGetValue(r, out var inv) ? inv : 0) >= cost[r]))
            {
                mine.Dug = true;
                mine.InvestmentEnabled.Clear();
                _state.EventLog.Add(GameEventType.DeepestMineDug);
                OnDeepestMineDug?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Ouvre l'Inframonde si la Mine Profonde est creusée (feature) ou si une ancienne
        /// sauvegarde contient le bâtiment legacy Mine Profonde.
        /// </summary>
        private void TryInitializeUnderworld()
        {
            if (_state == null || _state.Layers.ContainsKey(LayerState.UnderworldZ)) return;

            var playerCiv = _state.PlayerCivilization;

            bool hasDugMine = _state.Features.OfType<DeepestMine>().Any(m => m.Dug);
            bool hasLegacyBuilding = playerCiv.Cities.Any(city =>
                city.Buildings.Any(b => b.Type == Model.Buildings.BuildingType.DeepestMine && b.Level > 0));

            if (!hasDugMine && !hasLegacyBuilding) return;

            var underworldLayer = LayerState.EstablishOupostInNewAutoExpandLayer(playerCiv);
            _state.AddLayer(LayerState.UnderworldZ, underworldLayer);
            _state.Visibility.RecalculateFor(playerCiv.Index);
        }

        public bool HasDeepestMineUnlocked(Civilization playerCiv)
            => playerCiv.ModifierAggregator.ApplyModifiers(ECategory.UNLOCK_DEEPEST_MINE, "", 0) > 0;

        public bool CanPlaceDeepestMine(Civilization playerCiv)
        {
            if (!HasDeepestMineUnlocked(playerCiv)) return false;
            if (_state?.Features.OfType<DeepestMine>().Any() == true) return false;
            return true;
        }

        /// <summary>
        /// Hexes de Montagne en surface, adjacents à une ville du joueur,
        /// sans ville ennemie adjacente et sans autre feature.
        /// </summary>
        public List<HexCoord> GetPlaceableHexes()
        {
            if (_state == null) return new List<HexCoord>();

            var playerCiv = _state.PlayerCivilization;

            var playerCityHexes = new HashSet<HexCoord>();
            foreach (var city in playerCiv.Cities)
                foreach (var hex in city.Position.GetHexes())
                    playerCityHexes.Add(hex);

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
                if (hex.Z != IslandMap.SurfaceLayer) continue;
                var tile = _state.GetMapFor(hex).GetTile(hex);
                if (tile == null) continue;
                if (tile.TerrainType != TerrainType.Mountain) continue;
                if (enemyZone.Contains(hex)) continue;
                if (_state.Features.Any(f => f.Position.Equals(hex))) continue;
                result.Add(hex);
            }

            return result;
        }

        public DeepestMine? PlaceDeepestMine(HexCoord position)
        {
            if (_state == null) return null;
            var mine = new DeepestMine(position);
            _state.AddFeature(mine);
            _state.EventLog.Add(GameEventType.DeepestMinePlaced);
            OnDeepestMinePlaced?.Invoke(this, EventArgs.Empty);
            return mine;
        }
    }
}
