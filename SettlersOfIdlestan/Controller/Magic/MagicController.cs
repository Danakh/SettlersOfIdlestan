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
    /// Gère aussi la génération passive de cristaux des Cercles de Fées et Dolmens,
    /// et leur apparition sur l'île quand les vertex de prestige sont achetés.
    /// </summary>
    public class MagicController
    {
        /// <summary>Durée d'un cycle d'entretien des rituels (1000 ticks = 10 s).</summary>
        public const long UpkeepIntervalTicks = 1000L;

        private WorldState? _state;
        private GameClock? _clock;
        private GamePRNG? _prng;
        private MagicModifierProvider? _provider;
        private long _lastPassiveTick;

        /// <summary>Déclenché à chaque lancement/arrêt/changement de puissance d'un rituel.</summary>
        public event EventHandler? OnRitualsChanged;

        internal MagicController() { }

        internal void Initialize(WorldState? state, GameClock? clock, GamePRNG? prng = null)
        {
            if (_clock != null)
                _clock.Advanced -= OnClockAdvanced;

            _state = state;
            _clock = clock;
            _prng = prng;
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

        /// <summary>Nombre maximal de rituels actifs simultanés.</summary>
        public int MaxActiveRituals
        {
            get
            {
                var civ = GetPlayerCiv();
                if (civ == null || !IsMagicUnlocked()) return 0;
                int towers = MageTowerCount;
                if (towers == 0) return 0;
                return civ.ModifierAggregator.ApplyModifiers(ECategory.RITUAL_MAX_COUNT, "", towers);
            }
        }

        /// <summary>Budget total de puissance (somme des niveaux des tours × bonus).</summary>
        public int TotalPowerBudget
        {
            get
            {
                var civ = GetPlayerCiv();
                if (civ == null || !IsMagicUnlocked()) return 0;
                double multiplier = civ.ModifierAggregator.ApplyModifiers(ECategory.RITUAL_TOTAL_POWER, "", 1.0);
                return (int)Math.Floor(MageTowerTotalLevel * multiplier);
            }
        }

        /// <summary>Puissance actuellement consommée par les rituels actifs.</summary>
        public int UsedPower => _state?.Magic.ActiveRituals.Sum(r => r.Power) ?? 0;

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

        public bool CanCastSpell(SpellId id)
        {
            var civ = GetPlayerCiv();
            var def = SpellDefinitions.Get(id);
            if (civ == null || def == null) return false;
            if (!IsMagicUnlocked() || !IsSpellKnown(id)) return false;
            return civ.GetResourceQuantity(Resource.Crystal) >= def.CrystalCost;
        }

        /// <summary>Lance un sort sans ciblage : effet instantané, sans entretien ni puissance.</summary>
        public bool CastSpell(SpellId id)
        {
            var def = SpellDefinitions.Get(id);
            if (def == null || def.TargetKind != SpellTargetKind.None) return false;
            if (!CanCastSpell(id)) return false;
            var civ = GetPlayerCiv()!;

            civ.RemoveResource(Resource.Crystal, def.CrystalCost);
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

            civ.RemoveResource(Resource.Crystal, def.CrystalCost);
            city.Soldiers = Math.Min(city.MaxSoldiers, city.Soldiers + def.TroopReward);
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

        // ── Cercles de Fées & Dolmens ─────────────────────────────────────────

        private void ProcessPassiveCycle()
        {
            if (_state == null || _clock == null || _state.Civilizations.Count == 0) return;

            long now = _clock.CurrentTick;
            if (now - _lastPassiveTick < UpkeepIntervalTicks) return;
            _lastPassiveTick = now;

            EnsureMagicFeatures();

            var civ = _state.PlayerCivilization;
            // Les Cercles de Fées sont récoltés par la Hutte d'Alchimie (HarvestController), pas par ce cycle passif.
            int crystals = _state.Features.OfType<Dolmen>().Count(f => f.Found) * Dolmen.CrystalsPerCycle;
            if (crystals > 0)
            {
                try { civ.AddResource(Resource.Crystal, crystals); }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[MagicController] AddResource Crystal: {ex.Message}"); }
            }
        }

        /// <summary>
        /// Fait apparaître les Cercles de Fées et Dolmens manquants sur la surface,
        /// selon les modificateurs MAGIC_FEATURE_COUNT (vertex de prestige).
        /// </summary>
        public void EnsureMagicFeatures()
        {
            if (_state == null || _state.Civilizations.Count == 0) return;

            var aggregator = _state.PlayerCivilization.ModifierAggregator;
            int targetCircles = aggregator.ApplyModifiers(ECategory.MAGIC_FEATURE_COUNT, nameof(FairyCircle), 0);
            int targetDolmens = aggregator.ApplyModifiers(ECategory.MAGIC_FEATURE_COUNT, nameof(Dolmen), 0);

            SpawnMissingFeatures(targetCircles - _state.Features.OfType<FairyCircle>().Count(), pos => new FairyCircle(pos));
            SpawnMissingFeatures(targetDolmens - _state.Features.OfType<Dolmen>().Count(), pos => new Dolmen(pos));
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
