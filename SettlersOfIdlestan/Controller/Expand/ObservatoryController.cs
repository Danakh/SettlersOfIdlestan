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
    /// Monument débloqué par la recherche Cartes des Étoiles : bâti sur une Montagne de la surface,
    /// il monte de niveau par investissement progressif (Verre/Acier/Mithril + points de recherche,
    /// voir <see cref="Observatory"/>). Chaque niveau abaisse le multiplicateur exponentiel du coût
    /// en points de recherche des routes du Vide, de ×3 à ×2.4 une fois l'Observatoire complet
    /// (voir RoadController.GetVoidRouteResearchCost).
    /// </summary>
    public class ObservatoryController
    {
        private WorldState? _state;
        private GameClock? _clock;
        private HarvestController? _harvestController;

        public const long InvestmentIntervalTicks = MonumentInvestment.IntervalTicks;

        public event EventHandler? OnObservatoryPlaced;
        public event EventHandler<int>? OnObservatoryLevelUp;

        internal ObservatoryController() { }

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
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[ObservatoryController] {nameof(ProcessInvestment)}: {ex}"); }
        }

        public static ResourceSet GetLevelCost(int level) => Observatory.GetLevelCost(level);

        public static long GetLevelResearchCost(int level) => Observatory.GetLevelResearchCost(level);

        private void ProcessInvestment()
        {
            if (_state == null || _clock == null) return;
            var observatory = _state.Features.OfType<Observatory>().FirstOrDefault();
            if (observatory == null || observatory.IsMaxLevel) return;

            var playerCiv = _state.PlayerCivilization;
            long now = _clock.CurrentTick;

            var cost = observatory.GetInvestmentCost(playerCiv);
            bool resourcesDone = MonumentInvestment.ProcessTick(observatory, cost, playerCiv, now);
            bool researchDone = MonumentInvestment.ProcessResearchTick(observatory, observatory.GetRequiredResearch(playerCiv), playerCiv, now);
            if (!resourcesDone || !researchDone) return;

            observatory.Level++;
            observatory.InvestedResources.Clear();
            observatory.InvestmentEnabled.Clear();
            observatory.InvestedResearch = 0;
            observatory.ResearchInvestmentEnabled = false;
            _state.EventLog.Add(GameEventType.ObservatoryLevelUp, observatory.Level.ToString(), toast: true);
            if (_harvestController != null && !observatory.IsMaxLevel)
                MonumentInvestment.TryAutoStartInvestment(observatory, observatory.GetInvestmentCost(playerCiv), playerCiv, _harvestController, _state);
            OnObservatoryLevelUp?.Invoke(this, observatory.Level);
        }

        public bool HasObservatoryUnlocked(Civilization playerCiv)
            => playerCiv.ModifierAggregator.HasModifier(ECategory.UNLOCK_OBSERVATORY);

        public int GetObservatoryLevel()
            => _state?.Features.OfType<Observatory>().FirstOrDefault()?.Level ?? 0;

        public bool CanPlaceObservatory(Civilization playerCiv)
        {
            if (!HasObservatoryUnlocked(playerCiv)) return false;
            if (_state?.Features.OfType<Observatory>().Any() == true) return false;
            return true;
        }

        /// <summary>
        /// Hexes de Montagne en surface, adjacents à une ville du joueur, sans ville ennemie
        /// adjacente et sans autre feature — mêmes règles que la Mine Profonde, ordre du moins au
        /// plus coûteux à sacrifier compris (voir <see cref="MonumentInvestment.OrderByLeastSacrifice"/>).
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
                var tile = _state.GetMapFor(hex)?.GetTile(hex);
                if (tile == null) continue;
                if (tile.TerrainType != TerrainType.Mountain) continue;
                if (enemyZone.Contains(hex)) continue;
                if (_state.HasMonumentBlockingFeaturesAt(hex)) continue;
                result.Add(hex);
            }

            return MonumentInvestment.OrderByLeastSacrifice(result, playerCiv, _state);
        }

        public Observatory? PlaceObservatory(HexCoord position)
        {
            if (_state == null) return null;
            if (_state.GetMapFor(position) == null) return null;
            var observatory = new Observatory(position);
            _state.AddFeature(observatory);
            _state.EventLog.Add(GameEventType.ObservatoryPlaced);
            if (_harvestController != null)
                MonumentInvestment.TryAutoStartInvestment(observatory, observatory.GetInvestmentCost(_state.PlayerCivilization), _state.PlayerCivilization, _harvestController, _state);
            OnObservatoryPlaced?.Invoke(this, EventArgs.Empty);
            return observatory;
        }
    }
}
