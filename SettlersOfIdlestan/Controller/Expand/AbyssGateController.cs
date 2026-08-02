using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandFeatures;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Prestige;
using System;
using System.Linq;

namespace SettlersOfIdlestan.Controller.Expand
{
    /// <summary>
    /// Gère la Faille des Abysses : évolution de la Spire de Corruption, débloquée une fois une Spire
    /// bâtie et le pic de corruption jamais nettoyé (<see cref="PrestigeState.MaxCorruptionLevelCleared"/>,
    /// voir <see cref="IsAbyssGateEligible"/>) ayant atteint <see cref="AbyssGate.RequiredCorruptionLevel"/>
    /// ou plus. Ce record est global à la partie — n'importe quel hex compte, nettoyé par n'importe
    /// quel mécanisme (Temple, débordement, annulation par le Dominion, décroissance de monument), pas
    /// seulement l'hex de la Spire elle-même. N'est pas une action de civilisation — l'évolution
    /// remplace la Spire sur son hex et se construit par investissement progressif comme tout Monument.
    /// </summary>
    public class AbyssGateController
    {
        private WorldState? _state;
        private GameClock? _clock;
        private HarvestController? _harvestController;
        private PrestigeState? _prestigeState;

        public const long InvestmentIntervalTicks = MonumentInvestment.IntervalTicks;

        public event EventHandler? OnAbyssGatePlaced;
        public event EventHandler? OnAbyssGateBuilt;

        internal AbyssGateController() { }

        internal void Initialize(WorldState? state, GameClock? clock = null, HarvestController? harvestController = null, PrestigeState? prestigeState = null)
        {
            if (_clock != null)
                _clock.Advanced -= OnClockAdvanced;

            _state = state;
            _clock = clock;
            _harvestController = harvestController;
            _prestigeState = prestigeState;

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
        /// True si une Spire de Corruption est bâtie, qu'aucune Faille des Abysses n'existe déjà, et
        /// que <see cref="PrestigeState.MaxCorruptionLevelCleared"/> a atteint
        /// <see cref="AbyssGate.RequiredCorruptionLevel"/> ou plus. Ce record est mis à jour par
        /// <see cref="Controller.Island.CorruptionController.ReduceLevel"/> à chaque zone de Corruption
        /// entièrement dissipée, n'importe où sur la carte et par n'importe quel mécanisme (Temple,
        /// débordement — y compris annulation par le Dominion — ou décroissance de monument) : ce n'est
        /// pas la corruption courante (ni même le pic) du seul hex de la Spire qui compte, mais le
        /// meilleur nettoyage jamais réalisé par la civilisation. Se base sur ce record plutôt que sur
        /// une corruption "en cours" précisément parce que la Spire bâtie réduit systématiquement la
        /// corruption sur son propre hex (voir CorruptionController.ProcessMonumentCorruptionDecay) —
        /// une condition sur le niveau courant de cet hex précis se démentirait presque aussitôt vérifiée.
        /// </summary>
        public bool IsAbyssGateEligible()
        {
            if (_state == null) return false;
            if (_state.Features.OfType<AbyssGate>().Any()) return false;
            if (!_state.Features.OfType<CorruptionSpire>().Any(s => s.Built)) return false;

            return (_prestigeState?.MaxCorruptionLevelCleared ?? 0) >= AbyssGate.RequiredCorruptionLevel;
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
