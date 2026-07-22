using SettlersOfIdlestan.Controller.Island;
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
    /// progressif comme tout Monument ; une fois bâtie, son rayon de décroissance (CorruptionSpire.Radius)
    /// reste améliorable indéfiniment par le même mécanisme d'investissement.
    /// </summary>
    public class CorruptionSpireController
    {
        private WorldState? _state;
        private GameClock? _clock;
        private HarvestController? _harvestController;

        public const int AbyssUnlockThreshold = 3;
        public const long InvestmentIntervalTicks = MonumentInvestment.IntervalTicks;

        public event EventHandler? OnCorruptionSpirePlaced;
        public event EventHandler? OnCorruptionSpireBuilt;
        public event EventHandler<int>? OnCorruptionSpireRadiusUpgraded;

        internal CorruptionSpireController() { }

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
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[CorruptionSpireController] {nameof(ProcessInvestment)}: {ex}"); }
        }

        /// <summary>
        /// Investissement progressif de la Spire : d'abord la construction initiale (Built = false),
        /// puis, une fois bâtie, l'amélioration indéfinie de son rayon de décroissance (Radius),
        /// chaque niveau coûtant 50% de plus que le précédent (voir CorruptionSpire.GetRadiusUpgradeCost).
        /// </summary>
        private void ProcessInvestment()
        {
            if (_state == null || _clock == null) return;
            var spire = _state.Features.OfType<CorruptionSpire>().FirstOrDefault();
            if (spire == null || spire.InvestmentEnabled.Count == 0) return;
            if (_clock.CurrentTick - spire.LastInvestmentTick < InvestmentIntervalTicks) return;

            var playerCiv = _state.PlayerCivilization;
            var cost = spire.GetInvestmentCost(playerCiv);
            if (!MonumentInvestment.ProcessTick(spire, cost, playerCiv, _clock.CurrentTick)) return;

            spire.InvestedResources.Clear();
            spire.InvestmentEnabled.Clear();

            if (!spire.Built)
            {
                spire.Built = true;
                _state.EventLog.Add(GameEventType.CorruptionSpireBuilt, toast: true);
                OnCorruptionSpireBuilt?.Invoke(this, EventArgs.Empty);

                // Si la Spire repose sur une zone assez corrompue, prévient le joueur que l'évolution
                // en Faille des Abysses est désormais disponible depuis le panneau de la Spire.
                var corruption = _state.Features.OfType<Corruption>().FirstOrDefault(c => c.Position.Equals(spire.Position));
                if (corruption != null && corruption.Level >= AbyssGate.RequiredCorruptionLevel)
                    _state.EventLog.Add(GameEventType.AbyssGateEligible, toast: true);
            }
            else
            {
                spire.Radius++;
                _state.EventLog.Add(GameEventType.CorruptionSpireRadiusUpgraded, spire.Radius.ToString(), toast: true);
                OnCorruptionSpireRadiusUpgraded?.Invoke(this, spire.Radius);
            }

            if (_harvestController != null)
                MonumentInvestment.TryAutoStartInvestment(spire, spire.GetInvestmentCost(playerCiv), playerCiv, _harvestController, _state);
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
                if (tile.TerrainType == TerrainType.Water) continue;

                bool hasOtherFeature = _state.GetFeaturesAt(hex).Any(f => f is not Corruption);
                if (hasOtherFeature) continue;

                result.Add(hex);
            }

            return result;
        }

        /// <summary>Niveau de corruption de l'hex donné (0 si aucune feature Corruption présente).</summary>
        public int GetCorruptionLevel(HexCoord hex)
            => _state?.Features.OfType<Corruption>().FirstOrDefault(f => f.Position.Equals(hex))?.Level ?? 0;

        public CorruptionSpire? PlaceCorruptionSpire(HexCoord position)
        {
            if (_state == null) return null;
            var spire = new CorruptionSpire(position);
            _state.AddFeature(spire);
            _state.EventLog.Add(GameEventType.CorruptionSpirePlaced);
            if (_harvestController != null)
                MonumentInvestment.TryAutoStartInvestment(spire, spire.GetInvestmentCost(_state.PlayerCivilization), _state.PlayerCivilization, _harvestController, _state);
            OnCorruptionSpirePlaced?.Invoke(this, EventArgs.Empty);
            return spire;
        }
    }
}
