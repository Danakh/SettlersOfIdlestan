using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.IslandFeatures;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Monsters;
using System;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;
using TechId = SettlersOfIdlestan.Model.Civilization.TechnologyId;

namespace SettlersOfIdlestan.Controller.Military;

public class SoldierAttackEventArgs(Vertex cityVertex, HexCoord monsterPosition) : EventArgs
{
    public Vertex CityVertex { get; } = cityVertex;
    public HexCoord MonsterPosition { get; } = monsterPosition;
}

public class CityAttackEventArgs(Vertex sourceCity, Vertex targetCity, List<Vertex> path) : EventArgs
{
    public Vertex SourceCity { get; } = sourceCity;
    public Vertex TargetCity { get; } = targetCity;
    public List<Vertex> Path { get; } = path;
}

public class CityBuildingDestroyedEventArgs(Vertex cityVertex) : EventArgs
{
    public Vertex CityVertex { get; } = cityVertex;
}

public class CityDestroyedEventArgs(Vertex cityVertex, int civilizationIndex = -1) : EventArgs
{
    public Vertex CityVertex { get; } = cityVertex;
    public int CivilizationIndex { get; } = civilizationIndex;
}

public class ReinforcementEventArgs(Vertex sourceCity, Vertex targetCity, List<Vertex> path) : EventArgs
{
    public Vertex SourceCity { get; } = sourceCity;
    public Vertex TargetCity { get; } = targetCity;
    public List<Vertex> Path { get; } = path;
}

/// <summary>
/// Gère la production de soldats par les Casernes et le combat contre toutes les cibles
/// militaires (bandits, civilisations adverses à venir…).
/// </summary>
public class MilitaryController
{
    private WorldState? _state;
    private GameClock? _clock;
    private RoadController? _roadController;

    private long _lastPlayerAutoReinforcementTick = 0;
    private long _lastPlayerAutoAttackTick = 0;

    /// <summary>Intervalle entre deux recalculs des flux de renfort automatiques du joueur (100 ticks = 1 s).</summary>
    public const long AutoReinforcementIntervalTicks = 100L;

    /// <summary>Intervalle entre deux recalculs des cibles d'attaque automatique du joueur (100 ticks = 1 s).</summary>
    public const long AutoAttackIntervalTicks = 100L;

    /// <summary>Intervalle de production d'un soldat (1 000 ticks = 10 s à vitesse normale).</summary>
    public const long SoldierProductionIntervalTicks = 1_000L;

    /// <summary>Intervalle entre deux attaques d'une même cible (synchronisé avec MovementIntervalTicks).</summary>
    public const long CombatIntervalTicks = 100L;

    /// <summary>Niveau de Caserne à partir duquel la production de soldats est active.</summary>
    public const int SoldierProductionMinLevel = 1;

    /// <summary>Intervalle de régénération d'un point de défense (1 000 ticks).</summary>
    public const long DefenseRegenIntervalTicks = 500L;

    /// <summary>Intervalle minimum entre deux attaques de ville lancées par la même ville.</summary>
    public const long CityAttackIntervalTicks = 100L;

    /// <summary>Distance de base en edges en deçà de laquelle une ville adverse déclenche une attaque automatique.</summary>
    private const int DefaultCityAttackRange = 3;

    /// <summary>Distance de base en edges dans laquelle une ville peut envoyer des renforts à une ville alliée.</summary>
    private const int DefaultReinforcementRange = 5;

    /// <summary>Intervalle minimum entre deux envois de renforts depuis la même ville.</summary>
    public const long ReinforcementIntervalTicks = 100L;

    /// <summary>Intervalle entre deux cycles de consommation de nourriture par les soldats (1 000 ticks = 10 s).</summary>
    public const long SoldierFeedIntervalTicks = 1_000L;

    /// <summary>Distance effective en edges, après application des modificateurs de civilisation.</summary>
    public int CityAttackRange(Civilization civ)
        => civ.ModifierAggregator.ApplyModifiers(ECategory.CITY_ATTACK_RANGE, "", DefaultCityAttackRange);

    /// <summary>Portée de renfort effective en edges, après application des modificateurs de civilisation.</summary>
    public int ReinforcementRange(Civilization civ)
        => civ.ModifierAggregator.ApplyModifiers(ECategory.REINFORCEMENT_RANGE, "", DefaultReinforcementRange);

    public event EventHandler<SoldierAttackEventArgs>? SoldierAttackedMonster;
    /// <summary>Alias de compatibilité — préférer SoldierAttackedMonster.</summary>
    public event EventHandler<SoldierAttackEventArgs>? SoldierAttackedBandit;
    public event EventHandler<CityAttackEventArgs>? SoldierAttackedCity;
    public event EventHandler<CityBuildingDestroyedEventArgs>? CityBuildingDestroyed;
    public event EventHandler<CityDestroyedEventArgs>? CityDestroyed;
    public event EventHandler<ReinforcementEventArgs>? ReinforcementSent;

    /// <summary>Nombre de soldats disponibles dans la ville.</summary>
    public int GetAttackScore(City city) => city.Soldiers;

    /// <summary>Capacité maximale de soldats de la ville, tous bâtiments garnison confondus.</summary>
    public int GetMaximumSoldierCapacity(City city, Civilization civ)
        => city.MaxSoldiers + civ.CityMaxSoldiersBonus;

    /// <summary>
    /// Soldats produits par seconde dans la ville (0 si pas de Caserne active au niveau minimum).
    /// Tient compte du modificateur UnitProductionSpeed de la civilisation.
    /// </summary>
    public double GetSoldierProductionRate(City city, Civilization civ)
    {
        var barracks = city.Buildings.OfType<Barracks>()
            .FirstOrDefault(b => b.ActivationStatus == ActivationStatus.ACTIVE && b.Level >= SoldierProductionMinLevel);
        if (barracks == null) return 0;
        const double ticksPerSecond = 100.0;
        return civ.UnitProductionSpeed * ticksPerSecond / SoldierProductionIntervalTicks;
    }

    /// <summary>Score de défense maximal de la ville (bâtiments + modificateurs de civilisation).</summary>
    public int GetDefenseScore(City city, Civilization? civ = null)
    {
        int score = city.MaxDefense;
        if (civ != null)
            score += civ.ModifierAggregator.ApplyModifiers(ECategory.CITY_DEFENSE, "", 0);
        return score;
    }

    internal void Initialize(WorldState? state, GameClock? clock, RoadController? roadController = null)
    {
        if (_clock != null)
            _clock.Advanced -= OnClockAdvanced;

        _state = state;
        _clock = clock;
        _roadController = roadController;

        if (_clock != null)
            _clock.Advanced += OnClockAdvanced;
    }

    private void OnClockAdvanced(object? sender, GameClockAdvancedEventArgs e)
    {
        try { Update(e.CurrentTick); }
        catch (Exception) { }
    }

    private void Update(long currentTick)
    {
        if (_state == null) return;
        ProduceSoldiers(currentTick);
        ResolveSoldierFeeding(currentTick);
        ResolveMonsterCombat(currentTick);
        ResolveDefenseRegen(currentTick);
        ResolveCityAttacks(currentTick);
        ResolveReinforcements(currentTick);
        ResolvePlayerAutoReinforcement(currentTick);
        ResolvePlayerAutoAttack(currentTick);
    }

    private void ResolvePlayerAutoReinforcement(long currentTick)
    {
        if (_state == null) return;
        if (!_state.AutomationSettings.MilitaryReinforcementAutomationEnabled) return;
        if (currentTick - _lastPlayerAutoReinforcementTick < AutoReinforcementIntervalTicks) return;
        _lastPlayerAutoReinforcementTick = currentTick;

        var playerCiv = _state.PlayerCivilization;
        if (!playerCiv.ModifierAggregator.HasModifier(ECategory.UNLOCK_AUTO_REINFORCEMENT)) return;

        UpdateCivilizationReinforcementFlows(playerCiv);
    }

    private void ResolvePlayerAutoAttack(long currentTick)
    {
        if (_state == null) return;
        if (!_state.AutomationSettings.MilitaryAttackAutomationEnabled) return;
        if (currentTick - _lastPlayerAutoAttackTick < AutoAttackIntervalTicks) return;
        _lastPlayerAutoAttackTick = currentTick;

        var playerCiv = _state.PlayerCivilization;
        if (!playerCiv.ModifierAggregator.HasModifier(ECategory.UNLOCK_AUTO_ATTACK)) return;

        foreach (var city in playerCiv.Cities)
        {
            if (city.FlowTarget != null && IsEnemyCityAt(city.FlowTarget, playerCiv)) continue;
            var enemy = FindNearbyEnemyCity(city, playerCiv);
            if (enemy != null)
                SetCityFlow(city, enemy.Position);
        }
    }

    // ── Production ───────────────────────────────────────────────────────────

    private void ProduceSoldiers(long currentTick)
    {
        if (_state == null) return;

        foreach (var civ in _state.Civilizations)
            foreach (var city in civ.Cities)
            {
                if (city.Soldiers >= GetMaximumSoldierCapacity(city, civ)) continue;
                long effectiveProductionInterval = (long)(SoldierProductionIntervalTicks / civ.UnitProductionSpeed);
                if (currentTick - city.LastSoldierProductionTick < effectiveProductionInterval) continue;

                var barracks = city.Buildings.OfType<Barracks>()
                    .FirstOrDefault(b => b.ActivationStatus == ActivationStatus.ACTIVE && b.Level >= SoldierProductionMinLevel);
                if (barracks == null) continue;
                if (civ.GetResourceQuantity(Resource.Ore) < 1)
                {
                    civ.RaiseLowStock(Resource.Ore);
                    continue;
                }

                civ.RemoveResource(Resource.Ore, 1);
                city.Soldiers++;
                city.LastSoldierProductionTick = currentTick;

                if (civ.Index == _state.PlayerCivilization.Index)
                {
                    int oreQty = civ.GetResourceQuantity(Resource.Ore);
                    int oreMax = civ.GetResourceMaxQuantity(Resource.Ore);
                    if (oreMax > 0 && oreQty * 10 <= oreMax)
                        civ.RaiseLowStock(Resource.Ore);
                }
            }
    }

    // ── Consommation de nourriture ───────────────────────────────────────────

    private void ResolveSoldierFeeding(long currentTick)
    {
        if (_state == null) return;
        if (currentTick - _state.LastSoldierFeedTick < SoldierFeedIntervalTicks) return;
        _state.LastSoldierFeedTick = currentTick;

        foreach (var civ in _state.Civilizations)
        {
            int totalSoldiers = civ.Cities.Sum(city => city.Soldiers);
            if (totalSoldiers == 0) continue;

            int availableFood = civ.GetResourceQuantity(Resource.Food);
            int fedSoldiers = Math.Min(totalSoldiers, availableFood);
            int starvedSoldiers = totalSoldiers - fedSoldiers;

            if (fedSoldiers > 0)
            {
                civ.RemoveResource(Resource.Food, fedSoldiers);

                if (civ.Index == _state.PlayerCivilization.Index)
                {
                    int foodQty = civ.GetResourceQuantity(Resource.Food);
                    int foodMax = civ.GetResourceMaxQuantity(Resource.Food);
                    if (foodMax > 0 && foodQty * 10 <= foodMax)
                        civ.RaiseLowStock(Resource.Food);
                }
                else
                {
                    civ.RaiseLowStock(Resource.Food);
                }
            }

            if (starvedSoldiers > 0)
            {
                int toKill = starvedSoldiers;
                foreach (var city in civ.Cities)
                {
                    if (toKill <= 0) break;
                    int kill = Math.Min(toKill, city.Soldiers);
                    city.Soldiers -= kill;
                    toKill -= kill;
                }

                if (civ.Index == _state.PlayerCivilization.Index)
                    _state.EventLog.Add(GameEventType.SoldierStarved);
            }
        }
    }

    // ── Combat — monstres (générique) ────────────────────────────────────────

    private void ResolveMonsterCombat(long currentTick)
    {
        if (_state == null) return;

        var deadMonsters = new List<MonsterFeature>();
        foreach (var monster in _state.Features.OfType<MonsterFeature>())
        {
            if (currentTick - monster.LastAttackedByMilitaryTick < CombatIntervalTicks) continue;

            if (AttackMonsterWithSoldiers(monster, currentTick) && monster.Hp <= 0)
                deadMonsters.Add(monster);
        }

        foreach (var m in deadMonsters)
        {
            _state.RemoveFeature(m);
            _state.EventLog.Add(m.RemovedEventType);
        }
    }

    private bool AttackMonsterWithSoldiers(MonsterFeature monster, long currentTick)
    {
        if (_state == null) return false;

        foreach (var civ in _state.Civilizations)
        {
            foreach (var city in civ.Cities)
            {
                if (city.Soldiers == 0) continue;

                var cityHexes = city.Position.GetHexes();
                if (!cityHexes.Any(h => h.Equals(monster.Position))) continue;

                city.Soldiers--;
                monster.Hp--;
                monster.LastAttackedByMilitaryTick = currentTick;
                SoldierAttackedMonster?.Invoke(this, new SoldierAttackEventArgs(city.Position, monster.Position));
                SoldierAttackedBandit?.Invoke(this, new SoldierAttackEventArgs(city.Position, monster.Position));
                return true;
            }
        }
        return false;
    }

    // ── Régénération de défense ──────────────────────────────────────────────
    // Coûte 1 bois + 1 pierre par point régénéré. Non désactivable.

    private void ResolveDefenseRegen(long currentTick)
    {
        foreach (var civ in _state!.Civilizations)
            foreach (var city in civ.Cities)
            {
                int maxDef = GetDefenseScore(city, civ);
                if (city.CurrentDefense >= maxDef) continue;
                long effectiveRegenInterval = (long)(DefenseRegenIntervalTicks / civ.CityDefenseRegenSpeed);
                if (currentTick - city.LastDefenseRegenTick < effectiveRegenInterval) continue;
                if (civ.GetResourceQuantity(Resource.Wood) < 1 || civ.GetResourceQuantity(Resource.Stone) < 1) continue;
                civ.RemoveResource(Resource.Wood, 1);
                civ.RemoveResource(Resource.Stone, 1);
                city.CurrentDefense++;
                city.LastDefenseRegenTick = currentTick;
            }
    }

    // ── Flux joueur ──────────────────────────────────────────────────────────

    /// <summary>Définit ou efface le flux militaire d'une cité.</summary>
    public void SetCityFlow(City city, Vertex? target) => city.FlowTarget = target;

    /// <summary>
    /// Réévalue et assigne les flux de renfort pour chaque cité de la civilisation.
    /// Les flux ciblant une ville ennemie (attaque manuelle) ne sont pas modifiés.
    /// </summary>
    public void UpdateCivilizationReinforcementFlows(Civilization civ)
    {
        foreach (var city in civ.Cities)
        {
            if (city.FlowTarget != null && IsEnemyCityAt(city.FlowTarget, civ)) continue;

            Vertex? newFlow = null;
            int capacity = city.MaxSoldiers;
            if (capacity > 0
                && city.Soldiers * 4 >= capacity
                && FindNearbyEnemyCity(city, civ) == null)
            {
                int range = ReinforcementRange(civ);
                City? target = null;
                int fewestSoldiers = city.Soldiers;

                foreach (var friendly in civ.Cities)
                {
                    if (friendly == city) continue;
                    if (friendly.Position.Z != city.Position.Z) continue;
                    int dist = city.Position.EdgeDistanceTo(friendly.Position);
                    if (dist > range) continue;

                    int tCap = friendly.MaxSoldiers;
                    if (tCap == 0 || friendly.Soldiers * 2 > tCap) continue;
                    if (friendly.Soldiers + 2 >= city.Soldiers) continue;

                    if (friendly.Soldiers < fewestSoldiers)
                    {
                        target = friendly;
                        fewestSoldiers = friendly.Soldiers;
                    }
                }

                if (target != null)
                    newFlow = target.Position;
            }

            SetCityFlow(city, newFlow);
        }
    }

    private bool IsEnemyCityAt(Vertex target, Civilization civ)
        => _state!.Civilizations.Any(c => c.Index != civ.Index && c.Cities.Any(cc => cc.Position.Equals(target)));

    // ── Combat — villes adverses ─────────────────────────────────────────────

    private void ResolveCityAttacks(long currentTick)
    {
        var citiesToDestroy = new List<(Civilization civ, City city)>();

        foreach (var attackerCiv in _state!.Civilizations)
        {
            foreach (var attackerCity in attackerCiv.Cities.ToList())
            {
                if (currentTick - attackerCity.LastCityAttackTick < CityAttackIntervalTicks) continue;
                if (attackerCity.Soldiers == 0) continue;
                if (attackerCity.FlowTarget == null) continue;

                var targetCity = FindEnemyCityAt(attackerCity.FlowTarget, attackerCiv);
                if (targetCity == null) continue;
                if (attackerCity.Position.EdgeDistanceTo(targetCity.Position) > CityAttackRange(attackerCiv)) continue;

                attackerCity.Soldiers--;
                attackerCity.LastCityAttackTick = currentTick;

                var path = HexGridPathfinder.FindVertexPath(attackerCity.Position, targetCity.Position);
                SoldierAttackedCity?.Invoke(this, new CityAttackEventArgs(attackerCity.Position, targetCity.Position, path));

                bool destroyed = ApplyAttackToCity(targetCity);
                if (destroyed)
                {
                    var ownerCiv = _state.Civilizations.FirstOrDefault(c => c.Index == targetCity.CivilizationIndex);
                    if (ownerCiv != null)
                        citiesToDestroy.Add((ownerCiv, targetCity));
                }
            }
        }

        foreach (var (civ, city) in citiesToDestroy)
        {
            city.RaiseDestroyed();
            civ.RemoveCity(city);
            _roadController?.OnCityDestroyed(civ, city.Position);
            NotifyCityDestroyed(city.Position, civ.Index);
            _state!.RecalculateVisibleIslandMaps();
        }
    }

    private City? FindEnemyCityAt(Vertex target, Civilization attackerCiv)
    {
        foreach (var civ in _state!.Civilizations)
        {
            if (civ.Index == attackerCiv.Index) continue;
            var city = civ.Cities.FirstOrDefault(c => c.Position.Equals(target));
            if (city != null) return city;
        }
        return null;
    }

    /// <summary>
    /// Clears flows, fires <see cref="CityDestroyed"/>, and recalculates visibility.
    /// Call after removing the city from its civilization and cleaning up roads.
    /// </summary>
    public void NotifyCityDestroyed(Vertex position, int civilizationIndex)
    {
        ClearFlowsTargeting(position);
        CityDestroyed?.Invoke(this, new CityDestroyedEventArgs(position, civilizationIndex));
    }

    private void ClearFlowsTargeting(Vertex position)
    {
        if (_state == null) return;
        foreach (var city in _state.Civilizations.SelectMany(c => c.Cities))
            if (city.FlowTarget != null && city.FlowTarget.Equals(position))
                city.FlowTarget = null;
    }

    public City? FindNearbyEnemyCity(City attackerCity, Civilization attackerCiv, IReadOnlyCollection<int>? targetCivIndices = null)
    {
        int range = CityAttackRange(attackerCiv);
        City? closest = null;
        int closestDist = int.MaxValue;

        foreach (var defenderCiv in _state!.Civilizations)
        {
            if (defenderCiv.Index == attackerCiv.Index) continue;
            if (targetCivIndices != null && targetCivIndices.Count > 0 && !targetCivIndices.Contains(defenderCiv.Index)) continue;
            foreach (var defenderCity in defenderCiv.Cities)
            {
                if (defenderCity.Position.Z != attackerCity.Position.Z) continue;
                if (!IsCityVisibleTo(defenderCity, attackerCiv)) continue;
                int dist = attackerCity.Position.EdgeDistanceTo(defenderCity.Position);
                if (dist <= range && dist < closestDist)
                {
                    closest = defenderCity;
                    closestDist = dist;
                }
            }
        }

        return closest;
    }

    // ── Renforts entre villes alliées ────────────────────────────────────────

    private void ResolveReinforcements(long currentTick)
    {
        if (_state == null) return;

        foreach (var civ in _state.Civilizations)
        {
            foreach (var sourceCity in civ.Cities.ToList())
            {
                if (currentTick - sourceCity.LastReinforcementTick < ReinforcementIntervalTicks) continue;
                if (sourceCity.Soldiers == 0) continue;
                if (sourceCity.FlowTarget == null) continue;

                var targetCity = civ.Cities.FirstOrDefault(c => c != sourceCity && c.Position.Equals(sourceCity.FlowTarget));
                if (targetCity == null) continue;
                if (sourceCity.Position.EdgeDistanceTo(targetCity.Position) > ReinforcementRange(civ)) continue;
                if (targetCity.Soldiers >= GetMaximumSoldierCapacity(targetCity, civ)) continue;

                sourceCity.Soldiers--;
                targetCity.Soldiers++;
                sourceCity.LastReinforcementTick = currentTick;

                var path = HexGridPathfinder.FindVertexPath(sourceCity.Position, targetCity.Position);
                ReinforcementSent?.Invoke(this, new ReinforcementEventArgs(sourceCity.Position, targetCity.Position, path));
            }
        }
    }

    private bool IsCityVisibleTo(City city, Civilization civ)
    {
        var visibleMaps = _state!.GetVisibleIslandMapsForZ(city.Position.Z);
        if (!visibleMaps.TryGetValue(civ.Index, out var visibleMap)) return true;
        return city.Position.GetHexes().Any(h => visibleMap.HasTile(h));
    }

    /// <summary>
    /// Applique une attaque à la ville cible. Retourne true si la ville est détruite.
    /// </summary>
    private bool ApplyAttackToCity(City targetCity)
    {
        // Les soldats défenseurs absorbent l'attaque : les deux soldats meurent, la défense est intacte.
        if (targetCity.Soldiers > 0)
        {
            targetCity.Soldiers--;
            return false;
        }

        if (targetCity.CurrentDefense > 0)
        {
            targetCity.CurrentDefense--;
            return false;
        }

        var townHall = targetCity.Buildings.OfType<TownHall>().FirstOrDefault();
        if (townHall != null)
        {
            townHall.Level--;
            if (townHall.Level <= 0)
            {
                targetCity.Buildings.Remove(townHall);
                CityBuildingDestroyed?.Invoke(this, new CityBuildingDestroyedEventArgs(targetCity.Position));
            }
            return false;
        }

        return true;
    }

}
