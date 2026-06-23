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
    /// Gère la Spire de Corruption : Monument de l'Inframonde, plaçable uniquement sur une zone
    /// corrompue, débloquée une fois la Faille des Abysses entièrement ouverte (3/3 : Faille des
    /// Abysses + Porte Planaire + Rituel de l'Éclipse Noire). Construite par investissement
    /// progressif comme tout Monument.
    /// </summary>
    public class CorruptionSpireController
    {
        private WorldState? _state;
        private GameClock? _clock;

        public const int AbyssUnlockThreshold = 3;
        public const long InvestmentIntervalTicks = MonumentInvestment.IntervalTicks;

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
            if (_clock.CurrentTick - spire.LastInvestmentTick < InvestmentIntervalTicks) return;

            var playerCiv = _state.PlayerCivilization;
            var cost = spire.GetInvestmentCost(playerCiv);
            if (!MonumentInvestment.ProcessTick(spire, cost, playerCiv, _clock.CurrentTick)) return;

            // Laisse InvestedResources au maximum (comme les autres Monuments déjà complétés) :
            // la Spire ne se monte plus une fois construite, le panneau d'investissement reste à 100%.
            spire.Built = true;
            spire.InvestmentEnabled.Clear();
            _state.EventLog.Add(GameEventType.CorruptionSpireBuilt, toast: true);
            OnCorruptionSpireBuilt?.Invoke(this, EventArgs.Empty);

            // Si la Spire repose sur une zone assez corrompue, prévient le joueur que l'évolution
            // en Faille des Abysses est désormais disponible depuis le panneau de la Spire.
            var corruption = _state.Features.OfType<Corruption>().FirstOrDefault(c => c.Position.Equals(spire.Position));
            if (corruption != null && corruption.Level >= AbyssGate.RequiredCorruptionLevel)
                _state.EventLog.Add(GameEventType.AbyssGateEligible, toast: true);
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
