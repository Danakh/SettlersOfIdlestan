using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandFeatures;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Magic;
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

        /// <summary>Bonus additif de puissance maximale par niveau cumulé de Tour de Mages (10 %).</summary>
        public const double MageTowerPowerBonusPerLevel = 0.10;

        private WorldState? _state;
        private GameClock? _clock;
        private GamePRNG? _prng;
        private MagicModifierProvider? _provider;
        private long _lastPassiveTick;
        private CityBuilderController? _cityBuilder;
        private BuildingController? _buildingController;
        private HarvestController? _harvestController;

        /// <summary>Déclenché à chaque lancement/arrêt/changement de puissance d'un rituel.</summary>
        public event EventHandler? OnRitualsChanged;

        internal MagicController() { }

        internal void Initialize(WorldState? state, GameClock? clock, GamePRNG? prng = null,
            CityBuilderController? cityBuilder = null, BuildingController? buildingController = null,
            HarvestController? harvestController = null)
        {
            if (_clock != null)
                _clock.Advanced -= OnClockAdvanced;

            _state = state;
            _clock = clock;
            _prng = prng;
            _cityBuilder = cityBuilder;
            _buildingController = buildingController;
            _harvestController = harvestController;
            _lastPassiveTick = 0;

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
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[MagicController] {nameof(ProcessUpkeep)}: {ex}"); }
            try { ProcessPassiveCycle(); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[MagicController] {nameof(ProcessPassiveCycle)}: {ex}"); }
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

        /// <summary>Décomposition du débit net de cristaux par source, pour affichage (tooltip).</summary>
        public readonly record struct CrystalRateBreakdown(
            double AlchimistHutPerSecond,
            double MageTowerPerSecond,
            double PassivePerSecond,
            double RitualUpkeepPerSecond)
        {
            public double NetPerSecond
                => AlchimistHutPerSecond + MageTowerPerSecond + PassivePerSecond - RitualUpkeepPerSecond;
        }

        public CrystalRateBreakdown GetCrystalRateBreakdown()
        {
            var civ = GetPlayerCiv();
            if (civ == null || _state == null) return default;

            double alchimistHut = _harvestController?.GetAlchimistHutCrystalRatePerSecond(civ.Index) ?? 0.0;
            double mageTower = _harvestController?.GetMageTowerCrystalRatePerSecond(civ.Index) ?? 0.0;
            double cycleSeconds = UpkeepIntervalTicks / 100.0;
            double passive = civ.ModifierAggregator.ApplyModifiers(ECategory.PASSIVE_RESOURCE_GENERATION, nameof(Resource.Crystal), 0);

            double upkeep = 0;
            foreach (var active in _state.Magic.ActiveRituals)
            {
                var def = RitualDefinitions.Get(active.Id);
                if (def != null) upkeep += GetUpkeepCost(def, active.Power);
            }
            upkeep /= cycleSeconds;

            return new CrystalRateBreakdown(alchimistHut, mageTower, passive, upkeep);
        }

        // ── Coûts ─────────────────────────────────────────────────────────────

        /// <summary>Coût de lancement en cristaux : base × puissance².</summary>
        public static int GetLaunchCost(RitualDefinition def, int power)
            => def.BaseLaunchCost * power * power;

        /// <summary>Coût d'entretien par cycle : base × puissance², réduit par RITUAL_UPKEEP_REDUCTION.</summary>
        public int GetUpkeepCost(RitualDefinition def, int power)
        {
            double reduction = GetPlayerCiv()?.ModifierAggregator
                .ApplyModifiers(ECategory.RITUAL_UPKEEP_REDUCTION, "", 0.0) ?? 0.0;
            reduction = Math.Clamp(reduction, 0.0, 0.9);
            return (int)Math.Ceiling(def.BaseUpkeepCost * power * power * (1.0 - reduction));
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
            if (UsedPower + 1 > TotalPowerBudget) return false;
            return civ.GetResourceQuantity(Resource.Crystal) >= GetLaunchCost(def, 1);
        }

        public bool LaunchRitual(RitualId id)
        {
            if (!CanLaunchRitual(id)) return false;
            var civ = GetPlayerCiv()!;
            var def = RitualDefinitions.Get(id)!;

            civ.RemoveResource(Resource.Crystal, GetLaunchCost(def, 1));
            _state!.Magic.ActiveRituals.Add(new ActiveRitual(id, 1, _clock?.CurrentTick ?? 0));
            NotifyRitualsChanged();
            return true;
        }

        public bool StopRitual(RitualId id)
        {
            var active = GetActiveRitual(id);
            if (active == null) return false;
            _state!.Magic.ActiveRituals.Remove(active);
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

        public bool CanIncreaseRitualPower(RitualId id)
        {
            var civ = GetPlayerCiv();
            var active = GetActiveRitual(id);
            if (civ == null || active == null) return false;
            if (UsedPower + 1 > TotalPowerBudget) return false;
            return civ.GetResourceQuantity(Resource.Crystal) >= GetPowerIncreaseCost(id);
        }

        public bool IncreaseRitualPower(RitualId id)
        {
            if (!CanIncreaseRitualPower(id)) return false;
            var civ = GetPlayerCiv()!;
            civ.RemoveResource(Resource.Crystal, GetPowerIncreaseCost(id));
            GetActiveRitual(id)!.Power++;
            NotifyRitualsChanged();
            return true;
        }

        /// <summary>Diminue la puissance d'un rituel (gratuit). À puissance 1, arrête le rituel.</summary>
        public bool DecreaseRitualPower(RitualId id)
        {
            var active = GetActiveRitual(id);
            if (active == null) return false;
            if (active.Power <= 1) return StopRitual(id);
            active.Power--;
            NotifyRitualsChanged();
            return true;
        }

        // ── Sorts instantanés ────────────────────────────────────────────────

        public bool IsSpellKnown(SpellId id)
            => GetPlayerCiv()?.ModifierAggregator.HasModifier(ECategory.UNLOCK_SPELL, id.ToString()) == true;

        /// <summary>Sorts débloqués par la recherche, dans l'ordre des définitions.</summary>
        public IReadOnlyList<SpellDefinition> GetKnownSpells()
            => SpellDefinitions.All.Where(s => IsSpellKnown(s.Id)).ToList();

        /// <summary>Coût en cristaux d'un sort, réduit par SPELL_COST_REDUCTION (SubCategory = SpellId name).</summary>
        public int GetSpellCost(SpellDefinition def)
        {
            double reduction = GetPlayerCiv()?.ModifierAggregator
                .ApplyModifiers(ECategory.SPELL_COST_REDUCTION, def.Id.ToString(), 0.0) ?? 0.0;
            reduction = Math.Clamp(reduction, 0.0, 0.9);
            return (int)Math.Ceiling(def.CrystalCost * (1.0 - reduction));
        }

        public bool CanCastSpell(SpellId id)
        {
            var civ = GetPlayerCiv();
            var def = SpellDefinitions.Get(id);
            if (civ == null || def == null) return false;
            if (!IsMagicUnlocked() || !IsSpellKnown(id)) return false;
            if (def.TargetKind == SpellTargetKind.AllyCity && GetAllyCityTargets().Count == 0) return false;
            if (def.TargetKind == SpellTargetKind.BuildableVertex && GetBuildableCityTargets().Count == 0) return false;
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
            city.Soldiers = Math.Min(city.MaxSoldiers, city.Soldiers + def.TroopReward);
            return true;
        }

        /// <summary>Vertex constructibles par le joueur sur le calque actuellement affiché, ciblables par le sort d'édification.</summary>
        public List<Vertex> GetBuildableCityTargets()
        {
            var civ = GetPlayerCiv();
            if (civ == null || _state == null || _cityBuilder == null) return new List<Vertex>();
            int currentLayer = _state.CurrentViewedLayer;
            return _cityBuilder.GetBuildableVertices(civ.Index).Where(v => v.Z == currentLayer).ToList();
        }

        /// <summary>
        /// Lance un sort ciblant un vertex constructible : fonde gratuitement une ville déjà développée
        /// (Hôtel de ville niveau <see cref="ArcaneEdificationTownHallLevel"/>, tous les bâtiments disponibles
        /// au niveau <see cref="ArcaneEdificationBuildingLevel"/>, défense et garnison au maximum).
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
            try { city = _cityBuilder.CreateCityFree(civ.Index, vertex); }
            catch (InvalidOperationException) { return false; }
            catch (ArgumentException) { return false; }

            civ.RemoveResource(Resource.Crystal, GetSpellCost(def));

            var townHall = city.Buildings.FirstOrDefault(b => b.Type == BuildingType.TownHall);
            if (townHall == null)
            {
                townHall = BuildingController.CreateBuilding(BuildingType.TownHall)!;
                city.Buildings.Add(townHall);
            }
            townHall.Level = Math.Max(townHall.Level, ArcaneEdificationTownHallLevel);
            city.InvalidateLevelCache();

            foreach (var building in _buildingController.GetBuildingsAndBuildables(city))
            {
                if (building.Type == BuildingType.TownHall) continue;
                if (!city.Buildings.Contains(building))
                    city.Buildings.Add(building);
                building.Level = Math.Max(building.Level, ArcaneEdificationBuildingLevel);
            }

            city.CurrentDefense = city.MaxDefense;
            city.Soldiers = city.MaxSoldiers;

            BuildingController.RecalculateStorageCapacity(civ);
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
                if (now - active.LastUpkeepTick < UpkeepIntervalTicks) continue;
                active.LastUpkeepTick = now;

                var def = RitualDefinitions.Get(active.Id);
                if (def == null) continue;

                int upkeep = GetUpkeepCost(def, active.Power);
                if (civ.GetResourceQuantity(Resource.Crystal) >= upkeep)
                {
                    if (upkeep > 0) civ.RemoveResource(Resource.Crystal, upkeep);
                }
                else
                {
                    CollapseRitual(active);
                    changed = true;
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
        /// Fait apparaître les Cercles de Fées manquants sur la surface,
        /// selon le modificateur MAGIC_FEATURE_COUNT (vertex de prestige).
        /// </summary>
        public void EnsureMagicFeatures()
        {
            if (_state == null || _state.Civilizations.Count == 0) return;

            var aggregator = _state.PlayerCivilization.ModifierAggregator;
            int targetCircles = aggregator.ApplyModifiers(ECategory.MAGIC_FEATURE_COUNT, nameof(FairyCircle), 0);

            SpawnMissingFeatures(targetCircles - _state.Features.OfType<FairyCircle>().Count(), pos => new FairyCircle(pos));
        }

        private void SpawnMissingFeatures(int missing, Func<HexCoord, IslandFeature> factory)
        {
            if (missing <= 0 || _state == null) return;
            if (!_state.TryGetMapForZ(IslandMap.SurfaceLayer, out var map)) return;

            var occupied = new HashSet<HexCoord>(_state.Features.Select(f => f.Position));
            var candidates = map.Tiles.Values
                .Where(t => t.TerrainType != TerrainType.Water && !occupied.Contains(t.Coord))
                .Select(t => t.Coord)
                .ToList();

            for (int i = 0; i < missing && candidates.Count > 0; i++)
            {
                int index = _prng!.Next(candidates.Count);
                var position = candidates[index];
                candidates.RemoveAt(index);
                _state.AddFeature(factory(position));
            }
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
