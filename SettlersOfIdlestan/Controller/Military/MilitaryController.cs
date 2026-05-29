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
    private IslandState? _state;
    private GameClock? _clock;

    /// <summary>Intervalle de production d'un soldat (1 000 ticks = 10 s à vitesse normale).</summary>
    public const long SoldierProductionIntervalTicks = 1_000L;

    /// <summary>Intervalle entre deux attaques d'une même cible (synchronisé avec MovementIntervalTicks).</summary>
    public const long CombatIntervalTicks = 100L;

    /// <summary>Niveau de Caserne à partir duquel la production de soldats est active.</summary>
    public const int SoldierProductionMinLevel = 1;

    /// <summary>Capacité maximale de soldats dans une Caserne.</summary>
    public const int MaxSoldiers = 10;

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

    /// <summary>Nombre total de soldats disponibles dans la ville (toutes casernes).</summary>
    public int GetAttackScore(City city)
        => city.Buildings.OfType<Barracks>().Sum(b => b.Soldiers);

    /// <summary>Capacité maximale de soldats de la ville, tous bâtiments garnison confondus.</summary>
    public int GetMaximumSoldierCapacity(City city, Civilization? civ = null)
    {
        int capacity = city.Buildings.OfType<Barracks>().Count() * MaxSoldiers;
        return capacity;
    }

    /// <summary>Score de défense de la ville : Palissade=10, Caserne=5, plus modificateurs de civilisation.</summary>
    public int GetDefenseScore(City city, Civilization? civ = null)
    {
        int score = 0;
        foreach (var b in city.Buildings)
            score += b switch { Palisade => 10, Barracks => 5, _ => 0 };
        if (civ != null)
            score += civ.ModifierAggregator.ApplyModifiers(ECategory.CITY_DEFENSE, "", 0);
        return score;
    }

    internal void Initialize(IslandState? state, GameClock? clock)
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
                foreach (var barracks in city.Buildings.OfType<Barracks>())
                {
                    if (barracks.ActivationStatus != ActivationStatus.ACTIVE) continue;
                    if (barracks.Level < SoldierProductionMinLevel) continue;
                    if (barracks.Soldiers >= MaxSoldiers) continue;
                    if (currentTick - barracks.LastSoldierProductionTick < SoldierProductionIntervalTicks) continue;
                    if (civ.GetResourceQuantity(Resource.Ore) < 1) continue;

                    civ.RemoveResource(Resource.Ore, 1);
                    barracks.Soldiers++;
                    barracks.LastSoldierProductionTick = currentTick;
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
                var barracks = city.Buildings.OfType<Barracks>().FirstOrDefault(b => b.Soldiers > 0);
                if (barracks == null) continue;

                var cityHexes = city.Position.GetHexes();
                bool isOnCityHex = cityHexes.Any(h => h.Equals(hideout.Position));
                if (!isOnCityHex) continue;

                barracks.Soldiers--;
                hideout.Hp--;
                hideout.LastAttackedTick = currentTick;
                SoldierAttackedHideout?.Invoke(this, new SoldierAttackEventArgs(city.Position, hideout.Position));
                return;
            }
        }
    }

    // ── Régénération de défense ──────────────────────────────────────────────

    private void ResolveDefenseRegen(long currentTick)
    {
        foreach (var civ in _state!.Civilizations)
            foreach (var city in civ.Cities)
            {
                int maxDef = GetDefenseScore(city, civ);
                if (city.CurrentDefense >= maxDef) continue;
                if (currentTick - city.LastDefenseRegenTick < DefenseRegenIntervalTicks) continue;
                city.CurrentDefense++;
                city.LastDefenseRegenTick = currentTick;
            }
    }

    // ── Combat — villes adverses ─────────────────────────────────────────────

    private void ResolveCityAttacks(long currentTick)
    {
        var citiesToDestroy = new List<(Civilization civ, City city)>();

        foreach (var attackerCiv in _state!.Civilizations)
        {
            foreach (var attackerCity in attackerCiv.Cities.ToList())
            {
                if (currentTick - attackerCity.LastCityAttackTick < CityAttackIntervalTicks) continue;
                if (GetAttackScore(attackerCity) == 0) continue;

                var targetCity = FindNearbyEnemyCity(attackerCity, attackerCiv);
                if (targetCity == null) continue;

                var barracks = attackerCity.Buildings.OfType<Barracks>().FirstOrDefault(b => b.Soldiers > 0);
                if (barracks == null) continue;

                barracks.Soldiers--;
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
            CityDestroyed?.Invoke(this, new CityDestroyedEventArgs(city.Position));
            _state!.RecalculateVisibleIslandMaps();
        }
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

                int capacity = GetMaximumSoldierCapacity(sourceCity, civ);
                if (capacity == 0) continue;

                int totalSoldiers = GetAttackScore(sourceCity);
                if (totalSoldiers * 2 < capacity) continue;

                if (FindNearbyEnemyCity(sourceCity, civ) != null) continue;

                int range = ReinforcementRange(civ);
                City? targetCity = null;
                int closestDist = int.MaxValue;

                foreach (var friendlyCity in civ.Cities)
                {
                    if (friendlyCity == sourceCity) continue;
                    int dist = sourceCity.Position.EdgeDistanceTo(friendlyCity.Position);
                    if (dist > range || dist >= closestDist) continue;

                    int targetCapacity = GetMaximumSoldierCapacity(friendlyCity, civ);
                    if (targetCapacity == 0) continue;

                    int targetSoldiers = GetAttackScore(friendlyCity);
                    if (targetSoldiers * 2 > targetCapacity) continue;
                    if (targetSoldiers + 2 >= totalSoldiers) continue;

                    targetCity = friendlyCity;
                    closestDist = dist;
                }

                if (targetCity == null) continue;

                var receiver = targetCity.Buildings.OfType<Barracks>()
                    .FirstOrDefault(b => b.Soldiers < MaxSoldiers);
                if (receiver == null) continue;

                var donor = sourceCity.Buildings.OfType<Barracks>()
                    .OrderByDescending(b => b.Soldiers).FirstOrDefault();
                if (donor == null || donor.Soldiers == 0) continue;

                donor.Soldiers--;
                receiver.Soldiers++;
                sourceCity.LastReinforcementTick = currentTick;

                var path = HexGridPathfinder.FindVertexPath(sourceCity.Position, targetCity.Position);
                ReinforcementSent?.Invoke(this, new ReinforcementEventArgs(sourceCity.Position, targetCity.Position, path));
            }
        }
    }

    private bool IsCityVisibleTo(City city, Civilization civ)
    {
        if (!_state!.VisibleIslandMaps.TryGetValue(civ.Index, out var visibleMap)) return true;
        return city.Position.GetHexes().Any(h => visibleMap.HasTile(h));
    }

    /// <summary>
    /// Applique une attaque à la ville cible. Retourne true si la ville est détruite.
    /// </summary>
    private bool ApplyAttackToCity(City targetCity)
    {
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
    /// Cherche une Caserne avec des soldats dont un hex de ville est voisin du bandit,
    /// et déclenche une attaque (1 soldat consommé, 1 PV de dégât).
    /// </summary>
    private void AttackBandit(Bandit bandit)
    {
        if (_state == null) return;

        foreach (var civ in _state.Civilizations)
        {
            foreach (var city in civ.Cities)
            {
                var barracks = city.Buildings.OfType<Barracks>().FirstOrDefault(b => b.Soldiers > 0);
                if (barracks == null) continue;

                var cityHexes = city.Position.GetHexes();
                bool isOnCityHex = cityHexes.Any(h => h.Equals(bandit.Position));
                if (!isOnCityHex) continue;

                barracks.Soldiers--;
                bandit.Hp--;
                SoldierAttackedBandit?.Invoke(this, new SoldierAttackEventArgs(city.Position, bandit.Position));
                return;
            }
        }
    }
}
