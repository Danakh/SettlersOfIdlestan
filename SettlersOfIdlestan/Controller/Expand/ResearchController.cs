using System;
using System.Linq;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Prestige;
using SettlersOfIdlestan.Model.Prestige.PrestigeMap;

namespace SettlersOfIdlestan.Controller.Expand
{
    public class ResearchController
    {
        private IslandState? _state;
        private GameClock? _clock;
        private PrestigeState? _prestigeState;

        public const long ResearchConsumptionCooldownTicks = 100L;
        public const int MaxResearchPoints = 9999;

        public event EventHandler<TechnologyId>? OnResearchCompleted;

        // Convenience accessors for renderers — go through PrestigeState so the source is explicit.
        public int ResearchPoints => _prestigeState?.TechnologyTree.ResearchPoints ?? 0;
        public TechnologyId? ActiveResearch => _prestigeState?.TechnologyTree.ActiveResearch;

        private TechnologyTree? Tree => _prestigeState?.TechnologyTree;

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
            if (_state == null || _clock == null || Tree == null) return;
            var tree = Tree;
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

            foreach (var city in _state.PlayerCivilization.Cities)
            {
                var lab = city.Buildings.OfType<Laboratory>().FirstOrDefault();
                if (lab == null || lab.Level < 1 || lab.ActivationStatus != ActivationStatus.ACTIVE) continue;

                long cooldown = lab.GetResearchCooldownTicks();
                if (lab.LastResearchTick == 0)
                {
                    lab.LastResearchTick = now;
                    continue;
                }
                if (now - lab.LastResearchTick < cooldown) continue;
                if (_state.PlayerCivilization.GetResourceQuantity(Resource.Gold) < 1) continue;

                _state.PlayerCivilization.RemoveResource(Resource.Gold, 1);
                int batch = Laboratory.ResearchPointsPerBatch + _state.PlayerCivilization.LaboratoryResearchBonus;
                tree.ResearchPoints = Math.Min(tree.ResearchPoints + batch, MaxResearchPoints);
                lab.LastResearchTick = now;
            }
        }

        private void AdvanceActiveResearch()
        {
            if (_state == null || _clock == null || Tree == null) return;
            var tree = Tree;
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

            int consumed = Math.Max(1, tree.ResearchPoints / 100);
            consumed = Math.Min(consumed, tree.ResearchPoints);
            tree.ResearchPoints -= consumed;
            tree.ActiveResearchConsumed += consumed;
            tree.ActiveResearchLastConsumptionTick = now;

            int effectiveCost = GetEffectiveCost(tech);
            if (tree.ActiveResearchConsumed >= effectiveCost)
            {
                tree.CompleteResearch(techId);
                OnResearchCompleted?.Invoke(this, techId);

                // Auto-démarrer la recherche suivante si elle est en file d'attente
                if (tree.QueuedResearch.HasValue)
                {
                    var queued = tree.QueuedResearch.Value;
                    tree.QueuedResearch = null;
                    StartResearch(queued);
                }
            }
        }

        public bool StartResearch(TechnologyId id)
        {
            if (_state == null || Tree == null) return false;
            var tree = Tree;
            if (tree.CompletedTechnologies.Contains(id)) return false;
            if (tree.ActiveResearch == id) return false;

            var tech = TechnologyDefinitions.Get(id);
            if (tech == null) return false;
            if (!ArePrerequisitesMet(tree, tech)) return false;
            if (!IsPrestigeRequirementMet(id)) return false;

            tree.ActiveResearch = id;
            tree.ActiveResearchConsumed = 0;
            tree.ActiveResearchLastConsumptionTick = _clock?.CurrentTick ?? 0;
            return true;
        }

        public TechnologyId? GetQueuedResearch()
            => Tree?.QueuedResearch;

        public bool SetQueuedResearch(TechnologyId? id)
        {
            if (Tree == null) return false;
            var tree = Tree;
            if (id == null)
            {
                tree.QueuedResearch = null;
                return true;
            }
            if (!CanBeQueued(id.Value)) return false;
            tree.QueuedResearch = id.Value;
            return true;
        }

        public bool CanBeQueued(TechnologyId id)
        {
            if (Tree == null) return false;
            if (!IsResearchQueueUnlocked()) return false;
            var tree = Tree;
            if (tree.CompletedTechnologies.Contains(id)) return false;
            if (tree.ActiveResearch == id) return false;
            var tech = TechnologyDefinitions.Get(id);
            if (tech == null) return false;
            if (!IsPrestigeRequirementMet(id)) return false;
            return ArePrerequisitesMet(tree, tech) || WillBeAvailableAfterActiveResearch(tree, tech);
        }

        private static bool WillBeAvailableAfterActiveResearch(TechnologyTree tree, Technology tech)
        {
            if (tree.ActiveResearch == null) return false;
            var activeId = tree.ActiveResearch.Value;
            if (!tech.Prerequisites.Contains(activeId)) return false;
            foreach (var prereq in tech.Prerequisites)
            {
                if (!tree.CompletedTechnologies.Contains(prereq) && prereq != activeId)
                    return false;
            }
            return true;
        }

        public TechnologyStatus GetStatus(TechnologyId id)
        {
            if (Tree == null) return TechnologyStatus.Inactive;
            var tree = Tree;

            if (tree.CompletedTechnologies.Contains(id)) return TechnologyStatus.Completed;
            if (tree.ActiveResearch == id) return TechnologyStatus.InProgress;

            var tech = TechnologyDefinitions.Get(id);
            if (tech == null || !ArePrerequisitesMet(tree, tech) || !IsPrestigeRequirementMet(id)) return TechnologyStatus.Inactive;

            return TechnologyStatus.Available;
        }

        public (int consumed, int total) GetResearchProgress(TechnologyId id)
        {
            if (Tree == null) return (0, 1);
            var tree = Tree;
            var tech = TechnologyDefinitions.Get(id);
            if (tech == null) return (0, 1);

            int cost = GetEffectiveCost(tech);
            if (tree.ActiveResearch == id)
                return (tree.ActiveResearchConsumed, cost);
            if (tree.CompletedTechnologies.Contains(id))
                return (cost, cost);
            return (0, cost);
        }

        public double GetResearchPointsPerSecond()
        {
            if (_state == null) return 0.0;
            double total = 0.0;
            foreach (var city in _state.PlayerCivilization.Cities)
            {
                var library = city.Buildings.OfType<Library>().FirstOrDefault();
                if (library == null || !library.CanProduceResearch) continue;
                long cooldown = library.GetResearchCooldownTicks();
                total += 100.0 / cooldown;

                var lab = city.Buildings.OfType<Laboratory>().FirstOrDefault();
                if (lab == null || lab.Level < 1 || lab.ActivationStatus != ActivationStatus.ACTIVE) continue;
                long labCooldown = lab.GetResearchCooldownTicks();
                total += Laboratory.ResearchPointsPerBatch * 100.0 / labCooldown;
            }
            return total;
        }

        public bool IsResearchUnlocked()
            => _prestigeState?.PurchasedVertices.Contains(PrestigeMap.CentralVertex) == true;

        public bool IsResearchQueueUnlocked()
            => _prestigeState?.PurchasedVertices.Contains(PrestigeMap.KnowledgeMasteryVertex) == true;

        private bool IsPrestigeRequirementMet(TechnologyId id)
        {
            string techKey = id.ToString();
            bool hasRequirement = PrestigeMapController.DefaultMap.Vertices
                .Any(v => v.Modifiers.Any(m =>
                    m.Category == Modifier.ECategory.UNLOCK_RESEARCH && m.SubCategory == techKey));
            if (!hasRequirement) return true;
            return _state?.PlayerCivilization.ModifierAggregator.HasModifier(
                Modifier.ECategory.UNLOCK_RESEARCH, techKey) == true;
        }

        public bool ShouldDisplay(TechnologyId id)
        {
            if (Tree == null) return false;
            var tree = Tree;

            if (tree.CompletedTechnologies.Contains(id)) return true;
            if (tree.ActiveResearch == id) return true;
            if (!IsPrestigeRequirementMet(id)) return false;

            var tech = TechnologyDefinitions.Get(id);
            if (tech == null) return false;

            if (ArePrerequisitesMet(tree, tech)) return true;

            // Visible si tous les prérequis manquants sont eux-mêmes faisables (Available ou InProgress)
            foreach (var prereqId in tech.Prerequisites)
            {
                if (tree.CompletedTechnologies.Contains(prereqId)) continue;
                var prereqStatus = GetStatus(prereqId);
                if (prereqStatus != TechnologyStatus.Available && prereqStatus != TechnologyStatus.InProgress)
                    return false;
            }
            return true;
        }

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
