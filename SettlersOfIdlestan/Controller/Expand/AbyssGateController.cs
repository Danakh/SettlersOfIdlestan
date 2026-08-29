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
    /// bâtie et une zone de Corruption de niveau <see cref="AbyssGate.RequiredCorruptionLevel"/> ou plus
    /// entièrement nettoyée <b>sur l'île courante</b> (<see cref="Model.Tasks.RunRecord.MaxCorruptionLevelCleared"/>,
    /// voir <see cref="IsAbyssGateEligible"/>). Ce record est propre au run — n'importe quel hex compte,
    /// nettoyé par n'importe quel mécanisme (Temple, débordement, annulation par le Dominion,
    /// décroissance de monument), pas seulement l'hex de la Spire elle-même. N'est pas une action de
    /// civilisation — l'évolution remplace la Spire sur son hex et se construit par investissement
    /// progressif comme tout Monument.
    /// </summary>
    public class AbyssGateController
    {
        private WorldState? _state;
        private GameClock? _clock;
        private HarvestController? _harvestController;
        private GodState? _godState;

        public const long InvestmentIntervalTicks = MonumentInvestment.IntervalTicks;

        public event EventHandler? OnAbyssGatePlaced;
        public event EventHandler? OnAbyssGateBuilt;

        internal AbyssGateController() { }

        internal void Initialize(WorldState? state, GameClock? clock = null, HarvestController? harvestController = null, GodState? godState = null)
        {
            if (_clock != null)
                _clock.Advanced -= OnClockAdvanced;

            _state = state;
            _clock = clock;
            _harvestController = harvestController;
            _godState = godState;

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
            gate.WasEverBuilt = true;
            gate.InvestmentEnabled.Clear();
            _state.EventLog.Add(GameEventType.AbyssGateBuilt, toast: true);
            OnAbyssGateBuilt?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// True si une Spire de Corruption est bâtie, qu'aucune Faille des Abysses n'existe déjà, et
        /// que <see cref="Model.Tasks.RunRecord.MaxCorruptionLevelCleared"/> a atteint
        /// <see cref="AbyssGate.RequiredCorruptionLevel"/> ou plus. Ce record est mis à jour par
        /// <see cref="Controller.Island.CorruptionController.ReduceLevel"/> à chaque zone de Corruption
        /// entièrement dissipée, n'importe où sur la carte et par n'importe quel mécanisme (Temple,
        /// débordement — y compris annulation par le Dominion — ou décroissance de monument) : ce n'est
        /// pas la corruption courante (ni même le pic) du seul hex de la Spire qui compte, mais le
        /// meilleur nettoyage réalisé sur l'île courante. Se base sur un nettoyage passé plutôt que sur
        /// une corruption "en cours" précisément parce que la Spire bâtie réduit systématiquement la
        /// corruption sur son propre hex (voir CorruptionController.ProcessMonumentCorruptionDecay) —
        /// une condition sur le niveau courant de cet hex précis se démentirait presque aussitôt vérifiée.
        /// Volontairement basé sur le <b>record du run</b> et non sur le record global de la partie
        /// (<see cref="PrestigeState.MaxCorruptionLevelCleared"/>, qui ne sert qu'au bonus de prestige) :
        /// une première ouverture ne doit pas rendre les suivantes gratuites — chaque nouvelle île exige
        /// à nouveau un nettoyage de niveau <see cref="AbyssGate.RequiredCorruptionLevel"/> ou plus.
        /// </summary>
        public bool IsAbyssGateEligible()
        {
            if (_state == null) return false;
            if (_state.Features.OfType<AbyssGate>().Any()) return false;
            if (!_state.Features.OfType<CorruptionSpire>().Any(s => s.Built)) return false;

            return _state.RunRecord.MaxCorruptionLevelCleared >= AbyssGate.RequiredCorruptionLevel;
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

            // L'avant-poste est posé directement par EstablishOupostInNewAutoExpandLayer, en dehors du
            // chemin normal CityBuilderController.BuildCity/CreateCityAt — il faut donc lui accorder ici
            // les bâtiments NEW_CITY_BUILDING (p. ex. la Tour de Guet offerte par une recherche), sans
            // quoi ce premier avant-poste en serait dépourvu malgré son autorisation dans les Abysses.
            var outpost = playerCiv.Cities.First(c => c.Position.Z == LayerState.AbyssZ);
            PrestigeMapController.GrantNewCityBuildings(_state, outpost, playerCiv);

            _state.Visibility.RecalculateFor(playerCiv.Index);
        }

        /// <summary>
        /// À appeler lorsqu'une ville du joueur est détruite. Si c'était la dernière ville dans les
        /// Abysses, c'est une perte totale — miroir de
        /// <see cref="DeepestMineController.OnCityDestroyed"/> pour l'Inframonde : (1) les essences
        /// divines récoltées pendant le run courant sont perdues (GodState.DivineEssence, remise à
        /// zéro), mais jamais celles déjà garanties par le Reliquaire (GodState.DivineEssenceReliquaryFloor,
        /// qui n'en fait déjà pas partie) ; (2) toute la
        /// carte des Abysses est détruite — <b>y compris les routes du Vide</b>, qui ne survivent qu'à
        /// une perte partielle (voir l'exclusion dans RoadController.OnCityDestroyed/RemoveDisconnectedRoads,
        /// qui ne s'applique pas ici puisqu'on vide la carte entière plutôt que de retirer route par
        /// route) ; (3) comme la Mine Profonde/la Percée de Surface, la Faille retombe à 50 %
        /// d'investissement. <see cref="TryInitializeAbyss"/> ne génère une carte neuve qu'une fois la
        /// Faille rebâtie (elle vérifie <see cref="HasAbyssGateBuilt"/>).
        /// </summary>
        public void OnCityDestroyed(Vertex cityVertex, int civilizationIndex)
        {
            if (_state == null || _godState == null) return;
            var playerCiv = _state.PlayerCivilization;
            if (civilizationIndex != playerCiv.Index) return;
            if (cityVertex.Z != LayerState.AbyssZ) return;

            // La ville a déjà été retirée : vérifie s'il en reste dans les Abysses
            if (playerCiv.Cities.Any(c => c.Position.Z == LayerState.AbyssZ)) return;

            int lost = _godState.DivineEssence;
            if (lost > 0)
            {
                _godState.DivineEssence = 0;
                _state.EventLog.Add(GameEventType.AbyssLostDivineEssence, message: lost.ToString(), toast: true);
            }

            // La Faille (feature de surface/Inframonde, jamais elle-même sur le layer des Abysses)
            // doit être retrouvée avant de vider la carte : les Os Divins qu'on va retirer ci-dessous
            // sont, eux, bien positionnés sur ce layer.
            var gate = _state.Features.OfType<AbyssGate>().FirstOrDefault(g => g.Built);

            // Remplace la couche par une map vide sans la supprimer, comme l'Inframonde :
            // les features dont Position.Z == AbyssZ restent valides pour GetMapFor, mais trouvent une
            // carte sans tuiles (elles deviennent invisibles). Le Z doit être explicite : IslandMap(empty)
            // defaulte à Z=0.
            _state.AddLayer(LayerState.AbyssZ, new LayerState(new IslandMap(Array.Empty<HexTile>(), LayerState.AbyssZ)));

            // Retire les features orphelines de l'ancienne couche (Os Divins, etc.)
            foreach (var feature in _state.Features.Where(f => f.Position.Z == LayerState.AbyssZ).ToList())
                _state.RemoveFeature(feature);

            // Nettoie les routes des Abysses pour toutes les civilisations — y compris les routes du
            // Vide, qui ne survivent qu'aux pertes partielles (voir résumé ci-dessus).
            foreach (var civ in _state.Civilizations)
                civ.RemoveAllRoads(r => r.Position.Z == LayerState.AbyssZ);

            // Retire les civilisations NPC dont toutes les villes étaient dans les Abysses
            _state.Civilizations.RemoveAll(c =>
                c.Index != playerCiv.Index
                && c.Cities.Count > 0
                && c.Cities.All(city => city.Position.Z == LayerState.AbyssZ));

            // Revient sur la surface si le joueur regardait les Abysses
            if (_state.CurrentViewedLayer == LayerState.AbyssZ)
                _state.CurrentViewedLayer = IslandMap.SurfaceLayer;

            if (gate != null)
            {
                gate.Built = false;
                gate.InvestmentEnabled.Clear();
                gate.InvestedResources.Clear();
                gate.CompletedInvestmentCost.Clear();
                var cost = gate.GetInvestmentCost(playerCiv);
                foreach (var kvp in cost)
                    gate.InvestedResources[kvp.Key] = kvp.Value / 2;
                // Contrairement à la construction initiale, on ne relance jamais l'investissement
                // automatique ici (même si "Automatiser les Monuments" est actif) : perdre la dernière
                // ville des Abysses est un revers que le joueur doit choisir de réparer explicitement,
                // pas quelque chose qui se referme tout seul en tâche de fond. Voir WasEverBuilt côté
                // panneau pour le message de reconstruction.
            }

            _state.EventLog.Add(GameEventType.AbyssGateLost, toast: true);
            _state.Visibility.Recalculate();
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
            // Amorce le cooldown d'investissement sur le tick de pose (voir WonderController.PlaceWonder).
            gate.LastInvestmentTick = _clock?.CurrentTick ?? 0;
            _state.AddFeature(gate);
            _state.EventLog.Add(GameEventType.AbyssGatePlaced);
            if (_harvestController != null)
                MonumentInvestment.TryAutoStartInvestment(gate, gate.GetInvestmentCost(_state.PlayerCivilization), _state.PlayerCivilization, _harvestController, _state);
            OnAbyssGatePlaced?.Invoke(this, EventArgs.Empty);
            return gate;
        }
    }
}
