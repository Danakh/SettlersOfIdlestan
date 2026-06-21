using System;
using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandFeatures;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Monsters;

namespace SettlersOfIdlestan.Controller.Military;

public class MonsterFeatureController
{
    private WorldState? _state;
    private GameClock? _clock;
    private GamePRNG? _prng;
    private CityBuilderController? _cityBuilderController;

    private List<MonsterFeature> _monsters = new();

    /// <summary>Intervalle de déplacement par défaut (3 000 ticks = 30 s à vitesse normale).</summary>
    public const long MovementIntervalTicks = 3_000L;

    internal void Initialize(WorldState? state, GameClock? clock, GamePRNG? prng = null, CityBuilderController? cityBuilderController = null)
    {
        if (_clock != null)
            _clock.Advanced -= OnClockAdvanced;

        if (_state != null)
        {
            _state.FeatureAdded -= OnFeatureAdded;
            _state.FeatureRemoved -= OnFeatureRemoved;
        }

        _state = state;
        _clock = clock;
        if (prng != null) _prng = prng;
        _cityBuilderController = cityBuilderController;

        RebuildCache();

        if (_state != null)
        {
            _state.FeatureAdded += OnFeatureAdded;
            _state.FeatureRemoved += OnFeatureRemoved;
        }

        if (_clock != null)
            _clock.Advanced += OnClockAdvanced;
    }

    private void RebuildCache()
    {
        _monsters = _state?.Features.OfType<MonsterFeature>().ToList() ?? new();
    }

    private void OnFeatureAdded(object? sender, IslandFeature feature)
    {
        if (feature is MonsterFeature m) _monsters.Add(m);
    }

    private void OnFeatureRemoved(object? sender, IslandFeature feature)
    {
        if (feature is MonsterFeature m) _monsters.Remove(m);
    }

    private void OnClockAdvanced(object? sender, GameClockAdvancedEventArgs e)
    {
        try { Update(e.CurrentTick); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[MonsterFeatureController] {nameof(Update)}: {ex}"); }
    }

    private void Update(long currentTick)
    {
        if (_state == null) return;

        UpdateSpawns(currentTick);
        UpdateAdventurerSpawns(currentTick);

        foreach (var monster in _monsters.ToList())
        {
            if (!monster.Found)
            {
                if (monster.CanMove)
                {
                    monster.LastMovedTick = currentTick;
                    monster.LastAttackTick = currentTick;
                }
                continue;
            }

            RegenHp(monster, currentTick);

            bool moved = false;
            if (monster.CanMove && currentTick - monster.LastMovedTick >= monster.MovementIntervalTicks)
            {
                MoveMonster(monster, currentTick);
                moved = true;
            }

            if (!moved && monster.AttackRangeInHexes > 0)
            {
                if (monster.AttacksOtherMonsters)
                    AttackNearbyMonster(monster, currentTick);
                else
                    AttackNearbyCity(monster, currentTick);
            }
        }
    }

    // ── Invocation de nouvelles créatures ────────────────────────────────────

    private void UpdateSpawns(long currentTick)
    {
        foreach (var monster in _monsters.ToList())
        {
            var spawn = monster.TrySpawn(_monsters, currentTick);
            if (spawn != null)
                _state!.AddFeature(spawn);
        }
    }

    /// <summary>
    /// Fait apparaître un Aventurier près de chaque Guilde des Aventuriers construite, tant
    /// qu'aucun n'est déjà en vie (bâtiment unique : au plus un Aventurier à la fois).
    /// </summary>
    private void UpdateAdventurerSpawns(long currentTick)
    {
        if (_state == null) return;
        if (_monsters.Any(m => m is Adventurer)) return;

        foreach (var civ in _state.Civilizations)
        {
            foreach (var city in civ.Cities)
            {
                var guild = city.Buildings.OfType<AdventurersGuild>().FirstOrDefault(b => b.Level > 0);
                if (guild == null) continue;
                if (currentTick - guild.LastAdventurerSpawnTick < AdventurersGuild.AdventurerRespawnCooldownTicks) continue;

                guild.LastAdventurerSpawnTick = currentTick;
                _state.AddFeature(new Adventurer(city.Position.GetHexes().First()));
                return;
            }
        }
    }

    // ── Combat contre les autres monstres (Aventurier) ───────────────────────

    private void AttackNearbyMonster(MonsterFeature monster, long currentTick)
    {
        if (_state == null) return;
        if (currentTick - monster.LastAttackTick < monster.AttackIntervalTicks) return;
        monster.LastAttackTick = currentTick;

        var target = _monsters.FirstOrDefault(m =>
            m != monster && !m.AttacksOtherMonsters && m.Found && m.Hp > 0 &&
            m.Position.DistanceTo(monster.Position) <= monster.AttackRangeInHexes);
        if (target == null) return;

        target.Hp -= monster.AttackDamage;
        monster.Hp -= target.AttackDamage;

        if (target.Hp <= 0)
        {
            _state.RemoveFeature(target);
            _state.EventLog.Add(target.RemovedEventType);
        }
        if (monster.Hp <= 0)
        {
            _state.RemoveFeature(monster);
            _state.EventLog.Add(monster.RemovedEventType);
        }
    }

    // ── Régénération de PV ───────────────────────────────────────────────────

    private static void RegenHp(MonsterFeature monster, long currentTick)
    {
        if (monster.HpRegenAmount <= 0) return;
        if (currentTick - monster.LastHpRegenTick < monster.HpRegenIntervalTicks) return;
        monster.Hp = Math.Min(monster.MaxHp, monster.Hp + monster.HpRegenAmount);
        monster.LastHpRegenTick = currentTick;
    }

    // ── Déplacement ──────────────────────────────────────────────────────────

    private void MoveMonster(MonsterFeature monster, long currentTick)
    {
        if (_state == null) return;

        int steps = Math.Max(1, monster.MovementRangeInHexes);
        int movedSteps = 0;
        for (int i = 0; i < steps; i++)
        {
            if (!TryMoveOneHex(monster, currentTick)) break;
            movedSteps++;
        }

        monster.LastMovedTick = currentTick;
        monster.LastAttackedByMilitaryTick = currentTick; // grâce après mouvement
        if (movedSteps > 0)
        {
            monster.LastAttackTick = currentTick;
            monster.LastAttackTargetVertex = null;
        }
    }

    /// <summary>Déplace le monstre d'un seul hex. Retourne false si aucun voisin n'est franchissable.</summary>
    private bool TryMoveOneHex(MonsterFeature monster, long currentTick)
    {
        var map = _state!.GetMapFor(monster.Position)!;
        var neighbors = monster.Position.Neighbors()
            .Where(n => map.HasTile(n) && map.GetTile(n)!.TerrainType != TerrainType.Water)
            .ToList();

        if (neighbors.Count == 0) return false;

        var noBlockingNoCooldown = neighbors
            .Where(n => !_state.GetFeaturesAt(n).Any(f => f.BlocksHarvest) &&
                        (!_state.PlunderCooldownUntil.TryGetValue(n, out var until) || currentTick >= until))
            .ToList();

        var noBlocking = neighbors
            .Where(n => !_state.GetFeaturesAt(n).Any(f => f.BlocksHarvest))
            .ToList();

        var candidates = noBlockingNoCooldown.Count > 0 ? noBlockingNoCooldown
                       : noBlocking.Count > 0 ? noBlocking
                       : neighbors;

        var oldPosition = monster.Position;
        _state.MoveFeature(monster, candidates[_prng!.Next(candidates.Count)]);

        if (!oldPosition.Equals(monster.Position) && monster.DepartureCooldownTicks > 0)
        {
            _state.SetPlunderCooldown(oldPosition, currentTick + monster.DepartureCooldownTicks);
            _state.PlunderCooldownDuration[oldPosition] = monster.DepartureCooldownTicks;
        }

        return true;
    }

    // ── Attaque des villes ───────────────────────────────────────────────────

    private void AttackNearbyCity(MonsterFeature monster, long currentTick)
    {
        if (_state == null) return;
        if (currentTick - monster.LastAttackTick < monster.AttackIntervalTicks) return;

        var target = FindAttackTarget(monster);

        if (target == null)
        {
            monster.LastAttackTick = currentTick;
            monster.LastAttackTargetVertex = null;
            monster.LastAttackResourcesString = null;
            return;
        }

        ApplyMonsterAttack(monster, target, currentTick);
    }

    private City? FindAttackTarget(MonsterFeature monster)
    {
        // Priorité : villes dont un hex coïncide avec la position du monstre
        foreach (var civ in _state!.Civilizations)
            foreach (var city in civ.Cities)
                if (city.Position.GetHexes().Any(h => h.Equals(monster.Position)))
                    return city;

        if (monster.AttackRangeInHexes < 2) return null;

        // Portée étendue : hexes voisins du monstre
        var map = _state.GetMapFor(monster.Position)!;
        var neighborSet = monster.Position.Neighbors()
            .Where(n => map.HasTile(n))
            .ToHashSet();

        foreach (var civ in _state.Civilizations)
            foreach (var city in civ.Cities)
                if (city.Position.GetHexes().Any(h => neighborSet.Contains(h)))
                    return city;

        return null;
    }

    private void ApplyMonsterAttack(MonsterFeature monster, City city, long tick)
    {
        monster.LastAttackTick = tick;
        var civ = _state!.Civilizations.FirstOrDefault(c => c.Index == city.CivilizationIndex);
        if (civ == null) return;

        if (!monster.IgnoresPalisade && city.Buildings.OfType<Palisade>().Any(b => b.Level > 0))
        {
            monster.LastAttackTargetVertex = null;
            monster.LastAttackResourcesString = null;
            return;
        }

        bool didSomething = false;

        // ── Dégâts en cascade ────────────────────────────────────────────────
        int damage = monster.AttackDamage;

        if (damage > 0)
        {
            // 1. Soldats — Armures d'Acier : chaque soldat touché peut survivre en consommant 1 Acier
            int soldierDmg = Math.Min(damage, city.Soldiers);
            if (soldierDmg > 0)
            {
                int saved = SteelArmorEngine.TrySaveSoldiers(civ, city, soldierDmg, _prng!);
                city.Soldiers -= soldierDmg - saved;
                damage -= soldierDmg;
                didSomething = true;
            }

            // 2. Défense
            if (damage > 0)
            {
                int defenseDmg = Math.Min(damage, city.CurrentDefense);
                if (defenseDmg > 0) { city.CurrentDefense -= defenseDmg; damage -= defenseDmg; didSomething = true; }
            }

            // 3. Niveaux de Townhall (1 dégât = 1 niveau)
            if (damage > 0)
            {
                var townHall = city.Buildings.OfType<TownHall>().FirstOrDefault();
                if (townHall != null)
                {
                    int thDmg = Math.Min(damage, townHall.Level);
                    townHall.Level -= thDmg;
                    damage -= thDmg;
                    didSomething = true;
                    if (townHall.Level <= 0)
                    {
                        city.Buildings.Remove(townHall);
                        city.InvalidateLevelCache();
                    }
                    BuildingController.RecalculateStorageCapacity(civ);
                    civ.TrimResourcesToMax();
                }
            }

            // 4. Destruction de la ville — plus de Townhall (même si damage tombé à 0 pendant la cascade)
            if (!city.Buildings.OfType<TownHall>().Any())
            {
                monster.LastAttackTargetVertex = city.Position;
                monster.LastAttackResourcesString = null;
                _cityBuilderController?.DestroyCity(city, CityDestructionCause.Monster);
                return;
            }
        }

        // ── Ressources volées ────────────────────────────────────────────────
        if (monster.AttackResources > 0)
        {
            var stolen = new List<string>(monster.AttackResources);
            for (int i = 0; i < monster.AttackResources; i++)
            {
                var stealable = Enum.GetValues<Resource>()
                    .Where(r => civ.GetResourceQuantity(r) > 0)
                    .ToList();
                if (stealable.Count == 0) break;
                var resource = stealable[_prng!.Next(stealable.Count)];
                civ.RemoveResource(resource, 1);
                stolen.Add(resource.ToString());
            }
            if (stolen.Count > 0)
            {
                monster.LastAttackResourcesString = string.Join(",", stolen);
                didSomething = true;
            }
        }

        if (didSomething)
            monster.LastAttackTargetVertex = city.Position;
        else
        {
            monster.LastAttackTargetVertex = null;
            monster.LastAttackResourcesString = null;
        }
    }

    // ── API publique ─────────────────────────────────────────────────────────

    /// <summary>
    /// Retourne true si le cooldown de départ d'un monstre mobile est actif sur ce hex.
    /// </summary>
    public bool HasDepartureCooldown(HexCoord hex, long currentTick)
    {
        if (_state == null) return false;
        if (_state.PlunderCooldownUntil.TryGetValue(hex, out var until))
            return currentTick < until;
        return false;
    }

    /// <summary>
    /// Retourne true si une feature bloquante est présente sur ce hex ou si le cooldown est actif.
    /// </summary>
    public bool IsHarvestBlocked(HexCoord hex, long currentTick)
    {
        if (_state == null) return false;

        if (_state.GetFeaturesAt(hex).Any(f => f.BlocksHarvest))
            return true;

        return HasDepartureCooldown(hex, currentTick);
    }
}
