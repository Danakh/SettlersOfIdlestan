using System;
using System.Collections.Generic;
using System.Linq;
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
    public event EventHandler<CityDestroyedEventArgs>? CityDestroyedByMonster;

    private WorldState? _state;
    private GameClock? _clock;
    private GamePRNG _prng = new();

    private List<MonsterFeature> _monsters = new();

    /// <summary>Intervalle de déplacement par défaut (3 000 ticks = 30 s à vitesse normale).</summary>
    public const long MovementIntervalTicks = 3_000L;

    internal void Initialize(WorldState? state, GameClock? clock, GamePRNG? prng = null)
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
        catch { }
    }

    private void Update(long currentTick)
    {
        if (_state == null) return;

        UpdateSpawns(currentTick);

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
                AttackNearbyCity(monster, currentTick);
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

        var map = _state.GetMapFor(monster.Position);
        var neighbors = monster.Position.Neighbors()
            .Where(n => map.HasTile(n) && map.GetTile(n)!.TerrainType != TerrainType.Water)
            .ToList();

        if (neighbors.Count == 0)
        {
            monster.LastMovedTick = currentTick;
            monster.LastAttackedByMilitaryTick = currentTick;
            return;
        }

        var noBlockingNoCooldown = neighbors
            .Where(n => !_state.Features.Any(f => f.Position.Equals(n) && f.BlocksHarvest) &&
                        (!_state.PlunderCooldownUntil.TryGetValue(n, out var until) || currentTick >= until))
            .ToList();

        var noBlocking = neighbors
            .Where(n => !_state.Features.Any(f => f.Position.Equals(n) && f.BlocksHarvest))
            .ToList();

        var candidates = noBlockingNoCooldown.Count > 0 ? noBlockingNoCooldown
                       : noBlocking.Count > 0 ? noBlocking
                       : neighbors;

        var oldPosition = monster.Position;
        monster.Position = candidates[_prng.Next(candidates.Count)];
        monster.LastMovedTick = currentTick;
        monster.LastAttackTick = currentTick;
        monster.LastAttackTargetVertex = null;
        monster.LastAttackedByMilitaryTick = currentTick; // grâce après mouvement

        if (!oldPosition.Equals(monster.Position) && monster.DepartureCooldownTicks > 0)
        {
            _state.PlunderCooldownUntil[oldPosition] = currentTick + monster.DepartureCooldownTicks;
            _state.PlunderCooldownDuration[oldPosition] = monster.DepartureCooldownTicks;
        }
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
            return;
        }

        ApplyMonsterAttack(monster, target.Value.city, target.Value.civ, currentTick);
    }

    private (City city, Civilization civ)? FindAttackTarget(MonsterFeature monster)
    {
        // Priorité : villes dont un hex coïncide avec la position du monstre
        foreach (var civ in _state!.Civilizations)
            foreach (var city in civ.Cities)
                if (city.Position.GetHexes().Any(h => h.Equals(monster.Position)))
                    return (city, civ);

        if (monster.AttackRangeInHexes < 2) return null;

        // Portée étendue : hexes voisins du monstre
        var map = _state.GetMapFor(monster.Position);
        var neighborSet = monster.Position.Neighbors()
            .Where(n => map.HasTile(n))
            .ToHashSet();

        foreach (var civ in _state.Civilizations)
            foreach (var city in civ.Cities)
                if (city.Position.GetHexes().Any(h => neighborSet.Contains(h)))
                    return (city, civ);

        return null;
    }

    private void ApplyMonsterAttack(MonsterFeature monster, City city, Civilization civ, long tick)
    {
        monster.LastAttackTick = tick;

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
            // 1. Soldats
            int soldierDmg = Math.Min(damage, city.Soldiers);
            if (soldierDmg > 0) { city.Soldiers -= soldierDmg; damage -= soldierDmg; didSomething = true; }

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
                }
            }

            // 4. Destruction de la ville — dégâts résiduels après townhall détruit
            if (damage > 0 && !city.Buildings.OfType<TownHall>().Any())
            {
                monster.LastAttackTargetVertex = city.Position;
                monster.LastAttackResourcesString = null;
                city.RaiseDestroyed();
                civ.RemoveCity(city);
                CityDestroyedByMonster?.Invoke(this, new CityDestroyedEventArgs(city.Position, civ.Index));
                _state!.RecalculateVisibleIslandMaps();
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
                var resource = stealable[_prng.Next(stealable.Count)];
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

        if (_state.Features.Any(f => f.Position.Equals(hex) && f.BlocksHarvest))
            return true;

        return HasDepartureCooldown(hex, currentTick);
    }
}
