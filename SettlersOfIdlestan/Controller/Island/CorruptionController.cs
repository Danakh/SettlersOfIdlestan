using System;
using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandFeatures;
using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestan.Controller.Island;

/// <summary>
/// Gère la lutte Corruption/Dominion. Deux mécaniques indépendantes, toutes deux au rythme de
/// <see cref="ProductionIntervalTicks"/> (10 s) :
/// 1. <see cref="ProcessTempleProduction"/> — chaque Temple de niveau 2-4 (atteignable uniquement une
///    fois le pouvoir divin Foi débloqué, voir AscensionController.GetModifiers — BUILDING_MAX_LEVEL
///    "Temple" +3) cible un hex aléatoire parmi les 3 hexes touchant sa ville : réduit la Corruption
///    d'un point si elle y est présente, sinon pose ou augmente le Dominion d'un point (plafonné à
///    <see cref="TempleDominionCapPerLevel"/> × niveau du Temple).
/// 2. <see cref="ProcessSpread"/> — chaque hex de Corruption ou de Dominion (toutes couches confondues)
///    a niveau×10% de chance de déborder sur un voisin aléatoire : annulation mutuelle (-1/-1) si ce
///    voisin porte le statut opposé, propagation (-1 source / +1 voisin) si le voisin partage le même
///    statut (un voisin vide compte comme statut identique de niveau 0) avec un écart de niveau &gt; 2.
///    Un voisin vide peut donc se voir semer une nouvelle poche à niveau 1 si la source est assez forte
///    (niveau &gt; 2), ce qui permet à terme au Dominion d'un Temple de gagner du terrain à distance,
///    au-delà des hexes directement produits, et à plusieurs Temples de voir leurs poches se rejoindre.
/// </summary>
public class CorruptionController
{
    /// <summary>10 secondes (1 tick = 0.01 s) — rythme commun à la production des Temples et au débordement.</summary>
    public const long ProductionIntervalTicks = 1000L;

    private const int TempleMinDominionLevel = 2;
    private const int TempleMaxDominionLevel = 4;
    private const int TempleDominionCapPerLevel = 2;

    private const int SpreadChancePercentPerLevel = 10;
    private const int SpreadSameStatusLevelGap = 2;

    private WorldState? _state;
    private GameClock? _clock;
    private GamePRNG? _prng;

    private long _lastSpreadTick;

    public void Initialize(WorldState state, GameClock? clock, GamePRNG prng)
    {
        if (_clock != null)
            _clock.Advanced -= OnClockAdvanced;

        _state = state;
        _clock = clock;
        _prng = prng;
        _lastSpreadTick = 0;

        if (_clock != null)
            _clock.Advanced += OnClockAdvanced;
    }

    private void OnClockAdvanced(object? sender, GameClockAdvancedEventArgs e)
    {
        try { ProcessTempleProduction(e.CurrentTick); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[CorruptionController] {nameof(ProcessTempleProduction)}: {ex}"); }

        try { ProcessSpread(e.CurrentTick); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[CorruptionController] {nameof(ProcessSpread)}: {ex}"); }
    }

    /// <summary>Cooldown par Temple (comme AlchimistHut.LastCrystalProductionTick) — chaque Temple agit toutes les 10 s depuis sa dernière action.</summary>
    private void ProcessTempleProduction(long currentTick)
    {
        if (_state == null || _prng == null) return;

        foreach (var civ in _state.Civilizations)
        {
            foreach (var city in civ.Cities)
            {
                var temple = city.Buildings.OfType<Temple>()
                    .FirstOrDefault(t => t.Level >= TempleMinDominionLevel && t.Level <= TempleMaxDominionLevel);
                if (temple == null) continue;
                if (currentTick - temple.LastDominionProductionTick < ProductionIntervalTicks) continue;
                temple.LastDominionProductionTick = currentTick;

                var hexes = city.Position.GetHexes().Where(IsValidLandHex).ToList();
                if (hexes.Count == 0) continue;

                var hex = hexes[_prng.Next(hexes.Count)];
                var corruption = _state.GetFeaturesAt(hex).OfType<Corruption>().FirstOrDefault();
                if (corruption != null)
                {
                    ReduceLevel(corruption);
                    continue;
                }

                var dominion = _state.GetFeaturesAt(hex).OfType<Dominion>().FirstOrDefault();
                int cap = TempleDominionCapPerLevel * temple.Level;
                if (dominion == null)
                    _state.AddFeature(new Dominion(hex, level: 1));
                else if (dominion.Level < cap)
                    dominion.Level++;
            }
        }
    }

    private void ProcessSpread(long currentTick)
    {
        if (_state == null || _prng == null) return;
        if (currentTick - _lastSpreadTick < ProductionIntervalTicks) return;
        _lastSpreadTick = currentTick;

        // Snapshot : ReduceLevel peut retirer des features de _state.Features pendant l'itération.
        var sources = _state.Features.Where(f => f is Corruption or Dominion).ToList();

        foreach (var source in sources)
        {
            if (!_state.Features.Contains(source)) continue; // déjà supprimée plus tôt dans cette passe

            int level = GetLevel(source);
            if (_prng.Next(100) >= level * SpreadChancePercentPerLevel) continue;

            var candidates = source.Position.Neighbors().Where(IsValidLandHex).ToList();
            if (candidates.Count == 0) continue;

            var neighborHex = candidates[_prng.Next(candidates.Count)];
            bool sourceIsDominion = source is Dominion;

            var opposite = sourceIsDominion
                ? (IslandFeature?)_state.GetFeaturesAt(neighborHex).OfType<Corruption>().FirstOrDefault()
                : _state.GetFeaturesAt(neighborHex).OfType<Dominion>().FirstOrDefault();

            if (opposite != null)
            {
                ReduceLevel(source);
                ReduceLevel(opposite);
                continue;
            }

            var same = sourceIsDominion
                ? (IslandFeature?)_state.GetFeaturesAt(neighborHex).OfType<Dominion>().FirstOrDefault()
                : _state.GetFeaturesAt(neighborHex).OfType<Corruption>().FirstOrDefault();

            // Un voisin vide compte comme un "même statut" de niveau 0 : une source suffisamment
            // forte (écart > SpreadSameStatusLevelGap) sème une nouvelle poche à niveau 1, ce qui
            // permet au Dominion/à la Corruption de progresser au-delà des poches déjà existantes.
            int sameLevel = same != null ? GetLevel(same) : 0;
            if (Math.Abs(sameLevel - level) > SpreadSameStatusLevelGap)
            {
                ReduceLevel(source);
                if (same != null)
                    IncreaseLevel(same);
                else
                    SeedFeature(sourceIsDominion, neighborHex);
            }
        }
    }

    private void SeedFeature(bool isDominion, HexCoord hex)
    {
        if (isDominion)
            _state!.AddFeature(new Dominion(hex, level: 1));
        else
            _state!.AddFeature(new Corruption(hex, level: 1));
    }

    private static int GetLevel(IslandFeature feature) => feature switch
    {
        Corruption c => c.Level,
        Dominion d => d.Level,
        _ => 0,
    };

    private static void IncreaseLevel(IslandFeature feature)
    {
        switch (feature)
        {
            case Corruption c: c.Level++; break;
            case Dominion d: d.Level++; break;
        }
    }

    private void ReduceLevel(IslandFeature feature)
    {
        switch (feature)
        {
            case Corruption c: c.Level--; break;
            case Dominion d: d.Level--; break;
        }

        if (GetLevel(feature) <= 0)
            _state!.RemoveFeature(feature);
    }

    /// <summary>Hors eau, cohérent avec le placement de Corruption existant (jamais sur l'eau — voir IslandMapGenerator.PlaceSurfaceCorruption).</summary>
    private bool IsValidLandHex(HexCoord hex)
        => _state!.GetMapFor(hex)?.GetTile(hex) is { TerrainType: not TerrainType.Water };
}
