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
    /// Gère la Purification des Os Divins : investissement double (Cristal via le mécanisme
    /// Monument standard + points de recherche via un pool séparé, voir DivineBones.InvestedResearch),
    /// à coût croissant avec le nombre d'essences divines déjà collectées (cross-prestige, GodState).
    /// Une Purification terminée octroie toujours 1 os divin (WorldState.DivineBoneCount, perdu au
    /// prestige) ; DivineBones.BonesPerEssence os réunis sur la même île se convertissent
    /// automatiquement en 1 essence divine (révélant l'onglet Ascension, voir
    /// TabBarRenderer.HasGodPoints), seulement si GodState.DivineEssence n'a pas déjà atteint le
    /// plafond de la feature (DivineBones.GetEssenceCap).
    /// </summary>
    public class DivineBonesController
    {
        private WorldState? _state;
        private GameClock? _clock;
        private GodState? _godState;
        private GamePRNG? _prng;

        public const long InvestmentIntervalTicks = MonumentInvestment.IntervalTicks;

        public event EventHandler<DivineBones>? OnDivineBonesPurified;

        internal DivineBonesController() { }

        internal void Initialize(WorldState? state, GameClock? clock, GodState? godState, GamePRNG? prng)
        {
            if (_clock != null)
                _clock.Advanced -= OnClockAdvanced;

            _state = state;
            _clock = clock;
            _godState = godState;
            _prng = prng;

            if (_clock != null)
                _clock.Advanced += OnClockAdvanced;
        }

        private void OnClockAdvanced(object? sender, GameClockAdvancedEventArgs e)
        {
            try { ProcessInvestment(); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[DivineBonesController] {nameof(ProcessInvestment)}: {ex}"); }
        }

        private void ProcessInvestment()
        {
            if (_state == null || _clock == null || _godState == null || _prng == null) return;

            var playerCiv = _state.PlayerCivilization;
            long now = _clock.CurrentTick;

            foreach (var bones in _state.Features.OfType<DivineBones>())
            {
                if (bones.Purified) continue;

                // Toujours resynchronisé (indépendamment du cooldown d'investissement) pour que le
                // panneau affiche un coût à jour dès qu'une autre Purification a fait progresser N.
                bones.EssenceAlreadyCollected = _godState.TotalDivineEssenceEarned;

                var crystalCost = bones.GetInvestmentCost(playerCiv);
                bool crystalDone = MonumentInvestment.ProcessTick(bones, crystalCost, playerCiv, now);
                bool researchDone = ProcessResearchInvestment(bones, playerCiv, now);

                if (!crystalDone || !researchDone) continue;

                bones.Purified = true;
                bones.InvestmentEnabled.Clear();
                bones.ResearchInvestmentEnabled = false;

                // Chaque Purification octroie 1 os divin, stocké sur l'île courante (donc perdu au
                // prestige). BonesPerEssence os se convertissent en 1 essence divine, seulement si le
                // plafond (une essence par niveau de corruption à partir du niveau 4) n'est pas déjà
                // atteint — au-delà, il faut prestige pour relever ce plafond (les os en excédent
                // restent sur l'île en attendant).
                _state.DivineBoneCount++;
                bones.EssenceGranted = false;
                while (_state.DivineBoneCount >= DivineBones.BonesPerEssence
                       && _godState.DivineEssence < bones.GetEssenceCap())
                {
                    _state.DivineBoneCount -= DivineBones.BonesPerEssence;
                    _godState.DivineEssence++;
                    _godState.TotalDivineEssenceEarned++;
                    bones.EssenceGranted = true;
                }

                _state.EventLog.Add(bones.EssenceGranted ? GameEventType.DivineBonesPurified : GameEventType.DivineBonesPurifiedNoEssence, toast: true);
                OnDivineBonesPurified?.Invoke(this, bones);
            }
        }

        /// <summary>
        /// Prélève jusqu'à 1% du pool de points de recherche courant vers InvestedResearch, au plus
        /// une fois par <see cref="InvestmentIntervalTicks"/> ticks — même rythme que MonumentInvestment.ProcessTick,
        /// mais contre TechnologyTree.ResearchPoints plutôt que l'inventaire de ressources.
        /// </summary>
        private static bool ProcessResearchInvestment(DivineBones bones, Civilization playerCiv, long now)
        {
            long required = bones.GetRequiredResearch();
            if (bones.InvestedResearch >= required) return true;
            if (!bones.ResearchInvestmentEnabled) return false;
            if (now - bones.LastResearchInvestmentTick < InvestmentIntervalTicks) return false;
            if (!MonumentInvestment.HasAdjacentCity(bones.Position, playerCiv)) return false;
            bones.LastResearchInvestmentTick = now;

            var tree = playerCiv.TechnologyTree;
            long pool = tree.ResearchPoints;
            if (pool < 1) return false;

            long remaining = required - bones.InvestedResearch;
            long amount = Math.Min(remaining, Math.Max(1L, pool / 100));
            amount = Math.Min(amount, pool);

            tree.ResearchPoints -= amount;
            bones.InvestedResearch += amount;
            if (bones.InvestedResearch >= required)
                bones.ResearchInvestmentEnabled = false;

            return bones.InvestedResearch >= required;
        }
    }
}
