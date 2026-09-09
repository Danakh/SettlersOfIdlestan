using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandFeatures;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Monsters;
using SettlersOfIdlestan.Model.Prestige;
using SettlersOfIdlestan.Model.Races;
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
        private GodState? _godState;

        public const long InvestmentIntervalTicks = MonumentInvestment.IntervalTicks;

        public event EventHandler? OnPandemoniumGatePlaced;
        public event EventHandler? OnPandemoniumGateBuilt;

        internal PandemoniumGateController() { }

        internal void Initialize(WorldState? state, GameClock? clock = null, HarvestController? harvestController = null,
            GamePRNG? prng = null, PrestigeState? prestigeState = null, GodState? godState = null)
        {
            if (_state != null)
                _state.FeatureRemoved -= OnFeatureRemoved;

            InitializeCore(state, clock, harvestController);
            _prng = prng;
            _prestigeState = prestigeState;
            _godState = godState;

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
        ///
        /// <para>Marque la Tentacule quand le portail surgit vraiment
        /// (<see cref="Tentacle.OpenedPandemoniumGate"/>) : l'appelant qui journalise sa mort juste
        /// après annonce ainsi l'ouverture pour la première seulement, jamais pour les suivantes.</para>
        /// </summary>
        private void OnFeatureRemoved(object? sender, IslandFeature feature)
        {
            if (_state == null) return;
            if (feature is not Tentacle tentacle) return;
            if (tentacle.Hp > 0) return;
            if (tentacle.Position.Z != LayerState.AbyssZ) return;
            if (HasPandemoniumGate(_state)) return;

            tentacle.OpenedPandemoniumGate = PlaceMonument(tentacle.Position) != null;
        }

        /// <summary>
        /// Vrai si le Portail du Pandémonium de cette île existe déjà — posé ou bâti. Un seul par
        /// île : c'est ce qui interdit une deuxième pose, et ce qui évite au journal de promettre
        /// un portail à qui en a déjà un (voir <see cref="GameEventType.TentacleDiscoveredNoGate"/>).
        /// </summary>
        public static bool HasPandemoniumGate(WorldState? state)
            => state?.Features.OfType<PandemoniumGate>().Any() == true;

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
            gate.WasEverBuilt = true;
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
        /// joueur au bord (voir <see cref="Generator.PandemoniumGenerator"/>). Le triangle d'arrivée
        /// couvre Forêt/Montagne/Colline, la Colline étant remplacée par le terrain préféré de la
        /// race courante s'il y en a un (voir <see cref="RaceDefinition.UndergroundStartVertexTerrain"/>).
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

            var race = RaceDefinitions.Get(_godState?.AscensionState.SelectedRace ?? RaceId.Human);
            var layout = Generator.PandemoniumGenerator.Create(playerCiv, _prng, monsterLevel, race.UndergroundStartVertexTerrain);
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

        /// <summary>
        /// À appeler lorsqu'une ville du joueur est détruite. Si c'était la dernière ville du
        /// Pandémonium, c'est une perte totale — miroir de
        /// <see cref="AbyssGateController.OnCityDestroyed"/> pour l'Abysse : (1) toute la carte du
        /// Pandémonium est détruite, dieu démon et Tentacules compris, ainsi que les routes de la
        /// couche ; (2) le portail retombe à 50 % d'investissement.
        /// <see cref="TryInitializePandemonium"/> ne régénère une arène neuve qu'une fois le portail
        /// rebâti (elle vérifie <see cref="HasPandemoniumGateBuilt"/>) — vider la couche est donc
        /// obligatoire et non cosmétique : la régénération repose le décor par-dessus, et les
        /// monstres de l'ancienne arène s'ajouteraient à ceux de la nouvelle.
        /// </summary>
        public void OnCityDestroyed(Vertex cityVertex, int civilizationIndex)
        {
            if (_state == null) return;
            var playerCiv = _state.PlayerCivilization;
            if (civilizationIndex != playerCiv.Index) return;
            if (cityVertex.Z != LayerState.PandemoniumZ) return;

            // La ville a déjà été retirée : vérifie s'il en reste dans le Pandémonium
            if (playerCiv.Cities.Any(c => c.Position.Z == LayerState.PandemoniumZ)) return;

            // Le portail est une feature de l'Abysse (hex de la Tentacule abattue), jamais du
            // Pandémonium lui-même : il survit au vidage de la couche ci-dessous.
            var gate = _state.Features.OfType<PandemoniumGate>().FirstOrDefault(g => g.Built);

            // Remplace la couche par une map vide sans la supprimer, comme l'Abysse : les features
            // dont Position.Z == PandemoniumZ restent valides pour GetMapFor, mais trouvent une carte
            // sans tuiles. Le Z doit être explicite : IslandMap(empty) defaulte à Z=0.
            _state.AddLayer(LayerState.PandemoniumZ, new LayerState(new IslandMap(Array.Empty<HexTile>(), LayerState.PandemoniumZ)));

            // Retire les features orphelines de l'ancienne arène (dieu démon, Tentacules, Corruption…)
            foreach (var feature in _state.Features.Where(f => f.Position.Z == LayerState.PandemoniumZ).ToList())
                _state.RemoveFeature(feature);

            foreach (var civ in _state.Civilizations)
                civ.RemoveAllRoads(r => r.Position.Z == LayerState.PandemoniumZ);

            // Revient sur la surface si le joueur regardait le Pandémonium
            if (_state.CurrentViewedLayer == LayerState.PandemoniumZ)
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
                // Comme la Faille des Abysses, on ne relance jamais l'investissement automatique ici
                // (même si "Automatiser les Monuments" est actif) : perdre la dernière ville du
                // Pandémonium est un revers que le joueur doit choisir de réparer explicitement, pas
                // quelque chose qui se referme tout seul en tâche de fond. Voir WasEverBuilt côté
                // panneau pour le message de reconstruction.
            }

            _state.EventLog.Add(GameEventType.PandemoniumGateLost, toast: true);
            _state.Visibility.Recalculate();
        }
    }
}
