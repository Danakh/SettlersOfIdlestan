using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
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
    public class PandemoniumGateController : MonumentControllerBase<PandemoniumGate>
    {
        private GamePRNG? _prng;
        private PrestigeState? _prestigeState;

        public const long InvestmentIntervalTicks = MonumentInvestment.IntervalTicks;

        public event EventHandler? OnPandemoniumGatePlaced;
        public event EventHandler? OnPandemoniumGateBuilt;

        internal PandemoniumGateController() { }

        internal void Initialize(WorldState? state, GameClock? clock = null, HarvestController? harvestController = null,
            GamePRNG? prng = null, PrestigeState? prestigeState = null)
        {
            if (_state != null)
                _state.FeatureRemoved -= OnFeatureRemoved;

            InitializeCore(state, clock, harvestController);
            _prng = prng;
            _prestigeState = prestigeState;

            if (_state != null)
                _state.FeatureRemoved += OnFeatureRemoved;
        }

        protected override void OnClockAdvancedExtra()
        {
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

            PlaceMonument(tentacle.Position);
        }

        protected override PandemoniumGate CreateFeature(HexCoord position) => new(position);

        protected override GameEventType PlacedEventType => GameEventType.PandemoniumGatePlaced;

        /// <summary>Seule pose de Monument qui remonte un toast : le portail surgit sans que le joueur l'ait demandé.</summary>
        protected override bool PlacedEventIsToast => true;

        protected override void RaisePlaced() => OnPandemoniumGatePlaced?.Invoke(this, EventArgs.Empty);

        protected override bool IsInvestmentComplete(PandemoniumGate gate) => gate.Built;

        protected override void OnInvestmentCycleCompleted(PandemoniumGate gate, Civilization playerCiv)
        {
            // Comme la Faille des Abysses : l'investissement reste affiché à 100% une fois bâti.
            gate.Built = true;
            gate.InvestmentEnabled.Clear();
            _state!.EventLog.Add(GameEventType.PandemoniumGateBuilt, toast: true);
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
