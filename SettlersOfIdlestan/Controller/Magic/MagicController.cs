using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandFeatures;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Magic;
using SettlersOfIdlestan.Model.Monsters;
using SettlersOfIdlestan.Model.Prestige;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SettlersOfIdlestan.Controller.Magic
{
    /// <summary>
    /// Gère les rituels magiques : lancement (coût en cristaux = base × puissance²),
    /// entretien par cycle (base × puissance²), effet linéaire (× puissance) via
    /// <see cref="MagicModifierProvider"/>, effondrement quand les cristaux manquent.
    /// Le nombre de Tours de Mages limite le nombre de rituels actifs ; la somme de
    /// leurs niveaux limite la puissance totale.
    /// Gère aussi la génération passive de cristaux des Cercles de Fées,
    /// et leur apparition sur l'île quand les vertex de prestige sont achetés.
    /// </summary>
    public class MagicController
    {
        /// <summary>Durée d'un cycle d'entretien des rituels (1000 ticks = 10 s).</summary>
        public const long UpkeepIntervalTicks = 1000L;

        /// <summary>Intervalle entre deux ajustements de puissance des rituels automatisés (100 ticks = 1 s).</summary>
        public const long RitualAutomationIntervalTicks = 100L;

        /// <summary>Intervalle entre deux applications des dégâts du rituel Lumière des Profondeurs (100 ticks = 1 s).</summary>
        public const long TempleMonsterDamageIntervalTicks = 100L;

        /// <summary>Bonus additif de puissance maximale par niveau cumulé de Tour de Mages (10 %).</summary>
        public const double MageTowerPowerBonusPerLevel = 0.10;

        /// <summary>
        /// Nombre maximum de charges de lancement qu'un sort peut accumuler sous Magie Divine (voir
        /// <see cref="GetSpellCharges"/>), affiché sous forme de 5 cercles sous la barre de cooldown.
        /// </summary>
        public const int MaxSpellCharges = 5;

        private WorldState? _state;
        private GameClock? _clock;
        private GamePRNG? _prng;
        private MagicModifierProvider? _provider;
        private long _lastPassiveTick;
        private long _lastTempleDamageTick;
        private long _lastRitualAutomationTick;
        private CityBuilderController? _cityBuilder;
        private BuildingController? _buildingController;
        private HarvestController? _harvestController;
        private RoadController? _roadController;
        private GodState? _godState;

        /// <summary>Déclenché à chaque lancement/arrêt/changement de puissance d'un rituel.</summary>
        public event EventHandler? OnRitualsChanged;

        internal MagicController() { }

        internal void Initialize(WorldState? state, GameClock? clock, GamePRNG? prng = null,
            CityBuilderController? cityBuilder = null, BuildingController? buildingController = null,
            HarvestController? harvestController = null, RoadController? roadController = null,
            GodState? godState = null)
        {
            if (_clock != null)
                _clock.Advanced -= OnClockAdvanced;

            _state = state;
            _clock = clock;
            _prng = prng;
            _cityBuilder = cityBuilder;
            _buildingController = buildingController;
            _harvestController = harvestController;
            _roadController = roadController;
            _godState = godState;
            _lastPassiveTick = 0;
            // Non persisté (recréé à chaque Initialize, y compris au chargement d'une sauvegarde) :
            // seedé au tick courant plutôt qu'à 0, sinon TickCooldown calcule un nombre de cycles de
            // rattrapage proportionnel à tout le tick courant sur une partie déjà avancée (voir
            // ProcessTempleMonsterDamage, qui rejoue un cycle par cycle — potentiellement des
            // millions d'itérations au lieu du léger différé attendu en début de partie).
            _lastTempleDamageTick = clock?.CurrentTick ?? 0;
            _lastRitualAutomationTick = clock?.CurrentTick ?? 0;

            MigrateLegacyAutomationFlags();

            if (_state != null && _state.Civilizations.Count > 0)
            {
                _provider = new MagicModifierProvider(_state.Magic);
                _state.PlayerCivilization.AddCustomAggregator(_provider);
                EnsureMagicFeatures();
            }

            if (_clock != null)
                _clock.Advanced += OnClockAdvanced;
        }

        private void OnClockAdvanced(object? sender, GameClockAdvancedEventArgs e)
        {
            try { ProcessUpkeep(); }
            catch (Exception ex) { GameLog.Error(nameof(MagicController), nameof(ProcessUpkeep), ex); }
            try { ProcessPassiveCycle(); }
            catch (Exception ex) { GameLog.Error(nameof(MagicController), nameof(ProcessPassiveCycle), ex); }
            try { ProcessTempleMonsterDamage(e.CurrentTick); }
            catch (Exception ex) { GameLog.Error(nameof(MagicController), nameof(ProcessTempleMonsterDamage), ex); }
            try { ProcessSpellExhaustion(); }
            catch (Exception ex) { GameLog.Error(nameof(MagicController), nameof(ProcessSpellExhaustion), ex); }
            try { ProcessAbundanceAutoCast(); }
            catch (Exception ex) { GameLog.Error(nameof(MagicController), nameof(ProcessAbundanceAutoCast), ex); }
            try { ProcessRitualPowerAutomation(); }
            catch (Exception ex) { GameLog.Error(nameof(MagicController), nameof(ProcessRitualPowerAutomation), ex); }
        }

        // ── État général ──────────────────────────────────────────────────────

        public bool IsMagicUnlocked()
            => GetPlayerCiv()?.ModifierAggregator.HasModifier(ECategory.UNLOCK_MAGIC) == true;

        public bool IsRitualKnown(RitualId id)
            => GetPlayerCiv()?.ModifierAggregator.HasModifier(ECategory.UNLOCK_RITUAL, id.ToString()) == true;

        /// <summary>Rituels débloqués par la recherche, dans l'ordre des définitions.</summary>
        public IReadOnlyList<RitualDefinition> GetKnownRituals()
            => RitualDefinitions.All.Where(r => IsRitualKnown(r.Id)).ToList();

        public ActiveRitual? GetActiveRitual(RitualId id)
            => _state?.Magic.ActiveRituals.FirstOrDefault(r => r.Id == id);

        public IReadOnlyList<ActiveRitual> ActiveRituals
            => _state?.Magic.ActiveRituals ?? (IReadOnlyList<ActiveRitual>)Array.Empty<ActiveRitual>();

        // ── Capacités liées aux Tours de Mages ────────────────────────────────

        /// <summary>Nombre de Tours de Mages construites (niveau ≥ 1).</summary>
        public int MageTowerCount
            => GetPlayerCiv()?.Cities.Sum(c => c.Buildings.Count(b => b.Type == BuildingType.MageTower && b.Level >= 1)) ?? 0;

        /// <summary>Somme des niveaux des Tours de Mages.</summary>
        public int MageTowerTotalLevel
            => GetPlayerCiv()?.Cities.Sum(c => c.Buildings.Where(b => b.Type == BuildingType.MageTower).Sum(b => b.Level)) ?? 0;

        /// <summary>Nombre maximal de rituels actifs simultanés : fixe à 1, augmenté uniquement par l'Archimage.</summary>
        public int MaxActiveRituals
        {
            get
            {
                var civ = GetPlayerCiv();
                if (civ == null || !IsMagicUnlocked()) return 0;
                return civ.ModifierAggregator.ApplyModifiers(ECategory.RITUAL_MAX_COUNT, "", 1);
            }
        }

        /// <summary>
        /// Budget de puissance exact avant arrondi : base 1, +10 % par niveau cumulé de Tour de Mages,
        /// puis bonus additifs de prestige (Archimage, Lignes Telluriques, ...).
        /// </summary>
        public double TotalPowerBudgetExact
        {
            get
            {
                var civ = GetPlayerCiv();
                if (civ == null || !IsMagicUnlocked()) return 0;
                double towerBonus = MageTowerTotalLevel * MageTowerPowerBonusPerLevel;
                return civ.ModifierAggregator.ApplyModifiers(ECategory.RITUAL_TOTAL_POWER, "", 1.0 + towerBonus);
            }
        }

        /// <summary>Budget total de puissance, arrondi à l'inférieur.</summary>
        public int TotalPowerBudget => (int)Math.Floor(TotalPowerBudgetExact);

        /// <summary>Puissance actuellement consommée par les rituels actifs.</summary>
        public int UsedPower => _state?.Magic.ActiveRituals.Sum(r => r.Power) ?? 0;

        /// <summary>
        /// Puissance consommée par les seuls rituels non automatisés. Sert de référence au lancement et
        /// au réglage manuel de puissance (<see cref="CanLaunchRitual"/>, <see cref="CanIncreaseRitualPower"/>)
        /// à la place de <see cref="UsedPower"/> : un rituel automatisé cède immédiatement 1 point de
        /// puissance (voir <see cref="ReduceAutomatedPowerIfOverBudget"/>) dès qu'une augmentation manuelle
        /// ferait dépasser le budget total, donc le bouton + ne doit pas être bloqué par la puissance déjà
        /// occupée par l'automatisation.
        /// </summary>
        public int UsedPowerByNonAutomatedRituals
            => _state?.Magic.ActiveRituals.Where(r => !IsRitualAutomated(r.Id)).Sum(r => r.Power) ?? 0;

        /// <summary>Clé de source pour l'entretien en cristaux des rituels actifs.</summary>
        public const string RitualUpkeepSourceKey = "tooltip_source_ritual_upkeep";

        /// <summary>Cristaux/seconde consommés par l'entretien des rituels actuellement actifs.</summary>
        public double GetRitualUpkeepPerSecond()
        {
            if (_state == null) return 0.0;
            double upkeep = 0;
            foreach (var active in _state.Magic.ActiveRituals)
            {
                var def = RitualDefinitions.Get(active.Id);
                if (def != null) upkeep += GetUpkeepCost(def, active.Power);
            }
            return upkeep / (UpkeepIntervalTicks / 100.0);
        }

        /// <summary>
        /// Détaille toutes les sources de gain et de perte de cristaux/seconde (récolte automatique,
        /// génération passive, entretien des Huttes d'Alchimie, investissement en Monuments, entretien
        /// des rituels). Réutilise exactement les mêmes listes que l'infobulle de la barre de ressources
        /// (<see cref="HarvestController.GetProductionRatesBySource"/> / <c>GetConsumptionRatesBySource</c> /
        /// <see cref="Controller.Expand.MonumentInvestment.GetInvestmentRatesBySource"/>), pour garantir un
        /// total identique entre la page Rituels et cette infobulle.
        /// </summary>
        public (List<(string SourceKey, double Rate)> Gains, List<(string SourceKey, double Rate)> Losses) GetCrystalGainsAndLosses()
        {
            var gains = new List<(string SourceKey, double Rate)>();
            var losses = new List<(string SourceKey, double Rate)>();

            var civ = GetPlayerCiv();
            if (civ == null || _state == null) return (gains, losses);

            if (_harvestController != null)
            {
                if (_harvestController.GetProductionRatesBySource(civ.Index).TryGetValue(Resource.Crystal, out var prod))
                    gains.AddRange(prod);
                if (_harvestController.GetConsumptionRatesBySource(civ.Index).TryGetValue(Resource.Crystal, out var cons))
                    losses.AddRange(cons);
            }

            if (Controller.Expand.MonumentInvestment.GetInvestmentRatesBySource(_state, civ).TryGetValue(Resource.Crystal, out var monument))
                losses.AddRange(monument);

            double ritualUpkeep = GetRitualUpkeepPerSecond();
            if (ritualUpkeep > 0.0001) losses.Add((RitualUpkeepSourceKey, ritualUpkeep));

            return (gains, losses);
        }

        // ── Coûts ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Exposant de la puissance dans les formules de coût des rituels (base 2, coût = base × puissance²),
        /// réduit par RITUAL_COST_SCALING_REDUCTION (0.2 = -20% → exposant 1,6). Partagé par
        /// <see cref="GetLaunchCost"/> et <see cref="GetUpkeepCost"/>.
        /// </summary>
        private double GetRitualCostExponent()
        {
            double reduction = GetPlayerCiv()?.ModifierAggregator
                .ApplyModifiers(ECategory.RITUAL_COST_SCALING_REDUCTION, "", 0.0) ?? 0.0;
            reduction = Math.Clamp(reduction, 0.0, 0.9);
            return 2.0 * (1.0 - reduction);
        }

        /// <summary>Coût de lancement en cristaux : base × puissance², réduit par RITUAL_COST_SCALING_REDUCTION.</summary>
        public int GetLaunchCost(RitualDefinition def, int power)
            => (int)Math.Ceiling(def.BaseLaunchCost * Math.Pow(power, GetRitualCostExponent()));

        /// <summary>Coût d'entretien par cycle : base × puissance², réduit par RITUAL_UPKEEP_REDUCTION et
        /// RITUAL_COST_SCALING_REDUCTION.</summary>
        public int GetUpkeepCost(RitualDefinition def, int power)
        {
            double reduction = GetPlayerCiv()?.ModifierAggregator
                .ApplyModifiers(ECategory.RITUAL_UPKEEP_REDUCTION, "", 0.0) ?? 0.0;
            reduction = Math.Clamp(reduction, 0.0, 0.9);
            return (int)Math.Ceiling(def.BaseUpkeepCost * Math.Pow(power, GetRitualCostExponent()) * (1.0 - reduction));
        }

        // ── Lancement / arrêt / puissance ─────────────────────────────────────

        public bool CanLaunchRitual(RitualId id)
        {
            var civ = GetPlayerCiv();
            var def = RitualDefinitions.Get(id);
            if (civ == null || def == null) return false;
            if (!IsMagicUnlocked() || !IsRitualKnown(id)) return false;
            if (GetActiveRitual(id) != null) return false;
            if (_state!.Magic.ActiveRituals.Count >= MaxActiveRituals) return false;
            if (UsedPowerByNonAutomatedRituals + 1 > TotalPowerBudget) return false;
            return civ.GetResourceQuantity(Resource.Crystal) >= GetLaunchCost(def, 1);
        }

        public bool LaunchRitual(RitualId id)
        {
            if (!CanLaunchRitual(id)) return false;
            var civ = GetPlayerCiv()!;
            var def = RitualDefinitions.Get(id)!;

            civ.RemoveResource(Resource.Crystal, GetLaunchCost(def, 1));
            _state!.Magic.ActiveRituals.Add(new ActiveRitual(id, 1, _clock?.CurrentTick ?? 0));
            ReduceAutomatedPowerIfOverBudget();
            NotifyRitualsChanged();
            return true;
        }

        /// <summary>Arrête un rituel à la demande du joueur : désarme aussi son automatisation, sans quoi
        /// <see cref="ProcessRitualPowerAutomation"/> le relancerait aussitôt.</summary>
        public bool StopRitual(RitualId id)
        {
            var active = GetActiveRitual(id);
            if (active == null) return false;
            _state!.Magic.ActiveRituals.Remove(active);
            _state.Magic.AutomatedRituals.Remove(id);
            NotifyRitualsChanged();
            return true;
        }

        /// <summary>Coût en cristaux pour passer de la puissance actuelle à puissance + 1.</summary>
        public int GetPowerIncreaseCost(RitualId id)
        {
            var active = GetActiveRitual(id);
            var def = RitualDefinitions.Get(id);
            if (active == null || def == null) return 0;
            return GetLaunchCost(def, active.Power + 1) - GetLaunchCost(def, active.Power);
        }

        /// <summary>
        /// Faux pour un rituel automatisé : sa puissance n'est ajustée que par
        /// <see cref="ProcessRitualPowerAutomation"/>, jamais par les boutons -/+.
        /// </summary>
        public bool CanIncreaseRitualPower(RitualId id)
        {
            var civ = GetPlayerCiv();
            var active = GetActiveRitual(id);
            if (civ == null || active == null || IsRitualAutomated(id)) return false;
            if (UsedPowerByNonAutomatedRituals + 1 > TotalPowerBudget) return false;
            return civ.GetResourceQuantity(Resource.Crystal) >= GetPowerIncreaseCost(id);
        }

        public bool IncreaseRitualPower(RitualId id)
        {
            if (!CanIncreaseRitualPower(id)) return false;
            var civ = GetPlayerCiv()!;
            civ.RemoveResource(Resource.Crystal, GetPowerIncreaseCost(id));
            GetActiveRitual(id)!.Power++;
            ReduceAutomatedPowerIfOverBudget();
            NotifyRitualsChanged();
            return true;
        }

        /// <summary>Diminue la puissance d'un rituel (gratuit). À puissance 1, arrête le rituel. Sans effet
        /// sur un rituel automatisé, dont la puissance n'est ajustée que par <see cref="ProcessRitualPowerAutomation"/>.</summary>
        public bool DecreaseRitualPower(RitualId id)
        {
            var active = GetActiveRitual(id);
            if (active == null || IsRitualAutomated(id)) return false;
            if (active.Power <= 1) return StopRitual(id);
            active.Power--;
            NotifyRitualsChanged();
            return true;
        }

        /// <summary>
        /// Débloque la case à cocher "auto" (voir <see cref="SetRitualAutomated"/> et
        /// <see cref="ProcessRitualPowerAutomation"/>) : pouvoir divin Rituels Divins.
        /// </summary>
        public bool IsDivineRitualsActive => _godState?.AscensionState.IsDivineRitualsActive == true;

        /// <summary>Vrai si l'automatisation est armée sur ce rituel, qu'il soit actuellement actif ou non.</summary>
        public bool IsRitualAutomated(RitualId id) => _state?.Magic.AutomatedRituals.Contains(id) == true;

        /// <summary>
        /// Arme ou désarme l'ajustement automatique de puissance d'un rituel (case à cocher "auto"
        /// sous les boutons -/+, voir <see cref="ProcessRitualPowerAutomation"/>) — débloquée par le
        /// pouvoir divin Rituels Divins (<see cref="IsDivineRitualsActive"/>), visible dès lors même sur
        /// un rituel pas encore lancé : l'activer le lance au passage si les conditions de
        /// <see cref="LaunchRitual"/> sont réunies (cristaux, budget, nombre de rituels simultanés).
        /// Si elles ne le sont pas, l'automatisation reste armée malgré tout et le rituel sera lancé par
        /// <see cref="ProcessRitualPowerAutomation"/> dès que les cristaux le permettront : décocher la
        /// case parce que le lancement a échoué serait le contraire de ce qu'on attend d'une automatisation.
        /// </summary>
        public bool SetRitualAutomated(RitualId id, bool automated)
        {
            if (!IsDivineRitualsActive || _state == null) return false;
            if (automated && !IsRitualKnown(id)) return false;

            bool changed;
            if (automated)
            {
                changed = !_state.Magic.AutomatedRituals.Contains(id);
                if (changed) _state.Magic.AutomatedRituals.Add(id);
                if (GetActiveRitual(id) == null && CanLaunchRitual(id)) changed |= LaunchRitual(id);
            }
            else
            {
                changed = _state.Magic.AutomatedRituals.Remove(id);
            }

            if (changed) NotifyRitualsChanged();
            return true;
        }

        /// <summary>
        /// [Legacy v0.21] Reprend le drapeau <see cref="ActiveRitual.IsAutomated"/> des sauvegardes
        /// antérieures dans <see cref="MagicState.AutomatedRituals"/>, qui le remplace. Appelée à chaque
        /// <see cref="Initialize"/> : sans état hérité, elle ne fait rien.
        /// </summary>
        private void MigrateLegacyAutomationFlags()
        {
            if (_state == null) return;
            foreach (var active in _state.Magic.ActiveRituals)
            {
                if (!active.IsAutomated) continue;
                active.IsAutomated = false;
                if (!_state.Magic.AutomatedRituals.Contains(active.Id))
                    _state.Magic.AutomatedRituals.Add(active.Id);
            }
        }

        /// <summary>
        /// Après une augmentation manuelle de puissance (ou un nouveau lancement) qui peut avoir porté
        /// <see cref="UsedPower"/> au-delà de <see cref="TotalPowerBudget"/> — le contrôle d'admission de
        /// ces actions ne compare que <see cref="UsedPowerByNonAutomatedRituals"/> au budget — réduit
        /// immédiatement d'1 point de puissance le rituel automatisé le plus puissant pour rendre la place
        /// prise par l'action manuelle. Répété tant que nécessaire (plusieurs rituels automatisés, ou
        /// budget qui aurait changé entre-temps).
        /// </summary>
        private void ReduceAutomatedPowerIfOverBudget()
        {
            if (_state == null) return;
            while (UsedPower > TotalPowerBudget)
                if (!ReduceStrongestAutomatedRitual()) break;
        }

        // ── Sorts instantanés ────────────────────────────────────────────────

        public bool IsSpellKnown(SpellId id)
            => GetPlayerCiv()?.ModifierAggregator.HasModifier(ECategory.UNLOCK_SPELL, id.ToString()) == true;

        /// <summary>Sorts débloqués par la recherche, dans l'ordre des définitions.</summary>
        public IReadOnlyList<SpellDefinition> GetKnownSpells()
            => SpellDefinitions.All.Where(s => IsSpellKnown(s.Id)).ToList();

        /// <summary>Crans d'épuisement actuellement accumulés sur ce sort (voir <see cref="MagicState.SpellExhaustionStacks"/>).</summary>
        public int GetSpellExhaustionStacks(SpellId id)
            => _state != null && _state.Magic.SpellExhaustionStacks.TryGetValue(id, out var stacks) ? stacks : 0;

        /// <summary>
        /// Facteur multiplicatif actuel appliqué au coût de base par l'épuisement — <see cref="SpellDefinition.CostMultiplierPerCast"/>
        /// élevé à la puissance du nombre de crans, saturé à <see cref="int.MaxValue"/> comme <see cref="GetSpellCost"/>.
        /// Utilisé pour l'affichage (description du sort).
        /// </summary>
        public long GetSpellCostMultiplier(SpellId id)
        {
            var def = SpellDefinitions.Get(id);
            if (def == null) return 1;
            long factor = 1;
            int stacks = GetSpellExhaustionStacks(id);
            for (int i = 0; i < stacks && factor < int.MaxValue; i++)
                factor *= def.CostMultiplierPerCast;
            return factor;
        }

        /// <summary>
        /// Fraction écoulée (0 à 1) du cycle de cooldown en cours vers le retrait du prochain cran
        /// d'épuisement. Le cooldown tourne en continu dès que le sort est connu, même à 0 cran.
        /// </summary>
        public double GetSpellCooldownRatio(SpellId id)
        {
            var def = SpellDefinitions.Get(id);
            if (def == null || def.CooldownTicks <= 0 || _clock == null || _state == null) return 0.0;
            long lastTick = _state.Magic.SpellCooldownLastTick.TryGetValue(id, out var t) ? t : _clock.CurrentTick;
            return Math.Clamp((double)(_clock.CurrentTick - lastTick) / def.CooldownTicks, 0.0, 1.0);
        }

        /// <summary>Ticks restants avant le retrait du prochain cran d'épuisement.</summary>
        public long GetSpellCooldownRemainingTicks(SpellId id)
        {
            var def = SpellDefinitions.Get(id);
            if (def == null || _clock == null || _state == null) return 0;
            long lastTick = _state.Magic.SpellCooldownLastTick.TryGetValue(id, out var t) ? t : _clock.CurrentTick;
            return Math.Clamp(def.CooldownTicks - (_clock.CurrentTick - lastTick), 0, def.CooldownTicks);
        }

        /// <summary>
        /// Coût en cristaux d'un sort, réduit par SPELL_COST_REDUCTION (SubCategory = SpellId name).
        /// Le coût de base est d'abord multiplié par <see cref="GetSpellCostMultiplier"/>, qui reflète
        /// les crans d'épuisement accumulés (chaque lancement en ajoute un, le cooldown en retire un).
        /// Le calcul passe par un long et sature à <see cref="int.MaxValue"/> : au-delà de quelques
        /// crans le coût dépasse la capacité d'un int, et un débordement rendrait le sort gratuit.
        /// </summary>
        public int GetSpellCost(SpellDefinition def)
        {
            long baseCost = def.CrystalCost;
            if (def.CostMultiplierPerCast > 1)
            {
                int stacks = GetSpellExhaustionStacks(def.Id);
                for (int i = 0; i < stacks && baseCost < int.MaxValue; i++)
                    baseCost *= def.CostMultiplierPerCast;
            }

            double reduction = GetPlayerCiv()?.ModifierAggregator
                .ApplyModifiers(ECategory.SPELL_COST_REDUCTION, def.Id.ToString(), 0.0) ?? 0.0;
            reduction = Math.Clamp(reduction, 0.0, 0.9);
            double cost = Math.Ceiling(baseCost * (1.0 - reduction));
            return cost >= int.MaxValue ? int.MaxValue : (int)cost;
        }

        /// <summary>
        /// Vrai si Magie Divine est débloquée (pouvoir divin, voir <see cref="AscensionState.IsDivineMagicActive"/>) :
        /// seule condition qui permet aux sorts d'accumuler des charges (<see cref="GetSpellCharges"/>).
        /// </summary>
        public bool IsDivineMagicActive => _godState?.AscensionState.IsDivineMagicActive == true;

        /// <summary>
        /// Charges de lancement actuellement disponibles pour ce sort (voir <see cref="MagicState.SpellCharges"/>) :
        /// toujours 0 tant que Magie Divine n'a jamais été active, la charge initiale de chaque prestige
        /// (<see cref="MagicState.GrantInitialSpellCharges"/>) n'étant accordée que dans ce cas.
        /// </summary>
        public int GetSpellCharges(SpellId id)
            => _state != null && _state.Magic.SpellCharges.TryGetValue(id, out var charges) ? charges : 0;

        /// <summary>Charges maximales accumulables pour ce sort : <see cref="MaxSpellCharges"/> si Magie
        /// Divine est active, sinon 0 (voir <see cref="IsDivineMagicActive"/>) — sert à l'UI pour décider
        /// si la rangée de cercles de charges doit être affichée.</summary>
        public int GetSpellMaxCharges(SpellId id) => IsDivineMagicActive ? MaxSpellCharges : 0;

        /// <summary>Enregistre un lancement réussi : consomme une charge disponible sans épuisement
        /// (voir <see cref="GetSpellCharges"/>), sinon ajoute un cran d'épuisement qui fait doubler le coût.</summary>
        private void RegisterSpellCast(SpellId id)
        {
            if (_state == null) return;

            int charges = GetSpellCharges(id);
            if (charges > 0)
            {
                _state.Magic.SpellCharges[id] = charges - 1;
                return;
            }

            _state.Magic.SpellExhaustionStacks[id] = GetSpellExhaustionStacks(id) + 1;
        }

        public bool CanCastSpell(SpellId id)
        {
            var civ = GetPlayerCiv();
            var def = SpellDefinitions.Get(id);
            if (civ == null || def == null) return false;
            if (!IsMagicUnlocked() || !IsSpellKnown(id)) return false;
            if (def.TargetKind == SpellTargetKind.AllyCity && GetAllyCityTargets().Count == 0) return false;
            if (def.TargetKind == SpellTargetKind.BuildableVertex && GetBuildableCityTargets().Count == 0) return false;
            if (def.TargetKind == SpellTargetKind.VoidRoad && GetVoidBridgeTargets().Count == 0) return false;
            return civ.GetResourceQuantity(Resource.Crystal) >= GetSpellCost(def);
        }

        /// <summary>
        /// Clé de localisation expliquant pourquoi un sort connu ne peut pas être lancé actuellement
        /// (absence de cible valide ou cristaux insuffisants), ou null s'il est castable.
        /// </summary>
        public string? GetSpellBlockedReasonKey(SpellId id)
        {
            var civ = GetPlayerCiv();
            var def = SpellDefinitions.Get(id);
            if (civ == null || def == null) return null;
            if (def.TargetKind == SpellTargetKind.AllyCity && GetAllyCityTargets().Count == 0) return "spell_blocked_no_ally_city";
            if (def.TargetKind == SpellTargetKind.BuildableVertex && GetBuildableCityTargets().Count == 0) return "spell_blocked_no_buildable_vertex";
            if (def.TargetKind == SpellTargetKind.VoidRoad && GetVoidBridgeTargets().Count == 0) return "spell_blocked_no_void_road";
            if (civ.GetResourceQuantity(Resource.Crystal) < GetSpellCost(def)) return "spell_blocked_crystals";
            return null;
        }

        /// <summary>Lance un sort sans ciblage : effet instantané, sans entretien ni puissance.</summary>
        public bool CastSpell(SpellId id)
        {
            var def = SpellDefinitions.Get(id);
            if (def == null || def.TargetKind != SpellTargetKind.None) return false;
            if (!CanCastSpell(id)) return false;
            var civ = GetPlayerCiv()!;

            civ.RemoveResource(Resource.Crystal, GetSpellCost(def));
            civ.AddResource(Resource.Gold, def.GoldReward);
            RegisterSpellCast(id);
            return true;
        }

        /// <summary>Villes du joueur sur le calque actuellement affiché, ciblables par un sort d'invocation.</summary>
        public List<Vertex> GetAllyCityTargets()
        {
            var civ = GetPlayerCiv();
            if (civ == null || _state == null) return new List<Vertex>();
            int currentLayer = _state.CurrentViewedLayer;
            return civ.Cities.Where(c => c.Position.Z == currentLayer).Select(c => c.Position).ToList();
        }

        /// <summary>Lance un sort ciblant une ville alliée : consomme les cristaux et applique l'effet sur la ville visée.</summary>
        public bool CastSpellOnCity(SpellId id, Vertex cityVertex)
        {
            var def = SpellDefinitions.Get(id);
            if (def == null || def.TargetKind != SpellTargetKind.AllyCity) return false;
            if (!CanCastSpell(id)) return false;
            var civ = GetPlayerCiv()!;
            var city = _state!.FindCityAt(cityVertex);
            if (city == null || city.CivilizationIndex != civ.Index) return false;

            civ.RemoveResource(Resource.Crystal, GetSpellCost(def));
            int effectiveMaxSoldiers = city.MaxSoldiers + civ.CityMaxSoldiersBonus;
            city.Soldiers = Math.Min(effectiveMaxSoldiers, city.Soldiers + def.TroopReward);
            RegisterSpellCast(id);
            return true;
        }

        /// <summary>
        /// Vertex ciblables par le sort d'édification sur le calque actuellement affiché : tout vertex
        /// libre touché par une route du joueur. L'Édification Arcanique ignore les autres règles de
        /// placement (distance minimale aux autres villes, restrictions raciales de terrain), mais exige
        /// toujours qu'une route mène à l'emplacement — voir
        /// <see cref="CityBuilderController.GetRoadReachableFreeVertices"/>.
        /// </summary>
        public List<Vertex> GetBuildableCityTargets()
        {
            var civ = GetPlayerCiv();
            if (civ == null || _state == null || _cityBuilder == null) return new List<Vertex>();
            int currentLayer = _state.CurrentViewedLayer;
            return _cityBuilder.GetRoadReachableFreeVertices(civ.Index).Where(v => v.Z == currentLayer).ToList();
        }

        /// <summary>
        /// Lance un sort ciblant un vertex atteint par une route : fonde gratuitement une ville déjà
        /// développée (Hôtel de ville niveau <see cref="ArcaneEdificationTownHallLevel"/>, tous les bâtiments
        /// disponibles au niveau <see cref="ArcaneEdificationBuildingLevel"/>, défense et garnison au maximum),
        /// en s'affranchissant des règles de placement habituelles (distances entre villes, terrain racial).
        /// </summary>
        public const int ArcaneEdificationTownHallLevel = 3;
        public const int ArcaneEdificationBuildingLevel = 2;

        public bool CastSpellOnVertex(SpellId id, Vertex vertex)
        {
            var def = SpellDefinitions.Get(id);
            if (def == null || def.TargetKind != SpellTargetKind.BuildableVertex) return false;
            if (!CanCastSpell(id)) return false;
            if (_cityBuilder == null || _buildingController == null) return false;
            var civ = GetPlayerCiv()!;

            City city;
            try { city = _cityBuilder.CreateCityFree(civ.Index, vertex, ignorePlacementRestrictions: true); }
            catch (InvalidOperationException) { return false; }
            catch (ArgumentException) { return false; }

            civ.RemoveResource(Resource.Crystal, GetSpellCost(def));

            var townHall = city.Buildings.FirstOrDefault(b => b.Type == BuildingType.TownHall);
            if (townHall == null)
            {
                townHall = BuildingFactory.Create(BuildingType.TownHall)!;
                city.AddBuilding(townHall);
            }
            int townHallMaxLevel = _buildingController.GetMaxLevel(townHall, civ, city);
            townHall.Level = Math.Clamp(Math.Max(townHall.Level, ArcaneEdificationTownHallLevel), 0, townHallMaxLevel);
            city.InvalidateLevelCache();

            foreach (var building in _buildingController.GetBuildingsAndBuildables(city))
            {
                if (building.Type == BuildingType.TownHall) continue;
                if (!city.Buildings.Contains(building))
                    city.AddBuilding(building);
                int maxLevel = _buildingController.GetMaxLevel(building, civ, city);
                building.Level = Math.Clamp(Math.Max(building.Level, ArcaneEdificationBuildingLevel), 0, maxLevel);
            }

            city.InvalidateMaxSoldiersCache();
            city.CurrentDefense = city.MaxDefense;
            city.Soldiers = city.MaxSoldiers;

            civ.RecalculateStorageCapacity();
            RegisterSpellCast(id);
            return true;
        }

        // ── Pont du Vide ─────────────────────────────────────────────────────

        /// <summary>
        /// Routes du Vide ciblables par le Pont du Vide sur le calque actuellement affiché : arêtes séparant
        /// deux hexagones de Vide, tous deux visibles pour le joueur, et pas déjà occupées par une route de
        /// la civilisation — sinon le sort n'aurait rien à bâtir. La visibilité suffit à borner la portée :
        /// les routes révèlent les trois hexagones de chacune de leurs extrémités, donc chaque lancement
        /// rend ciblables les arêtes atteintes, de proche en proche.
        /// </summary>
        public List<Edge> GetVoidBridgeTargets()
        {
            var result = new List<Edge>();
            var civ = GetPlayerCiv();
            if (civ == null || _state == null || _roadController == null) return result;

            int currentLayer = _state.CurrentViewedLayer;
            if (!_state.Visibility.GetForZ(currentLayer).TryGetValue(civ.Index, out var visibleMap)) return result;

            var ownRoads = new HashSet<Edge>();
            foreach (var road in civ.Roads)
                if (road.Position.Z == currentLayer) ownRoads.Add(road.Position);

            var seen = new HashSet<Edge>();
            foreach (var tile in visibleMap.Tiles.Values)
            {
                if (tile.TerrainType != TerrainType.Void) continue;

                foreach (HexDirection direction in Enum.GetValues<HexDirection>())
                {
                    var edge = tile.Coord.Edge(direction);
                    if (!seen.Add(edge)) continue;
                    if (ownRoads.Contains(edge)) continue;
                    if (!_roadController.IsVoidBridgeEdge(edge, visibleMap)) continue;
                    result.Add(edge);
                }
            }
            return result;
        }

        /// <summary>
        /// Lance le Pont du Vide sur une arête bordée de Vide : bâtit gratuitement cette seule route
        /// (voir <see cref="RoadController.BuildVoidBridge"/>) et consomme les cristaux. Le coût double
        /// au lancement suivant.
        /// </summary>
        public bool CastSpellOnVoidRoad(SpellId id, Edge edge)
        {
            var def = SpellDefinitions.Get(id);
            if (def == null || def.TargetKind != SpellTargetKind.VoidRoad) return false;
            if (!CanCastSpell(id)) return false;
            if (_roadController == null) return false;
            if (!GetVoidBridgeTargets().Any(e => e.Equals(edge))) return false;
            var civ = GetPlayerCiv()!;

            int cost = GetSpellCost(def);
            if (!_roadController.BuildVoidBridge(civ.Index, edge)) return false;

            civ.RemoveResource(Resource.Crystal, cost);
            RegisterSpellCast(id);
            return true;
        }

        // ── Entretien & effondrement ──────────────────────────────────────────

        private void ProcessUpkeep()
        {
            if (_state == null || _clock == null || _state.Civilizations.Count == 0) return;
            if (_state.Magic.ActiveRituals.Count == 0) return;

            var civ = _state.PlayerCivilization;
            long now = _clock.CurrentTick;
            bool changed = false;

            foreach (var active in _state.Magic.ActiveRituals.ToList())
            {
                long lastTick = active.LastUpkeepTick;
                long cycles = TickCooldown.ConsumeElapsedCycles(now, ref lastTick, UpkeepIntervalTicks);
                active.LastUpkeepTick = lastTick;
                if (cycles <= 0) continue;

                var def = RitualDefinitions.Get(active.Id);
                if (def == null) continue;

                // Rejoué cycle par cycle (pas une multiplication directe) : le stock de cristaux peut
                // s'épuiser avant d'avoir consommé tous les cycles dus, ce qui doit effondrer le rituel
                // dès le cycle fautif plutôt qu'après coup.
                for (long i = 0; i < cycles; i++)
                {
                    int upkeep = GetUpkeepCost(def, active.Power);
                    if (civ.GetResourceQuantity(Resource.Crystal) >= upkeep)
                    {
                        if (upkeep > 0) civ.RemoveResource(Resource.Crystal, upkeep);
                    }
                    else
                    {
                        CollapseRitual(active);
                        changed = true;
                        break;
                    }
                }
            }

            // Si des tours ont été détruites, les rituels excédentaires s'effondrent
            while (_state.Magic.ActiveRituals.Count > MaxActiveRituals && _state.Magic.ActiveRituals.Count > 0)
            {
                CollapseRitual(_state.Magic.ActiveRituals[^1]);
                changed = true;
            }
            while (UsedPower > TotalPowerBudget && _state.Magic.ActiveRituals.Count > 0)
            {
                var last = _state.Magic.ActiveRituals[^1];
                if (last.Power > 1) last.Power--;
                else CollapseRitual(last);
                changed = true;
            }

            if (changed) NotifyRitualsChanged();
        }

        private void CollapseRitual(ActiveRitual active)
        {
            _state!.Magic.ActiveRituals.Remove(active);
            _state.EventLog.Add(GameEventType.RitualCollapsed);
        }

        // ── Automatisation de la puissance ───────────────────────────────────

        /// <summary>Somme des gains et pertes de cristaux/seconde actuels (mêmes sources que <see cref="GetCrystalGainsAndLosses"/>).</summary>
        private double GetNetCrystalPerSecond()
        {
            var (gains, losses) = GetCrystalGainsAndLosses();
            return gains.Sum(g => g.Rate) - losses.Sum(l => l.Rate);
        }

        /// <summary>
        /// Une fois par seconde, ajuste la puissance des rituels automatisés (case "auto" sous les
        /// boutons -/+, voir <see cref="SetRitualAutomated"/>) en les maintenant équilibrés en niveau :
        /// si le gain net de cristaux/seconde est négatif, réduit d'1 point le rituel automatisé le plus
        /// puissant (l'arrêtant s'il n'a plus que la puissance 1) ; sinon augmente d'1 point l'un des
        /// rituels automatisés les moins puissants, mais seulement si la puissance totale reste dans le
        /// budget ET si le supplément d'entretien qui en résulte laisse le gain net strictement positif.
        /// Un rituel armé mais pas encore lancé compte comme une puissance 0 : il est donc servi en
        /// premier, et son lancement obéit aux mêmes règles que n'importe quelle montée en puissance.
        /// Quand le budget est saturé, l'équilibrage reprend 1 point au plus puissant dès que son écart
        /// avec le plus faible atteint 2 (voir <see cref="AdjustAutomatedRitualPowerOnce"/>). Au plus un
        /// ajustement par seconde : on laisse le tick suivant réévaluer la situation plutôt que de
        /// converger d'un coup, comme demandé.
        /// </summary>
        private void ProcessRitualPowerAutomation()
        {
            if (_state == null || _clock == null || _state.Civilizations.Count == 0) return;

            long lastTick = _lastRitualAutomationTick;
            long cycles = TickCooldown.ConsumeElapsedCycles(_clock.CurrentTick, ref lastTick, RitualAutomationIntervalTicks);
            _lastRitualAutomationTick = lastTick;
            if (cycles <= 0) return;
            if (!IsDivineRitualsActive) return;
            if (_state.Magic.AutomatedRituals.Count == 0) return;

            bool changed = false;
            for (long i = 0; i < cycles; i++)
                if (AdjustAutomatedRitualPowerOnce()) changed = true;

            if (changed) NotifyRitualsChanged();
        }

        private bool AdjustAutomatedRitualPowerOnce()
        {
            if (_state!.Magic.AutomatedRituals.Count == 0) return false;

            double net = GetNetCrystalPerSecond();
            if (net < 0) return ReduceStrongestAutomatedRitual();

            var candidates = GetAutomationBalancingCandidates();
            if (candidates.Count == 0) return false;
            int minPower = candidates[0].Power; // trié par puissance croissante

            if (UsedPower >= TotalPowerBudget)
            {
                // Budget saturé : plus rien à distribuer, la seule marge d'équilibrage est de reprendre
                // 1 point au rituel le plus fort pour que le tick suivant le rende au plus faible. On ne
                // le fait qu'à partir d'un écart de 2, sans quoi le point ferait l'aller-retour à
                // l'infini ; un écart de 1 est de toute façon inévitable dès que le budget ne se divise
                // pas également entre les rituels automatisés.
                var strongest = GetStrongestAutomatedRitual();
                if (strongest == null || strongest.Power - minPower < 2) return false;
                strongest.Power--;
                return true;
            }

            var civ = GetPlayerCiv();
            if (civ == null) return false;

            // Seuls les rituels déjà au niveau le plus bas peuvent monter : un rituel plus puissant ne
            // prend jamais la place d'un plus faible bloqué (cristaux insuffisants, entretien trop lourd),
            // sinon l'écart se creuserait indéfiniment au lieu de se résorber.
            foreach (var (id, power) in candidates)
            {
                if (power > minPower) break;

                var def = RitualDefinitions.Get(id);
                if (def == null) continue;

                double upkeepIncreasePerSecond = (GetUpkeepCost(def, power + 1) - GetUpkeepCost(def, power))
                    / (UpkeepIntervalTicks / 100.0);
                if (net - upkeepIncreasePerSecond <= 0) continue;

                if (power == 0)
                {
                    if (!CanLaunchRitual(id)) continue;
                    LaunchRitual(id);
                    return true;
                }

                int cost = GetPowerIncreaseCost(id);
                if (civ.GetResourceQuantity(Resource.Crystal) < cost) continue;

                civ.RemoveResource(Resource.Crystal, cost);
                GetActiveRitual(id)!.Power++;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Rituels automatisés que l'équilibrage peut effectivement servir, triés par puissance croissante
        /// (puissance 0 = armé mais pas encore lancé), à égalité par identifiant pour rester déterministe.
        /// Un rituel armé qui ne peut pas être lancé parce que le nombre de rituels simultanés est déjà
        /// atteint est exclu : le laisser à puissance 0 dans la liste bloquerait la montée de tous les
        /// autres, qui attendraient éternellement que le plus faible rattrape son retard.
        /// </summary>
        private List<(RitualId Id, int Power)> GetAutomationBalancingCandidates()
        {
            var result = new List<(RitualId Id, int Power)>();
            bool slotFree = _state!.Magic.ActiveRituals.Count < MaxActiveRituals;

            foreach (var id in _state.Magic.AutomatedRituals)
            {
                var active = GetActiveRitual(id);
                if (active == null && (!slotFree || !IsRitualKnown(id))) continue;
                result.Add((id, active?.Power ?? 0));
            }

            result.Sort((a, b) => a.Power != b.Power
                ? a.Power.CompareTo(b.Power)
                : ((int)a.Id).CompareTo((int)b.Id));
            return result;
        }

        /// <summary>Rituel automatisé actif le plus puissant (à égalité, le plus petit identifiant).</summary>
        private ActiveRitual? GetStrongestAutomatedRitual()
        {
            ActiveRitual? strongest = null;
            var actives = _state!.Magic.ActiveRituals;
            for (int i = 0; i < actives.Count; i++)
            {
                var candidate = actives[i];
                if (!IsRitualAutomated(candidate.Id)) continue;
                if (strongest == null || candidate.Power > strongest.Power
                    || (candidate.Power == strongest.Power && (int)candidate.Id < (int)strongest.Id))
                    strongest = candidate;
            }
            return strongest;
        }

        /// <summary>Retire 1 point de puissance au rituel automatisé le plus puissant, l'arrêtant s'il
        /// n'a plus que la puissance 1. Le rituel arrêté reste armé : l'automatisation le relancera quand
        /// les cristaux reviendront.</summary>
        private bool ReduceStrongestAutomatedRitual()
        {
            var toReduce = GetStrongestAutomatedRitual();
            if (toReduce == null) return false;

            if (toReduce.Power > 1) toReduce.Power--;
            else _state!.Magic.ActiveRituals.Remove(toReduce);
            return true;
        }

        // ── Cercles de Fées ───────────────────────────────────────────────────

        private void ProcessPassiveCycle()
        {
            if (_state == null || _clock == null || _state.Civilizations.Count == 0) return;

            long now = _clock.CurrentTick;
            if (now - _lastPassiveTick < UpkeepIntervalTicks) return;
            _lastPassiveTick = now;

            EnsureMagicFeatures();
            // Les Cercles de Fées sont récoltés par la Hutte d'Alchimie (HarvestController), pas par ce cycle passif.
        }

        /// <summary>
        /// Révèle les Cercles de Fées débloqués par les vertex de prestige (MAGIC_FEATURE_COUNT), masse
        /// continentale par masse continentale : chacune atteint indépendamment le même quota, au lieu de
        /// se le partager globalement. Les Cercles sont tous pré-placés (invisibles) dès la génération de
        /// la carte par IslandMapGenerator.PlaceInvisibleFairyCircles ; cette méthode ne fait que basculer
        /// IsVisible sur les cercles manquants de chaque masse (FairyCircle.LandmassIndex), sans jamais en
        /// créer ni en retirer — aucun calcul de géométrie ni tirage aléatoire à l'exécution.
        /// </summary>
        public void EnsureMagicFeatures()
        {
            if (_state == null || _state.Civilizations.Count == 0) return;

            int targetPerLandmass = _state.PlayerCivilization.ModifierAggregator
                .ApplyModifiers(ECategory.MAGIC_FEATURE_COUNT, nameof(FairyCircle), 0);
            if (targetPerLandmass <= 0) return;

            foreach (var landmassCircles in _state.Features.OfType<FairyCircle>().GroupBy(f => f.LandmassIndex))
            {
                int toReveal = targetPerLandmass - landmassCircles.Count(f => f.IsVisible);
                if (toReveal <= 0) continue;

                foreach (var circle in landmassCircles.Where(f => !f.IsVisible).Take(toReveal))
                    circle.IsVisible = true;
            }
        }

        // ── Lumière des Profondeurs — dégâts aux monstres autour des Temples ────

        /// <summary>
        /// Rituel Lumière des Profondeurs (TEMPLE_MONSTER_DAMAGE_PER_SECOND) : chaque seconde, inflige
        /// les dégâts agrégés à tout monstre présent sur l'un des 3 hexes adjacents à une ville du joueur
        /// possédant un Temple (niveau ≥ 1). Sans effet si le rituel n'est pas actif (valeur agrégée = 0).
        /// </summary>
        private void ProcessTempleMonsterDamage(long currentTick)
        {
            if (_state == null || _state.Civilizations.Count == 0) return;

            long lastTick = _lastTempleDamageTick;
            long cycles = TickCooldown.ConsumeElapsedCycles(currentTick, ref lastTick, TempleMonsterDamageIntervalTicks);
            _lastTempleDamageTick = lastTick;
            if (cycles <= 0) return;

            var civ = _state.PlayerCivilization;
            int damage = civ.ModifierAggregator.ApplyModifiers(ECategory.TEMPLE_MONSTER_DAMAGE_PER_SECOND, "", 0);
            if (damage <= 0) return;

            var templeHexes = new HashSet<HexCoord>();
            foreach (var city in civ.Cities)
            {
                if (!city.Buildings.OfType<Temple>().Any(t => t.Level >= 1)) continue;
                foreach (var hex in city.Position.GetHexes().Where(IsValidHex))
                    templeHexes.Add(hex);
            }
            if (templeHexes.Count == 0) return;

            // Rejoué cycle par cycle (pas une multiplication directe) : la réduction d'armure est un
            // tirage aléatoire indépendant par tir, et un monstre tué en cours de cycles ne doit plus en
            // encaisser.
            for (long i = 0; i < cycles; i++)
            {
                var deadMonsters = new List<MonsterFeature>();
                bool anyTarget = false;
                foreach (var monster in _state.Features.OfType<MonsterFeature>())
                {
                    if (monster.AttacksOtherMonsters) continue; // monstres "amis" : jamais ciblés
                    if (!templeHexes.Contains(monster.Position)) continue;
                    anyTarget = true;

                    monster.Hp -= MonsterFeature.ApplyArmorReduction(damage, monster.Armor, _prng!);
                    if (monster.Hp <= 0)
                    {
                        monster.KilledByCivilizationIndex = civ.Index;
                        deadMonsters.Add(monster);
                    }
                }

                foreach (var m in deadMonsters)
                {
                    _state.RemoveFeature(m);
                    _state.EventLog.Add(m.RemovedEventType);
                }

                if (!anyTarget) break;
            }
        }

        private bool IsValidHex(HexCoord hex) => _state!.GetMapFor(hex)?.GetTile(hex) != null;

        // ── Épuisement des sorts ────────────────────────────────────────────────

        /// <summary>
        /// Retire un cran d'épuisement à chaque sort connu par cycle de <see cref="SpellDefinition.CooldownTicks"/>
        /// écoulé. Le décompte démarre dès la première fois qu'un sort est observé comme connu (via
        /// <c>coldStartOnZero</c>), pas au lancement : un sort connu depuis longtemps mais jamais lancé
        /// peut donc avoir déjà consommé un ou plusieurs cycles sans effet visible (rien à retirer à 0
        /// cran) — c'est ce qui rend le tout premier lancement d'un sort gratuit de tout épuisement si son
        /// cooldown a eu le temps de s'écouler depuis le début du run.
        ///
        /// <para>Sous Magie Divine (<see cref="IsDivineMagicActive"/>), un cycle écoulé qui ne trouve plus
        /// aucun cran d'épuisement à retirer crédite une charge de lancement (<see cref="MagicState.SpellCharges"/>)
        /// à la place, jusqu'à <see cref="MaxSpellCharges"/> — voir <see cref="RegisterSpellCast"/>, qui
        /// consomme ces charges en priorité.</para>
        /// </summary>
        private void ProcessSpellExhaustion()
        {
            if (_state == null || _clock == null || _state.Civilizations.Count == 0) return;
            long now = _clock.CurrentTick;
            bool divineMagicActive = IsDivineMagicActive;

            foreach (var def in SpellDefinitions.All)
            {
                if (!IsSpellKnown(def.Id)) continue;

                long lastTick = _state.Magic.SpellCooldownLastTick.TryGetValue(def.Id, out var t) ? t : 0;
                long cycles = TickCooldown.ConsumeElapsedCycles(now, ref lastTick, def.CooldownTicks, coldStartOnZero: true);
                _state.Magic.SpellCooldownLastTick[def.Id] = lastTick;
                if (cycles <= 0) continue;

                int stacks = GetSpellExhaustionStacks(def.Id);
                if (stacks > 0)
                {
                    long consumed = Math.Min(stacks, cycles);
                    stacks -= (int)consumed;
                    _state.Magic.SpellExhaustionStacks[def.Id] = stacks;
                    cycles -= consumed;
                }

                if (!divineMagicActive || cycles <= 0 || stacks > 0) continue;

                int charges = GetSpellCharges(def.Id);
                _state.Magic.SpellCharges[def.Id] = (int)Math.Min(MaxSpellCharges, charges + cycles);
            }
        }

        /// <summary>
        /// Automatisation (catégorie Comportements) : lance Abondance dès qu'elle a accumulé
        /// <see cref="MaxSpellCharges"/> charges (voir <see cref="ProcessSpellExhaustion"/>, qui les
        /// crédite), tant que l'entretien du sort le permet — sans quoi le lancement échoue en
        /// silence et sera retenté au tick suivant, le plafond de charges bloquant leur accumulation
        /// ultérieure tant qu'aucune n'est consommée.
        /// </summary>
        private void ProcessAbundanceAutoCast()
        {
            if (_state == null) return;
            if (!_state.AutomationSettings.IsAbundanceAutoCastActive) return;
            if (GetSpellCharges(SpellId.Abundance) < MaxSpellCharges) return;
            if (CanCastSpell(SpellId.Abundance))
                CastSpell(SpellId.Abundance);
        }

        private void NotifyRitualsChanged()
        {
            _provider?.NotifyChanged();
            OnRitualsChanged?.Invoke(this, EventArgs.Empty);
        }

        private Civilization? GetPlayerCiv()
            => _state != null && _state.Civilizations.Count > 0 ? _state.PlayerCivilization : null;
    }
}
