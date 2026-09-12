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
    ///
    /// Gère aussi le dénouement de la couche qu'il ouvre : la mort du Dieu démon, sa récompense et
    /// son record (<see cref="RegisterDemonGodDefeat"/>). Le boss vit et meurt dans le Pandémonium,
    /// dont ce contrôleur est le seul propriétaire ; les moteurs de combat qui l'abattent, eux, sont
    /// plusieurs et ne connaissent pas le GodState.
    /// </summary>
    public class PandemoniumGateController : MonumentControllerBase<PandemoniumGate>
    {
        private GamePRNG? _prng;
        private PrestigeState? _prestigeState;
        private GodState? _godState;

        public const long InvestmentIntervalTicks = MonumentInvestment.IntervalTicks;

        public event EventHandler? OnPandemoniumGatePlaced;
        public event EventHandler? OnPandemoniumGateBuilt;

        /// <summary>
        /// Le Dieu démon vient d'être abattu, récompense déjà versée et record déjà mis à jour. Porte
        /// le bilan complet de la victoire (voir <see cref="DemonGodDefeat"/>) : l'interface s'en sert
        /// pour ouvrir la modale de victoire à la toute première.
        /// </summary>
        public event EventHandler<DemonGodDefeat>? OnDemonGodDefeated;

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

            // Le boss du Pandémonium tombe par le même chemin : sa mort est notifiée ici, et non
            // depuis les moteurs de combat, qui sont plusieurs à tuer des monstres et n'ont à
            // connaître ni le GodState ni l'essence divine.
            if (feature is DemonGod demonGod)
            {
                if (demonGod.Hp <= 0) RegisterDemonGodDefeat(demonGod);
                return;
            }

            if (feature is not Tentacle tentacle) return;
            if (tentacle.Hp > 0) return;
            if (tentacle.Position.Z != LayerState.AbyssZ) return;
            if (HasPandemoniumGate(_state)) return;
            // Boss déjà abattu sur cette île : la branche est close jusqu'au prochain prestige, et
            // les Tentacules de l'Abysse tuées ensuite ne rouvrent plus rien.
            if (_state.RunRecord.DemonGodDefeated) return;

            tentacle.OpenedPandemoniumGate = PlaceMonument(tentacle.Position) != null;
        }

        /// <summary>
        /// Verse la récompense d'un Dieu démon abattu, puis marque le boss pour que l'entrée de
        /// journal que l'appelant ajoute juste après (<see cref="DemonGod.RemovedEventType"/>) dise
        /// laquelle des trois victoires c'était.
        ///
        /// <para>La récompense tient en deux temps, dans cet ordre : le niveau du boss relève le
        /// plafond d'essence divine du cycle en cours (GodState.DivineEssenceCapBonusFromDemonGod,
        /// remis à zéro au prestige comme à l'Ascension), puis la même quantité d'essence divine est
        /// créditée <b>sous ce plafond fraîchement relevé</b> — un joueur qui n'avait pas déjà saturé
        /// son plafond touche donc l'intégralité du niveau, et celui qui l'avait saturé touche
        /// exactement de quoi le saturer de nouveau. L'écrêtage suit la règle des Os Divins (voir
        /// DivineBonesController.GrantPurificationEssence) : le plafond ne compte que
        /// GodState.DivineEssence, jamais les essences du Reliquaire.</para>
        ///
        /// <para>Le record, lui, est cross-prestige ET cross-Ascension
        /// (GodState.HighestDemonGodLevelDefeated) : c'est la seule trace permanente de la victoire,
        /// et ce qui fait que la modale de félicitations ne s'ouvre qu'une fois dans une partie.</para>
        /// </summary>
        private void RegisterDemonGodDefeat(DemonGod demonGod)
        {
            if (_godState == null || _state == null) return;

            int level = Math.Max(1, demonGod.Level);
            int previousRecord = _godState.HighestDemonGodLevelDefeated;
            bool isFirstEver = previousRecord <= 0;
            bool beatsRecord = level > previousRecord;

            _godState.DivineEssenceCapBonusFromDemonGod += level;

            int cap = Ascension.AscensionController.GetDivineEssenceCap(_godState);
            int gained = Math.Clamp(cap - _godState.DivineEssence, 0, level);
            _godState.DivineEssence += gained;
            _godState.TotalDivineEssenceEarned += gained;

            // Horodatage des deux victoires que l'onglet Partie des statistiques affiche : la
            // première de la partie (figée à jamais) et celle qui détient le record (réécrite à
            // chaque fois qu'il tombe). Le tick est celui de l'horloge de la partie, jamais remise
            // à zéro, donc directement lisible comme un temps de jeu total.
            long now = _clock?.CurrentTick ?? 0;

            if (isFirstEver)
            {
                _godState.FirstDemonGodLevelDefeated = level;
                _godState.FirstDemonGodDefeatTick = now;
            }

            if (beatsRecord)
            {
                _godState.HighestDemonGodLevelDefeated = level;
                _godState.HighestDemonGodDefeatTick = now;
            }

            // Fin de la branche pour ce cycle : le Portail du Pandémonium s'efface avec son maître,
            // et le drapeau interdit à toute Tentacule de l'Abysse d'en faire surgir un autre
            // (voir OnFeatureRemoved). Le boss d'un cycle ne se combat donc qu'une fois : c'est le
            // prestige, et lui seul, qui en dresse un nouveau — plus haut, sur une île neuve.
            // Retrait sûr depuis ce gestionnaire : WorldState.RemoveFeature a déjà sorti le boss de
            // sa liste avant de nous notifier, et tous les sites qui tuent un monstre matérialisent
            // leur liste de morts avant de retirer quoi que ce soit.
            _state.RunRecord.DemonGodDefeated = true;
            foreach (var gate in _state.Features.OfType<PandemoniumGate>().ToList())
                _state.RemoveFeature(gate);

            var defeat = new DemonGodDefeat(
                Level: level,
                EssenceGained: gained,
                RecordLevel: _godState.HighestDemonGodLevelDefeated,
                IsFirstEver: isFirstEver,
                BeatsRecord: beatsRecord);

            demonGod.Defeat = defeat;
            OnDemonGodDefeated?.Invoke(this, defeat);
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

            // Rien à annoncer quand le boss est déjà tombé : le portail est parti avec lui (voir
            // RegisterDemonGodDefeat) et aucun ne se rouvrira d'ici le prestige — l'arène vidée
            // ci-dessus ne contenait que les Tentacules qui lui ont survécu, et le joueur n'a donc
            // aucun accès à reconquérir. Un portail absent ne suffit pas à le déduire : perdre
            // l'Abysse d'abord l'emporte lui aussi, et cette perte-là, elle, est bien à annoncer.
            if (!_state.RunRecord.DemonGodDefeated)
                _state.EventLog.Add(GameEventType.PandemoniumGateLost, toast: true);
            _state.Visibility.Recalculate();
        }
    }
}
