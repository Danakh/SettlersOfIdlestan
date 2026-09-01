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
        private GodState? _godState;

        public const long ResearchConsumptionCooldownTicks = 100L;

        // Plafond dynamique : base fixe + un bonus proportionnel à la somme des coûts de BASE (non réduits)
        // des recherches terminées. Calculé une fois au chargement (Initialize) puis mis à jour incrémentalement
        // à chaque recherche terminée — jamais persisté, donc aucune migration de save n'est nécessaire.
        public const int BaseMaxResearchPoints = 1000;
        public const double MaxResearchPointsInvestedRate = 0.1;

        // long : la somme des coûts de base (et les coûts unitaires des tiers 13+) dépasse int.MaxValue.
        private long _totalBaseResearchCostCompleted;

        /// <summary>Intervalle du cycle de génération plate de points de recherche (RESEARCH_POINTS_PASSIVE_GENERATION, ex. Académie) : 100 ticks = 1 seconde.</summary>
        private const long FlatResearchGenerationIntervalTicks = 100L;

        /// <summary>Dernier tick de génération plate, non persisté — voir PassiveGenerationEngine pour le même patron.</summary>
        private long _lastFlatResearchGenTick;

        public event EventHandler<TechnologyId>? OnResearchCompleted;

        // Convenience accessors for renderers — go through PrestigeState so the source is explicit.
        public long ResearchPoints => _prestigeState?.TechnologyTree.ResearchPoints ?? 0;
        public TechnologyId? ActiveResearch => _prestigeState?.TechnologyTree.ActiveResearch;
        public long ActiveResearchConsumed => _prestigeState?.TechnologyTree.ActiveResearchConsumed ?? 0;
        public long TotalResearchPointsInvested => _totalBaseResearchCostCompleted;
        public long MaxResearchPoints => BaseMaxResearchPoints + (long)(_totalBaseResearchCostCompleted * MaxResearchPointsInvestedRate);

        private TechnologyTree? Tree => _prestigeState?.TechnologyTree;

        internal ResearchController() { }

        internal void Initialize(WorldState? state, GameClock? clock, PrestigeState? prestigeState, GameSettings? settings = null, GodState? godState = null)
        {
            if (_clock != null)
                _clock.Advanced -= OnClockAdvanced;

            _state = state;
            _clock = clock;
            _prestigeState = prestigeState;
            _settings = settings;
            _godState = godState;

            _totalBaseResearchCostCompleted = 0;
            // Non persisté (recréé/réinitialisé à chaque Initialize, y compris au chargement d'une
            // sauvegarde) : le seeder au tick courant plutôt qu'à 0, sinon TickCooldown calcule un
            // nombre de cycles de rattrapage proportionnel à tout le tick courant (voir PassiveGenerationEngine).
            _lastFlatResearchGenTick = clock?.CurrentTick ?? 0;
            if (prestigeState != null)
                foreach (var id in prestigeState.TechnologyTree.CompletedTechnologies)
                {
                    var tech = TechnologyDefinitions.Get(id);
                    if (tech == null) continue;
                    // Une recherche répétable a pu être complétée plusieurs fois : chaque complétion
                    // contribue son coût de base pour CE palier (croissant à chaque relance, voir
                    // GetRepeatCostFactor), pas un simple multiple linéaire (voir l'incrément
                    // équivalent dans AdvanceActiveResearch).
                    if (tech.Repeatable)
                    {
                        int repeats = prestigeState.TechnologyTree.RepeatCounts.TryGetValue(id, out var c) ? c : 1;
                        for (int i = 0; i < repeats; i++)
                            _totalBaseResearchCostCompleted += (long)(tech.Cost * GetRepeatCostFactor(i));
                    }
                    else
                    {
                        _totalBaseResearchCostCompleted += tech.Cost;
                    }
                }

            if (_clock != null)
                _clock.Advanced += OnClockAdvanced;
        }

        private void OnClockAdvanced(object? sender, GameClockAdvancedEventArgs e)
        {
            try { ProduceResearchPoints(); }
            catch (Exception ex) { GameLog.Error(nameof(ResearchController), nameof(ProduceResearchPoints), ex); }
            try { AdvanceActiveResearch(); }
            catch (Exception ex) { GameLog.Error(nameof(ResearchController), nameof(AdvanceActiveResearch), ex); }
        }

        private void ProduceResearchPoints()
        {
            if (_state == null || _clock == null || Tree == null) return;
            var tree = Tree;
            if (tree.ResearchPoints >= MaxResearchPoints) return;

            long now = _clock.CurrentTick;
            double productionSpeed = _state.PlayerCivilization.ResearchProductionSpeed;

            long flatLast = _lastFlatResearchGenTick;
            long flatCycles = TickCooldown.ConsumeElapsedCycles(now, ref flatLast, FlatResearchGenerationIntervalTicks, coldStartOnZero: true);
            _lastFlatResearchGenTick = flatLast;
            if (flatCycles > 0)
            {
                double flatPerSecond = _state.PlayerCivilization.ModifierAggregator.ApplyModifiers(
                    Modifier.ECategory.RESEARCH_POINTS_PASSIVE_GENERATION, "", 0.0);
                if (flatPerSecond > 0)
                    tree.ResearchPoints = Math.Min(tree.ResearchPoints + (long)(flatPerSecond * flatCycles), MaxResearchPoints);
            }

            foreach (var city in _state.PlayerCivilization.Cities)
            {
                var library = city.FindBuilding<Library>(BuildingType.Library);
                if (library == null || !library.CanProduceResearch) continue;

                long cooldown = Math.Max(1L, (long)(library.GetResearchCooldownTicks() / productionSpeed));
                long lastTick = library.LastResearchTick;
                // +1 par cycle, sans aléatoire ni dépendance au stock courant : les cycles rattrapés
                // pendant un saut de temps peuvent donc être ajoutés en une fois plutôt que rejoués un
                // par un (voir TickCooldown).
                long cycles = TickCooldown.ConsumeElapsedCycles(now, ref lastTick, cooldown, coldStartOnZero: true);
                library.LastResearchTick = lastTick;
                if (cycles <= 0) continue;

                tree.ResearchPoints = Math.Min(tree.ResearchPoints + cycles, MaxResearchPoints);
            }

            foreach (var city in _state.PlayerCivilization.Cities)
            {
                var lab = city.FindBuilding<Laboratory>(BuildingType.Laboratory);
                if (lab == null || lab.Level < 1 || lab.ActivationStatus != ActivationStatus.ACTIVE) continue;

                long cooldown = Math.Max(1L, (long)(lab.GetResearchCooldownTicks() / productionSpeed));
                long lastTick = lab.LastResearchTick;
                long cycles = TickCooldown.ConsumeElapsedCycles(now, ref lastTick, cooldown, coldStartOnZero: true);
                lab.LastResearchTick = lastTick;
                if (cycles <= 0) continue;

                // Rejoué cycle par cycle (pas une multiplication directe) : chaque cycle dépend du
                // stock d'or courant, qui peut s'épuiser avant d'avoir consommé tous les cycles dus.
                int batch = Laboratory.ResearchPointsPerBatch + _state.PlayerCivilization.LaboratoryResearchBonus;
                for (long i = 0; i < cycles; i++)
                {
                    if (_state.PlayerCivilization.GetResourceQuantity(Resource.Gold) < 1)
                    {
                        _state.PlayerCivilization.RaiseLowStock(Resource.Gold);
                        break;
                    }

                    _state.PlayerCivilization.RemoveResource(Resource.Gold, 1);
                    tree.ResearchPoints = Math.Min(tree.ResearchPoints + batch, MaxResearchPoints);

                    int goldQty = _state.PlayerCivilization.GetResourceQuantity(Resource.Gold);
                    int goldMax = _state.PlayerCivilization.GetResourceMaxQuantity(Resource.Gold);
                    if (goldMax > 0 && goldQty * 10 <= goldMax)
                        _state.PlayerCivilization.RaiseLowStock(Resource.Gold);

                    if (tree.ResearchPoints >= MaxResearchPoints) break;
                }
            }
        }

        /// <summary>Clé de source pour l'or consommé par les Laboratoires actifs, pour l'infobulle de ressource.</summary>
        public const string LaboratoryGoldConsumptionSourceKey = "tooltip_source_laboratory_production";

        /// <summary>
        /// Or/seconde consommé par les Laboratoires actifs (niveau ≥ 1), toutes villes de la civilisation
        /// confondues. Reflète les mêmes conditions que <see cref="ProduceResearchPoints"/> : chaque
        /// Laboratoire éligible tourne son propre cooldown et consomme 1 or à la fin de celui-ci.
        /// </summary>
        public double GetLaboratoryGoldConsumptionRate(int civilizationIndex)
        {
            var civ = _state?.GetCivilization(civilizationIndex);
            if (civ == null) return 0;

            double productionSpeed = civ.ResearchProductionSpeed;
            const double ticksPerSecond = 100.0;
            double total = 0;

            foreach (var city in civ.Cities)
            {
                var lab = city.FindBuilding<Laboratory>(BuildingType.Laboratory);
                if (lab == null || lab.Level < 1 || lab.ActivationStatus != ActivationStatus.ACTIVE) continue;

                long cooldown = Math.Max(1L, (long)(lab.GetResearchCooldownTicks() / productionSpeed));
                total += ticksPerSecond / cooldown;
            }

            return total;
        }

        /// <summary>
        /// Vrai si la civilisation possède au moins un Laboratoire construit et actif. Sert à afficher la
        /// ligne de consommation d'or même à 0/s, pour la même raison que <see cref="SettlersOfIdlestan.Controller.Military.MilitaryController.HasAnySoldierProductionBuilding"/>.
        /// </summary>
        public bool HasAnyActiveLaboratory(int civilizationIndex)
        {
            var civ = _state?.GetCivilization(civilizationIndex);
            if (civ == null) return false;
            return civ.Cities.Any(c => c.FindBuilding<Laboratory>(BuildingType.Laboratory) is { } lab
                && lab.Level >= 1 && lab.ActivationStatus == ActivationStatus.ACTIVE);
        }

        private void AdvanceActiveResearch()
        {
            if (_state == null || _clock == null || Tree == null) return;
            var tree = Tree;
            if (tree.ActiveResearch == null || tree.ResearchPoints <= 0) return;

            long now = _clock.CurrentTick;
            long lastTick = tree.ActiveResearchLastConsumptionTick;
            long cycles = TickCooldown.ConsumeElapsedCycles(now, ref lastTick, ResearchConsumptionCooldownTicks, coldStartOnZero: true);
            tree.ActiveResearchLastConsumptionTick = lastTick;
            if (cycles <= 0) return;

            double speed = _state.PlayerCivilization.ResearchInvestmentSpeed;

            // Rejoué cycle par cycle (pas une formule fermée) : le montant consommé par cycle est un
            // pourcentage du pool courant, donc compose d'un cycle à l'autre — et un cycle peut faire
            // franchir un palier, ce qui bascule ActiveResearch vers la recherche suivante (file ou
            // répétition) pour les cycles restants du même événement.
            for (long i = 0; i < cycles; i++)
            {
                if (tree.ActiveResearch == null || tree.ResearchPoints <= 0) break;

                var techId = tree.ActiveResearch.Value;
                var tech = TechnologyDefinitions.Get(techId);
                if (tech == null) { tree.ActiveResearch = null; break; }

                long consumed = Math.Max(1L, (long)(tree.ResearchPoints / 100.0 * speed));
                consumed = Math.Min(consumed, tree.ResearchPoints);
                tree.ResearchPoints -= consumed;
                tree.ActiveResearchConsumed += consumed;

                long effectiveCost = GetEffectiveCost(tech);
                if (tree.ActiveResearchConsumed >= effectiveCost)
                    CompleteActiveResearch(tree, techId, tech);
            }
        }

        /// <summary>
        /// Palier de recherche active atteint (coût couvert) : comptabilise le coût de base, marque
        /// la recherche complétée et enchaîne — répétition en boucle ou recherche en file. Factorisé
        /// hors de <see cref="AdvanceActiveResearch"/> pour être rejouable plusieurs fois par
        /// événement <c>Advanced</c> quand un saut de temps fait franchir plusieurs paliers d'affilée.
        /// </summary>
        private void CompleteActiveResearch(TechnologyTree tree, TechnologyId techId, Technology tech)
        {
            // Compte le coût de BASE (non réduit) de la recherche terminée, pas la progression en cours
            // ni le coût réellement payé (voir commentaire sur _totalBaseResearchCostCompleted). Pour une
            // recherche répétable, ce coût de base croît à chaque relance (voir GetRepeatCostFactor) : il
            // faut donc le coût du palier qui vient d'être complété, pas le coût de base du palier 1.
            long baseCostCompleted = tech.Cost;
            if (tech.Repeatable)
            {
                int repeatCount = tree.RepeatCounts.TryGetValue(techId, out var rc) ? rc : 0;
                baseCostCompleted = (long)(tech.Cost * GetRepeatCostFactor(repeatCount));
            }
            _totalBaseResearchCostCompleted += baseCostCompleted;
            tree.CompleteResearch(techId);
            OnResearchCompleted?.Invoke(this, techId);

            // Recherche répétable en boucle : se relance elle-même indéfiniment (reste sa propre "file")
            if (tech.Repeatable && tree.LoopResearch == techId)
            {
                StartResearch(techId);
                return;
            }

            // Auto-démarrer la recherche suivante si elle est en file d'attente
            if (tree.QueuedResearch.HasValue)
            {
                var queued = tree.QueuedResearch.Value;
                tree.QueuedResearch = null;
                StartResearch(queued);

                // Si la recherche qui prend le relais est répétable, elle devient la nouvelle
                // répétition par défaut (comportement attendu : file et répétition ne coexistent
                // jamais, donc la file "se transforme" en répétition une fois lancée).
                var queuedTech = TechnologyDefinitions.Get(queued);
                if (queuedTech?.Repeatable == true)
                    tree.LoopResearch = queued;
            }
        }

        public bool IsDemoLocked(TechnologyId id)
            => _settings?.DemoMode == true && (TechnologyDefinitions.Get(id)?.Tier ?? 0) >= 4;

        public bool StartResearch(TechnologyId id)
        {
            if (_state == null || Tree == null) return false;
            if (IsDemoLocked(id)) return false;
            var tree = Tree;

            var tech = TechnologyDefinitions.Get(id);
            if (tech == null) return false;

            // Déjà accordée gratuitement par le bâtiment unique permanent correspondant (voir
            // IsFreeUniqueBuildingGrant) : rien à (re)chercher.
            if (IsFreeUniqueBuildingGrant(id)) return false;

            bool alreadyCompleted = tree.CompletedTechnologies.Contains(id);
            if (alreadyCompleted && !tech.Repeatable) return false;
            if (tree.ActiveResearch == id) return false;

            if (!ArePrerequisitesMet(tree, tech)) return false;
            if (!IsPrestigeRequirementMet(id)) return false;
            if (!IsDominionRequirementMet(id)) return false;

            tree.ActiveResearch = id;
            tree.ActiveResearchConsumed = 0;
            tree.ActiveResearchLastConsumptionTick = _clock?.CurrentTick ?? 0;
            return true;
        }

        /// <summary>Nombre de fois où cette recherche répétable a déjà été complétée (0 si jamais, ou non répétable).</summary>
        public int GetRepeatCount(TechnologyId id)
            => Tree?.RepeatCounts.TryGetValue(id, out var count) == true ? count : 0;

        /// <summary>True si le bouton "loop" peut être proposé pour cette recherche (répétable + file débloquée).</summary>
        public bool CanLoop(TechnologyId id)
        {
            if (Tree == null || !IsResearchQueueUnlocked()) return false;
            return TechnologyDefinitions.Get(id)?.Repeatable == true;
        }

        public bool IsLoopEnabled(TechnologyId id) => Tree?.LoopResearch == id;

        public bool ToggleLoopResearch(TechnologyId id)
        {
            if (Tree == null || !CanLoop(id)) return false;
            if (Tree.LoopResearch == id)
            {
                Tree.LoopResearch = null;
            }
            else
            {
                // Activer la répétition désactive la file : les deux sont mutuellement exclusives.
                Tree.LoopResearch = id;
                Tree.QueuedResearch = null;
            }
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
            if (!IsResearchCancelUnlocked()) return false;
            var tree = Tree;
            if (tree.ActiveResearch == null) return false;

            long refund = GetCancelRefundAmount();
            tree.ResearchPoints = Math.Min(tree.ResearchPoints + refund, MaxResearchPoints);
            if (tree.LoopResearch == tree.ActiveResearch)
                tree.LoopResearch = null;
            tree.ActiveResearch = null;
            tree.ActiveResearchConsumed = 0;
            tree.ActiveResearchLastConsumptionTick = 0;

            // Si une recherche différente était en file d'attente, elle démarre immédiatement
            // au lieu de laisser le slot actif vide (même logique qu'à la complétion normale,
            // voir AdvanceActiveResearch).
            if (tree.QueuedResearch.HasValue)
            {
                var queued = tree.QueuedResearch.Value;
                tree.QueuedResearch = null;
                StartResearch(queued);

                var queuedTech = TechnologyDefinitions.Get(queued);
                if (queuedTech?.Repeatable == true)
                    tree.LoopResearch = queued;
            }
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
            // Mettre une recherche en file désactive la répétition en cours : les deux sont mutuellement exclusives.
            tree.LoopResearch = null;
            return true;
        }

        public bool CanBeQueued(TechnologyId id)
        {
            if (Tree == null) return false;
            if (IsDemoLocked(id)) return false;
            if (!IsResearchQueueUnlocked()) return false;
            var tree = Tree;
            var tech = TechnologyDefinitions.Get(id);
            if (tech == null) return false;
            if (IsFreeUniqueBuildingGrant(id)) return false;
            if (tree.CompletedTechnologies.Contains(id) && !tech.Repeatable) return false;
            if (tree.ActiveResearch == id) return false;
            if (!IsPrestigeRequirementMet(id)) return false;
            if (!IsDominionRequirementMet(id)) return false;
            return ArePrerequisitesMet(tree, tech) || WillBeAvailableAfterActiveResearch(tree, tech);
        }

        private bool WillBeAvailableAfterActiveResearch(TechnologyTree tree, Technology tech)
        {
            if (tree.ActiveResearch == null) return false;
            var activeId = tree.ActiveResearch.Value;
            if (!tech.Prerequisites.Contains(activeId)) return false;
            foreach (var prereq in tech.Prerequisites)
            {
                if (prereq != activeId && !IsPrerequisiteSatisfied(tree, prereq))
                    return false;
            }
            return true;
        }

        public TechnologyStatus GetStatus(TechnologyId id)
        {
            if (Tree == null) return TechnologyStatus.Inactive;
            if (IsDemoLocked(id)) return TechnologyStatus.Inactive;
            var tree = Tree;

            // Vérifie ActiveResearch en premier : une recherche répétable en cours de relance est à la fois
            // "déjà complétée" (CompletedTechnologies) et "en cours" — c'est ce second état qui doit primer.
            if (tree.ActiveResearch == id) return TechnologyStatus.InProgress;
            if (tree.CompletedTechnologies.Contains(id)) return TechnologyStatus.Completed;
            if (IsFreeUniqueBuildingGrant(id)) return TechnologyStatus.Completed;

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
            if (tree.CompletedTechnologies.Contains(id) || IsFreeUniqueBuildingGrant(id))
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
            double total = _state.PlayerCivilization.ModifierAggregator.ApplyModifiers(
                Modifier.ECategory.RESEARCH_POINTS_PASSIVE_GENERATION, "", 0.0);
            foreach (var city in _state.PlayerCivilization.Cities)
            {
                var library = city.FindBuilding<Library>(BuildingType.Library);
                if (library == null || !library.CanProduceResearch) continue;
                long cooldown = library.GetResearchCooldownTicks();
                total += 100.0 / cooldown * productionSpeed;

                var lab = city.FindBuilding<Laboratory>(BuildingType.Laboratory);
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

        public bool IsResearchCancelUnlocked()
            => _state?.PlayerCivilization.ModifierAggregator.HasModifier(Modifier.ECategory.UNLOCK_RESEARCH_CANCEL) == true;

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
            if (IsFreeUniqueBuildingGrant(id)) return true;

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

            // Visible si tous les prérequis manquants sont eux-mêmes faisables (Available ou InProgress).
            // IsPrerequisiteSatisfied (et non tree.CompletedTechnologies.Contains) : un prérequis accordé
            // gratuitement (bâtiment permanent d'Ascension ou relance restaurée par Mémoire de Dieu) est
            // marqué Completed sans que son propre prérequis soit satisfait — voir IsPrerequisiteSatisfied.
            // Une recherche qui en dépend ne doit devenir visible que lorsque ce prérequis-là l'est aussi.
            foreach (var prereqId in techDef.Prerequisites)
            {
                if (IsPrerequisiteSatisfied(tree, prereqId)) continue;
                var prereqStatus = GetStatus(prereqId);
                if (prereqStatus != TechnologyStatus.Available && prereqStatus != TechnologyStatus.InProgress)
                    return false;
            }
            return true;
        }

        private long GetEffectiveCost(Technology tech)
        {
            double reduction = _state?.PlayerCivilization.ResearchCostReduction ?? 0.0;
            double baseCost = tech.Cost;

            if (tech.Repeatable && Tree != null)
            {
                int count = Tree.RepeatCounts.TryGetValue(tech.Id, out var c) ? c : 0;
                baseCost *= GetRepeatCostFactor(count);
            }

            double effective = baseCost * (1.0 - reduction);
            return effective >= long.MaxValue ? long.MaxValue : Math.Max(1L, (long)effective);
        }

        /// <summary>
        /// Multiplicateur appliqué au coût de base d'une recherche répétable déjà complétée
        /// <paramref name="repeats"/> fois. Sans bonus, chaque relance double le coût (facteur 2^n) ;
        /// REPEATABLE_RESEARCH_SCALING_REDUCTION rabote cette croissance — le pouvoir divin Mémoire
        /// de Dieu la réduit de moitié, soit ×1,5 par relance au lieu de ×2.
        ///
        /// <para>Le facteur courant sert aussi au cumul des coûts de base déjà payés
        /// (_totalBaseResearchCostCompleted) : celui-ci est recalculé à chaque Initialize, donc
        /// toujours cohérent avec le facteur en vigueur au chargement.</para>
        /// </summary>
        private double GetRepeatCostFactor(int repeats)
        {
            double reduction = Math.Clamp(_state?.PlayerCivilization.RepeatableResearchScalingReduction ?? 0.0, 0.0, 1.0);
            return Math.Pow(1.0 + (1.0 - reduction), repeats);
        }

        private bool ArePrerequisitesMet(TechnologyTree tree, Technology tech)
        {
            foreach (var prereq in tech.Prerequisites)
                if (!IsPrerequisiteSatisfied(tree, prereq))
                    return false;
            return true;
        }

        /// <summary>
        /// Vrai si <paramref name="id"/> compte comme rempli en tant que prérequis d'une autre recherche.
        /// Pour une recherche normalement terminée (financée par des points de recherche), toujours vrai.
        /// Pour une recherche accordée gratuitement — <see cref="IsFreeUniqueBuildingGrant"/> (bâtiment
        /// unique déjà rendu permanent par un choix d'Héritage divin) ou <see cref="IsFreeRepeatableGrant"/>
        /// (recherche répétable dont la complétion actuelle ne dépasse pas le palier restauré gratuitement
        /// par Mémoire de Dieu, voir AscensionController.RestoreRepeatableResearchToBest) — ne compte
        /// que si ses propres prérequis sont eux-mêmes remplis (récursivement) : une recherche gratuite
        /// ne doit jamais permettre de sauter la partie de l'arbre qu'elle est censée sanctionner.
        /// </summary>
        private bool IsPrerequisiteSatisfied(TechnologyTree tree, TechnologyId id)
        {
            bool completed = tree.CompletedTechnologies.Contains(id);
            bool freeUnique = !completed && IsFreeUniqueBuildingGrant(id);
            if (!completed && !freeUnique) return false;

            bool freeGrant = freeUnique || (completed && IsFreeRepeatableGrant(tree, id));
            if (!freeGrant) return true;

            var tech = TechnologyDefinitions.Get(id);
            if (tech == null) return true;
            foreach (var prereq in tech.Prerequisites)
                if (!IsPrerequisiteSatisfied(tree, prereq))
                    return false;
            return true;
        }

        /// <summary>
        /// Vrai si le seul effet de la recherche <paramref name="id"/> est de débloquer (BUILDING_MAX_LEVEL)
        /// un bâtiment unique actuellement choisi comme bâtiment permanent d'Ascension (voir
        /// AscensionController.PermanentUniqueBuildings/SelectPermanentUniqueBuilding) : le bâtiment
        /// fonctionne déjà pleinement sans que la recherche soit terminée (Civilization.
        /// SetAscensionGrantedUniqueBuildings l'ignore), donc la recherche elle-même n'apporte plus rien
        /// et est accordée gratuitement (voir GetStatus/StartResearch). Calculé en direct plutôt que
        /// persisté dans CompletedTechnologies : la sélection du bâtiment permanent étant réversible
        /// (DeselectPermanentUniqueBuilding), la gratuité de la recherche doit l'être tout autant.
        /// </summary>
        private bool IsFreeUniqueBuildingGrant(TechnologyId id)
        {
            if (_godState == null) return false;
            var tech = TechnologyDefinitions.Get(id);
            if (tech == null || tech.Repeatable || tech.Modifiers.Count != 1) return false;

            var modifier = tech.Modifiers[0];
            if (modifier.Category != Modifier.ECategory.BUILDING_MAX_LEVEL) return false;
            if (!Enum.TryParse<BuildingType>(modifier.SubCategory, out var buildingType)) return false;

            return _godState.AscensionState.PermanentUniqueBuildings.Contains(buildingType);
        }

        /// <summary>
        /// Vrai si la recherche répétable <paramref name="id"/> (déjà dans CompletedTechnologies) ne
        /// doit sa complétion actuelle qu'à la restauration de Mémoire de Dieu (AscensionState.
        /// BestRepeatCounts, voir AscensionController.RestoreRepeatableResearchToBest) et non à une
        /// relance effectivement financée ce cycle-ci : le palier courant ne dépasse pas le palier
        /// restauré gratuitement. Dès qu'une relance réelle porte le palier au-delà, StartResearch
        /// avait déjà vérifié ses prérequis à ce moment-là — la recherche redevient donc un prérequis
        /// valide sans condition.
        /// </summary>
        private bool IsFreeRepeatableGrant(TechnologyTree tree, TechnologyId id)
        {
            if (_godState == null) return false;
            var tech = TechnologyDefinitions.Get(id);
            if (tech == null || !tech.Repeatable) return false;

            int best = _godState.AscensionState.BestRepeatCounts.TryGetValue(id, out var b) ? b : 0;
            if (best <= 0) return false;

            int current = tree.RepeatCounts.TryGetValue(id, out var c) ? c : 0;
            return current <= best;
        }
    }
}
