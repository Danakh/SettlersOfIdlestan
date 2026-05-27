using System;
using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Model.Bandits;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandFeatures;
using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestan.Controller.Military;

public class BanditController
{
    private IslandState? _state;
    private GameClock? _clock;
    private GamePRNG _prng = new();

    // Caches locaux invalidés par FeatureAdded / FeatureRemoved
    private List<Bandit> _bandits = new();
    private List<BanditHideout> _hideouts = new();

    /// <summary>Intervalle de déplacement des bandits (3 000 ticks = 30 s à vitesse normale).</summary>
    public const long MovementIntervalTicks = 3_000L;

    /// <summary>Cooldown de récolte après le départ d'un bandit (1 000 ticks = 10 s).</summary>
    public const long DepartureCooldownTicks = 1_000L;

    internal void Initialize(IslandState? state, GameClock? clock, GamePRNG? prng = null)
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

        RebuildCaches();

        if (_state != null)
        {
            _state.FeatureAdded += OnFeatureAdded;
            _state.FeatureRemoved += OnFeatureRemoved;
        }

        if (_clock != null)
            _clock.Advanced += OnClockAdvanced;
    }

    private void RebuildCaches()
    {
        _bandits = _state?.Features.OfType<Bandit>().ToList() ?? new();
        _hideouts = _state?.Features.OfType<BanditHideout>().ToList() ?? new();
    }

    private void OnFeatureAdded(object? sender, IslandFeature feature)
    {
        if (feature is Bandit b) _bandits.Add(b);
        else if (feature is BanditHideout h) _hideouts.Add(h);
    }

    private void OnFeatureRemoved(object? sender, IslandFeature feature)
    {
        if (feature is Bandit b) _bandits.Remove(b);
        else if (feature is BanditHideout h) _hideouts.Remove(h);
    }

    private void OnClockAdvanced(object? sender, GameClockAdvancedEventArgs e)
    {
        try { Update(e.CurrentTick); }
        catch { }
    }

    private void Update(long currentTick)
    {
        UpdateBandits(currentTick);
        UpdateBanditHideouts(currentTick);
    }

    // ── Repaires de bandits ──────────────────────────────────────────────────

    private void UpdateBanditHideouts(long currentTick)
    {
        if (_state == null) return;

        foreach (var hideout in _hideouts)
        {
            if (!hideout.Found) continue;
            if (currentTick - hideout.LastSpawnTick < BanditHideout.SpawnIntervalTicks) continue;

            hideout.LastSpawnTick = currentTick;

            if (_bandits.Count < BanditHideout.MaxBanditsOnIsland)
                _state.AddFeature(new Bandit(hideout.Position, currentTick));
        }
    }

    // ── Mise à jour des bandits ──────────────────────────────────────────────

    private void UpdateBandits(long currentTick)
    {
        if (_state == null) return;

        foreach (var bandit in _bandits)
        {
            if (!bandit.Found)
            {
                bandit.LastMovedTick = currentTick;
                bandit.LastRaidTick = currentTick;
                continue;
            }

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

                // La palissade bloque le vol des ressources
                if (city.Buildings.OfType<Palisade>().Any(b => b.Level > 0))
                {
                    bandit.LastRaidTargetVertex = null;
                    bandit.LastStolenResource = null;
                    return;
                }

                var stealable = Enum.GetValues<Resource>()
                    .Where(r => civ.GetResourceQuantity(r) > 0)
                    .ToList();

                if (stealable.Count == 0)
                {
                    bandit.LastStolenResource = null;
                    return;
                }

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

        var cityHexes = new HashSet<HexCoord>();
        foreach (var city in _state.GetAllCities())
            foreach (var hex in city.Position.GetHexes())
                cityHexes.Add(hex);

        // Tier 1 : pas de bandit + pas de cooldown
        var noBanditNoCooldown = validDestinations
            .Where(n => !_bandits.Any(b => b.Position.Equals(n)) &&
                        (!_state.BanditCooldownUntil.TryGetValue(n, out var until) || currentTick >= until))
            .ToList();

        // Tier 2 : pas de bandit (cooldown acceptable)
        var noBandit = validDestinations
            .Where(n => !_bandits.Any(b => b.Position.Equals(n)))
            .ToList();

        // Sélection du meilleur tier disponible, avec préférence pour les hexs de ville
        var candidates = noBanditNoCooldown.Count > 0 ? noBanditNoCooldown
                       : noBandit.Count > 0 ? noBandit
                       : validDestinations;

        var cityAdjacent = candidates.Where(n => cityHexes.Contains(n)).ToList();
        HexCoord destination = cityAdjacent.Count > 0
            ? cityAdjacent[_prng.Next(cityAdjacent.Count)]
            : candidates[_prng.Next(candidates.Count)];

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
    /// Retourne true si un bandit ou un repaire est présent sur ce hex, ou si le cooldown de départ est actif.
    /// </summary>
    public bool IsHarvestBlocked(HexCoord hex, long currentTick)
    {
        if (_state == null) return false;

        if (_bandits.Any(b => b.Position.Equals(hex)))
            return true;

        if (_hideouts.Any(h => h.Position.Equals(hex)))
            return true;

        if (_state.BanditCooldownUntil.TryGetValue(hex, out var until))
            return currentTick < until;

        return false;
    }
}
