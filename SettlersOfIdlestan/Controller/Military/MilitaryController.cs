using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Model.Bandits;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.IslandFeatures;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using System;

namespace SettlersOfIdlestan.Controller.Military;

public class SoldierAttackEventArgs(Vertex cityVertex, HexCoord banditPosition) : EventArgs
{
    public Vertex CityVertex { get; } = cityVertex;
    public HexCoord BanditPosition { get; } = banditPosition;
}

public class CityAttackEventArgs(Vertex sourceCity, Vertex targetCity, List<HexCoord> path) : EventArgs
{
    public Vertex SourceCity { get; } = sourceCity;
    public Vertex TargetCity { get; } = targetCity;
    public List<HexCoord> Path { get; } = path;
}

public class CityBuildingDestroyedEventArgs(Vertex cityVertex) : EventArgs
{
    public Vertex CityVertex { get; } = cityVertex;
}

public class CityDestroyedEventArgs(Vertex cityVertex) : EventArgs
{
    public Vertex CityVertex { get; } = cityVertex;
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
    public const int SoldierProductionMinLevel = 2;

    /// <summary>Capacité maximale de soldats dans une Caserne.</summary>
    public const int MaxSoldiers = 10;

    /// <summary>Intervalle de régénération d'un point de défense (1 000 ticks).</summary>
    public const long DefenseRegenIntervalTicks = 1_000L;

    /// <summary>Intervalle minimum entre deux attaques de ville lancées par la même ville.</summary>
    public const long CityAttackIntervalTicks = 3_000L;

    /// <summary>Distance en hexagones en deçà de laquelle une ville adverse déclenche une attaque automatique.</summary>
    public const int CityAttackRange = 3;

    private readonly Random _random = new();

    public event EventHandler<SoldierAttackEventArgs>? SoldierAttackedBandit;
    public event EventHandler<SoldierAttackEventArgs>? SoldierAttackedHideout;
    public event EventHandler<CityAttackEventArgs>? SoldierAttackedCity;
    public event EventHandler<CityBuildingDestroyedEventArgs>? CityBuildingDestroyed;
    public event EventHandler<CityDestroyedEventArgs>? CityDestroyed;

    /// <summary>Nombre total de soldats disponibles dans la ville (toutes casernes).</summary>
    public int GetAttackScore(City city)
        => city.Buildings.OfType<Barracks>().Sum(b => b.Soldiers);

    /// <summary>Score de défense de la ville : Palissade=10, Caserne=5.</summary>
    public int GetDefenseScore(City city)
    {
        int score = 0;
        foreach (var b in city.Buildings)
            score += b switch { Palisade => 10, Barracks => 5, _ => 0 };
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
        catch { }
    }

    private void Update(long currentTick)
    {
        if (_state == null) return;
        ProduceSoldiers(currentTick);
        ResolveBanditCombat(currentTick);
        ResolveHideoutCombat(currentTick);
        ResolveDefenseRegen(currentTick);
        ResolveCityAttacks(currentTick);
    }

    // ── Production ───────────────────────────────────────────────────────────

    private void ProduceSoldiers(long currentTick)
    {
        if (_state == null) return;

        foreach (var civ in _state.Civilizations)
            foreach (var city in civ.Cities)
                foreach (var barracks in city.Buildings.OfType<Barracks>())
                    if (barracks.Level >= SoldierProductionMinLevel && barracks.Soldiers < MaxSoldiers)
                        if (currentTick - barracks.LastSoldierProductionTick >= SoldierProductionIntervalTicks)
                        {
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
                int maxDef = GetDefenseScore(city);
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

                var fromHex = GetValidHex(attackerCity) ?? attackerCity.Position.Hex1;
                var toHex = GetValidHex(targetCity) ?? targetCity.Position.Hex1;
                var path = FindPath(fromHex, toHex);

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

    private HexCoord? GetValidHex(City city)
        => city.Position.GetHexes()
            .FirstOrDefault(h => _state!.Map.HasTile(h) && _state.Map.GetTile(h)!.TerrainType != TerrainType.Water);

    private City? FindNearbyEnemyCity(City attackerCity, Civilization attackerCiv)
    {
        var attackerHexes = attackerCity.Position.GetHexes();
        foreach (var defenderCiv in _state!.Civilizations)
        {
            if (defenderCiv.Index == attackerCiv.Index) continue;
            foreach (var defenderCity in defenderCiv.Cities)
            {
                if (!IsCityVisibleTo(defenderCity, attackerCiv)) continue;
                var defenderHexes = defenderCity.Position.GetHexes();
                int minDist = attackerHexes
                    .SelectMany(ah => defenderHexes.Select(dh => ah.DistanceTo(dh)))
                    .Min();
                if (minDist <= CityAttackRange)
                    return defenderCity;
            }
        }
        return null;
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

        if (targetCity.Buildings.Count > 0)
        {
            int idx = _random.Next(targetCity.Buildings.Count);
            targetCity.Buildings.RemoveAt(idx);
            CityBuildingDestroyed?.Invoke(this, new CityBuildingDestroyedEventArgs(targetCity.Position));
            return false;
        }

        return true;
    }

    // ── Pathfinding A* ──────────────────────────────────────────────────────

    private List<HexCoord> FindPath(HexCoord from, HexCoord to)
    {
        if (from.Equals(to)) return new List<HexCoord> { from };

        var open = new PriorityQueue<HexCoord, int>();
        var cameFrom = new Dictionary<HexCoord, HexCoord?>();
        var gScore = new Dictionary<HexCoord, int>();
        var closed = new HashSet<HexCoord>();

        open.Enqueue(from, 0);
        gScore[from] = 0;
        cameFrom[from] = null;

        const int maxIterations = 500;
        int iterations = 0;

        while (open.Count > 0 && iterations++ < maxIterations)
        {
            var current = open.Dequeue();
            if (closed.Contains(current)) continue;
            closed.Add(current);

            if (current.Equals(to))
            {
                var path = new List<HexCoord>();
                HexCoord? node = to;
                while (node != null)
                {
                    path.Add(node);
                    cameFrom.TryGetValue(node, out node);
                }
                path.Reverse();
                return path;
            }

            foreach (var neighbor in current.Neighbors())
            {
                if (closed.Contains(neighbor)) continue;
                if (!_state!.Map.HasTile(neighbor)) continue;
                if (_state.Map.GetTile(neighbor)!.TerrainType == TerrainType.Water) continue;

                int tentativeG = gScore[current] + 1;
                if (!gScore.TryGetValue(neighbor, out int existingG) || tentativeG < existingG)
                {
                    gScore[neighbor] = tentativeG;
                    cameFrom[neighbor] = current;
                    open.Enqueue(neighbor, tentativeG + neighbor.DistanceTo(to));
                }
            }
        }

        return new List<HexCoord> { from, to };
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
