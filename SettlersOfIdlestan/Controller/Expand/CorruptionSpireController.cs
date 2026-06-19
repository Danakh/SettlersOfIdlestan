using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandFeatures;
using SettlersOfIdlestan.Model.IslandMap;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SettlersOfIdlestan.Controller.Expand
{
    /// <summary>
    /// Gère la Spire de Corruption : merveille de l'Inframonde, plaçable uniquement sur une zone
    /// corrompue, débloquée une fois la Faille des Abysses entièrement ouverte (3/3 : Faille des
    /// Abysses + Porte Planaire + Rituel de l'Éclipse Noire). Construite par investissement
    /// progressif comme une Merveille / Mine Profonde.
    /// </summary>
    public class CorruptionSpireController
    {
        private WorldState? _state;
        private GameClock? _clock;

        public const int AbyssUnlockThreshold = 3;
        public const long InvestmentIntervalTicks = 100L;

        public event EventHandler? OnCorruptionSpirePlaced;
        public event EventHandler? OnCorruptionSpireBuilt;

        internal CorruptionSpireController() { }

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
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[CorruptionSpireController] {nameof(ProcessInvestment)}: {ex}"); }
        }

        private void ProcessInvestment()
        {
            if (_state == null || _clock == null) return;
            var spire = _state.Features.OfType<CorruptionSpire>().FirstOrDefault();
            if (spire == null || spire.Built || spire.InvestmentEnabled.Count == 0) return;

            long now = _clock.CurrentTick;
            if (now - spire.LastInvestmentTick < InvestmentIntervalTicks) return;
            spire.LastInvestmentTick = now;

            var playerCiv = _state.PlayerCivilization;
            var cost = spire.GetInvestmentCost(playerCiv);
            var toDeselect = new List<Resource>();

            foreach (var resource in spire.InvestmentEnabled)
            {
                if (!cost.Contains(resource)) continue;
                long invested = spire.InvestedResources.TryGetValue(resource, out var inv) ? inv : 0;
                long required = cost[resource];
                if (invested >= required) { toDeselect.Add(resource); continue; }

                int stock = playerCiv.GetResourceQuantity(resource);
                if (stock < 1) continue;
                int amount = Math.Max(1, stock / 100);

                int maxStock = playerCiv.GetResourceMaxQuantity(resource);
                if (maxStock > 0 && stock > maxStock * 0.5)
                    amount = Math.Max(1, (int)(amount * playerCiv.InvestmentSpeedHighStockBonus));

                long remaining = required - invested;
                if (amount > remaining) amount = (int)remaining;

                playerCiv.RemoveResource(resource, amount);
                long newInvested = invested + amount;
                spire.InvestedResources[resource] = newInvested;
                if (newInvested >= required)
                    toDeselect.Add(resource);
            }

            foreach (var r in toDeselect)
                spire.InvestmentEnabled.Remove(r);

            if (cost.Keys.All(r => (spire.InvestedResources.TryGetValue(r, out var inv) ? inv : 0) >= cost[r]))
            {
                // Laisse InvestedResources au maximum (comme la Mine Profonde) : la Spire ne se
                // monte plus une fois construite, le panneau d'investissement reste à 100%.
                spire.Built = true;
                spire.InvestmentEnabled.Clear();
                _state.EventLog.Add(GameEventType.CorruptionSpireBuilt, toast: true);
                OnCorruptionSpireBuilt?.Invoke(this, EventArgs.Empty);
            }
        }

        public bool HasCorruptionSpireUnlocked(Civilization playerCiv)
            => playerCiv.ModifierAggregator.ApplyModifiers(ECategory.UNLOCK_ABYSS, "", 0) >= AbyssUnlockThreshold;

        public bool CanPlaceCorruptionSpire(Civilization playerCiv)
        {
            if (!HasCorruptionSpireUnlocked(playerCiv)) return false;
            if (_state?.Features.OfType<CorruptionSpire>().Any() == true) return false;
            return true;
        }

        public bool HasCorruptionSpireBuilt()
            => _state?.Features.OfType<CorruptionSpire>().Any(f => f.Built) == true;

        /// <summary>
        /// Hexes de l'Inframonde portant la feature Corruption, libres de toute autre feature.
        /// </summary>
        public List<HexCoord> GetPlaceableHexes()
        {
            if (_state == null) return new List<HexCoord>();

            var result = new List<HexCoord>();
            foreach (var feature in _state.Features.OfType<Corruption>())
            {
                var hex = feature.Position;
                if (hex.Z != LayerState.UnderworldZ) continue;

                var tile = _state.GetMapFor(hex)?.GetTile(hex);
                if (tile == null) continue;

                bool hasOtherFeature = _state.GetFeaturesAt(hex).Any(f => f is not Corruption);
                if (hasOtherFeature) continue;

                result.Add(hex);
            }

            return result;
        }

        public CorruptionSpire? PlaceCorruptionSpire(HexCoord position)
        {
            if (_state == null) return null;
            var spire = new CorruptionSpire(position);
            _state.AddFeature(spire);
            _state.EventLog.Add(GameEventType.CorruptionSpirePlaced);
            OnCorruptionSpirePlaced?.Invoke(this, EventArgs.Empty);
            return spire;
        }
    }
}
