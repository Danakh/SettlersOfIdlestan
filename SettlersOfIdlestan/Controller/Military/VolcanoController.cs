using System;
using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.IslandFeatures;
using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestan.Controller.Military;

/// <summary>
/// Gère les éruptions périodiques des VolcanoFeature :
/// anneau 1 (vertex sur le hex volcan) 50 %, anneau 2 (vertex voisin) 25 %, 10 dégâts.
/// </summary>
public class VolcanoController
{
    public const long EruptionIntervalTicks = 10_000L;
    public const int EruptionDamage = 10;
    public const int Ring1ChancePercent = 50;
    public const int Ring2ChancePercent = 25;

    private WorldState? _state;
    private GameClock? _clock;
    private GamePRNG? _prng;
    private CityBuilderController? _cityBuilderController;
    private List<VolcanoFeature> _volcanoes = new();

    internal void Initialize(WorldState? state, GameClock? clock, GamePRNG? prng, CityBuilderController? cityBuilderController)
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
        _prng = prng;
        _cityBuilderController = cityBuilderController;

        _volcanoes = _state?.Features.OfType<VolcanoFeature>().ToList() ?? new();

        if (_state != null)
        {
            _state.FeatureAdded += OnFeatureAdded;
            _state.FeatureRemoved += OnFeatureRemoved;
        }

        if (_clock != null)
            _clock.Advanced += OnClockAdvanced;
    }

    private void OnFeatureAdded(object? sender, IslandFeature feature)
    {
        if (feature is VolcanoFeature v) _volcanoes.Add(v);
    }

    private void OnFeatureRemoved(object? sender, IslandFeature feature)
    {
        if (feature is VolcanoFeature v) _volcanoes.Remove(v);
    }

    private void OnClockAdvanced(object? sender, GameClockAdvancedEventArgs e)
    {
        try { Update(e.CurrentTick); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[VolcanoController] {nameof(Update)}: {ex}"); }
    }

    private void Update(long currentTick)
    {
        if (_state == null) return;

        foreach (var volcano in _volcanoes)
        {
            if (!volcano.Found) continue;
            if (currentTick - volcano.LastEruptionTick < EruptionIntervalTicks) continue;

            volcano.LastEruptionTick = currentTick;
            TriggerEruption(volcano, currentTick);
        }
    }

    private void TriggerEruption(VolcanoFeature volcano, long currentTick)
    {
        if (_state == null || _prng == null) return;

        var volcanoHex = volcano.Position;
        var neighborHexes = volcanoHex.Neighbors().ToHashSet();

        foreach (var civ in _state.Civilizations.ToList())
        {
            foreach (var city in civ.Cities.ToList())
            {
                var cityHexes = city.Position.GetHexes();
                bool touchesVolcano = cityHexes.Any(h => h.Equals(volcanoHex));

                if (touchesVolcano)
                {
                    if (_prng.Next(100) < Ring1ChancePercent)
                        ApplyEruptionDamage(civ, city, EruptionDamage);
                }
                else if (cityHexes.Any(h => neighborHexes.Contains(h)))
                {
                    if (_prng.Next(100) < Ring2ChancePercent)
                        ApplyEruptionDamage(civ, city, EruptionDamage);
                }
            }
        }
    }

    private void ApplyEruptionDamage(Civilization civ, City city, int damage)
    {
        if (damage <= 0) return;

        // 1. Soldats — Armures d'Acier peuvent sauver des soldats
        int soldierDmg = Math.Min(damage, city.Soldiers);
        if (soldierDmg > 0)
        {
            int saved = SteelArmorEngine.TrySaveSoldiers(civ, city, soldierDmg, _prng!);
            city.Soldiers -= soldierDmg - saved;
            damage -= soldierDmg;
        }

        // 2. Défense de la ville
        if (damage > 0)
        {
            int defenseDmg = Math.Min(damage, city.CurrentDefense);
            if (defenseDmg > 0) { city.CurrentDefense -= defenseDmg; damage -= defenseDmg; }
        }

        // 3. Niveaux de Townhall (1 dégât = 1 niveau)
        if (damage > 0)
        {
            var townHall = city.Buildings.OfType<TownHall>().FirstOrDefault();
            if (townHall != null)
            {
                int thDmg = Math.Min(damage, townHall.Level);
                townHall.Level -= thDmg;
                if (townHall.Level <= 0)
                {
                    city.Buildings.Remove(townHall);
                    city.InvalidateLevelCache();
                }
                BuildingController.RecalculateStorageCapacity(civ);
                civ.TrimResourcesToMax();
            }
        }

        // 4. Destruction si plus de Townhall
        if (!city.Buildings.OfType<TownHall>().Any())
            _cityBuilderController?.DestroyCity(city, CityDestructionCause.Monster);
    }
}
