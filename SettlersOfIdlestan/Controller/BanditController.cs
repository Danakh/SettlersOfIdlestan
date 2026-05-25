using System;
using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Model.Bandits;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestan.Controller;

public class BanditController
{
    private IslandState? _state;
    private GameClock? _clock;
    private GamePRNG _prng = new();

    /// <summary>Intervalle de déplacement des bandits (3 000 ticks = 30 s à vitesse normale).</summary>
    public const long MovementIntervalTicks = 3_000L;

    /// <summary>Cooldown de récolte après le départ d'un bandit (1 000 ticks = 10 s).</summary>
    public const long DepartureCooldownTicks = 1_000L;

    internal void Initialize(IslandState? state, GameClock? clock, GamePRNG? prng = null)
    {
        if (_clock != null)
            _clock.Advanced -= OnClockAdvanced;

        _state = state;
        _clock = clock;
        if (prng != null) _prng = prng;

        if (_clock != null)
            _clock.Advanced += OnClockAdvanced;
    }

    private void OnClockAdvanced(object? sender, GameClockAdvancedEventArgs e)
    {
        try { UpdateBandits(e.CurrentTick); }
        catch { }
    }

    private void UpdateBandits(long currentTick)
    {
        if (_state == null) return;

        foreach (var bandit in _state.Bandits)
        {
            if (currentTick - bandit.LastMovedTick >= MovementIntervalTicks)
                MoveBandit(bandit, currentTick);
            else
                RaidNearbyCity(bandit, currentTick);
        }
    }

    private void RaidNearbyCity(Bandit bandit, long currentTick)
    {
        if (_state == null) return;
        if (currentTick - bandit.LastRaidTick < Bandit.RaidIntervalTicks) return;

        foreach (var civ in _state.Civilizations)
        {
            foreach (var city in civ.Cities)
            {
                if (!city.Position.GetHexes().Contains(bandit.Position)) continue;

                bandit.LastRaidTick = currentTick;

                var stealable = Enum.GetValues<Resource>()
                    .Where(r => civ.GetResourceQuantity(r) > 0)
                    .ToList();

                if (stealable.Count == 0) return;

                var resource = stealable[_prng.Next(stealable.Count)];
                civ.RemoveResource(resource, 1);
                bandit.LastRaidTargetVertex = city.Position;
                bandit.LastStolenResource = resource.ToString();
                return;
            }
        }

        bandit.LastRaidTick = currentTick;
    }

    private void MoveBandit(Bandit bandit, long currentTick)
    {
        if (_state == null) return;

        // Voisins valides : sur la carte, pas d'eau
        var neighbors = bandit.Position.Neighbors()
            .Where(n => _state.Map.HasTile(n) && _state.Map.GetTile(n)!.TerrainType != TerrainType.Water)
            .ToList();

        if (neighbors.Count == 0)
        {
            bandit.LastMovedTick = currentTick;
            return;
        }

        // Hexs protégés par les Barracks (3 hexs de chaque ville avec Barracks actives)
        var protectedHexes = GetBarracksProtectedHexes();

        // Destinations valides : non protégées
        var validDestinations = neighbors.Where(n => !protectedHexes.Contains(n)).ToList();

        if (validDestinations.Count == 0)
        {
            bandit.LastMovedTick = currentTick;
            return;
        }

        // Hexs des villes (les 3 hexs du vertex de chaque ville)
        var cityHexes = new HashSet<HexCoord>();
        foreach (var city in _state.GetAllCities())
            foreach (var hex in city.Position.GetHexes())
                cityHexes.Add(hex);

        // Destinations voisines d'une ville
        var cityAdjacentDestinations = validDestinations.Where(n => cityHexes.Contains(n)).ToList();

        HexCoord destination;
        if (cityAdjacentDestinations.Count > 0)
            destination = cityAdjacentDestinations[_prng.Next(cityAdjacentDestinations.Count)];
        else
            destination = validDestinations[_prng.Next(validDestinations.Count)];

        var oldPosition = bandit.Position;
        bandit.Position = destination;
        bandit.LastMovedTick = currentTick;
        bandit.LastRaidTick = currentTick; // no raid when moving

        // Cooldown sur l'ancienne position si le bandit a bougé
        if (!oldPosition.Equals(destination))
            _state.BanditCooldownUntil[oldPosition] = currentTick + DepartureCooldownTicks;
    }

    private HashSet<HexCoord> GetBarracksProtectedHexes()
    {
        if (_state == null) return new HashSet<HexCoord>();

        var protectedHexes = new HashSet<HexCoord>();
        foreach (var civ in _state.Civilizations)
            foreach (var city in civ.Cities)
                if (city.Buildings.OfType<Barracks>().Any(b => b.Level > 0))
                    foreach (var hex in city.Position.GetHexes())
                        protectedHexes.Add(hex);

        return protectedHexes;
    }

    /// <summary>
    /// Retourne true si un bandit est présent sur ce hex, ou si le cooldown de départ est actif.
    /// </summary>
    public bool IsHarvestBlocked(HexCoord hex, long currentTick)
    {
        if (_state == null) return false;

        if (_state.Bandits.Any(b => b.Position.Equals(hex)))
            return true;

        if (_state.BanditCooldownUntil.TryGetValue(hex, out var until))
            return currentTick < until;

        return false;
    }
}
