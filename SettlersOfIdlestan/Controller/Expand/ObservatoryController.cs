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
    /// <summary>
    /// Monument débloqué par la recherche Observatoire (même palier que les Tours de Guet) : bâti
    /// sur Désert ou Montagne, il fournit un bonus de prestige par niveau et des effets de portée
    /// liés aux Tours de Guet / routes maritimes une fois ces branches de prestige débloquées.
    /// </summary>
    public class ObservatoryController
    {
        private WorldState? _state;
        private GameClock? _clock;

        public const long InvestmentIntervalTicks = MonumentInvestment.IntervalTicks;

        public event EventHandler? OnObservatoryPlaced;
        public event EventHandler<int>? OnObservatoryLevelUp;

        internal ObservatoryController() { }

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
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[ObservatoryController] {nameof(ProcessInvestment)}: {ex}"); }
        }

        public static ResourceSet GetLevelCost(int level) => Observatory.GetLevelCost(level);

        private void ProcessInvestment()
        {
            if (_state == null || _clock == null) return;
            var observatory = _state.Features.OfType<Observatory>().FirstOrDefault();
            if (observatory == null || observatory.IsMaxLevel || observatory.InvestmentEnabled.Count == 0) return;
            if (_clock.CurrentTick - observatory.LastInvestmentTick < InvestmentIntervalTicks) return;

            var playerCiv = _state.PlayerCivilization;
            var cost = observatory.GetInvestmentCost(playerCiv);
            if (!MonumentInvestment.ProcessTick(observatory, cost, playerCiv, _clock.CurrentTick)) return;

            observatory.Level++;
            observatory.InvestedResources.Clear();
            observatory.InvestmentEnabled.Clear();
            _state.EventLog.Add(GameEventType.ObservatoryLevelUp, observatory.Level.ToString(), toast: true);
            OnObservatoryLevelUp?.Invoke(this, observatory.Level);
        }

        public bool HasObservatoryUnlocked(Civilization playerCiv)
            => playerCiv.ModifierAggregator.ApplyModifiers(ECategory.UNLOCK_OBSERVATORY, "", 0) > 0;

        public int GetObservatoryLevel()
            => _state?.Features.OfType<Observatory>().FirstOrDefault()?.Level ?? 0;

        /// <summary>
        /// Observatoire niveau 2 : débloque la construction de Balises Maritimes
        /// (voir MaritimeBeaconController), qui servent d'ancrage côtier artificiel pour prolonger
        /// les routes maritimes en pleine mer une fois routes maritimes débloquées (UNLOCK_MARITIME_ROUTES).
        /// </summary>
        public bool AreMaritimeBeaconsUnlocked() => GetObservatoryLevel() >= 2;

        public bool CanPlaceObservatory(Civilization playerCiv)
        {
            if (!HasObservatoryUnlocked(playerCiv)) return false;
            if (_state?.Features.OfType<Observatory>().Any() == true) return false;
            return true;
        }

        /// <summary>
        /// Hexes de Désert ou de Montagne adjacents aux villes du joueur, sans ville ennemie adjacente.
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
                var tile = _state.GetMapFor(hex)?.GetTile(hex);
                if (tile == null) continue;
                if (tile.TerrainType != TerrainType.Desert && tile.TerrainType != TerrainType.Mountain) continue;
                if (enemyZone.Contains(hex)) continue;
                if (_state.HasFeaturesAt(hex)) continue;
                result.Add(hex);
            }

            return result;
        }

        public Observatory? PlaceObservatory(HexCoord position)
        {
            if (_state == null) return null;
            if (_state.GetMapFor(position) == null) return null;
            var observatory = new Observatory(position);
            _state.AddFeature(observatory);
            _state.EventLog.Add(GameEventType.ObservatoryPlaced);
            OnObservatoryPlaced?.Invoke(this, EventArgs.Empty);
            return observatory;
        }
    }
}
