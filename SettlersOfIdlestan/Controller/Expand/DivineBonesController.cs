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
                _state.RunRecord.DivineBonesPurified++;

                // Chaque Purification octroie directement 1 essence divine, seulement si le plafond
                // (une essence par niveau de corruption à partir du niveau 4, plus 1 par pouvoir divin
                // déjà débloqué — voir AscensionController.GetDivineEssenceCap) n'est pas déjà atteint —
                // au-delà, il faut prestige ou débloquer un nouveau pouvoir divin pour relever ce
                // plafond (la Purification a quand même lieu, mais n'accorde aucune essence).
                // GodState.DivineEssence ne compte déjà pas les essences garanties par le Reliquaire
                // (GodState.DivineEssenceReliquaryFloor, distinct) : un Reliquaire plein n'interdit
                // donc jamais de nouvelles essences en début de cycle.
                int essenceCap = bones.GetEssenceCap();
                bones.EssenceGranted = _godState.DivineEssence < essenceCap;
                if (bones.EssenceGranted)
                {
                    _godState.DivineEssence++;
                    _godState.TotalDivineEssenceEarned++;
                }

                _state.EventLog.Add(bones.EssenceGranted ? GameEventType.DivineBonesPurified : GameEventType.DivineBonesPurifiedNoEssence, toast: true);
                OnDivineBonesPurified?.Invoke(this, bones);

                // Une fois purifiés, les Os Divins n'ont plus rien à offrir : ils disparaissent de la carte.
                _state.RemoveFeature(bones);
            }
        }

    }
}
