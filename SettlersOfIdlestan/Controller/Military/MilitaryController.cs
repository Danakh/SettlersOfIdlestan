using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Model.Bandits;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.IslandFeatures;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using System;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SettlersOfIdlestan.Controller.Military;

public class SoldierAttackEventArgs(Vertex cityVertex, HexCoord banditPosition) : EventArgs
{
    public Vertex CityVertex { get; } = cityVertex;
    public HexCoord BanditPosition { get; } = banditPosition;
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

public class CityDestroyedEventArgs(Vertex cityVertex) : EventArgs
{
    public Vertex CityVertex { get; } = cityVertex;
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

    public event EventHandler<SoldierAttackEventArgs>? SoldierAttackedBandit;
    public event EventHandler<SoldierAttackEventArgs>? SoldierAttackedHideout;
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

    internal void Initialize(WorldState? state, GameClock? clock)
    {
        if (_clock != null)
            _clock.Advanced -= OnClockAdvanced;

        _state = state;
        _clock = clock;

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
        ResolveBanditCombat(currentTick);
        ResolveHideoutCombat(currentTick);
        ResolveDefenseRegen(currentTick);
        ResolveCityAttacks(currentTick);
        ResolveReinforcements(currentTick);
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

    // ── Combat — bandits ─────────────────────────────────────────────────────

    private void ResolveBanditCombat(long currentTick)
    {
        if (_state == null) return;

        var deadBandits = new List<Bandit>();
        foreach (var bandit in _state.Features.OfType<Bandit>())
        {
            if (currentTick - bandit.LastMovedTick < CombatIntervalTicks) continue;

            AttackBandit(bandit);
            if (bandit.Hp <= 0)
                deadBandits.Add(bandit);
        }

        foreach (var b in deadBandits)
        {
            _state.RemoveFeature(b);
            _state.EventLog.Add(b.RemovedEventType);
        }
    }

    // ── Combat — repaires de bandits ─────────────────────────────────────────

    private void ResolveHideoutCombat(long currentTick)
    {
        if (_state == null) return;

        var deadHideouts = new List<BanditHideout>();
        foreach (var hideout in _state.Features.OfType<BanditHideout>())
        {
            if (!hideout.Found) continue;
            if (currentTick - hideout.LastAttackedTick < CombatIntervalTicks) continue;

            AttackHideout(hideout, currentTick);
            if (hideout.Hp <= 0)
                deadHideouts.Add(hideout);
        }

        foreach (var h in deadHideouts)
        {
            _state.RemoveFeature(h);
            _state.EventLog.Add(h.RemovedEventType);
        }
    }

    private void AttackHideout(BanditHideout hideout, long currentTick)
    {
        if (_state == null) return;

        foreach (var civ in _state.Civilizations)
        {
            foreach (var city in civ.Cities)
            {
                if (city.Soldiers == 0) continue;

                var cityHexes = city.Position.GetHexes();
                bool isOnCityHex = cityHexes.Any(h => h.Equals(hideout.Position));
                if (!isOnCityHex) continue;

                city.Soldiers--;
                hideout.Hp--;
                hideout.LastAttackedTick = currentTick;
                SoldierAttackedHideout?.Invoke(this, new SoldierAttackEventArgs(city.Position, hideout.Position));
                return;
            }
        }
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

    /// <summary>Définit ou efface le flux militaire d'une cité joueur.</summary>
    public void SetCityFlow(City city, Vertex? target) => city.FlowTarget = target;

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
            civ.Cities.Remove(city);
            ClearFlowsTargeting(city.Position);
            CityDestroyed?.Invoke(this, new CityDestroyedEventArgs(city.Position));
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

    private void ClearFlowsTargeting(Vertex position)
    {
        if (_state == null) return;
        foreach (var city in _state.Civilizations.SelectMany(c => c.Cities))
            if (city.FlowTarget != null && city.FlowTarget.Equals(position))
                city.FlowTarget = null;
    }

    public City? FindNearbyEnemyCity(City attackerCity, Civilization attackerCiv)
    {
        int range = CityAttackRange(attackerCiv);
        City? closest = null;
        int closestDist = int.MaxValue;

        foreach (var defenderCiv in _state!.Civilizations)
        {
            if (defenderCiv.Index == attackerCiv.Index) continue;
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

    /// <summary>
    /// Cherche une ville avec des soldats dont un hex est voisin du bandit,
    /// et déclenche une attaque (1 soldat consommé, 1 PV de dégât).
    /// </summary>
    private void AttackBandit(Bandit bandit)
    {
        if (_state == null) return;

        foreach (var civ in _state.Civilizations)
        {
            foreach (var city in civ.Cities)
            {
                if (city.Soldiers == 0) continue;

                var cityHexes = city.Position.GetHexes();
                bool isOnCityHex = cityHexes.Any(h => h.Equals(bandit.Position));
                if (!isOnCityHex) continue;

                city.Soldiers--;
                bandit.Hp--;
                SoldierAttackedBandit?.Invoke(this, new SoldierAttackEventArgs(city.Position, bandit.Position));
                return;
            }
        }
    }
}
