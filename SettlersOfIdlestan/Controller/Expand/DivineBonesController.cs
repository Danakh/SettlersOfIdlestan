using SettlersOfIdlestan.Controller.Ascension;
using SettlersOfIdlestan.Controller.Expand;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.IslandFeatures;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Prestige;
using System;
using System.Linq;

namespace SettlersOfIdlestan.Controller.Island
{
    /// <summary>
    /// Gère la Purification des Os Divins : investissement en Cristal, Mithril et Acier via le
    /// mécanisme Monument standard, à coût croissant avec le nombre d'essences divines détenues depuis la dernière Ascension
    /// (GodState.DivineEssence) — l'Ascension, en convertissant les essences en points divins,
    /// réinitialise donc le coût de Purification.
    /// Une Purification terminée octroie directement 1 essence divine (GodState.DivineEssence),
    /// seulement si le plafond de la feature (DivineBones.GetEssenceCap) n'est pas déjà atteint —
    /// au-delà, la Purification a toujours lieu mais n'accorde aucune essence.
    /// </summary>
    public class DivineBonesController
    {
        private WorldState? _state;
        private GameClock? _clock;
        private GodState? _godState;
        private GamePRNG? _prng;
        private HarvestController? _harvestController;

        public const long InvestmentIntervalTicks = MonumentInvestment.IntervalTicks;

        public event EventHandler<DivineBones>? OnDivineBonesPurified;

        internal DivineBonesController() { }

        internal void Initialize(WorldState? state, GameClock? clock, GodState? godState, GamePRNG? prng, HarvestController? harvestController = null)
        {
            if (_clock != null)
                _clock.Advanced -= OnClockAdvanced;

            _state = state;
            _clock = clock;
            _godState = godState;
            _prng = prng;
            _harvestController = harvestController;

            if (_clock != null)
                _clock.Advanced += OnClockAdvanced;

            // Nettoyage rétrocompatible : les sauvegardes antérieures à la suppression automatique
            // des Os Divins purifiés (voir ProcessInvestment) peuvent encore en contenir sur la carte.
            if (_state != null)
                foreach (var bones in _state.Features.OfType<DivineBones>().Where(b => b.Purified).ToList())
                    _state.RemoveFeature(bones);
        }

        private void OnClockAdvanced(object? sender, GameClockAdvancedEventArgs e)
        {
            try { ProcessInvestment(); }
            catch (Exception ex) { GameLog.Error(nameof(DivineBonesController), nameof(ProcessInvestment), ex); }
        }

        private void ProcessInvestment()
        {
            if (_state == null || _clock == null || _godState == null || _prng == null) return;

            var playerCiv = _state.PlayerCivilization;
            long now = _clock.CurrentTick;

            // Copie défensive : une Purification retire sa DivineBones de _state.Features en cours
            // de boucle (voir plus bas), ce qui invaliderait l'énumération directe.
            foreach (var bones in _state.Features.OfType<DivineBones>().ToList())
            {
                if (bones.Purified) continue;

                // Toujours resynchronisé (indépendamment du cooldown d'investissement) pour que le
                // panneau affiche un coût à jour dès qu'une autre Purification a fait progresser N
                // — ou qu'une Ascension l'a remis à zéro.
                bones.EssenceAlreadyCollected = _godState.DivineEssence;
                bones.UnlockedPowersBonus = _godState.AscensionState.UnlockedPowers.Count;

                var investmentCost = bones.GetInvestmentCost(playerCiv);

                // La Purification d'un autre Os Divin peut avoir fait grimper N (voir
                // EssenceAlreadyCollected) et donc le coût ci-dessus depuis le dernier tick : une
                // ressource déjà couverte à l'ancien coût, désélectionnée par ProcessTick, doit
                // reprendre l'investissement automatique si elle ne suffit plus au nouveau coût.
                if (_harvestController != null)
                    MonumentInvestment.ResumeAutoInvestmentIfUnderfunded(bones, investmentCost, playerCiv, _harvestController, _state);

                bool investmentDone = MonumentInvestment.ProcessTick(bones, investmentCost, playerCiv, now);

                if (!investmentDone) continue;

                bones.Purified = true;
                bones.InvestmentEnabled.Clear();

                GrantPurificationEssence(bones, _godState, _state);
                OnDivineBonesPurified?.Invoke(this, bones);

                // Une fois purifiés, les Os Divins n'ont plus rien à offrir : ils disparaissent de la carte.
                _state.RemoveFeature(bones);
            }
        }

        /// <summary>
        /// Octroie l'essence divine d'une Purification qui vient de s'achever, puis journalise le
        /// résultat. Partagé par la Purification ordinaire (<see cref="ProcessInvestment"/>) et par
        /// la Purification Supérieure de la Nécropole
        /// (NecropolisController.HarvestBonesUnderNecropolis) : les deux doivent appliquer exactement
        /// le même plafond et produire les mêmes entrées de journal.
        ///
        /// <para>Chaque Purification octroie directement 1 essence divine, seulement si le plafond
        /// (une essence par niveau de corruption, plus 1 par pouvoir divin déjà débloqué — voir
        /// DivineBones.GetEssenceCap et AscensionController.GetDivineEssenceCap) n'est pas déjà
        /// atteint — au-delà, il faut prestige ou débloquer un nouveau pouvoir divin pour le relever
        /// (la Purification a quand même lieu, mais n'accorde aucune essence).
        /// GodState.DivineEssence ne compte déjà pas les essences garanties par le Reliquaire
        /// (GodState.DivineEssenceReliquaryFloor, distinct) : un Reliquaire plein n'interdit donc
        /// jamais de nouvelles essences en début de cycle.</para>
        ///
        /// <para>L'essence qui atteint le plafond signale en plus
        /// <see cref="GameEventType.DivineEssenceCapReached"/> (avec toast) : c'est le seul moment où
        /// le joueur apprend que sa récolte est terminée pour ce niveau de corruption. La condition
        /// exige l'octroi, ce qui la fait se déclencher une seule fois par plafond — les
        /// Purifications suivantes, elles, n'accordent rien et journalisent
        /// DivineBonesPurifiedNoEssence. Si ce plafond est inférieur aux points divins requis pour
        /// ascensionner, un second événement rappelle les trois moyens d'atteindre quand même le
        /// seuil.</para>
        /// </summary>
        internal static void GrantPurificationEssence(DivineBones bones, GodState godState, WorldState state)
        {
            int essenceCap = bones.GetEssenceCap();
            bones.EssenceGranted = godState.DivineEssence < essenceCap;
            if (bones.EssenceGranted)
            {
                godState.DivineEssence++;
                godState.TotalDivineEssenceEarned++;
            }

            state.EventLog.Add(bones.EssenceGranted ? GameEventType.DivineBonesPurified : GameEventType.DivineBonesPurifiedNoEssence, toast: true);

            if (!bones.EssenceGranted || godState.DivineEssence < essenceCap) return;

            state.EventLog.Add(GameEventType.DivineEssenceCapReached, essenceCap.ToString(), toast: true);

            if (essenceCap < AscensionController.MinDivineEssenceForAscension)
                state.EventLog.Add(GameEventType.DivineEssenceCapBelowAscension, essenceCap.ToString());
        }
    }
}
