using System;
using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandFeatures;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Prestige;

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
/// 3. <see cref="ProcessMonumentCorruptionDecay"/> — ni la Faille des Abysses ni la Spire de Corruption
///    ne protègent leur hex des deux mécaniques ci-dessus (Temple et débordement peuvent y agir
///    normalement) ; ce process leur ajoute simplement une réduction garantie (contrairement au ciblage
///    aléatoire du Temple) d'un point de Corruption par intervalle sur leur propre hex (Faille), ou sur
///    tous les hexes dans un rayon de <see cref="IslandFeatures.CorruptionSpire.Radius"/> autour d'elle
///    (Spire, rayon améliorable indéfiniment par investissement — voir CorruptionSpireController).
/// </summary>
public class CorruptionController
{
    /// <summary>10 secondes (1 tick = 0.01 s) — rythme commun à la production des Temples, au débordement et à la décroissance sous les monuments.</summary>
    public const long ProductionIntervalTicks = 1000L;

    private const int TempleMinDominionLevel = 2;
    private const int TempleMaxDominionLevel = 4;
    private const int TempleDominionCapPerLevel = 2;

    private const int SpreadChancePercentPerLevel = 10;
    private const int SpreadSameStatusLevelGap = 2;

    private WorldState? _state;
    private GameClock? _clock;
    private GamePRNG? _prng;
    private PrestigeState? _prestigeState;

    private long _lastSpreadTick;
    private long _lastMonumentDecayTick;

    public void Initialize(WorldState state, GameClock? clock, GamePRNG prng, PrestigeState? prestigeState = null)
    {
        if (_clock != null)
            _clock.Advanced -= OnClockAdvanced;

        _state = state;
        _clock = clock;
        _prng = prng;
        _prestigeState = prestigeState;
        _lastSpreadTick = 0;
        _lastMonumentDecayTick = 0;

        if (_clock != null)
            _clock.Advanced += OnClockAdvanced;
    }

    private void OnClockAdvanced(object? sender, GameClockAdvancedEventArgs e)
    {
        try { ProcessTempleProduction(e.CurrentTick); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[CorruptionController] {nameof(ProcessTempleProduction)}: {ex}"); }

        try { ProcessSpread(e.CurrentTick); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[CorruptionController] {nameof(ProcessSpread)}: {ex}"); }

        try { ProcessMonumentCorruptionDecay(e.CurrentTick); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[CorruptionController] {nameof(ProcessMonumentCorruptionDecay)}: {ex}"); }
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

                var hexes = city.Position.GetHexes().Where(IsValidHex).ToList();
                if (hexes.Count == 0) continue;

                var hex = hexes[_prng.Next(hexes.Count)];
                ApplyTempleActionOnHex(civ, temple, hex);
            }
        }
    }

    /// <summary>
    /// Action de Temple sur un hex : dissipe un point de Corruption si elle est présente, sinon
    /// pose ou augmente le Dominion d'un point, plafonné par le niveau du Temple (voir
    /// TempleDominionCapPerLevel + TEMPLE_DOMINION_CAP).
    /// </summary>
    private void ApplyTempleActionOnHex(Civilization civ, Temple temple, HexCoord hex)
    {
        var corruption = _state!.GetFeaturesAt(hex).OfType<Corruption>().FirstOrDefault();
        if (corruption != null)
        {
            ReduceLevel(corruption);
            return;
        }

        var dominion = _state.GetFeaturesAt(hex).OfType<Dominion>().FirstOrDefault();
        // Dogme de l'Emprise (TEMPLE_DOMINION_CAP) relève le plafond par niveau de Temple.
        int capPerLevel = TempleDominionCapPerLevel
            + civ.ModifierAggregator.ApplyModifiers(Modifier.ECategory.TEMPLE_DOMINION_CAP, "", 0);
        int cap = capPerLevel * temple.Level;
        if (dominion == null)
            _state.AddFeature(new Dominion(hex, level: 1));
        else if (dominion.Level < cap)
            dominion.Level++;
    }

    /// <summary>
    /// Effet de la Ziggourat (TEMPLE_INSTANT_DOMINION) : à la construction ou l'amélioration d'un
    /// Temple, applique instantanément l'action de Temple sur les 3 hexs de la ville, à 100 %
    /// (contre 1 hex aléatoire toutes les 10 s en production normale). L'appelant
    /// (MainGameController) vérifie le flag et consomme un déclenchement de
    /// City.ZigguratTriggersUsed (max <see cref="Ziggurat.MaxTriggersPerCity"/> par ville).
    /// </summary>
    public void ApplyZigguratInstantProduction(City city)
    {
        if (_state == null) return;

        var civ = _state.Civilizations.FirstOrDefault(c => c.Index == city.CivilizationIndex);
        var temple = city.Buildings.OfType<Temple>().FirstOrDefault(t => t.Level >= 1);
        if (civ == null || temple == null) return;

        foreach (var hex in city.Position.GetHexes().Where(IsValidHex))
            ApplyTempleActionOnHex(civ, temple, hex);
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

            bool sourceIsDominion = source is Dominion;

            // Évangélisation (DOMINION_SPREAD_CHANCE) : le Dominion déborde plus souvent que la
            // Corruption (points de % supplémentaires par niveau).
            int chancePerLevel = SpreadChancePercentPerLevel
                + (sourceIsDominion ? GetDominionSpreadChanceBonus() : 0);

            int level = GetLevel(source);
            if (_prng.Next(100) >= level * chancePerLevel) continue;

            var candidates = source.Position.Neighbors().Where(IsValidHex).ToList();
            if (candidates.Count == 0) continue;

            var neighborHex = candidates[_prng.Next(candidates.Count)];

            var opposite = sourceIsDominion
                ? (IslandFeature?)_state.GetFeaturesAt(neighborHex).OfType<Corruption>().FirstOrDefault()
                : _state.GetFeaturesAt(neighborHex).OfType<Dominion>().FirstOrDefault();

            if (opposite != null)
            {
                // Terre Consacrée : le Dominion des hexs d'une ville avec Temple a une chance de ne
                // pas perdre de niveau dans l'annulation mutuelle — la Corruption perd toujours le sien.
                var dominionSide = sourceIsDominion ? source : opposite;
                var corruptionSide = sourceIsDominion ? opposite : source;
                if (!IsDominionSpared(dominionSide.Position))
                    ReduceLevel(dominionSide);
                ReduceLevel(corruptionSide);
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

    /// <summary>Points de % de chance de débordement supplémentaires par niveau pour le Dominion (Évangélisation).</summary>
    private int GetDominionSpreadChanceBonus()
        => _state!.PlayerCivilization.ModifierAggregator.ApplyModifiers(Modifier.ECategory.DOMINION_SPREAD_CHANCE, "", 0);

    /// <summary>
    /// Vrai si le Dominion de cet hex échappe (tirage aléatoire) à la perte de niveau d'une annulation
    /// mutuelle avec la Corruption : recherche Terre Consacrée (TEMPLE_DOMINION_PROTECTION_CHANCE) et
    /// hex touchant une ville du joueur possédant un Temple.
    /// </summary>
    private bool IsDominionSpared(HexCoord hex)
    {
        double chance = _state!.PlayerCivilization.ModifierAggregator
            .ApplyModifiers(Modifier.ECategory.TEMPLE_DOMINION_PROTECTION_CHANCE, "", 0.0);
        if (chance <= 0) return false;

        bool nearTemple = _state.PlayerCivilization.Cities.Any(c =>
            c.Buildings.OfType<Temple>().Any() && c.Position.GetHexes().Contains(hex));
        if (!nearTemple) return false;

        return _prng!.Next(100) < (int)Math.Round(chance * 100);
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
            case Corruption c:
                c.Level++;
                if (c.Level > c.PeakLevel) c.PeakLevel = c.Level;
                break;
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
        {
            // Zone de Corruption entièrement nettoyée (par Temple, débordement ou décroissance de
            // monument) : enregistre son pic comme record global si c'est le meilleur jamais atteint.
            if (feature is Corruption cleared && _prestigeState != null && cleared.PeakLevel > _prestigeState.MaxCorruptionLevelCleared)
                _prestigeState.MaxCorruptionLevelCleared = cleared.PeakLevel;

            _state!.RemoveFeature(feature);
        }
    }

    /// <summary>
    /// Réduit la Corruption d'un point à chaque intervalle, de façon garantie (contrairement à la
    /// production de Temple, qui cible un hex aléatoire parmi 3) : sur l'hex d'une Faille des Abysses,
    /// et sur tous les hexes dans un rayon de <see cref="CorruptionSpire.Radius"/> autour de chaque
    /// Spire de Corruption (rayon 1 de base, incluant donc l'hex de la Spire elle-même et ses voisins
    /// immédiats). Aucun de ces hexes n'est protégé du reste : Temple et débordement peuvent toujours
    /// y agir normalement (voir <see cref="ApplyTempleActionOnHex"/>, <see cref="ProcessSpread"/>).
    /// Utilise <see cref="ReduceLevel"/> comme les autres mécaniques : la suppression à 0 enregistre le
    /// pic atteint dans <see cref="PrestigeState.MaxCorruptionLevelCleared"/>.
    /// </summary>
    private void ProcessMonumentCorruptionDecay(long currentTick)
    {
        if (_state == null) return;
        if (currentTick - _lastMonumentDecayTick < ProductionIntervalTicks) return;
        _lastMonumentDecayTick = currentTick;

        var decayHexes = new HashSet<HexCoord>();
        foreach (var gate in _state.Features.OfType<AbyssGate>())
            decayHexes.Add(gate.Position);
        foreach (var spire in _state.Features.OfType<CorruptionSpire>())
            foreach (var hex in GetHexesInRadius(spire.Position, spire.Radius))
                decayHexes.Add(hex);

        foreach (var hex in decayHexes)
        {
            var corruption = _state.GetFeaturesAt(hex).OfType<Corruption>().FirstOrDefault();
            if (corruption != null)
                ReduceLevel(corruption);
        }
    }

    /// <summary>Le centre puis, anneau par anneau, tous les hexes à distance ≤ radius de center (BFS via les 6 directions).</summary>
    private static IEnumerable<HexCoord> GetHexesInRadius(HexCoord center, int radius)
    {
        var visited = new HashSet<HexCoord> { center };
        yield return center;

        var frontier = new List<HexCoord> { center };
        for (int i = 0; i < radius; i++)
        {
            var next = new List<HexCoord>();
            foreach (var hex in frontier)
            {
                foreach (HexDirection dir in Enum.GetValues<HexDirection>())
                {
                    var neighbor = hex.Neighbor(dir);
                    if (visited.Add(neighbor))
                        next.Add(neighbor);
                }
            }
            foreach (var hex in next)
                yield return hex;
            frontier = next;
        }
    }

    /// <summary>
    /// Tout hex existant de la carte, eau incluse : la Corruption et le Dominion peuvent s'étendre
    /// sur l'eau (le Dominion en mer est le prérequis de la terraformation par Marche de Dieu —
    /// voir AscensionController.GetWalkOfGodTargetHexes). Seule la génération initiale sème encore
    /// la Corruption sur la terre uniquement (voir IslandMapGenerator.PlaceSurfaceCorruption).
    /// </summary>
    private bool IsValidHex(HexCoord hex)
        => _state!.GetMapFor(hex)?.GetTile(hex) != null;
}
