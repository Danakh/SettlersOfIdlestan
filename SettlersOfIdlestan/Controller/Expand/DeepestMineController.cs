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
    /// Gère la Mine Profonde : placement (comme tout Monument, uniquement sur Montagne),
    /// creusement par investissement progressif (1000 Acier entre autres), puis ouverture
    /// de l'avant-poste dans l'Inframonde.
    /// </summary>
    public class DeepestMineController
    {
        private WorldState? _state;
        private GameClock? _clock;

        public const long InvestmentIntervalTicks = MonumentInvestment.IntervalTicks;

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
            if (_clock.CurrentTick - mine.LastInvestmentTick < InvestmentIntervalTicks) return;

            var playerCiv = _state.PlayerCivilization;
            var cost = mine.GetInvestmentCost(playerCiv);
            if (!MonumentInvestment.ProcessTick(mine, cost, playerCiv, _clock.CurrentTick)) return;

            mine.Dug = true;
            mine.WasEverDug = true;
            mine.InvestmentEnabled.Clear();
            _state.EventLog.Add(GameEventType.DeepestMineDug);
            OnDeepestMineDug?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Ouvre l'Inframonde si la Mine Profonde est creusée (feature) ou si une ancienne
        /// sauvegarde contient le bâtiment legacy Mine Profonde.
        /// La couche peut déjà exister (vide) après une perte de l'Inframonde : on teste
        /// la présence d'un avant-poste joueur plutôt que l'existence de la couche.
        /// </summary>
        private void TryInitializeUnderworld()
        {
            if (_state == null) return;

            var playerCiv = _state.PlayerCivilization;

            // Déjà un avant-poste joueur dans l'Inframonde → rien à faire
            if (playerCiv.Cities.Any(c => c.Position.Z == LayerState.UnderworldZ)) return;

            bool hasDugMine = _state.Features.OfType<DeepestMine>().Any(m => m.Dug);
            bool hasLegacyBuilding = playerCiv.Cities.Any(city =>
                city.Buildings.Any(b => b.Type == Model.Buildings.BuildingType.DeepestMine && b.Level > 0));

            if (!hasDugMine && !hasLegacyBuilding) return;

            var underworldLayer = LayerState.EstablishOupostInNewAutoExpandLayer(playerCiv);
            _state.AddLayer(LayerState.UnderworldZ, underworldLayer);
            _state.Visibility.RecalculateFor(playerCiv.Index);
        }

        /// <summary>
        /// À appeler lorsqu'une ville du joueur est détruite.
        /// Si c'était la dernière ville dans l'Inframonde, réinitialise la mine à 50 % d'investissement.
        /// </summary>
        public void OnCityDestroyed(Vertex cityVertex, int civilizationIndex)
        {
            if (_state == null) return;
            var playerCiv = _state.PlayerCivilization;
            if (civilizationIndex != playerCiv.Index) return;
            if (cityVertex.Z != LayerState.UnderworldZ) return;

            // La ville a déjà été retirée : vérifie s'il en reste dans l'Inframonde
            if (playerCiv.Cities.Any(c => c.Position.Z == LayerState.UnderworldZ)) return;

            ResetUnderworldAfterLastCityDestroyed();
        }

        private void ResetUnderworldAfterLastCityDestroyed()
        {
            if (_state == null) return;
            var mine = _state.Features.OfType<DeepestMine>().FirstOrDefault();
            if (mine == null) return;

            // Remplace la couche par une map vide sans la supprimer :
            // les features dont Position.Z == UnderworldZ restent valides pour GetMapFor,
            // mais trouvent une carte sans tuiles (elles deviennent invisibles).
            // Le Z doit être explicitement UnderworldZ : IslandMap(empty) defaulte à Z=0.
            _state.AddLayer(LayerState.UnderworldZ,
                new LayerState(new IslandMap(System.Array.Empty<HexTile>(), LayerState.UnderworldZ)));

            // Retire les features orphelines de l'ancienne couche
            foreach (var feature in _state.Features.Where(f => f.Position.Z == LayerState.UnderworldZ).ToList())
                _state.RemoveFeature(feature);

            // Nettoie les routes de l'Inframonde pour toutes les civilisations
            foreach (var civ in _state.Civilizations)
                civ.RemoveAllRoads(r => r.Position.Z == LayerState.UnderworldZ);

            // Retire les civilisations NPC dont toutes les villes étaient dans l'Inframonde
            _state.Civilizations.RemoveAll(c =>
                c.Index != _state.PlayerCivilization.Index
                && c.Cities.Count > 0
                && c.Cities.All(city => city.Position.Z == LayerState.UnderworldZ));

            // Revient sur la surface si le joueur regardait l'Inframonde
            _state.CurrentViewedLayer = IslandMap.SurfaceLayer;

            // Remet la mine à 50 % du coût total pour permettre un nouveau creusement
            mine.Dug = false;
            mine.InvestmentEnabled.Clear();
            mine.InvestedResources.Clear();
            var cost = mine.GetInvestmentCost(_state.PlayerCivilization);
            foreach (var kvp in cost)
                mine.InvestedResources[kvp.Key] = kvp.Value / 2;

            _state.EventLog.Add(GameEventType.UnderworldLost);
            _state.Visibility.Recalculate();
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
                var tile = _state.GetMapFor(hex)?.GetTile(hex);
                if (tile == null) continue;
                if (tile.TerrainType != TerrainType.Mountain) continue;
                if (enemyZone.Contains(hex)) continue;
                if (_state.HasFeaturesAt(hex)) continue;
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
