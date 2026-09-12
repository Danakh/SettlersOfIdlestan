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
/// Gère la lutte Corruption/Dominion. La Corruption et le Dominion sont <b>passifs</b> : un hex déjà
/// posé ne déborde jamais de lui-même. Seules les <b>sources</b> sont simulées, toutes au rythme de
/// <see cref="ProductionIntervalTicks"/> (10 s), et chacune produit un point par intervalle.
/// 1. <see cref="ProcessTempleProduction"/> — chaque Temple au-delà du niveau 1 (atteignable
///    uniquement via les pouvoirs divins Foi +3 et Magisterium Divin +1, voir
///    AscensionBuildingMaxLevelGrants) produit du Dominion depuis les 3 hexes touchant sa ville, plafonné à
///    <see cref="TempleDominionCapPerLevel"/> × niveau effectif du Temple. Le niveau effectif est le
///    niveau réel augmenté de TEMPLE_DOMINION_LEVEL_BONUS (Ziggourat +1), ce qui abaisse aussi d'autant
///    le niveau à partir duquel un Temple produit — voir <see cref="ProducesDominion"/>.
///    DOMINION_SPREAD_CHANCE (Humains) accorde en plus, par niveau effectif de Temple, une chance de
///    produire un second point dans le même intervalle (voir <see cref="RollDoubleProduction"/>).
/// 2. <see cref="ProcessDivineBonesCorruptionGrowth"/>, <see cref="ProcessMonsterCorruptionGrowth"/> et
///    <see cref="ProcessCorruptionSourceGrowth"/> — chaque Os Divin non purifié, chaque monstre
///    enraciné dans la Corruption (<see cref="MonsterFeature.GeneratesCorruption"/>) et chaque Source
///    de Corruption produit un point de Corruption depuis son propre hex, sous son propre plafond.
///    Seule leur disparition tarit la source : purification des Os (DivineBonesController), mort du
///    monstre, Spire bâtie sur la Source (CorruptionSpireController). À l'apparition d'un monstre,
///    <see cref="SeedCorruptionAroundNewMonster"/> (appelé par les générateurs) corrompt d'office son
///    hex et ses six voisins au niveau de l'île, soit la moitié de son plafond.
/// 3. <see cref="FindProductionTarget"/> — <b>la cascade</b>, commune à toutes ces sources et seul
///    moyen désormais pour la Corruption comme pour le Dominion de gagner du terrain. La production
///    vise l'hex le moins fourni parmi les hexes de départ de la source (les 3 hexes de ville d'un
///    Temple, son propre hex pour les autres). S'ils sont tous saturés, elle retente anneau par anneau
///    — rayon 1, 2, … jusqu'à <see cref="MaxCascadeRadius"/> — et s'arrête au premier anneau offrant un
///    hex non saturé, le moins fourni de cet anneau. Au-delà, la production est perdue.
/// 4. <see cref="GetFill"/> — le « remplissage » qui classe les candidats compte le statut opposé en
///    <b>négatif</b> : un hex tenu par l'adversaire est donc toujours le candidat le plus attirant, et
///    le plus fort d'abord. La production s'y dépense alors en combat (−1 à l'adversaire, rien de posé)
///    au lieu de s'y ajouter — c'est à la frontière, et seulement là, que se joue la lutte, le
///    débordement passif ne l'assurant plus. Une Corruption repoussée par un Temple peut perdre 2 ou 3
///    points d'un coup (Évangélisation, voir <see cref="ReduceCorruption"/>) ; un Dominion attaqué peut
///    résister (Terre Consacrée, voir <see cref="IsDominionSpared"/>).
/// 5. <see cref="ProcessMonumentCorruptionDecay"/> — hors cascade, et inchangé : ni la Faille des
///    Abysses ni la Spire de Corruption ne protègent leur hex des mécaniques ci-dessus (une production
///    peut y agir normalement) ; ce process leur ajoute simplement une réduction garantie
///    (contrairement au ciblage de la cascade) d'un point de Corruption par intervalle sur leur propre
///    hex (Faille), ou sur tous les hexes dans un rayon fixe de
///    <see cref="IslandFeatures.CorruptionSpire.DecayRadius"/> autour d'elle (Spire — son niveau n'est
///    pas améliorable, voir CorruptionSpireController). La Spire n'agit qu'une fois
///    <see cref="IslandFeatures.CorruptionSpire.Built"/> : pendant sa construction, aucune décroissance
///    n'est appliquée sur son hex.
/// Plafonds, par source : <see cref="GetTempleDominionCap"/> (Temple),
/// <see cref="IslandFeatures.DivineBones.GetCorruptionCap"/> (2× le niveau de corruption de l'île figé
/// à la génération des Os), <see cref="GetMonsterCorruptionCap"/> (2× le niveau de corruption courant
/// de l'île) et <see cref="IslandFeatures.CorruptionSource.GetCorruptionCap"/> (le niveau de corruption
/// de l'île à sa génération, jamais doublé). Le plafond d'une source s'applique tel quel à tout hex
/// qu'elle atteint en cascade, et ne borne que sa propre production : un hex déjà au-dessus n'est
/// jamais rabaissé, il cesse simplement d'être un candidat.
/// Invariant : Corruption et Dominion ne coexistent jamais sur un même hex —
/// <see cref="ApplyProduction"/> combat toujours le statut opposé au lieu de poser le sien par-dessus.
/// </summary>
public class CorruptionController
{
    /// <summary>10 secondes (1 tick = 0.01 s) — rythme commun à toutes les sources et à la décroissance sous les monuments.</summary>
    public const long ProductionIntervalTicks = 1000L;

    private const int TempleMinDominionLevel = 2;
    private const int TempleMaxDominionLevel = 4;
    private const int TempleDominionCapPerLevel = 2;

    /// <summary>
    /// Rayon maximal exploré par la cascade autour des hexes de départ d'une source (voir
    /// <see cref="FindProductionTarget"/>) : une source entièrement cernée d'hexes saturés jusqu'à
    /// cette distance perd sa production. Borne aussi l'emprise maximale d'une source isolée — un
    /// disque de 91 hexes autour de son point de départ.
    /// </summary>
    public const int MaxCascadeRadius = 5;

    /// <summary>Niveaux de Corruption que l'Évangélisation peut retirer au-delà du premier (voir <see cref="RollExtraCleanseLevels"/>) : 2 au plus, soit 3 points d'un coup.</summary>
    private const int MaxExtraCleanseLevels = 2;

    /// <summary>Malus de base appliqué aux chances d'action du Dominion par couche franchie sous la surface (÷2 Inframonde, ÷4 Abysses, ÷8 Pandémonium), avant réduction par DOMINION_LAYER_PENALTY_REDUCTION.</summary>
    private const double DominionLayerPenaltyBase = 2.0;

    /// <summary>
    /// Échelle (millièmes) dans laquelle sont exprimés les diviseurs de profondeur du Dominion :
    /// le Dogme de l'Emprise ramène le malus par couche à 1,5, les diviseurs (1,5 / 2,25 / 3,375)
    /// ne sont donc plus entiers. 1000 = pas de malus.
    /// </summary>
    private const int LayerDivisorMilliScale = 1000;

    private WorldState? _state;
    private GameClock? _clock;
    private GamePRNG? _prng;
    private PrestigeState? _prestigeState;

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
        catch (Exception ex) { GameLog.Error(nameof(CorruptionController), nameof(ProcessTempleProduction), ex); }

        try { ProcessMonumentCorruptionDecay(e.CurrentTick); }
        catch (Exception ex) { GameLog.Error(nameof(CorruptionController), nameof(ProcessMonumentCorruptionDecay), ex); }

        try { ProcessDivineBonesCorruptionGrowth(e.CurrentTick); }
        catch (Exception ex) { GameLog.Error(nameof(CorruptionController), nameof(ProcessDivineBonesCorruptionGrowth), ex); }

        try { ProcessMonsterCorruptionGrowth(e.CurrentTick); }
        catch (Exception ex) { GameLog.Error(nameof(CorruptionController), nameof(ProcessMonsterCorruptionGrowth), ex); }

        try { ProcessCorruptionSourceGrowth(e.CurrentTick); }
        catch (Exception ex) { GameLog.Error(nameof(CorruptionController), nameof(ProcessCorruptionSourceGrowth), ex); }
    }

    /// <summary>Cooldown par Temple (comme AlchimistHut.LastCrystalProductionTick) — chaque Temple agit toutes les 10 s depuis sa dernière action.</summary>
    private void ProcessTempleProduction(long currentTick)
    {
        if (_state == null || _prng == null) return;

        foreach (var civ in _state.Civilizations)
        {
            foreach (var city in civ.Cities)
            {
                var temple = city.FindBuilding<Temple>(BuildingType.Temple) is { } t0 && ProducesDominion(civ, t0.Level) ? t0 : null;
                if (temple == null) continue;

                // coldStartOnZero: true — LastDominionProductionTick est persisté à 0 tant que le Temple
                // n'a jamais encore produit (construction/promotion au niveau 2 en cours de partie déjà
                // avancée). Sans ce garde-fou, ConsumeElapsedCycles traiterait ce 0 comme un tracker actif
                // depuis le tick 0 et rattraperait tout l'écoulé de la partie en un seul appel — un Temple
                // tout juste construit inonderait aussitôt ses hexes voisins de Dominion.
                long lastTick = temple.LastDominionProductionTick;
                long cycles = TickCooldown.ConsumeElapsedCycles(currentTick, ref lastTick, ProductionIntervalTicks, coldStartOnZero: true);
                temple.LastDominionProductionTick = lastTick;
                if (cycles <= 0) continue;

                // Pénalité de profondeur du Dominion (÷2/÷4/÷8, allégée par le Dogme de l'Emprise) :
                // le cooldown reste identique, mais chaque tir n'a qu'une chance sur
                // GetDominionLayerDivisorMilli d'aboutir. Rejoué cycle par cycle (pas de
                // multiplication) : chaque cycle est un tirage indépendant, et la cascade repart
                // chaque fois de l'état laissé par le cycle précédent.
                int divisorMilli = GetDominionLayerDivisorMilli(_state.PlayerCivilization, city.Position.Z);
                int cap = GetTempleDominionCap(civ, temple.Level);

                // Hexes de départ de la cascade : les 3 hexes de la ville, identiques d'un cycle à
                // l'autre. Tampon réutilisé — le Where/ToList allouait une fermeture, un itérateur et
                // une liste par cycle et par ville avec Temple. Les hexes hors carte sont laissés
                // dans la liste : ils fixent la distance des anneaux sans jamais devenir candidats
                // (voir PickLeastFilled).
                var seeds = _cascadeSeedsScratch;
                seeds.Clear();
                var cityHexes = city.Position.GetHexes();
                for (int h = 0; h < cityHexes.Length; h++)
                    seeds.Add(cityHexes[h]);

                for (long i = 0; i < cycles; i++)
                {
                    if (divisorMilli > LayerDivisorMilliScale && !RollLayerChance(1, 1, divisorMilli)) continue;

                    ProduceOnce(isDominion: true, seeds, cap);

                    // Le second point des Humains n'est pas soumis à un second tirage de profondeur :
                    // le cycle a déjà passé le sien, ce bonus double sa portée, pas sa probabilité.
                    if (RollDoubleProduction(civ, temple.Level))
                        ProduceOnce(isDominion: true, seeds, cap);
                }
            }
        }
    }

    /// <summary>
    /// Vrai si ce Temple produit un second point de Dominion dans le même intervalle :
    /// DOMINION_SPREAD_CHANCE points de % par niveau effectif de Temple (Humains, 2 → 6% à niveau
    /// effectif 3, 10% à 5). Le tirage n'est fait que si le modificateur est acquis : sans lui, la
    /// séquence du PRNG doit rester exactement la même (voir CLAUDE.md, sauvegardes de
    /// SOITests/saves/current). Comme les autres modificateurs de cette mécanique, il est lu sur la
    /// civilisation qui produit.
    /// </summary>
    private bool RollDoubleProduction(Civilization civ, int templeLevel)
    {
        int chancePerLevel = civ.ModifierAggregator
            .ApplyModifiers(Modifier.ECategory.DOMINION_SPREAD_CHANCE, "", 0);
        if (chancePerLevel <= 0) return false;

        return _prng!.Next(100) < chancePerLevel * GetTempleDominionLevel(civ, templeLevel);
    }

    /// <summary>Plafond de Dominion par hex qu'un Temple de ce niveau peut atteindre pour cette civilisation (TEMPLE_DOMINION_CAP relève le plafond par niveau de Temple ; aucune source ne l'accorde actuellement). Utilisé par <see cref="ProcessTempleProduction"/> et par le tooltip du panneau ville.</summary>
    public static int GetTempleDominionCap(Civilization civ, int templeLevel)
    {
        int capPerLevel = TempleDominionCapPerLevel
            + civ.ModifierAggregator.ApplyModifiers(Modifier.ECategory.TEMPLE_DOMINION_CAP, "", 0);
        return capPerLevel * GetTempleDominionLevel(civ, templeLevel);
    }

    /// <summary>
    /// Niveau auquel un Temple de niveau <paramref name="templeLevel"/> produit du Dominion pour cette
    /// civilisation : son niveau réel plus TEMPLE_DOMINION_LEVEL_BONUS (Ziggourat +1). Ne concerne que
    /// la production de Dominion — le niveau réel du bâtiment (coûts, défense, plafond de niveau)
    /// n'est pas touché.
    /// </summary>
    public static int GetTempleDominionLevel(Civilization civ, int templeLevel)
        => templeLevel + civ.ModifierAggregator.ApplyModifiers(Modifier.ECategory.TEMPLE_DOMINION_LEVEL_BONUS, "", 0);

    /// <summary>
    /// Vrai si un Temple bâti de ce niveau produit du Dominion pour cette civilisation : son niveau
    /// effectif (voir <see cref="GetTempleDominionLevel"/>) atteint <see cref="TempleMinDominionLevel"/>.
    /// La borne haute porte sur le niveau réel, pas sur l'effectif : la Ziggourat doit continuer de
    /// bénéficier au Temple de niveau maximal, pas l'exclure.
    /// </summary>
    public static bool ProducesDominion(Civilization civ, int templeLevel)
        => templeLevel >= 1
        && templeLevel <= TempleMaxDominionLevel
        && GetTempleDominionLevel(civ, templeLevel) >= TempleMinDominionLevel;

    /// <summary>
    /// Tampons de la cascade (<see cref="FindProductionTarget"/>), réutilisés d'une source, d'un cycle
    /// et d'un événement d'horloge à l'autre : hexes de départ, parcours en largeur anneau par anneau,
    /// et candidats non saturés de l'anneau courant. Réalloués, ils feraient l'essentiel des
    /// allocations de ce contrôleur — la cascade est relancée à chaque production de chaque source.
    /// </summary>
    private readonly List<HexCoord> _cascadeSeedsScratch = new(3);
    private readonly HashSet<HexCoord> _cascadeVisitedScratch = new();
    private readonly List<HexCoord> _cascadeFrontierScratch = new();
    private readonly List<HexCoord> _cascadeNextScratch = new();
    private readonly List<HexCoord> _cascadeCandidatesScratch = new();

    /// <summary>
    /// Produit un point pour une source : trouve la cible par cascade (voir
    /// <see cref="FindProductionTarget"/>) et l'applique (voir <see cref="ApplyProduction"/>). Ne fait
    /// rien si aucune cible n'est trouvée dans <see cref="MaxCascadeRadius"/> — la production est alors
    /// perdue. Point d'entrée unique de toutes les sources, Temples comme générateurs de Corruption.
    /// </summary>
    private void ProduceOnce(bool isDominion, List<HexCoord> seeds, int cap)
    {
        var target = FindProductionTarget(isDominion, seeds, cap);
        if (target != null)
            ApplyProduction(isDominion, target.Value);
    }

    /// <summary>
    /// Cible d'une production, en cascade. Examine d'abord les hexes de départ de la source
    /// (<paramref name="seeds"/> — les 3 hexes de ville d'un Temple, le seul hex d'un générateur de
    /// Corruption), puis, s'ils sont tous saturés, les anneaux de rayon 1, 2, … jusqu'à
    /// <see cref="MaxCascadeRadius"/>. Retourne l'hex le <b>moins fourni</b> (voir
    /// <see cref="GetFill"/>) du premier anneau qui en offre un non saturé, ou null si la production
    /// n'a nulle part où aller.
    ///
    /// <para>Les anneaux sont des anneaux de <b>distance</b> : le parcours traverse aussi les hexes
    /// hors carte (bord de carte) pour que le rayon reste une distance et non une connexité, mais
    /// ceux-ci ne sont jamais candidats (voir <see cref="IsValidHex"/>). Les ex aequo de remplissage
    /// sont départagés au tirage, comme l'était le ciblage du Temple.</para>
    /// </summary>
    private HexCoord? FindProductionTarget(bool isDominion, List<HexCoord> seeds, int cap)
    {
        int effectiveCap = GetEffectiveCap(isDominion, cap);

        var visited = _cascadeVisitedScratch;
        var frontier = _cascadeFrontierScratch;
        var next = _cascadeNextScratch;
        visited.Clear();
        frontier.Clear();

        for (int i = 0; i < seeds.Count; i++)
            if (visited.Add(seeds[i]))
                frontier.Add(seeds[i]);

        for (int radius = 0; ; radius++)
        {
            var target = PickLeastFilled(isDominion, frontier, effectiveCap);
            if (target != null) return target;

            if (radius >= MaxCascadeRadius) return null;

            next.Clear();
            for (int i = 0; i < frontier.Count; i++)
            {
                var hex = frontier[i];
                for (int d = 0; d < HexDirections.Length; d++)
                {
                    var neighbor = hex.Neighbor(HexDirections[d]);
                    if (visited.Add(neighbor))
                        next.Add(neighbor);
                }
            }
            if (next.Count == 0) return null;

            frontier.Clear();
            frontier.AddRange(next);
        }
    }

    /// <summary>Les 6 directions, une fois pour toutes : <c>Enum.GetValues</c> alloue un tableau à chaque appel, et la cascade balaie jusqu'à 91 hexes par production.</summary>
    private static readonly HexDirection[] HexDirections = Enum.GetValues<HexDirection>();

    /// <summary>
    /// Hex le moins fourni parmi <paramref name="candidates"/>, en ignorant les hexes hors carte et
    /// ceux déjà saturés. Null si l'anneau n'offre rien. Les ex aequo sont départagés au tirage — un
    /// seul appel au PRNG, et uniquement quand il y a vraiment plusieurs candidats à égalité.
    /// </summary>
    private HexCoord? PickLeastFilled(bool isDominion, List<HexCoord> candidates, int effectiveCap)
    {
        var best = _cascadeCandidatesScratch;
        best.Clear();
        int bestFill = int.MaxValue;

        for (int i = 0; i < candidates.Count; i++)
        {
            var hex = candidates[i];
            if (!IsValidHex(hex)) continue;

            int fill = GetFill(isDominion, hex);
            if (fill >= effectiveCap) continue;

            if (fill < bestFill)
            {
                bestFill = fill;
                best.Clear();
                best.Add(hex);
            }
            else if (fill == bestFill)
            {
                best.Add(hex);
            }
        }

        if (best.Count == 0) return null;
        return best.Count == 1 ? best[0] : best[_prng!.Next(best.Count)];
    }

    /// <summary>
    /// « Remplissage » d'un hex du point de vue d'une production : le niveau du <b>même</b> statut
    /// (0 si l'hex est sain), ou l'<b>opposé</b> du niveau du statut adverse. Un hex tenu par
    /// l'adversaire est donc toujours moins fourni qu'un hex sain, et le plus fort l'est le moins :
    /// la cascade attaque le front avant de coloniser le vide, et frappe d'abord là où l'adversaire
    /// est le plus solide. C'est ce qui remplace l'annulation mutuelle de l'ancien débordement.
    /// </summary>
    private int GetFill(bool isDominion, HexCoord hex)
    {
        var opposite = isDominion
            ? (IslandFeature?)_state!.GetFirstFeatureAt<Corruption>(hex)
            : _state!.GetFirstFeatureAt<Dominion>(hex);
        if (opposite != null) return -GetLevel(opposite);

        var same = isDominion
            ? (IslandFeature?)_state.GetFirstFeatureAt<Dominion>(hex)
            : _state.GetFirstFeatureAt<Corruption>(hex);
        return same != null ? GetLevel(same) : 0;
    }

    /// <summary>
    /// Plafond réellement atteignable par une source : son propre plafond, borné par le plafond dur de
    /// la feature (<see cref="Corruption.MaxLevel"/> / <see cref="Dominion.MaxLevel"/>). Sans cette
    /// borne, une source dont le plafond dépasse 10 verrait éternellement comme « non saturé » un hex
    /// bloqué à 10 par le setter de niveau, et y perdrait toutes ses productions au lieu de cascader.
    /// </summary>
    private static int GetEffectiveCap(bool isDominion, int cap)
        => Math.Min(cap, isDominion ? Dominion.MaxLevel : Corruption.MaxLevel);

    /// <summary>
    /// Applique un point de production sur un hex choisi par la cascade. Si l'hex porte le statut
    /// <b>opposé</b>, la production s'y dépense en combat : elle lui retire un niveau et ne pose rien
    /// (la Corruption peut en perdre 2 ou 3 d'un coup avec l'Évangélisation, voir
    /// <see cref="ReduceCorruption"/> ; le Dominion peut résister avec Terre Consacrée, voir
    /// <see cref="IsDominionSpared"/> — la production est alors simplement perdue). Sinon elle sème le
    /// statut de la source à niveau 1 sur un hex sain, ou lui ajoute un niveau.
    ///
    /// <para>C'est ce combat systématique qui tient l'invariant « Corruption et Dominion ne coexistent
    /// jamais sur un même hex » dans les deux sens.</para>
    /// </summary>
    private void ApplyProduction(bool isDominion, HexCoord hex)
    {
        if (isDominion)
        {
            var corruption = _state!.GetFirstFeatureAt<Corruption>(hex);
            if (corruption != null)
            {
                ReduceCorruption(corruption);
                return;
            }

            var dominion = _state.GetFirstFeatureAt<Dominion>(hex);
            if (dominion == null)
                _state.AddFeature(new Dominion(hex, level: 1));
            else
                IncreaseLevel(dominion);
            return;
        }

        var opposingDominion = _state!.GetFirstFeatureAt<Dominion>(hex);
        if (opposingDominion != null)
        {
            if (!IsDominionSpared(hex))
                ReduceLevel(opposingDominion);
            return;
        }

        var existing = _state.GetFirstFeatureAt<Corruption>(hex);
        if (existing == null)
            _state.AddFeature(new Corruption(hex, level: 1));
        else
            IncreaseLevel(existing);
    }


    /// <summary>
    /// Diviseur appliqué aux chances de production d'un Temple selon la couche de sa ville (voir
    /// <see cref="ProcessTempleProduction"/>) : l'Évangélisation peine à s'exporter en profondeur, un
    /// Temple de l'Inframonde ne produit donc qu'un intervalle sur deux, un des Abysses un sur quatre.
    /// Exprimé en millièmes (<see cref="LayerDivisorMilliScale"/>) car le malus par couche est
    /// fractionnaire dès que le Dogme de l'Emprise (DOMINION_LAYER_PENALTY_REDUCTION) l'allège :
    /// 2/4/8 devient 1,5/2,25/3,375. Le malus du joueur s'applique à toutes les civilisations — seul
    /// le joueur bâtit en profondeur.
    /// </summary>
    public static int GetDominionLayerDivisorMilli(Civilization civ, int z)
    {
        int depth = z switch
        {
            LayerState.UnderworldZ => 1,
            LayerState.AbyssZ => 2,
            LayerState.PandemoniumZ => 3,
            _ => 0,
        };
        if (depth == 0) return LayerDivisorMilliScale;

        double reduction = civ.ModifierAggregator
            .ApplyModifiers(Modifier.ECategory.DOMINION_LAYER_PENALTY_REDUCTION, "", 0.0);
        double penalty = Math.Max(1.0, DominionLayerPenaltyBase - reduction);
        return (int)Math.Round(Math.Pow(penalty, depth) * LayerDivisorMilliScale);
    }

    /// <summary>
    /// Tirage « <paramref name="successes"/> chances sur <paramref name="outOf"/>, divisées par le
    /// malus de profondeur <paramref name="divisorMilli"/> » (voir
    /// <see cref="GetDominionLayerDivisorMilli"/>). Sans malus, le tirage entier d'origine est
    /// conservé tel quel : la séquence du PRNG ne doit pas changer en surface, où rien du jeu ne
    /// change (voir CLAUDE.md, sauvegardes de SOITests/saves/current).
    /// </summary>
    private bool RollLayerChance(int successes, int outOf, int divisorMilli)
        => divisorMilli <= LayerDivisorMilliScale
            ? _prng!.Next(outOf) < successes
            : _prng!.Next(outOf * divisorMilli) < successes * LayerDivisorMilliScale;

    /// <summary>
    /// Vrai si le Dominion de cet hex échappe (tirage aléatoire) à la perte de niveau que lui inflige
    /// la production d'une source de Corruption arrivée sur lui (voir <see cref="ApplyProduction"/>) :
    /// recherche Terre Consacrée (TEMPLE_DOMINION_PROTECTION_CHANCE) et
    /// hex touchant une ville du joueur possédant un Temple. Le modificateur est une réduction
    /// <b>multiplicative</b> du risque de perdre le niveau, appliquée une fois par Os Divin purifié
    /// sur l'île courante (0,1 = −10%/Os) : le risque vaut (1 − 0,1)^Os, donc la chance de résister
    /// 1 − 0,9^Os — 10% à 1 Os, 65% à 10, 88% à 20, jamais tout à fait 100%. Sans purification, la
    /// recherche ne protège rien.
    /// </summary>
    private bool IsDominionSpared(HexCoord hex)
    {
        double perBone = _state!.PlayerCivilization.ModifierAggregator
            .ApplyModifiers(Modifier.ECategory.TEMPLE_DOMINION_PROTECTION_CHANCE, "", 0.0);
        int bones = _state.RunRecord.DivineBonesPurified;
        if (perBone <= 0 || bones <= 0) return false;

        double chance = 1.0 - Math.Pow(Math.Max(0.0, 1.0 - perBone), bones);
        if (chance <= 0) return false;

        // Index hexagone → villes (voir Civilization.GetCitiesAdjacentTo) plutôt qu'un balayage des
        // centaines de villes du joueur : cette question est posée à chaque production de Corruption
        // qui tombe sur du Dominion, donc des milliers de fois par saut de temps.
        var cities = _state.PlayerCivilization.GetCitiesAdjacentTo(hex);
        bool nearTemple = false;
        for (int i = 0; i < cities.Count && !nearTemple; i++)
            nearTemple = cities[i].FindBuilding(BuildingType.Temple) != null;
        if (!nearTemple) return false;

        return _prng!.Next(100) < (int)Math.Round(chance * 100);
    }

    /// <summary>
    /// Produit un point de Corruption pour un générateur direct (Os Divin, Source de Corruption,
    /// monstre enraciné), depuis son propre hex et en cascade au-delà s'il est saturé — voir
    /// <see cref="ProduceOnce"/>. Les trois générateurs ne diffèrent que par leur plafond.
    /// </summary>
    private void ProduceCorruptionFrom(HexCoord hex, int cap)
    {
        var seeds = _cascadeSeedsScratch;
        seeds.Clear();
        seeds.Add(hex);
        ProduceOnce(isDominion: false, seeds, cap);
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

    /// <summary>
    /// Réduit la Corruption d'un point, ou de deux (voire trois) d'un coup sur un tirage réussi de
    /// CORRUPTION_DOUBLE_CLEANSE_CHANCE (Évangélisation). N'est utilisé que pour la mécanique que la
    /// recherche vise — une production de Dominion qui tombe sur de la Corruption (voir
    /// <see cref="ApplyProduction"/>) : la décroissance sous
    /// les monuments passe toujours par <see cref="ReduceLevel"/>, elle, et retire toujours un point.
    /// Le tirage n'est fait que si la recherche est acquise : sans elle, la séquence du PRNG doit
    /// rester exactement celle d'avant (voir CLAUDE.md, sauvegardes de SOITests/saves/current).
    /// Comme le modificateur du joueur pour la profondeur (voir <see cref="GetDominionLayerDivisorMilli"/>),
    /// il s'applique à toutes les civilisations.
    /// </summary>
    private void ReduceCorruption(IslandFeature corruption)
    {
        int extra = RollExtraCleanseLevels();

        ReduceLevel(corruption);

        // Le premier point peut avoir vidé la poche : ReduceLevel l'a alors retirée de l'état (et
        // enregistré son pic), il n'y a plus rien à retirer.
        while (extra-- > 0 && GetLevel(corruption) > 0)
            ReduceLevel(corruption);
    }

    /// <summary>
    /// Nombre de niveaux de Corruption retirés <b>en plus</b> du premier (0 à
    /// <see cref="MaxExtraCleanseLevels"/>) par l'Évangélisation. Le modificateur est une chance
    /// <b>par Os Divin purifié</b> sur l'île courante (0,1 = 10%/Os) : le total est dépensé point par
    /// point, chaque tranche pleine de 100% garantissant un niveau supplémentaire et le reste étant
    /// tiré au sort. À 13 Os purifiés (130%), le second point est donc acquis et le troisième tombe
    /// 30% du temps. Aucun tirage n'est consommé quand il n'y a rien d'aléatoire à décider.
    /// </summary>
    private int RollExtraCleanseLevels()
    {
        double perBone = _state!.PlayerCivilization.ModifierAggregator
            .ApplyModifiers(Modifier.ECategory.CORRUPTION_DOUBLE_CLEANSE_CHANCE, "", 0.0);
        if (perBone <= 0) return 0;

        double chance = perBone * _state.RunRecord.DivineBonesPurified;
        int extra = 0;
        while (extra < MaxExtraCleanseLevels && chance > 0)
        {
            if (chance >= 1.0)
            {
                extra++;
                chance -= 1.0;
                continue;
            }

            if (_prng!.Next(100) < (int)Math.Round(chance * 100)) extra++;
            break;
        }

        return extra;
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
            // Zone de Corruption entièrement nettoyée — par une production de Dominion (Temple, en
            // cascade ou non) ou par la décroissance sous un monument : enregistre
            // son pic dans RunRecord.MaxCorruptionLevelCleared, record de l'île courante reparti de
            // zéro à chaque prestige, peu importe quel hex ni quel mécanisme l'a nettoyée. Il ne sert
            // qu'à conditionner l'ouverture de la Faille des Abysses (voir
            // AbyssGateController.IsAbyssGateEligible) : le bonus de prestige de nettoyage, lui, ne
            // dépend pas d'une zone dissipée mais de la présence d'une Spire de Corruption bâtie sur
            // l'île (voir PrestigeController.GetCorruptionClearBonusMultiplier, qui ne mémorise rien).
            // Si ce nettoyage vient de faire franchir au
            // record du run le seuil requis, prévient le joueur qu'une Spire déjà bâtie peut évoluer.
            if (feature is Corruption cleared)
            {
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
    /// cascade, qui ne vise qu'un hex par production) : sur l'hex d'une Faille des Abysses,
    /// et sur tous les hexes dans le rayon fixe <see cref="CorruptionSpire.DecayRadius"/> autour de
    /// chaque Spire de Corruption déjà construite (<see cref="CorruptionSpire.Built"/> ; rayon 1,
    /// incluant donc l'hex de la Spire elle-même et ses voisins immédiats). Une Spire en cours de
    /// construction ne réduit pas encore la corruption, y compris sur son propre hex. Aucun de ces
    /// hexes n'est protégé du reste : n'importe quelle production peut toujours y agir normalement
    /// (voir <see cref="ApplyProduction"/>). Utilise
    /// <see cref="ReduceLevel"/> comme les autres mécaniques : la suppression à 0 enregistre le pic
    /// atteint dans <see cref="Model.Tasks.RunRecord.MaxCorruptionLevelCleared"/>.
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
            foreach (var hex in GetHexesInRadius(spire.Position, CorruptionSpire.DecayRadius))
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
    /// produit, de façon garantie et à chaque intervalle, un point de Corruption depuis son propre hex
    /// — en la semant à niveau 1 si l'hex est sain (une Spire voisine peut l'avoir nettoyé), et en
    /// cascadant autour dès qu'il est saturé (voir <see cref="ProduceCorruptionFrom"/>). Le plafond
    /// <see cref="DivineBones.GetCorruptionCap"/> (2× le niveau de corruption de l'île à la génération
    /// des Os) borne uniquement cette génération, sur l'hex des Os comme sur ceux que la cascade
    /// atteint : une Corruption déjà au-dessus n'est jamais réduite ici, elle cesse simplement d'être
    /// une cible.
    /// Passe volontairement après la décroissance des monuments : sous une Spire ou une Faille, les
    /// deux effets s'annulent exactement, la Corruption de l'hex reste figée tant que les Os ne sont pas
    /// purifiés. Une Purification retire les Os de la carte (voir DivineBonesController.ProcessInvestment) :
    /// la source se tarit alors d'elle-même, sans laisser de générateur résiduel.
    /// <see cref="IncreaseLevel"/> tient à jour <see cref="Corruption.PeakLevel"/>, donc la Corruption
    /// engendrée ici compte normalement dans le record de nettoyage une fois la zone dissipée.
    /// Voir <see cref="ApplyProduction"/> : si un Dominion occupe l'hex visé, il perd un
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
                ProduceCorruptionFrom(bones.Position, bones.GetCorruptionCap());
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
    /// sous une Spire, les deux effets s'annulent exactement. Voir <see cref="ApplyProduction"/> :
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
                ProduceCorruptionFrom(monster.Position, cap);
    }

    /// <summary>
    /// Miroir de <see cref="ProcessDivineBonesCorruptionGrowth"/> pour les Sources de Corruption
    /// (voir <see cref="IslandFeatures.CorruptionSource"/>) : chaque Source ajoute, de façon garantie
    /// et à chaque intervalle, un point de Corruption sur son propre hex — en la semant à niveau 1 si
    /// l'hex est sain — tant que le niveau y reste sous <see cref="IslandFeatures.CorruptionSource.GetCorruptionCap"/>.
    /// Contrairement aux Os Divins, ce plafond n'est jamais doublé : il vaut exactement le niveau de
    /// corruption de l'île au moment de la génération de la Source. Une Source n'est jamais purifiée
    /// par le joueur ; elle disparaît uniquement quand une Spire de Corruption est bâtie sur son hex
    /// (voir CorruptionSpireController.ProcessInvestment). Voir <see cref="ApplyProduction"/> :
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
                ProduceCorruptionFrom(source.Position, source.GetCorruptionCap());
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
