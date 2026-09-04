using SettlersOfIdlestan.Controller.Expand;
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
    /// Squelette commun à tous les contrôleurs de Monument (Merveille, Grand Phare, Observatoire,
    /// Nécropole, Mine Profonde, Percée de Surface, Spire de Corruption, Faille des Abysses, Portail
    /// du Pandémonium) : abonnement à l'horloge, cycle d'investissement par tick, pose de la feature
    /// et liste des hexes recevables autour des villes du joueur.
    ///
    /// <para>Les dix contrôleurs étaient des copies quasi conformes les uns des autres ; seuls les
    /// points réellement variables sont restés, sous forme de membres abstraits ou virtuels. Le
    /// comportement propre à un monument (ouverture d'une couche, creusement, rayon, purification,
    /// réaction à la destruction d'une ville) reste entièrement dans la classe concrète.</para>
    ///
    /// <para><see cref="DivineBonesController"/> est volontairement hors de cette hiérarchie : il
    /// traite toutes les features Os Divins de la carte à chaque tick, pas un monument unique, et
    /// n'a ni pose, ni prédicat de déblocage, ni liste d'hexes recevables.</para>
    /// </summary>
    /// <typeparam name="TFeature">Type concret de la feature Monument pilotée par le contrôleur.</typeparam>
    public abstract class MonumentControllerBase<TFeature> where TFeature : Monument
    {
        protected WorldState? _state;
        protected GameClock? _clock;
        protected HarvestController? _harvestController;

        /// <summary>
        /// Câblage commun appelé par le <c>Initialize</c> de chaque contrôleur concret — qui garde sa
        /// propre signature, certains ayant besoin d'états supplémentaires (GodState, GamePRNG,
        /// PrestigeState). Le désabonnement préalable est indispensable : <c>Initialize</c> est
        /// rappelé à chaque changement d'île, et sans lui le contrôleur traiterait un cycle
        /// d'investissement par abonnement accumulé.
        /// </summary>
        protected void InitializeCore(WorldState? state, GameClock? clock, HarvestController? harvestController)
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
            // Le nom de source loggué est celui du type concret (GetType().Name), pas celui de la
            // base : c'est le contrôleur fautif que l'on veut lire dans le journal.
            try { ProcessInvestment(); }
            catch (Exception ex) { GameLog.Error(GetType().Name, nameof(ProcessInvestment), ex); }
            OnClockAdvancedExtra();
        }

        /// <summary>
        /// Travail supplémentaire à faire à chaque tick, après le cycle d'investissement (ouverture
        /// d'une couche, fondation d'une ville…). La redéfinition porte son propre try/catch, pour
        /// que le nom de méthode loggué soit le sien.
        /// </summary>
        protected virtual void OnClockAdvancedExtra() { }

        /// <summary>Le monument piloté, ou null s'il n'est pas encore posé.</summary>
        protected TFeature? FindFeature() => _state?.GetFirstFeature<TFeature>();

        /// <summary>
        /// Un cycle d'investissement : prélève les ressources dues et, si l'objectif courant est
        /// entièrement couvert, franchit le palier (voir <see cref="OnInvestmentCycleCompleted"/>).
        /// </summary>
        private void ProcessInvestment()
        {
            if (_state == null || _clock == null) return;
            var monument = FindFeature();
            if (monument == null || IsInvestmentComplete(monument)) return;

            // Pas de garde sur InvestmentEnabled ici (contrairement à l'ancien code) : la retirer
            // ferait geler LastInvestmentTick pendant que l'investissement est désactivé, et le
            // réactiver des heures plus tard rattraperait d'un coup tous les cycles "manqués" durant
            // la pause — même bug que celui corrigé sur AutoExtendController.TrySpawnBorderMonsters,
            // mais ici résolu en appelant ProcessTick à chaque tick : son cooldown interne
            // (TickCooldown) ne coûte rien tant qu'il n'est pas écoulé, et sa boucle d'investissement
            // est déjà un no-op si InvestmentEnabled est vide.
            var playerCiv = _state.PlayerCivilization;
            long now = _clock.CurrentTick;

            var cost = monument.GetInvestmentCost(playerCiv);
            bool resourcesDone = MonumentInvestment.ProcessTick(monument, cost, playerCiv, now);
            // Les axes supplémentaires sont traités même quand les ressources ne suffisent pas
            // encore : leur propre cooldown doit avancer au même rythme, sans quoi ils rattraperaient
            // eux aussi tous les cycles manqués une fois les ressources complétées.
            bool extraAxesDone = ProcessExtraInvestmentAxes(monument, playerCiv, now);
            if (!resourcesDone || !extraAxesDone) return;

            OnInvestmentCycleCompleted(monument, playerCiv);
        }

        /// <summary>
        /// True quand le monument n'a plus rien à recevoir (niveau maximum atteint, creusé, bâti) :
        /// <see cref="ProcessInvestment"/> s'arrête alors avant tout prélèvement.
        /// </summary>
        protected abstract bool IsInvestmentComplete(TFeature monument);

        /// <summary>
        /// Axes d'investissement supplémentaires à celui des ressources (points de recherche de
        /// l'Observatoire). Retourne true quand ils sont tous couverts — donc true par défaut, quand
        /// il n'y en a aucun.
        /// </summary>
        protected virtual bool ProcessExtraInvestmentAxes(TFeature monument, Civilization playerCiv, long now) => true;

        /// <summary>
        /// Objectif courant entièrement couvert : effets de complétion propres au monument
        /// (montée de niveau, creusement, construction…).
        /// </summary>
        protected abstract void OnInvestmentCycleCompleted(TFeature monument, Civilization playerCiv);

        /// <summary>
        /// Remises à zéro d'un palier franchi : nouvel objectif, donc plus rien d'investi ni de
        /// complétion à comparer (voir <see cref="Monument.CompletedInvestmentCost"/>), et
        /// investissement automatique à re-souscrire.
        /// </summary>
        protected static void ResetInvestment(TFeature monument)
        {
            monument.InvestedResources.Clear();
            monument.CompletedInvestmentCost.Clear();
            monument.InvestmentEnabled.Clear();
        }

        /// <summary>
        /// Remises à zéro des axes supplémentaires (voir <see cref="ProcessExtraInvestmentAxes"/>),
        /// appliquées juste après <see cref="ResetInvestment"/> lors d'une montée de niveau.
        /// </summary>
        protected virtual void ResetExtraInvestmentAxes(TFeature monument) { }

        /// <summary>
        /// Fin commune d'une montée de niveau, pour les monuments à niveaux (Merveille, Grand Phare,
        /// Observatoire, Nécropole) : remises à zéro, entrée de journal et relance de
        /// l'investissement automatique vers le niveau suivant. L'incrément du niveau et le
        /// déclenchement de l'événement public restent à l'appelant — <c>Level</c> et
        /// <c>IsMaxLevel</c> sont déclarés sur chaque feature concrète, pas sur
        /// <see cref="Monument"/>.
        /// </summary>
        protected void CompleteLevelUp(TFeature monument, Civilization playerCiv, int newLevel, bool isMaxLevel, GameEventType levelUpEvent)
        {
            ResetInvestment(monument);
            ResetExtraInvestmentAxes(monument);
            _state!.EventLog.Add(levelUpEvent, newLevel.ToString(), toast: true);
            if (_harvestController != null && !isMaxLevel)
                MonumentInvestment.TryAutoStartInvestment(monument, monument.GetInvestmentCost(playerCiv), playerCiv, _harvestController, _state);
        }

        /// <summary>Fabrique la feature à la position donnée.</summary>
        protected abstract TFeature CreateFeature(HexCoord position);

        /// <summary>Événement de journal émis à la pose.</summary>
        protected abstract GameEventType PlacedEventType { get; }

        /// <summary>True si la pose doit remonter un toast (seul le Portail du Pandémonium le fait).</summary>
        protected virtual bool PlacedEventIsToast => false;

        /// <summary>
        /// Amorçage des cooldowns des axes supplémentaires à la pose (voir
        /// <see cref="ProcessExtraInvestmentAxes"/>), pour la même raison que
        /// <see cref="Monument.LastInvestmentTick"/>.
        /// </summary>
        protected virtual void PrimeExtraInvestmentAxesOnPlacement(TFeature monument) { }

        /// <summary>Déclenche l'événement public de pose, déclaré sur le contrôleur concret.</summary>
        protected abstract void RaisePlaced();

        /// <summary>
        /// Pose la feature à la position donnée, journalise et démarre l'investissement automatique.
        /// </summary>
        protected TFeature? PlaceMonument(HexCoord position)
        {
            if (_state == null) return null;
            if (_state.GetMapFor(position) == null) return null;
            var monument = CreateFeature(position);
            // Amorce le cooldown d'investissement sur le tick de pose plutôt que de laisser la valeur
            // par défaut à 0 : sans ça, ProcessTick voit un écart énorme dès le premier cycle
            // (now - 0) et rattrape d'un coup tous les cycles "manqués" depuis le tick 0 de la partie,
            // ce qui vide le stock de ressources d'un coup au lieu de démarrer progressivement (bug
            // vécu après un prestige, où le tick courant est déjà élevé au moment de la pose).
            monument.LastInvestmentTick = _clock?.CurrentTick ?? 0;
            PrimeExtraInvestmentAxesOnPlacement(monument);
            _state.AddFeature(monument);
            _state.EventLog.Add(PlacedEventType, toast: PlacedEventIsToast);
            if (_harvestController != null)
                MonumentInvestment.TryAutoStartInvestment(monument, monument.GetInvestmentCost(_state.PlayerCivilization), _state.PlayerCivilization, _harvestController, _state);
            RaisePlaced();
            return monument;
        }

        /// <summary>
        /// Restriction de couche du monument (surface, Inframonde…). Vrai partout par défaut.
        /// </summary>
        protected virtual bool IsPlacementLayerAllowed(HexCoord hex) => true;

        /// <summary>Terrain recevable pour ce monument (Montagne, hors Eau, côtier…).</summary>
        protected virtual bool IsPlacementTerrainAllowed(HexTile tile, IslandMap map, HexCoord hex) => true;

        /// <summary>
        /// Features déjà présentes qui interdisent la pose : celles qui bloquent tout monument (voir
        /// <see cref="WorldState.HasMonumentBlockingFeaturesAt"/>). Corruption et Dominion se
        /// superposent au terrain sans l'occuper et ne bloquent donc aucune pose.
        /// </summary>
        private bool IsPlacementBlockedByFeatures(HexCoord hex) => _state!.HasMonumentBlockingFeaturesAt(hex);

        /// <summary>
        /// Hexes adjacents aux vertex de ville du joueur, sans ville ennemie adjacente, recevables
        /// selon la couche, le terrain et les features déjà posées, ordonnés du moins au plus
        /// coûteux à sacrifier (voir <see cref="MonumentInvestment.OrderByLeastSacrifice"/>).
        /// </summary>
        protected List<HexCoord> GetPlaceableHexesAroundPlayerCities()
        {
            if (_state == null) return new List<HexCoord>();

            var playerCiv = _state.PlayerCivilization;

            // Hexes qui font partie d'un vertex de ville du joueur
            var playerCityHexes = new HashSet<HexCoord>();
            foreach (var city in playerCiv.Cities)
                foreach (var hex in city.Position.GetHexes())
                    playerCityHexes.Add(hex);

            // Hexes qui font partie d'un vertex de ville PNJ, ou qui lui sont adjacents
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
                if (!IsPlacementLayerAllowed(hex)) continue;
                var map = _state.GetMapFor(hex);
                var tile = map?.GetTile(hex);
                if (map == null || tile == null) continue;
                if (!IsPlacementTerrainAllowed(tile, map, hex)) continue;
                if (enemyZone.Contains(hex)) continue;
                if (IsPlacementBlockedByFeatures(hex)) continue;
                result.Add(hex);
            }

            return MonumentInvestment.OrderByLeastSacrifice(result, playerCiv, _state);
        }
    }
}
