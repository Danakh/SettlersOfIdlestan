using System;
using System.Linq;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Prestige;
using SettlersOfIdlestan.Model.Prestige.PrestigeMap;

namespace SettlersOfIdlestan.Controller
{
    public class ResearchController
    {
        private IslandState? _state;
        private GameClock? _clock;
        private PrestigeState? _prestigeState;

        public const long ResearchConsumptionCooldownTicks = 100L;
        public const int MaxResearchPoints = 9999;

        public event EventHandler<TechnologyId>? OnResearchCompleted;

        internal ResearchController() { }

        internal void Initialize(IslandState? state, GameClock? clock, PrestigeState? prestigeState)
        {
            if (_clock != null)
                _clock.Advanced -= OnClockAdvanced;

            _state = state;
            _clock = clock;
            _prestigeState = prestigeState;

            if (_clock != null)
                _clock.Advanced += OnClockAdvanced;
        }

        private void OnClockAdvanced(object? sender, GameClockAdvancedEventArgs e)
        {
            try { ProduceResearchPoints(); } catch { }
            try { AdvanceActiveResearch(); } catch { }
        }

        private void ProduceResearchPoints()
        {
            if (_state == null || _clock == null) return;
            var tree = _state.PlayerCivilization.TechnologyTree;
            if (tree.ResearchPoints >= MaxResearchPoints) return;

            long now = _clock.CurrentTick;
            foreach (var city in _state.PlayerCivilization.Cities)
            {
                var library = city.Buildings.OfType<Library>().FirstOrDefault();
                if (library == null || !library.CanProduceResearch) continue;

                long cooldown = library.GetResearchCooldownTicks();
                if (library.LastResearchTick == 0)
                {
                    library.LastResearchTick = now;
                    continue;
                }
                if (now - library.LastResearchTick < cooldown) continue;

                tree.ResearchPoints = Math.Min(tree.ResearchPoints + 1, MaxResearchPoints);
                library.LastResearchTick = now;
            }
        }

        private void AdvanceActiveResearch()
        {
            if (_state == null || _clock == null) return;
            var tree = _state.PlayerCivilization.TechnologyTree;
            if (tree.ActiveResearch == null || tree.ResearchPoints <= 0) return;

            long now = _clock.CurrentTick;
            if (tree.ActiveResearchLastConsumptionTick == 0)
            {
                tree.ActiveResearchLastConsumptionTick = now;
                return;
            }
            if (now - tree.ActiveResearchLastConsumptionTick < ResearchConsumptionCooldownTicks) return;

            var techId = tree.ActiveResearch.Value;
            var tech = TechnologyDefinitions.Get(techId);
            if (tech == null) { tree.ActiveResearch = null; return; }

            tree.ResearchPoints -= 1;
            tree.ActiveResearchConsumed += 1;
            tree.ActiveResearchLastConsumptionTick = now;

            int effectiveCost = GetEffectiveCost(tech);
            if (tree.ActiveResearchConsumed >= effectiveCost)
            {
                tree.CompleteResearch(techId);
                OnResearchCompleted?.Invoke(this, techId);
            }
        }

        public bool StartResearch(TechnologyId id)
        {
            if (_state == null) return false;
            var tree = _state.PlayerCivilization.TechnologyTree;
            if (tree.CompletedTechnologies.Contains(id)) return false;
            if (tree.ActiveResearch == id) return false;

            var tech = TechnologyDefinitions.Get(id);
            if (tech == null) return false;
            if (!ArePrerequisitesMet(tree, tech)) return false;

            tree.ActiveResearch = id;
            tree.ActiveResearchConsumed = 0;
            tree.ActiveResearchLastConsumptionTick = _clock?.CurrentTick ?? 0;
            return true;
        }

        public TechnologyStatus GetStatus(TechnologyId id)
        {
            if (_state == null) return TechnologyStatus.Inactive;
            var tree = _state.PlayerCivilization.TechnologyTree;

            if (tree.CompletedTechnologies.Contains(id)) return TechnologyStatus.Completed;
            if (tree.ActiveResearch == id) return TechnologyStatus.InProgress;

            var tech = TechnologyDefinitions.Get(id);
            if (tech == null || !ArePrerequisitesMet(tree, tech)) return TechnologyStatus.Inactive;

            return TechnologyStatus.Available;
        }

        public (int consumed, int total) GetResearchProgress(TechnologyId id)
        {
            if (_state == null) return (0, 1);
            var tree = _state.PlayerCivilization.TechnologyTree;
            var tech = TechnologyDefinitions.Get(id);
            if (tech == null) return (0, 1);

            int cost = GetEffectiveCost(tech);
            if (tree.ActiveResearch == id)
                return (tree.ActiveResearchConsumed, cost);
            if (tree.CompletedTechnologies.Contains(id))
                return (cost, cost);
            return (0, cost);
        }

        public bool IsResearchUnlocked()
            => _prestigeState?.PurchasedVertices.Contains(PrestigeVertexId.Central) == true;

        private int GetEffectiveCost(Technology tech)
        {
            double reduction = _state?.PlayerCivilization.ResearchCostReduction ?? 0.0;
            return Math.Max(1, (int)(tech.Cost * (1.0 - reduction)));
        }

        private static bool ArePrerequisitesMet(TechnologyTree tree, Technology tech)
        {
            foreach (var prereq in tech.Prerequisites)
                if (!tree.CompletedTechnologies.Contains(prereq))
                    return false;
            return true;
        }
    }
}
