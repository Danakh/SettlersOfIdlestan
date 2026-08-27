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
using SettlersOfIdlestan.Model.Monsters;
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
///    voisin porte le statut opposé, propagation (+1 voisin, source inchangée) si le voisin partage le
///    même statut (un voisin vide compte comme statut identique de niveau 0) avec un écart de niveau
///    &gt; 2. Un voisin vide peut donc se voir semer une nouvelle poche à niveau 1 si la source est assez
///    forte (niveau &gt; 2), ce qui permet à terme au Dominion d'un Temple de gagner du terrain à distance,
///    au-delà des hexes directement produits, et à plusieurs Temples de voir leurs poches se rejoindre.
/// 3. <see cref="ProcessMonumentCorruptionDecay"/> — ni la Faille des Abysses ni la Spire de Corruption
///    ne protègent leur hex des deux mécaniques ci-dessus (Temple et débordement peuvent y agir
///    normalement) ; ce process leur ajoute simplement une réduction garantie (contrairement au ciblage
///    aléatoire du Temple) d'un point de Corruption par intervalle sur leur propre hex (Faille), ou sur
///    tous les hexes dans un rayon de <see cref="IslandFeatures.CorruptionSpire.Radius"/> autour d'elle
///    (Spire, rayon améliorable indéfiniment par investissement — voir CorruptionSpireController). La
///    Spire n'agit qu'une fois <see cref="IslandFeatures.CorruptionSpire.Built"/> : pendant sa
///    construction, aucune décroissance n'est appliquée sur son hex.
/// 4. <see cref="ProcessDivineBonesCorruptionGrowth"/> — miroir du process précédent : chaque Os Divin
///    non purifié ajoute un point de Corruption sur son propre hex (en la semant à niveau 1 si l'hex est
///    sain), tant que le niveau y reste sous <see cref="IslandFeatures.DivineBones.GetCorruptionCap"/>
///    (2× le niveau de corruption de l'île). Purifier les Os les retire de la carte (voir
///    DivineBonesController) et tarit donc la source ; la Corruption déjà semée, elle, reste à nettoyer.
/// 5. <see cref="ProcessMonsterCorruptionGrowth"/> — même chose pour les monstres enracinés dans la
///    Corruption (Tentacules et Dieu démon, voir <see cref="MonsterFeature.GeneratesCorruption"/>) :
///    chacun ajoute un point de Corruption sur son propre hex tant que le niveau y reste sous
///    <see cref="GetMonsterCorruptionCap"/> (2× le niveau de corruption courant de l'île). Le
///    Pandémonium se re-corrompt donc depuis son centre et sa couronne de Tentacules : seules leurs
///    morts tarissent les sources. À leur apparition, <see cref="SeedCorruptionAroundNewMonster"/>
///    (appelé par les générateurs) corrompt d'office leur hex et ses six voisins au niveau de l'île,
///    soit la moitié de ce plafond.
/// 6. <see cref="ProcessCorruptionSourceGrowth"/> — même mécanique que les Os Divins pour les
///    Sources de Corruption (voir <see cref="IslandFeatures.CorruptionSource"/>, semées par
///    AutoExtendController.TrySpawnUnderworldDenizen) : chacune ajoute un point de Corruption sur son
///    propre hex tant que le niveau y reste sous <see cref="IslandFeatures.CorruptionSource.GetCorruptionCap"/>
///    — le niveau de corruption de l'île figé à sa génération, jamais doublé (contrairement aux Os
///    Divins). C'est le seul hex sur lequel une Spire de Corruption peut être bâtie ; la construire
///    détruit la Source (voir CorruptionSpireController.ProcessInvestment).
/// Invariant : Corruption et Dominion ne coexistent jamais sur un même hex. Le Temple (1) l'assure déjà
/// dans un sens (il ne pose du Dominion que sur un hex sans Corruption). Les trois générateurs directs
/// (4, 5, 6) l'assurent dans l'autre sens via <see cref="GrowOrSeedCorruptionOnHex"/> : si un Dominion
/// occupe déjà leur hex, ils le combattent (-1) au lieu d'y semer de la Corruption. Nécessaire car (2)
/// peut semer un nouveau Dominion sur un hex dont la Corruption vient d'être réduite à zéro plus tôt
/// dans le même tick (par 1) — sans ce garde-fou, (4)/(5)/(6), qui passent en dernier, la ressèmeraient
/// par-dessus sans regarder ce qui s'y trouve déjà.
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
    private long _lastDivineBonesGrowthTick;
    private long _lastMonsterGrowthTick;
    private long _lastCorruptionSourceGrowthTick;

    public void Initialize(WorldState state, GameClock? clock, GamePRNG prng, PrestigeState? prestigeState = null)
    {
        if (_clock != null)
            _clock.Advanced -= OnClockAdvanced;

        _state = state;
        _clock = clock;
        _prng = prng;
        _prestigeState = prestigeState;

        // Initialisés au tick courant, pas à 0 : ces trackers ne sont pas persistés (recréés à
        // chaque Initialize, y compris au chargement d'une sauvegarde) et TickCooldown traite 0
        // littéralement (pas de garde de démarrage à froid ici, voir ses autres appelants). Sur une
        // partie déjà avancée, les laisser à 0 ferait calculer un nombre de cycles de rattrapage
        // proportionnel à tout le tick courant (potentiellement des millions), au lieu du léger
        // différé d'un cooldown attendu au tout début d'une partie neuve.
        long now = clock?.CurrentTick ?? 0;
        _lastSpreadTick = now;
        _lastMonumentDecayTick = now;
        _lastDivineBonesGrowthTick = now;
        _lastMonsterGrowthTick = now;
        _lastCorruptionSourceGrowthTick = now;

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

        try { ProcessDivineBonesCorruptionGrowth(e.CurrentTick); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[CorruptionController] {nameof(ProcessDivineBonesCorruptionGrowth)}: {ex}"); }

        try { ProcessMonsterCorruptionGrowth(e.CurrentTick); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[CorruptionController] {nameof(ProcessMonsterCorruptionGrowth)}: {ex}"); }

        try { ProcessCorruptionSourceGrowth(e.CurrentTick); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[CorruptionController] {nameof(ProcessCorruptionSourceGrowth)}: {ex}"); }
    }

    /// <summary>Cooldown par Temple (comme AlchimistHut.LastCrystalProductionTick) — chaque Temple agit toutes les 10 s depuis sa dernière action.</summary>
    private void ProcessTempleProduction(long currentTick)
    {
        if (_state == null || _prng == null) return;

        foreach (var civ in _state.Civilizations)
        {
            foreach (var city in civ.Cities)
            {
                var temple = city.FindBuilding<Temple>(BuildingType.Temple) is { } t0 && t0.Level >= TempleMinDominionLevel && t0.Level <= TempleMaxDominionLevel ? t0 : null;
                if (temple == null) continue;

                long lastTick = temple.LastDominionProductionTick;
                long cycles = TickCooldown.ConsumeElapsedCycles(currentTick, ref lastTick, ProductionIntervalTicks);
                temple.LastDominionProductionTick = lastTick;
                if (cycles <= 0) continue;

                // Même pénalité de profondeur que la propagation du Dominion (÷2/÷4/÷8) : le
                // cooldown reste identique, mais chaque tir a une chance sur GetDominionLayerDivisor
                // d'aboutir. Rejoué cycle par cycle (pas de multiplication) : chaque cycle est un
                // tirage indépendant, sur un hex tiré au hasard indépendamment lui aussi.
                int divisor = GetDominionLayerDivisor(city.Position.Z);
                for (long i = 0; i < cycles; i++)
                {
                    if (divisor > 1 && _prng.Next(divisor) != 0) continue;

                    var hexes = city.Position.GetHexes().Where(IsValidHex).ToList();
                    if (hexes.Count == 0) continue;

                    var hex = hexes[_prng.Next(hexes.Count)];
                    ApplyTempleActionOnHex(civ, temple, hex);
                }
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
        int cap = GetTempleDominionCap(civ, temple.Level);
        if (dominion == null)
            _state.AddFeature(new Dominion(hex, level: 1));
        else if (dominion.Level < cap)
            dominion.Level++;
    }

    /// <summary>Plafond de Dominion par hex qu'un Temple de ce niveau peut atteindre pour cette civilisation (Dogme de l'Emprise / TEMPLE_DOMINION_CAP relève le plafond par niveau de Temple). Utilisé par <see cref="ApplyTempleActionOnHex"/> et par le tooltip du panneau ville.</summary>
    public static int GetTempleDominionCap(Civilization civ, int templeLevel)
    {
        int capPerLevel = TempleDominionCapPerLevel
            + civ.ModifierAggregator.ApplyModifiers(Modifier.ECategory.TEMPLE_DOMINION_CAP, "", 0);
        return capPerLevel * templeLevel;
    }

    private void ProcessSpread(long currentTick)
    {
        if (_state == null || _prng == null) return;

        long lastTick = _lastSpreadTick;
        long cycles = TickCooldown.ConsumeElapsedCycles(currentTick, ref lastTick, ProductionIntervalTicks);
        _lastSpreadTick = lastTick;
        if (cycles <= 0) return;

        // Rejoué cycle par cycle (automate cellulaire) : l'état après le cycle N conditionne le
        // cycle N+1 (une poche semée par un cycle peut déborder au cycle suivant) — une simple
        // multiplication par `cycles` donnerait un résultat incohérent.
        for (long c = 0; c < cycles; c++)
        {
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

                // En profondeur, l'Évangélisation du Dominion est plus difficile : ÷2 Inframonde, ÷4
                // Abysses, ÷8 Pandémonium. La résolution du tirage est multipliée d'autant pour ne pas
                // perdre de précision par troncature entière sur de petits pourcentages.
                int divisor = sourceIsDominion ? GetDominionLayerDivisor(source.Position.Z) : 1;
                if (_prng.Next(100 * divisor) >= level * chancePerLevel) continue;

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
                // Comparaison directionnelle (pas Math.Abs) : seule la source la PLUS FORTE des deux fait
                // grandir l'autre. Un voisin plus faible ne doit jamais faire grandir un voisin déjà plus
                // fort que lui à son propre tour de débordement, sous peine de croissance sans plafond.
                int sameLevel = same != null ? GetLevel(same) : 0;
                if (level - sameLevel > SpreadSameStatusLevelGap)
                {
                    if (same != null)
                        IncreaseLevel(same);
                    else
                        SeedFeature(sourceIsDominion, neighborHex);
                }
            }
        }
    }

    /// <summary>Points de % de chance de débordement supplémentaires par niveau pour le Dominion (Évangélisation).</summary>
    private int GetDominionSpreadChanceBonus()
        => _state!.PlayerCivilization.ModifierAggregator.ApplyModifiers(Modifier.ECategory.DOMINION_SPREAD_CHANCE, "", 0);

    /// <summary>
    /// Diviseur commun aux chances d'action du Dominion selon la couche (débordement dans
    /// <see cref="ProcessSpread"/>, production de Temple dans <see cref="ProcessTempleProduction"/>) :
    /// l'Évangélisation peine à s'exporter en profondeur.
    /// </summary>
    private static int GetDominionLayerDivisor(int z) => z switch
    {
        LayerState.UnderworldZ => 2,
        LayerState.AbyssZ => 4,
        LayerState.PandemoniumZ => 8,
        _ => 1,
    };

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
            c.FindBuilding(BuildingType.Temple) != null && c.Position.GetHexes().Contains(hex));
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

    /// <summary>
    /// Fait grandir la Corruption sur le propre hex d'un générateur direct (Os Divin, Source de
    /// Corruption, monstre enraciné) jusqu'à <paramref name="cap"/> — sauf si un Dominion occupe déjà
    /// ce hex, auquel cas le combat l'emporte sur la croissance : le Dominion perd un point à la
    /// place, exactement comme <see cref="ApplyTempleActionOnHex"/> réduit la Corruption plutôt que
    /// d'ajouter du Dominion sur un hex déjà corrompu. Sans cette vérification, les deux pouvaient
    /// coexister durablement sur le même hex : <see cref="ProcessSpread"/> peut semer un nouveau
    /// Dominion sur un hex dont la Corruption vient d'être réduite à zéro plus tôt dans le même tick
    /// (par <see cref="ProcessTempleProduction"/>), avant que ce générateur, qui passe en dernier, ne
    /// la ressème par-dessus sans regarder ce qui s'y trouve déjà.
    /// </summary>
    private void GrowOrSeedCorruptionOnHex(HexCoord hex, int cap)
    {
        var dominion = _state!.GetFeaturesAt(hex).OfType<Dominion>().FirstOrDefault();
        if (dominion != null)
        {
            ReduceLevel(dominion);
            return;
        }

        var corruption = _state.GetFeaturesAt(hex).OfType<Corruption>().FirstOrDefault();
        if (corruption == null)
            _state.AddFeature(new Corruption(hex, level: 1));
        else if (corruption.Level < cap)
            IncreaseLevel(corruption);
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
            // Zone de Corruption entièrement nettoyée — par Temple, débordement (y compris annulation
            // mutuelle avec le Dominion, voir ProcessSpread) ou décroissance de monument : enregistre
            // son pic dans les deux records, peu importe quel hex ni quel mécanisme l'a nettoyée.
            // - PrestigeState.MaxCorruptionLevelCleared : record global de la partie, jamais remis à
            //   zéro, qui pilote le bonus de prestige (PrestigeController.GetCorruptionClearBonusMultiplier).
            // - RunRecord.MaxCorruptionLevelCleared : record de l'île courante, reparti de zéro à
            //   chaque prestige, qui seul conditionne l'ouverture de la Faille des Abysses (voir
            //   AbyssGateController.IsAbyssGateEligible). Si ce nettoyage vient de faire franchir au
            //   record du run le seuil requis, prévient le joueur qu'une Spire déjà bâtie peut évoluer.
            if (feature is Corruption cleared)
            {
                if (_prestigeState != null && cleared.PeakLevel > _prestigeState.MaxCorruptionLevelCleared)
                    _prestigeState.MaxCorruptionLevelCleared = cleared.PeakLevel;

                var runRecord = _state!.RunRecord;
                bool wasEligibleBefore = runRecord.MaxCorruptionLevelCleared >= AbyssGate.RequiredCorruptionLevel;
                if (cleared.PeakLevel > runRecord.MaxCorruptionLevelCleared)
                    runRecord.MaxCorruptionLevelCleared = cleared.PeakLevel;

                if (!wasEligibleBefore && runRecord.MaxCorruptionLevelCleared >= AbyssGate.RequiredCorruptionLevel)
                    RaiseAbyssGateEligibleToastIfApplicable();
            }

            _state!.RemoveFeature(feature);
        }
    }

    /// <summary>Prévient le joueur, au franchissement du seuil de nettoyage requis, qu'une Spire déjà bâtie peut évoluer en Faille des Abysses.</summary>
    private void RaiseAbyssGateEligibleToastIfApplicable()
    {
        if (_state!.Features.OfType<AbyssGate>().Any()) return;
        if (!_state.Features.OfType<CorruptionSpire>().Any(s => s.Built)) return;
        _state.EventLog.Add(GameEventType.AbyssGateEligible, toast: true);
    }

    /// <summary>
    /// Réduit la Corruption d'un point à chaque intervalle, de façon garantie (contrairement à la
    /// production de Temple, qui cible un hex aléatoire parmi 3) : sur l'hex d'une Faille des Abysses,
    /// et sur tous les hexes dans un rayon de <see cref="CorruptionSpire.Radius"/> autour de chaque
    /// Spire de Corruption déjà construite (<see cref="CorruptionSpire.Built"/> ; rayon 1 de base,
    /// incluant donc l'hex de la Spire elle-même et ses voisins immédiats). Une Spire en cours de
    /// construction ne réduit pas encore la corruption, y compris sur son propre hex. Aucun de ces
    /// hexes n'est protégé du reste : Temple et débordement peuvent toujours y agir normalement (voir
    /// <see cref="ApplyTempleActionOnHex"/>, <see cref="ProcessSpread"/>). Utilise
    /// <see cref="ReduceLevel"/> comme les autres mécaniques : la suppression à 0 enregistre le pic
    /// atteint dans <see cref="PrestigeState.MaxCorruptionLevelCleared"/> et dans
    /// <see cref="Model.Tasks.RunRecord.MaxCorruptionLevelCleared"/>.
    /// </summary>
    private void ProcessMonumentCorruptionDecay(long currentTick)
    {
        if (_state == null) return;

        long lastTick = _lastMonumentDecayTick;
        long cycles = TickCooldown.ConsumeElapsedCycles(currentTick, ref lastTick, ProductionIntervalTicks);
        _lastMonumentDecayTick = lastTick;
        if (cycles <= 0) return;

        // Les hexes concernés (Faille/Spires) ne changent pas d'un cycle à l'autre dans le même
        // événement : seule la Corruption qui s'y trouve décroît, donc recalculé une seule fois.
        var decayHexes = new HashSet<HexCoord>();
        foreach (var gate in _state.Features.OfType<AbyssGate>())
            decayHexes.Add(gate.Position);
        foreach (var spire in _state.Features.OfType<CorruptionSpire>().Where(s => s.Built))
            foreach (var hex in GetHexesInRadius(spire.Position, spire.Radius))
                decayHexes.Add(hex);

        for (long i = 0; i < cycles; i++)
            foreach (var hex in decayHexes)
            {
                var corruption = _state.GetFeaturesAt(hex).OfType<Corruption>().FirstOrDefault();
                if (corruption != null)
                    ReduceLevel(corruption);
            }
    }

    /// <summary>
    /// Miroir de <see cref="ProcessMonumentCorruptionDecay"/> : chaque Os Divin encore à purifier
    /// ajoute, de façon garantie et à chaque intervalle, un point de Corruption sur son propre hex —
    /// en la semant à niveau 1 si l'hex est sain (une Spire voisine peut l'avoir nettoyé). Le plafond
    /// <see cref="DivineBones.GetCorruptionCap"/> (2× le niveau de corruption de l'île à la génération
    /// des Os) borne uniquement cette génération : une Corruption déjà au-dessus n'est jamais réduite
    /// ici, elle cesse simplement de monter de ce fait.
    /// Passe volontairement après la décroissance des monuments : sous une Spire ou une Faille, les
    /// deux effets s'annulent exactement, la Corruption de l'hex reste figée tant que les Os ne sont pas
    /// purifiés. Une Purification retire les Os de la carte (voir DivineBonesController.ProcessInvestment) :
    /// la source se tarit alors d'elle-même, sans laisser de générateur résiduel.
    /// <see cref="IncreaseLevel"/> tient à jour <see cref="Corruption.PeakLevel"/>, donc la Corruption
    /// engendrée ici compte normalement dans le record de nettoyage une fois la zone dissipée.
    /// Voir <see cref="GrowOrSeedCorruptionOnHex"/> : si un Dominion occupe déjà l'hex, il perd un
    /// point à la place de la croissance — Corruption et Dominion ne peuvent jamais coexister.
    /// </summary>
    private void ProcessDivineBonesCorruptionGrowth(long currentTick)
    {
        if (_state == null) return;

        long lastTick = _lastDivineBonesGrowthTick;
        long cycles = TickCooldown.ConsumeElapsedCycles(currentTick, ref lastTick, ProductionIntervalTicks);
        _lastDivineBonesGrowthTick = lastTick;
        if (cycles <= 0) return;

        // Snapshot : semer une Corruption ajoute une feature à _state.Features pendant l'itération.
        // La liste des Os elle-même (et leur statut Purified) ne change pas d'un cycle à l'autre dans
        // le même événement — seule la Corruption qu'ils sèment progresse.
        var bonesList = _state.Features.OfType<DivineBones>().Where(b => !b.Purified).ToList();
        for (long i = 0; i < cycles; i++)
            foreach (var bones in bonesList)
                GrowOrSeedCorruptionOnHex(bones.Position, bones.GetCorruptionCap());
    }

    /// <summary>Multiplicateur appliqué au niveau de corruption de l'île pour obtenir le plafond de génération des monstres (miroir de <see cref="DivineBones.CorruptionCapMultiplier"/>).</summary>
    public const int MonsterCorruptionCapMultiplier = 2;

    /// <summary>
    /// Niveau de Corruption au-delà duquel les monstres <see cref="MonsterFeature.GeneratesCorruption"/>
    /// cessent d'alimenter leur hex : deux fois le niveau de corruption courant de l'île
    /// (<see cref="PrestigeState.CurrentCorruptionLevel"/>). Contrairement aux Os Divins, qui figent
    /// le niveau vu à leur génération, la référence est ici toujours le niveau courant — le
    /// Pandémonium n'existe que le temps d'un prestige, pendant lequel ce niveau ne bouge pas.
    /// </summary>
    public int GetMonsterCorruptionCap()
        => Math.Max(1, MonsterCorruptionCapMultiplier * Math.Max(1, _prestigeState?.CurrentCorruptionLevel ?? 1));

    /// <summary>
    /// Corruption posée d'office à l'apparition d'un monstre enraciné (Tentacule, Dieu démon) : son
    /// propre hex et ses six voisins sont portés au niveau de corruption de l'île — soit la moitié du
    /// plafond que sa génération continue atteindra ensuite (voir <see cref="GetMonsterCorruptionCap"/>).
    /// Le monstre naît donc déjà au milieu de sa flaque, à mi-chemin de son plafond, plutôt que de
    /// devoir la creuser point par point : le Pandémonium est corrompu dès l'arrivée du joueur, et une
    /// Tentacule de l'Abysse corrompt d'emblée son voisinage.
    /// Un hex déjà plus corrompu n'est jamais rabaissé, et les hexes de Void (jamais rendus ni
    /// interactifs, voir AutoExtendController.PlaceAbyssCorruption) sont ignorés.
    /// Statique : les deux appelants (AutoExtendController.PlaceTentacle pour l'Abysse,
    /// PandemoniumGateController.TryInitializePandemonium pour le Pandémonium) posent leurs monstres
    /// hors de ce contrôleur, mais doivent semer exactement la même chose.
    /// </summary>
    public static void SeedCorruptionAroundNewMonster(WorldState state, MonsterFeature monster, int islandCorruptionLevel)
    {
        if (!monster.GeneratesCorruption) return;

        int level = Math.Max(1, islandCorruptionLevel);

        RaiseCorruptionTo(state, monster.Position, level);
        foreach (var neighbor in monster.Position.Neighbors())
            RaiseCorruptionTo(state, neighbor, level);
    }

    /// <summary>Porte la Corruption d'un hex existant et non-Void à <paramref name="level"/>, en la semant si l'hex est sain ; ne la réduit jamais.</summary>
    private static void RaiseCorruptionTo(WorldState state, HexCoord hex, int level)
    {
        var tile = state.GetMapFor(hex)?.GetTile(hex);
        if (tile == null || tile.TerrainType == TerrainType.Void) return;

        var corruption = state.GetFeaturesAt(hex).OfType<Corruption>().FirstOrDefault();
        if (corruption == null)
        {
            state.AddFeature(new Corruption(hex, level));
            return;
        }

        if (corruption.Level >= level) return;
        corruption.Level = level;
        if (corruption.Level > corruption.PeakLevel) corruption.PeakLevel = corruption.Level;
    }

    /// <summary>
    /// Même mécanique que <see cref="ProcessDivineBonesCorruptionGrowth"/>, appliquée aux monstres
    /// enracinés dans la Corruption (Tentacules et Dieu démon, voir
    /// <see cref="MonsterFeature.GeneratesCorruption"/>) : chacun ajoute, à chaque intervalle, un point
    /// de Corruption sur son propre hex — en la semant à niveau 1 si l'hex est sain — tant que le
    /// niveau y reste sous <see cref="GetMonsterCorruptionCap"/>. Le Pandémonium se re-corrompt donc
    /// tout seul depuis son centre et sa couronne de Tentacules : le joueur doit abattre les monstres
    /// pour tarir les sources, exactement comme il purifie les Os Divins.
    /// Le plafond ne borne que cette génération : une Corruption déjà plus élevée (tirage initial de
    /// AutoExtendController.PlaceAbyssCorruption, débordement d'un voisin) n'est jamais réduite ici.
    /// Passe après la décroissance des monuments, pour la même raison que la croissance des Os Divins :
    /// sous une Spire, les deux effets s'annulent exactement. Voir <see cref="GrowOrSeedCorruptionOnHex"/> :
    /// un Dominion déjà présent perd un point à la place de la croissance.
    /// </summary>
    private void ProcessMonsterCorruptionGrowth(long currentTick)
    {
        if (_state == null) return;

        long lastTick = _lastMonsterGrowthTick;
        long cycles = TickCooldown.ConsumeElapsedCycles(currentTick, ref lastTick, ProductionIntervalTicks);
        _lastMonsterGrowthTick = lastTick;
        if (cycles <= 0) return;

        int cap = GetMonsterCorruptionCap();

        // Snapshot : semer une Corruption ajoute une feature à _state.Features pendant l'itération.
        // La liste des monstres elle-même ne change pas d'un cycle à l'autre dans le même événement.
        var monsters = _state.Features.OfType<MonsterFeature>().Where(m => m.GeneratesCorruption).ToList();
        for (long i = 0; i < cycles; i++)
            foreach (var monster in monsters)
                GrowOrSeedCorruptionOnHex(monster.Position, cap);
    }

    /// <summary>
    /// Miroir de <see cref="ProcessDivineBonesCorruptionGrowth"/> pour les Sources de Corruption
    /// (voir <see cref="IslandFeatures.CorruptionSource"/>) : chaque Source ajoute, de façon garantie
    /// et à chaque intervalle, un point de Corruption sur son propre hex — en la semant à niveau 1 si
    /// l'hex est sain — tant que le niveau y reste sous <see cref="IslandFeatures.CorruptionSource.GetCorruptionCap"/>.
    /// Contrairement aux Os Divins, ce plafond n'est jamais doublé : il vaut exactement le niveau de
    /// corruption de l'île au moment de la génération de la Source. Une Source n'est jamais purifiée
    /// par le joueur ; elle disparaît uniquement quand une Spire de Corruption est bâtie sur son hex
    /// (voir CorruptionSpireController.ProcessInvestment). Voir <see cref="GrowOrSeedCorruptionOnHex"/> :
    /// un Dominion déjà présent perd un point à la place de la croissance.
    /// </summary>
    private void ProcessCorruptionSourceGrowth(long currentTick)
    {
        if (_state == null) return;

        long lastTick = _lastCorruptionSourceGrowthTick;
        long cycles = TickCooldown.ConsumeElapsedCycles(currentTick, ref lastTick, ProductionIntervalTicks);
        _lastCorruptionSourceGrowthTick = lastTick;
        if (cycles <= 0) return;

        // Snapshot : semer une Corruption ajoute une feature à _state.Features pendant l'itération.
        // La liste des Sources elle-même ne change pas d'un cycle à l'autre dans le même événement.
        var sources = _state.Features.OfType<CorruptionSource>().ToList();
        for (long i = 0; i < cycles; i++)
            foreach (var source in sources)
                GrowOrSeedCorruptionOnHex(source.Position, source.GetCorruptionCap());
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
