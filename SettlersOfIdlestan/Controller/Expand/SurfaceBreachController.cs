using SettlersOfIdlestan.Controller.Expand;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandFeatures;
using SettlersOfIdlestan.Model.IslandMap;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SettlersOfIdlestan.Controller.Island
{
    /// <summary>
    /// Gère la Percée de Surface, miroir de la <see cref="DeepestMineController"/> pour les races
    /// démarrant dans l'Inframonde (Elfes noirs — voir RaceDefinition.StartsInUnderworld) :
    /// placement sur une Montagne souterraine, percement par investissement progressif, puis
    /// fondation de la première ville de surface sur le vertex d'arrivée mémorisé à la génération
    /// (LayerState.ArrivalVertex de la couche 0).
    ///
    /// Aucun modifier ne déverrouille la Percée : elle est constructible exactement quand elle a un
    /// sens, c'est-à-dire quand le joueur est enfermé sous terre (aucune ville en surface) et qu'un
    /// vertex d'arrivée de surface a été mémorisé — ce qui n'arrive que pour ces races.
    /// </summary>
    public class SurfaceBreachController
    {
        private WorldState? _state;
        private GameClock? _clock;
        private HarvestController? _harvestController;

        public const long InvestmentIntervalTicks = MonumentInvestment.IntervalTicks;

        public event EventHandler? OnSurfaceBreachPlaced;
        public event EventHandler? OnSurfaceBreachDug;

        internal SurfaceBreachController() { }

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
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[SurfaceBreachController] {nameof(ProcessInvestment)}: {ex}"); }
            try { TryEstablishSurface(); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[SurfaceBreachController] {nameof(TryEstablishSurface)}: {ex}"); }
        }

        private void ProcessInvestment()
        {
            if (_state == null || _clock == null) return;
            var breach = _state.Features.OfType<SurfaceBreach>().FirstOrDefault();
            if (breach == null || breach.Dug || breach.InvestmentEnabled.Count == 0) return;
            if (_clock.CurrentTick - breach.LastInvestmentTick < InvestmentIntervalTicks) return;

            var playerCiv = _state.PlayerCivilization;
            var cost = breach.GetInvestmentCost(playerCiv);
            if (!MonumentInvestment.ProcessTick(breach, cost, playerCiv, _clock.CurrentTick)) return;

            breach.Dug = true;
            breach.WasEverDug = true;
            breach.InvestmentEnabled.Clear();
            _state.EventLog.Add(GameEventType.SurfaceBreachDug);
            OnSurfaceBreachDug?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Fonde la première ville de surface une fois la Percée ouverte. Comme pour l'Inframonde, on
        /// teste la présence d'une ville du joueur en surface plutôt que l'existence de la couche :
        /// celle-ci existe depuis la génération, simplement inhabitée.
        /// </summary>
        private void TryEstablishSurface()
        {
            if (_state == null) return;

            var playerCiv = _state.PlayerCivilization;
            if (playerCiv.Cities.Any(c => c.Position.Z == IslandMap.SurfaceLayer)) return;

            if (!_state.Features.OfType<SurfaceBreach>().Any(b => b.Dug)) return;

            var arrivalVertex = GetSurfaceArrivalVertex();
            if (arrivalVertex == null) return;

            // Le Site d'Arrivée tenait la place depuis la génération : il la cède maintenant à la
            // ville. Le retirer avant d'ajouter la ville évite que les deux se comptent en double
            // dans les vérifications d'occupation (voir Civilization.BuildVertices).
            ReleaseLandingSite(playerCiv, arrivalVertex);

            var city = new City(arrivalVertex) { CivilizationIndex = playerCiv.Index };
            city.AddBuilding(new TownHall { Level = 1 });
            city.InvalidateLevelCache();
            playerCiv.AddCity(city);

            _state.CurrentViewedLayer = IslandMap.SurfaceLayer;
            _state.Visibility.RecalculateFor(playerCiv.Index);
        }

        private static void ReleaseLandingSite(Civilization playerCiv, Vertex vertex)
        {
            var site = playerCiv.LandingSites.FirstOrDefault(s => s.Position.Equals(vertex));
            if (site != null) playerCiv.RemoveLandingSite(site);
        }

        /// <summary>
        /// Réserve à nouveau le point de chute (voir <see cref="LandingSite"/>), après une perte de
        /// la surface. Sans quoi la place serait libre pendant tout le temps qu'il faut pour
        /// re-creuser la Percée — exactement la fenêtre où une ville adverse s'y installerait.
        /// </summary>
        private static void ReserveLandingSite(Civilization playerCiv, Vertex vertex)
        {
            if (playerCiv.LandingSites.Any(s => s.Position.Equals(vertex))) return;
            playerCiv.AddLandingSite(new LandingSite(vertex) { CivilizationIndex = playerCiv.Index });
        }

        /// <summary>
        /// Vertex d'arrivée en surface mémorisé par le générateur (voir
        /// IslandMapGenerator.GenerateWorldState) : bord d'île, terrain racial + Forêt + Eau. Il
        /// survit à la perte de la surface, pour que la Percée puisse être rouverte au même endroit.
        /// </summary>
        private Vertex? GetSurfaceArrivalVertex()
            => _state != null && _state.Layers.TryGetValue(IslandMap.SurfaceLayer, out var layer)
                ? layer.ArrivalVertex
                : null;


        /// <summary>
        /// À appeler lorsqu'une ville du joueur est détruite. Si c'était la dernière ville de surface,
        /// la Percée retombe à 50 % d'investissement et la surface redevient inaccessible — miroir de
        /// DeepestMineController.OnCityDestroyed. Le vertex d'arrivée, lui, reste mémorisé.
        /// </summary>
        public void OnCityDestroyed(Vertex cityVertex, int civilizationIndex)
        {
            if (_state == null) return;
            var playerCiv = _state.PlayerCivilization;
            if (civilizationIndex != playerCiv.Index) return;
            if (cityVertex.Z != IslandMap.SurfaceLayer) return;

            // La ville a déjà été retirée : vérifie s'il en reste en surface
            if (playerCiv.Cities.Any(c => c.Position.Z == IslandMap.SurfaceLayer)) return;

            ResetSurfaceAfterLastCityDestroyed();
        }

        private void ResetSurfaceAfterLastCityDestroyed()
        {
            if (_state == null) return;
            var breach = _state.Features.OfType<SurfaceBreach>().FirstOrDefault();
            if (breach == null || !breach.Dug) return;

            // Contrairement à l'Inframonde, la carte de surface n'est pas détruite : elle porte les
            // civilisations NPC et tout le contenu de l'île, qui continuent d'exister sans le joueur.
            // Seules disparaissent les routes de surface du joueur, devenues orphelines.
            _state.PlayerCivilization.RemoveAllRoads(r => r.Position.Z == IslandMap.SurfaceLayer);

            var arrivalVertex = GetSurfaceArrivalVertex();
            if (arrivalVertex != null)
                ReserveLandingSite(_state.PlayerCivilization, arrivalVertex);

            _state.CurrentViewedLayer = LayerState.UnderworldZ;

            breach.Dug = false;
            breach.InvestmentEnabled.Clear();
            breach.InvestedResources.Clear();
            breach.CompletedInvestmentCost.Clear();
            var cost = breach.GetInvestmentCost(_state.PlayerCivilization);
            foreach (var kvp in cost)
                breach.InvestedResources[kvp.Key] = kvp.Value / 2;
            if (_harvestController != null)
                MonumentInvestment.TryAutoStartInvestment(breach, cost, _state.PlayerCivilization, _harvestController, _state);

            _state.EventLog.Add(GameEventType.SurfaceLost);
            _state.Visibility.Recalculate();
        }

        /// <summary>
        /// La Percée n'a de sens que pour un joueur enfermé sous terre : aucune ville en surface, mais
        /// un vertex d'arrivée déjà mémorisé (départ souterrain, ou surface perdue depuis).
        /// </summary>
        public bool HasSurfaceBreachUnlocked(Civilization playerCiv)
            => GetSurfaceArrivalVertex() != null
               && !playerCiv.Cities.Any(c => c.Position.Z == IslandMap.SurfaceLayer);

        /// <summary>
        /// Nombre de villes souterraines exigé avant de pouvoir ouvrir le chantier de la Percée.
        ///
        /// <para>La Percée est un Monument : elle stérilise la récolte de son hexagone
        /// (<see cref="Model.IslandFeatures.Monument.BlocksHarvest"/>), et elle ne se pose que sur une
        /// Montagne. Or le triangle de départ d'une race souterraine n'en contient qu'une seule
        /// (RaceDefinition.UnderworldStartTerrains), donc la poser dès la première ville coupe la
        /// seule source de Pierre de la partie — et les routes de l'Inframonde coûtent justement de la
        /// Pierre et du Minerai, ce qui gèle toute expansion pour de bon. Mesuré au race gauntlet :
        /// Elfe noir bloqué à 1 ville, 0 route posée, 0 Pierre, sur 20 h simulées. Attendre quatre
        /// villes garantit que d'autres Montagnes ont été révélées avant d'en sacrifier une (voir
        /// aussi <see cref="MonumentInvestment.OrderByLeastSacrifice"/>, qui choisit alors la moins
        /// coûteuse).</para>
        /// </summary>
        public const int MinCitiesToPlace = 4;

        public bool CanPlaceSurfaceBreach(Civilization playerCiv)
        {
            if (!HasSurfaceBreachUnlocked(playerCiv)) return false;
            if (playerCiv.Cities.Count < MinCitiesToPlace) return false;
            if (_state?.Features.OfType<SurfaceBreach>().Any() == true) return false;
            return true;
        }

        /// <summary>
        /// Hexes de Montagne de l'Inframonde, adjacents à une ville du joueur,
        /// sans ville ennemie adjacente et sans autre feature — du moins au plus coûteux à sacrifier
        /// (voir <see cref="MonumentInvestment.OrderByLeastSacrifice"/>).
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
                if (hex.Z != LayerState.UnderworldZ) continue;
                var tile = _state.GetMapFor(hex)?.GetTile(hex);
                if (tile == null) continue;
                if (tile.TerrainType != TerrainType.Mountain) continue;
                if (enemyZone.Contains(hex)) continue;
                if (_state.HasFeaturesAt(hex)) continue;
                result.Add(hex);
            }

            return MonumentInvestment.OrderByLeastSacrifice(result, playerCiv, _state);
        }

        public SurfaceBreach? PlaceSurfaceBreach(HexCoord position)
        {
            if (_state == null) return null;
            var breach = new SurfaceBreach(position);
            // Amorce le cooldown d'investissement sur le tick de pose (voir WonderController.PlaceWonder).
            breach.LastInvestmentTick = _clock?.CurrentTick ?? 0;
            _state.AddFeature(breach);
            _state.EventLog.Add(GameEventType.SurfaceBreachPlaced);
            if (_harvestController != null)
                MonumentInvestment.TryAutoStartInvestment(breach, breach.GetInvestmentCost(_state.PlayerCivilization), _state.PlayerCivilization, _harvestController, _state);
            OnSurfaceBreachPlaced?.Invoke(this, EventArgs.Empty);
            return breach;
        }
    }
}
