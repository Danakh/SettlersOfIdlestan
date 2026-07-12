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
    /// Monument débloqué par la recherche Grand Phare (même palier que les Tours de Guet) : bâti
    /// sur un hex côtier (terre adjacente à de l'eau), il fournit un bonus de prestige par niveau
    /// et des effets de portée liés aux Tours de Guet / routes maritimes une fois ces branches de
    /// prestige débloquées.
    /// </summary>
    public class GreatLighthouseController
    {
        private WorldState? _state;
        private GameClock? _clock;

        public const long InvestmentIntervalTicks = MonumentInvestment.IntervalTicks;

        public event EventHandler? OnGreatLighthousePlaced;
        public event EventHandler<int>? OnGreatLighthouseLevelUp;

        internal GreatLighthouseController() { }

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
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[GreatLighthouseController] {nameof(ProcessInvestment)}: {ex}"); }
        }

        public static ResourceSet GetLevelCost(int level) => GreatLighthouse.GetLevelCost(level);

        private void ProcessInvestment()
        {
            if (_state == null || _clock == null) return;
            var greatLighthouse = _state.Features.OfType<GreatLighthouse>().FirstOrDefault();
            if (greatLighthouse == null || greatLighthouse.IsMaxLevel || greatLighthouse.InvestmentEnabled.Count == 0) return;
            if (_clock.CurrentTick - greatLighthouse.LastInvestmentTick < InvestmentIntervalTicks) return;

            var playerCiv = _state.PlayerCivilization;
            var cost = greatLighthouse.GetInvestmentCost(playerCiv);
            if (!MonumentInvestment.ProcessTick(greatLighthouse, cost, playerCiv, _clock.CurrentTick)) return;

            greatLighthouse.Level++;
            greatLighthouse.InvestedResources.Clear();
            greatLighthouse.InvestmentEnabled.Clear();
            _state.EventLog.Add(GameEventType.GreatLighthouseLevelUp, greatLighthouse.Level.ToString(), toast: true);
            OnGreatLighthouseLevelUp?.Invoke(this, greatLighthouse.Level);
        }

        public bool HasGreatLighthouseUnlocked(Civilization playerCiv)
            => playerCiv.ModifierAggregator.ApplyModifiers(ECategory.UNLOCK_GREAT_LIGHTHOUSE, "", 0) > 0;

        public int GetGreatLighthouseLevel()
            => _state?.Features.OfType<GreatLighthouse>().FirstOrDefault()?.Level ?? 0;

        /// <summary>
        /// Grand Phare niveau 2 : débloque la construction de Balises Maritimes
        /// (voir MaritimeBeaconController), qui servent d'ancrage côtier artificiel pour prolonger
        /// les routes maritimes en pleine mer une fois routes maritimes débloquées (UNLOCK_MARITIME_ROUTES).
        /// </summary>
        public bool AreMaritimeBeaconsUnlocked() => GetGreatLighthouseLevel() >= 2;

        public bool CanPlaceGreatLighthouse(Civilization playerCiv)
        {
            if (!HasGreatLighthouseUnlocked(playerCiv)) return false;
            if (_state?.Features.OfType<GreatLighthouse>().Any() == true) return false;
            return true;
        }

        /// <summary>
        /// Hexes côtiers (terre adjacente à de l'eau) adjacents aux villes du joueur, sans ville
        /// ennemie adjacente.
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
                var map = _state.GetMapFor(hex);
                var tile = map?.GetTile(hex);
                if (tile == null || map == null) continue;
                if (tile.TerrainType.IsWater()) continue;
                bool isCoastal = hex.Neighbors().Any(n => map.GetTile(n)?.TerrainType.IsWater() == true);
                if (!isCoastal) continue;
                if (enemyZone.Contains(hex)) continue;
                if (_state.HasFeaturesAt(hex)) continue;
                result.Add(hex);
            }

            return result;
        }

        public GreatLighthouse? PlaceGreatLighthouse(HexCoord position)
        {
            if (_state == null) return null;
            if (_state.GetMapFor(position) == null) return null;
            var greatLighthouse = new GreatLighthouse(position);
            _state.AddFeature(greatLighthouse);
            _state.EventLog.Add(GameEventType.GreatLighthousePlaced);
            OnGreatLighthousePlaced?.Invoke(this, EventArgs.Empty);
            return greatLighthouse;
        }
    }
}
