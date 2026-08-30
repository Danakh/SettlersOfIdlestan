using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.IslandFeatures;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Monsters;
using SettlersOfIdlestan.Model.Prestige;
using System;
using System.Linq;

namespace SettlersOfIdlestan.Controller.Expand
{
    /// <summary>
    /// Gère le Portail du Pandémonium : son apparition à la mort d'une Tentacule de l'Abysse, sa
    /// construction par investissement progressif (même coût que la Faille des Abysses, voir
    /// <see cref="PandemoniumGate"/>), puis l'ouverture de la couche Pandémonium.
    ///
    /// Contrairement à la Faille des Abysses, le portail n'est pas placé par le joueur : il surgit
    /// de lui-même sur l'hex de la Tentacule abattue (<see cref="OnFeatureRemoved"/>). Un seul
    /// portail existe par île — une deuxième Tentacule tuée n'en ouvre pas un second, que le
    /// premier soit déjà bâti ou non.
    /// </summary>
    public class PandemoniumGateController
    {
        private WorldState? _state;
        private GameClock? _clock;
        private HarvestController? _harvestController;
        private GamePRNG? _prng;
        private PrestigeState? _prestigeState;

        public const long InvestmentIntervalTicks = MonumentInvestment.IntervalTicks;

        public event EventHandler? OnPandemoniumGatePlaced;
        public event EventHandler? OnPandemoniumGateBuilt;

        internal PandemoniumGateController() { }

        internal void Initialize(WorldState? state, GameClock? clock = null, HarvestController? harvestController = null,
            GamePRNG? prng = null, PrestigeState? prestigeState = null)
        {
            if (_clock != null)
                _clock.Advanced -= OnClockAdvanced;
            if (_state != null)
                _state.FeatureRemoved -= OnFeatureRemoved;

            _state = state;
            _clock = clock;
            _harvestController = harvestController;
            _prng = prng;
            _prestigeState = prestigeState;

            if (_clock != null)
                _clock.Advanced += OnClockAdvanced;
            if (_state != null)
                _state.FeatureRemoved += OnFeatureRemoved;
        }

        private void OnClockAdvanced(object? sender, GameClockAdvancedEventArgs e)
        {
            try { ProcessInvestment(); }
            catch (Exception ex) { GameLog.Error(nameof(PandemoniumGateController), nameof(ProcessInvestment), ex); }
            try { TryInitializePandemonium(); }
            catch (Exception ex) { GameLog.Error(nameof(PandemoniumGateController), nameof(TryInitializePandemonium), ex); }
        }

        /// <summary>
        /// Fait surgir le portail sur l'hex d'une Tentacule tuée. Le filtre <c>Hp &lt;= 0</c> distingue
        /// une mort au combat d'un simple retrait de feature (nettoyage d'une couche perdue) ; le
        /// filtre sur <see cref="LayerState.AbyssZ"/> réserve la récompense aux Tentacules de
        /// l'Abysse, celles qui gardent le Pandémonium lui-même n'ouvrant évidemment rien.
        /// </summary>
        private void OnFeatureRemoved(object? sender, IslandFeature feature)
        {
            if (_state == null) return;
            if (feature is not Tentacle tentacle) return;
            if (tentacle.Hp > 0) return;
            if (tentacle.Position.Z != LayerState.AbyssZ) return;
            if (_state.Features.OfType<PandemoniumGate>().Any()) return;

            var gate = new PandemoniumGate(tentacle.Position);
            // Amorce le cooldown d'investissement sur le tick de pose (voir WonderController.PlaceWonder).
            gate.LastInvestmentTick = _clock?.CurrentTick ?? 0;
            _state.AddFeature(gate);
            _state.EventLog.Add(GameEventType.PandemoniumGatePlaced, toast: true);
            if (_harvestController != null)
                MonumentInvestment.TryAutoStartInvestment(gate, gate.GetInvestmentCost(_state.PlayerCivilization), _state.PlayerCivilization, _harvestController, _state);
            OnPandemoniumGatePlaced?.Invoke(this, EventArgs.Empty);
        }

        private void ProcessInvestment()
        {
            if (_state == null || _clock == null) return;
            var gate = _state.Features.OfType<PandemoniumGate>().FirstOrDefault();
            if (gate == null || gate.Built) return;

            // Pas de garde sur InvestmentEnabled avant ProcessTick — voir le commentaire de
            // WonderController.ProcessInvestment pour la raison (évite de figer LastInvestmentTick
            // pendant une désactivation, et le rattrapage massif que ça provoquerait à la réactivation).
            var playerCiv = _state.PlayerCivilization;
            var cost = gate.GetInvestmentCost(playerCiv);
            if (!MonumentInvestment.ProcessTick(gate, cost, playerCiv, _clock.CurrentTick)) return;

            // Comme la Faille des Abysses : l'investissement reste affiché à 100% une fois bâti.
            gate.Built = true;
            gate.InvestmentEnabled.Clear();
            _state.EventLog.Add(GameEventType.PandemoniumGateBuilt, toast: true);
            OnPandemoniumGateBuilt?.Invoke(this, EventArgs.Empty);
        }

        public bool HasPandemoniumGateBuilt()
            => _state?.Features.OfType<PandemoniumGate>().Any(f => f.Built) == true;

        /// <summary>
        /// Ouvre le Pandémonium une fois le portail bâti (comme
        /// <see cref="AbyssGateController.TryInitializeAbyss"/> pour l'Abysse) : île unique
        /// entièrement générée d'avance, avec son dieu démon, ses Tentacules et l'avant-poste du
        /// joueur au bord (voir <see cref="Generator.PandemoniumGenerator"/>).
        /// </summary>
        private void TryInitializePandemonium()
        {
            if (_state == null || _prng == null) return;

            var playerCiv = _state.PlayerCivilization;

            // Déjà un avant-poste joueur dans le Pandémonium → rien à faire
            if (playerCiv.Cities.Any(c => c.Position.Z == LayerState.PandemoniumZ)) return;

            if (!HasPandemoniumGateBuilt()) return;

            int monsterLevel = MonsterLeveling.UndergroundLevel(
                _prestigeState?.Tier ?? 1, _prestigeState?.CurrentCorruptionLevel ?? 1);

            var layout = Generator.PandemoniumGenerator.Create(playerCiv, _prng, monsterLevel);
            _state.AddLayer(LayerState.PandemoniumZ, layout.Layer);
            // Le dieu démon et ses Tentacules naissent au milieu de leur flaque : leur hex et ses
            // voisins sont corrompus au niveau de l'île, moitié du plafond que leur génération
            // continue atteindra (voir CorruptionController.ProcessMonsterCorruptionGrowth).
            foreach (var monster in layout.Monsters)
            {
                _state.AddFeature(monster);
                Island.CorruptionController.SeedCorruptionAroundNewMonster(
                    _state, monster, _prestigeState?.CurrentCorruptionLevel ?? 1);
            }
            _state.Visibility.RecalculateFor(playerCiv.Index);
        }
    }
}
