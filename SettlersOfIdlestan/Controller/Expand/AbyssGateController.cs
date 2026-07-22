using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandFeatures;
using SettlersOfIdlestan.Model.IslandMap;
using System;
using System.Linq;

namespace SettlersOfIdlestan.Controller.Expand
{
    /// <summary>
    /// Gère la Faille des Abysses : évolution de la Spire de Corruption, débloquée une fois une
    /// Spire bâtie sur une zone de corruption de niveau <see cref="AbyssGate.RequiredCorruptionLevel"/>
    /// ou plus. N'est pas une action de civilisation — l'évolution remplace la Spire sur son hex
    /// et se construit par investissement progressif comme tout Monument.
    /// </summary>
    public class AbyssGateController
    {
        private WorldState? _state;
        private GameClock? _clock;
        private HarvestController? _harvestController;

        public const long InvestmentIntervalTicks = MonumentInvestment.IntervalTicks;

        public event EventHandler? OnAbyssGatePlaced;
        public event EventHandler? OnAbyssGateBuilt;

        internal AbyssGateController() { }

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
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[AbyssGateController] {nameof(ProcessInvestment)}: {ex}"); }
            try { TryInitializeAbyss(); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[AbyssGateController] {nameof(TryInitializeAbyss)}: {ex}"); }
        }

        private void ProcessInvestment()
        {
            if (_state == null || _clock == null) return;
            var gate = _state.Features.OfType<AbyssGate>().FirstOrDefault();
            if (gate == null || gate.Built || gate.InvestmentEnabled.Count == 0) return;
            if (_clock.CurrentTick - gate.LastInvestmentTick < InvestmentIntervalTicks) return;

            var playerCiv = _state.PlayerCivilization;
            var cost = gate.GetInvestmentCost(playerCiv);
            if (!MonumentInvestment.ProcessTick(gate, cost, playerCiv, _clock.CurrentTick)) return;

            // Comme la Spire : l'investissement reste affiché à 100% une fois la Faille bâtie.
            gate.Built = true;
            gate.InvestmentEnabled.Clear();
            _state.EventLog.Add(GameEventType.AbyssGateBuilt, toast: true);
            OnAbyssGateBuilt?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// True si une Spire de Corruption bâtie repose sur une zone de corruption suffisamment
        /// puissante pour évoluer, et qu'aucune Faille des Abysses n'existe déjà.
        /// </summary>
        public bool IsAbyssGateEligible()
        {
            if (_state == null) return false;
            if (_state.Features.OfType<AbyssGate>().Any()) return false;

            var spire = _state.Features.OfType<CorruptionSpire>().FirstOrDefault(s => s.Built);
            if (spire == null) return false;

            var corruption = _state.Features.OfType<Corruption>().FirstOrDefault(c => c.Position.Equals(spire.Position));
            return corruption != null && corruption.Level >= AbyssGate.RequiredCorruptionLevel;
        }

        public bool HasAbyssGateBuilt()
            => _state?.Features.OfType<AbyssGate>().Any(f => f.Built) == true;

        /// <summary>
        /// Ouvre l'Abysse (comme <see cref="DeepestMineController.TryInitializeUnderworld"/> pour
        /// l'Inframonde) une fois la Faille des Abysses bâtie : crée le premier avant-poste, entouré
        /// de Void pour permettre à AutoExtendController de faire pousser des îles.
        /// </summary>
        private void TryInitializeAbyss()
        {
            if (_state == null) return;

            var playerCiv = _state.PlayerCivilization;

            // Déjà un avant-poste joueur dans l'Abysse → rien à faire
            if (playerCiv.Cities.Any(c => c.Position.Z == LayerState.AbyssZ)) return;

            if (!HasAbyssGateBuilt()) return;

            var abyssLayer = LayerState.EstablishOupostInNewAutoExpandLayer(playerCiv, LayerState.AbyssZ, surroundWithVoid: true);
            _state.AddLayer(LayerState.AbyssZ, abyssLayer);
            _state.Visibility.RecalculateFor(playerCiv.Index);
        }

        /// <summary>
        /// Remplace la Spire de Corruption éligible par une Faille des Abysses sur le même hex et
        /// démarre son investissement progressif. Retourne null si aucune évolution n'est possible.
        /// </summary>
        public AbyssGate? PlaceAbyssGate()
        {
            if (_state == null || !IsAbyssGateEligible()) return null;

            var spire = _state.Features.OfType<CorruptionSpire>().First(s => s.Built);
            HexCoord position = spire.Position;
            _state.RemoveFeature(spire);

            var gate = new AbyssGate(position);
            _state.AddFeature(gate);
            _state.EventLog.Add(GameEventType.AbyssGatePlaced);
            if (_harvestController != null)
                MonumentInvestment.TryAutoStartInvestment(gate, gate.GetInvestmentCost(_state.PlayerCivilization), _state.PlayerCivilization, _harvestController, _state);
            OnAbyssGatePlaced?.Invoke(this, EventArgs.Empty);
            return gate;
        }
    }
}
