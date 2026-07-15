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
        private WorldState? _state;
        private GameClock? _clock;
        private PrestigeState? _prestigeState;
        private GameSettings? _settings;

        public const long ResearchConsumptionCooldownTicks = 100L;

        // Plafond dynamique : base fixe + un bonus proportionnel à la somme des coûts de BASE (non réduits)
        // des recherches terminées. Calculé une fois au chargement (Initialize) puis mis à jour incrémentalement
        // à chaque recherche terminée — jamais persisté, donc aucune migration de save n'est nécessaire.
        public const int BaseMaxResearchPoints = 1000;
        public const double MaxResearchPointsInvestedRate = 0.1;

        // long : la somme des coûts de base (et les coûts unitaires des tiers 13+) dépasse int.MaxValue.
        private long _totalBaseResearchCostCompleted;

        public event EventHandler<TechnologyId>? OnResearchCompleted;

        // Convenience accessors for renderers — go through PrestigeState so the source is explicit.
        public long ResearchPoints => _prestigeState?.TechnologyTree.ResearchPoints ?? 0;
        public TechnologyId? ActiveResearch => _prestigeState?.TechnologyTree.ActiveResearch;
        public long ActiveResearchConsumed => _prestigeState?.TechnologyTree.ActiveResearchConsumed ?? 0;
        public long TotalResearchPointsInvested => _totalBaseResearchCostCompleted;
        public long MaxResearchPoints => BaseMaxResearchPoints + (long)(_totalBaseResearchCostCompleted * MaxResearchPointsInvestedRate);

        private TechnologyTree? Tree => _prestigeState?.TechnologyTree;

        internal ResearchController() { }

        internal void Initialize(WorldState? state, GameClock? clock, PrestigeState? prestigeState, GameSettings? settings = null)
        {
            if (_clock != null)
                _clock.Advanced -= OnClockAdvanced;

            _state = state;
            _clock = clock;
            _prestigeState = prestigeState;
            _settings = settings;

            _totalBaseResearchCostCompleted = 0;
            if (prestigeState != null)
                foreach (var id in prestigeState.TechnologyTree.CompletedTechnologies)
                    _totalBaseResearchCostCompleted += TechnologyDefinitions.Get(id)?.Cost ?? 0;

            if (_clock != null)
                _clock.Advanced += OnClockAdvanced;
        }

        private void OnClockAdvanced(object? sender, GameClockAdvancedEventArgs e)
        {
            try { ProduceResearchPoints(); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[ResearchController] {nameof(ProduceResearchPoints)}: {ex}"); }
            try { AdvanceActiveResearch(); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[ResearchController] {nameof(AdvanceActiveResearch)}: {ex}"); }
        }

        private void ProduceResearchPoints()
        {
            if (_state == null || _clock == null || Tree == null) return;
            var tree = Tree;
            if (tree.ResearchPoints >= MaxResearchPoints) return;

            long now = _clock.CurrentTick;
            double productionSpeed = _state.PlayerCivilization.ResearchProductionSpeed;
            foreach (var city in _state.PlayerCivilization.Cities)
            {
                var library = city.Buildings.OfType<Library>().FirstOrDefault();
                if (library == null || !library.CanProduceResearch) continue;

                long cooldown = Math.Max(1L, (long)(library.GetResearchCooldownTicks() / productionSpeed));
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

                long cooldown = Math.Max(1L, (long)(lab.GetResearchCooldownTicks() / productionSpeed));
                if (lab.LastResearchTick == 0)
                {
                    lab.LastResearchTick = now;
                    continue;
                }
                if (now - lab.LastResearchTick < cooldown) continue;
                if (_state.PlayerCivilization.GetResourceQuantity(Resource.Gold) < 1)
                {
                    _state.PlayerCivilization.RaiseLowStock(Resource.Gold);
                    continue;
                }

                _state.PlayerCivilization.RemoveResource(Resource.Gold, 1);
                int batch = Laboratory.ResearchPointsPerBatch + _state.PlayerCivilization.LaboratoryResearchBonus;
                tree.ResearchPoints = Math.Min(tree.ResearchPoints + batch, MaxResearchPoints);
                lab.LastResearchTick = now;

                int goldQty = _state.PlayerCivilization.GetResourceQuantity(Resource.Gold);
                int goldMax = _state.PlayerCivilization.GetResourceMaxQuantity(Resource.Gold);
                if (goldMax > 0 && goldQty * 10 <= goldMax)
                    _state.PlayerCivilization.RaiseLowStock(Resource.Gold);
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

            double speed = _state.PlayerCivilization.ResearchInvestmentSpeed;
            long consumed = Math.Max(1L, (long)(tree.ResearchPoints / 100.0 * speed));
            consumed = Math.Min(consumed, tree.ResearchPoints);
            tree.ResearchPoints -= consumed;
            tree.ActiveResearchConsumed += consumed;
            tree.ActiveResearchLastConsumptionTick = now;

            long effectiveCost = GetEffectiveCost(tech);
            if (tree.ActiveResearchConsumed >= effectiveCost)
            {
                // Compte le coût de BASE (non réduit) de la recherche terminée, pas la progression en cours
                // ni le coût réellement payé (voir commentaire sur _totalBaseResearchCostCompleted).
                _totalBaseResearchCostCompleted += tech.Cost;
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

        public bool IsDemoLocked(TechnologyId id)
            => _settings?.DemoMode == true && (TechnologyDefinitions.Get(id)?.Tier ?? 0) >= 4;

        public bool StartResearch(TechnologyId id)
        {
            if (_state == null || Tree == null) return false;
            if (IsDemoLocked(id)) return false;
            var tree = Tree;
            if (tree.CompletedTechnologies.Contains(id)) return false;
            if (tree.ActiveResearch == id) return false;

            var tech = TechnologyDefinitions.Get(id);
            if (tech == null) return false;
            if (!ArePrerequisitesMet(tree, tech)) return false;
            if (!IsPrestigeRequirementMet(id)) return false;
            if (!IsDominionRequirementMet(id)) return false;

            tree.ActiveResearch = id;
            tree.ActiveResearchConsumed = 0;
            tree.ActiveResearchLastConsumptionTick = _clock?.CurrentTick ?? 0;
            return true;
        }

        /// <summary>Taux de remboursement des points investis en cas d'annulation (base 50%, +bonus Académie, plafonné à 100%).</summary>
        public double GetCancelRefundRate()
            => Math.Min(1.0, 0.5 + (_state?.PlayerCivilization.ResearchCancelRefundBonus ?? 0.0));

        /// <summary>Points qui seraient récupérés si la recherche en cours était annulée maintenant.</summary>
        public long GetCancelRefundAmount()
            => (long)(ActiveResearchConsumed * GetCancelRefundRate());

        /// <summary>True si l'annulation entraînerait une perte de points (remboursement &lt; 100%).</summary>
        public bool HasCancelLoss()
            => GetCancelRefundAmount() < ActiveResearchConsumed;

        public bool CancelResearch()
        {
            if (Tree == null) return false;
            var tree = Tree;
            if (tree.ActiveResearch == null) return false;

            long refund = GetCancelRefundAmount();
            tree.ResearchPoints = Math.Min(tree.ResearchPoints + refund, MaxResearchPoints);
            tree.ActiveResearch = null;
            tree.ActiveResearchConsumed = 0;
            tree.ActiveResearchLastConsumptionTick = 0;
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
            if (IsDemoLocked(id)) return false;
            if (!IsResearchQueueUnlocked()) return false;
            var tree = Tree;
            if (tree.CompletedTechnologies.Contains(id)) return false;
            if (tree.ActiveResearch == id) return false;
            var tech = TechnologyDefinitions.Get(id);
            if (tech == null) return false;
            if (!IsPrestigeRequirementMet(id)) return false;
            if (!IsDominionRequirementMet(id)) return false;
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
            if (IsDemoLocked(id)) return TechnologyStatus.Inactive;
            var tree = Tree;

            if (tree.CompletedTechnologies.Contains(id)) return TechnologyStatus.Completed;
            if (tree.ActiveResearch == id) return TechnologyStatus.InProgress;

            var tech = TechnologyDefinitions.Get(id);
            if (tech == null || !ArePrerequisitesMet(tree, tech) || !IsPrestigeRequirementMet(id)
                || !IsDominionRequirementMet(id)) return TechnologyStatus.Inactive;

            return TechnologyStatus.Available;
        }

        public (long consumed, long total) GetResearchProgress(TechnologyId id)
        {
            if (Tree == null) return (0, 1);
            var tree = Tree;
            var tech = TechnologyDefinitions.Get(id);
            if (tech == null) return (0, 1);

            long cost = GetEffectiveCost(tech);
            if (tree.ActiveResearch == id)
                return (tree.ActiveResearchConsumed, cost);
            if (tree.CompletedTechnologies.Contains(id))
                return (cost, cost);
            return (0, cost);
        }

        public (double percent, double perSecond) GetResearchConsumptionInfo()
        {
            if (Tree?.ActiveResearch == null || ResearchPoints <= 0) return (0, 0);
            double speed = _state?.PlayerCivilization.ResearchInvestmentSpeed ?? 1.0;
            long consumed = Math.Max(1L, (long)(ResearchPoints / 100.0 * speed));
            double perSecond = consumed * (100.0 / ResearchConsumptionCooldownTicks);
            double percent = consumed * 100.0 / ResearchPoints;
            return (percent, perSecond);
        }

        public double GetResearchPointsPerSecond()
        {
            if (_state == null) return 0.0;
            double productionSpeed = _state.PlayerCivilization.ResearchProductionSpeed;
            double total = 0.0;
            foreach (var city in _state.PlayerCivilization.Cities)
            {
                var library = city.Buildings.OfType<Library>().FirstOrDefault();
                if (library == null || !library.CanProduceResearch) continue;
                long cooldown = library.GetResearchCooldownTicks();
                total += 100.0 / cooldown * productionSpeed;

                var lab = city.Buildings.OfType<Laboratory>().FirstOrDefault();
                if (lab == null || lab.Level < 1 || lab.ActivationStatus != ActivationStatus.ACTIVE) continue;
                long labCooldown = lab.GetResearchCooldownTicks();
                total += Laboratory.ResearchPointsPerBatch * 100.0 / labCooldown * productionSpeed;
            }
            return total;
        }

        public bool IsResearchUnlocked()
            => _state?.PlayerCivilization.ModifierAggregator.HasModifier(Modifier.ECategory.UNLOCK_RESEARCH_SYSTEM) == true;

        public bool IsResearchQueueUnlocked()
            => _state?.PlayerCivilization.ModifierAggregator.HasModifier(Modifier.ECategory.UNLOCK_RESEARCH_QUEUE) == true;

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

        /// <summary>
        /// Vrai si la recherche n'exige pas le Dominion, ou si le pouvoir divin Foi est débloqué
        /// (UNLOCK_DOMINION) — même verrou que les vertex/hexes de prestige du Dominion.
        /// </summary>
        private bool IsDominionRequirementMet(TechnologyId id)
        {
            var tech = TechnologyDefinitions.Get(id);
            if (tech == null || !tech.RequiresDominionUnlock) return true;
            return _state?.PlayerCivilization.ModifierAggregator.HasModifier(
                Modifier.ECategory.UNLOCK_DOMINION) == true;
        }

        public bool ShouldDisplay(TechnologyId id)
        {
            if (Tree == null) return false;
            var tree = Tree;

            if (tree.CompletedTechnologies.Contains(id)) return true;
            if (tree.ActiveResearch == id) return true;

            // En mode démo : affiche les nœuds tier 4+ seulement si tous leurs prérequis sont tier < 4
            // (une seule rangée de cadenas visible, les tiers suivants restent cachés)
            if (IsDemoLocked(id))
            {
                var tech = TechnologyDefinitions.Get(id);
                if (tech == null) return false;
                return tech.Prerequisites.All(p => !IsDemoLocked(p));
            }

            if (!IsPrestigeRequirementMet(id)) return false;
            if (!IsDominionRequirementMet(id)) return false;

            var techDef = TechnologyDefinitions.Get(id);
            if (techDef == null) return false;

            if (ArePrerequisitesMet(tree, techDef)) return true;

            // Visible si tous les prérequis manquants sont eux-mêmes faisables (Available ou InProgress)
            foreach (var prereqId in techDef.Prerequisites)
            {
                if (tree.CompletedTechnologies.Contains(prereqId)) continue;
                var prereqStatus = GetStatus(prereqId);
                if (prereqStatus != TechnologyStatus.Available && prereqStatus != TechnologyStatus.InProgress)
                    return false;
            }
            return true;
        }

        private long GetEffectiveCost(Technology tech)
        {
            double reduction = _state?.PlayerCivilization.ResearchCostReduction ?? 0.0;
            return Math.Max(1L, (long)(tech.Cost * (1.0 - reduction)));
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
