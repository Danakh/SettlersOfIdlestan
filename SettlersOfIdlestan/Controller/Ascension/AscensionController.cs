using System;
using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Controller.Generator;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Ascension;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandFeatures;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Monsters;
using SettlersOfIdlestan.Model.Prestige;
using SettlersOfIdlestan.Model.Races;

namespace SettlersOfIdlestan.Controller.Ascension;

/// <summary>
/// Gère les pouvoirs divins (GodState.AscensionState) : Foi est le pouvoir fondateur (toujours
/// disponible), qui déverrouille les colonnes indépendantes (Main/Oeil/Marche/Bras de Dieu,
/// Ascension Prestigieuse, Héritage) ; effets passifs (Main de Dieu, Oeil de Dieu, Bras de Dieu, Mémoire de
/// Dieu, Foi) et l'action ciblée Marche de Dieu.
/// Gère aussi l'Ascension elle-même (voir <see cref="PerformAscension"/>) : convertit l'essence
/// divine accumulée (DivineBonesController) en points divins et repart de zéro (île + prestige).
/// </summary>
public class AscensionController : IModifierProvider
{
    /// <summary>Nombre minimum d'essences divines requis pour pouvoir déclencher une Ascension.</summary>
    public const int MinDivineEssenceForAscension = 5;

    /// <summary>
    /// Manne versée par Corne d'Abondance, pour chaque ressource de base et par cycle de génération
    /// passive (HarvestController.PassiveResourceGenerationIntervalTicks, soit 1 s).
    /// </summary>
    public const int HornOfPlentyPassiveGenerationPerCycle = 20;

    /// <summary>
    /// Bâtiments uniques non-raciaux toujours choisissables comme bâtiment permanent d'Ascension
    /// (voir <see cref="SelectPermanentUniqueBuilding"/>) : un bâtiment accordé ainsi ne vit dans
    /// aucune ville (voir Civilization.SetAscensionGrantedUniqueBuildings) — seule une instance
    /// existe, enregistrée dans le cache Civilization._uniqueBuildingCache et créée à son niveau max
    /// absolu (voir Building.GetAbsoluteMaxLevel et Civilization.RebuildUniqueBuildingCache). Tous
    /// les bâtiments uniques vivants sont éligibles, sans exception : leur automatisation
    /// éventuelle interroge déjà ce cache via Civilization.GetUniqueBuilding plutôt que de parcourir
    /// les bâtiments physiques des villes (voir BuildingController/RoadController/CityBuilderController),
    /// donc rien ne dépend d'une présence physique en ville. Seul le bâtiment legacy DeepestMine
    /// (supprimé) faisait exception, faute d'effet de jeu. S'y ajoutent les bâtiments raciaux des
    /// races ayant déjà ascensionné — voir <see cref="PermanentUniqueBuildingChoices"/>.
    /// </summary>
    private static readonly IReadOnlyList<BuildingType> BasePermanentUniqueBuildingChoices = new[]
    {
        BuildingType.Academy,
        BuildingType.AdventurersGuild,
        BuildingType.ArcaneTower,
        BuildingType.ArtisansGuild,
        BuildingType.BlastFurnace,
        BuildingType.BuildersGuild,
        BuildingType.GrandTemple,
        BuildingType.HarvestersGuild,
        BuildingType.ImperialPort,
        BuildingType.TraderGuild,
        BuildingType.VolcanicForge,
        BuildingType.WarRoom,
    };

    /// <summary>
    /// Choix de bâtiment permanent actuels : la liste de base, plus le bâtiment racial de chaque
    /// race avec laquelle une Ascension a été effectuée (AscensionState.AscendedRaces) — ces
    /// bâtiments restent disponibles même en jouant une autre race.
    /// </summary>
    public IReadOnlyList<BuildingType> PermanentUniqueBuildingChoices
    {
        get
        {
            var choices = new List<BuildingType>(BasePermanentUniqueBuildingChoices);
            if (_ascensionState != null)
                foreach (var race in _ascensionState.AscendedRaces)
                    if (RaceDefinitions.Get(race).RacialBuilding is { } building && !choices.Contains(building))
                        choices.Add(building);
            return choices;
        }
    }

    private static readonly TerrainType[] RandomTerrainPool =
    {
        TerrainType.Forest, TerrainType.Hill, TerrainType.Plain, TerrainType.Mountain, TerrainType.Desert
    };

    private WorldState? _state;
    private GameClock? _clock;
    private GamePRNG? _prng;
    private HarvestController? _harvestController;
    private GodState? _godState;
    private AscensionState? _ascensionState;
    private CityBuilderController? _cityBuilderController;

    public event Action? OnModifiersChanged;

    /// <param name="cityBuilderController">
    /// Optionnel : requis uniquement pour que Marche de Dieu puisse détruire les villes que le terrain
    /// transformé ne permet plus d'occuper (voir <see cref="ApplyWalkOfGod"/>). Omis, la marche
    /// transforme le terrain sans jamais rien détruire.
    /// </param>
    public void Initialize(WorldState state, GameClock? clock, GamePRNG prng, HarvestController harvestController, GodState godState,
        CityBuilderController? cityBuilderController = null)
    {
        if (_clock != null)
            _clock.Advanced -= OnClockAdvanced;

        // Une nouvelle partie/île reconstruit son propre ModifierAggregator et se ré-enregistre
        // auprès de ce contrôleur (qui, lui, persiste) : on coupe les abonnements de l'ancien
        // aggregator pour éviter qu'il reste accroché indéfiniment.
        OnModifiersChanged = null;

        _state = state;
        _clock = clock;
        _prng = prng;
        _harvestController = harvestController;
        _godState = godState;
        _ascensionState = godState.AscensionState;
        _cityBuilderController = cityBuilderController;

        if (_clock != null)
            _clock.Advanced += OnClockAdvanced;
    }

    public bool IsPowerUnlocked(AscensionPowerId id) => _ascensionState?.UnlockedPowers.Contains(id) == true;

    /// <summary>
    /// Vrai si l'ordre de déblocage (colonne/Foi) autorise l'achat de ce pouvoir, indépendamment du
    /// coût en points divins — sert à l'UI pour distinguer "verrouillé par prérequis" de "points
    /// divins insuffisants" (voir <see cref="CanPurchasePower"/>).
    /// </summary>
    public bool ArePrerequisitesMet(AscensionPowerId id)
    {
        var def = AscensionPowerDefinitions.Get(id);
        if (def == null) return false;

        // Foi est le pouvoir fondateur : toujours disponible, sans prérequis.
        if (def.Column == AscensionPowerDefinition.FoundationColumn) return true;

        // Chaque colonne nécessite Foi, puis se débloque dans son propre ordre interne.
        if (!IsPowerUnlocked(AscensionPowerId.Faith)) return false;

        var column = AscensionPowerDefinitions.GetColumn(def.Column);
        int posInColumn = column.IndexOf(def);
        if (posInColumn <= 0) return true;

        return IsPowerUnlocked(column[posInColumn - 1].Id);
    }

    public bool CanPurchasePower(AscensionPowerId id)
    {
        if (_ascensionState == null || _godState == null || IsPowerUnlocked(id)) return false;

        var def = AscensionPowerDefinitions.Get(id);
        if (def == null) return false;

        return _godState.GodPoints >= def.GodPointCost && ArePrerequisitesMet(id);
    }

    public bool PurchasePower(AscensionPowerId id)
    {
        if (!CanPurchasePower(id)) return false;

        var def = AscensionPowerDefinitions.Get(id)!;
        _godState!.GodPoints -= def.GodPointCost;
        _ascensionState!.UnlockedPowers.Add(id);

        // Mémoire de Dieu rend immédiatement au joueur ce que les Ascensions passées lui avaient
        // repris : ses recherches répétables remontent au meilleur palier jamais atteint.
        if (id == AscensionPowerId.MemoryOfGod)
            RestoreRepeatableResearchToBest();

        // Ascension Prestigieuse est achetée juste après une Ascension, cycle où elle n'aurait
        // sinon rien donné : elle verse sa dotation tout de suite plutôt que de la faire attendre
        // jusqu'à l'Ascension suivante.
        if (id == AscensionPowerId.PrestigiousAscension)
            GrantPrestigiousAscensionPoints(_godState!);

        OnModifiersChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// Points de prestige versés par Ascension Prestigieuse : 1 par essence divine gagnée depuis le
    /// tout début de la partie (GodState.TotalDivineEssenceEarned, jamais remis à zéro). Versés au
    /// PrestigeState courant — celui du nouveau cycle quand l'appel vient de
    /// <see cref="PerformAscension(MainGameState, IslandParameters, RaceId)"/>. Ils comptent comme
    /// n'importe quel gain de prestige, y compris pour le palier d'île (PrestigeState.Tier).
    /// </summary>
    private static void GrantPrestigiousAscensionPoints(GodState godState)
    {
        var prestigeState = godState.PrestigeState;
        if (prestigeState == null) return;

        int points = godState.TotalDivineEssenceEarned;
        if (points <= 0) return;

        prestigeState.PrestigePoints += points;
        prestigeState.TotalPrestigePointsEarned += points;
    }

    /// <summary>
    /// Enregistre dans AscensionState.BestRepeatCounts le meilleur palier atteint par chaque
    /// recherche répétable de <paramref name="tree"/>. Appelé à chaque Ascension, avant que le
    /// PrestigeState (et donc l'arbre de recherche) ne soit remplacé — y compris quand Mémoire de
    /// Dieu n'est pas encore acquise, sans quoi le pouvoir ne saurait rien des cycles précédents.
    /// </summary>
    private static void RecordBestRepeatCounts(AscensionState ascensionState, TechnologyTree tree)
    {
        foreach (var (techId, count) in tree.RepeatCounts)
        {
            if (TechnologyDefinitions.Get(techId)?.Repeatable != true) continue;
            if (!ascensionState.BestRepeatCounts.TryGetValue(techId, out int best) || count > best)
                ascensionState.BestRepeatCounts[techId] = count;
        }
    }

    /// <summary>
    /// Ramène chaque recherche répétable de l'arbre courant au meilleur palier jamais atteint
    /// (AscensionState.BestRepeatCounts) : la recherche est marquée complétée et ses modificateurs
    /// sont recumulés d'autant. Appelé à l'achat de Mémoire de Dieu, puis au début de chaque cycle
    /// d'Ascension tant que le pouvoir est acquis (voir <see cref="PerformAscension"/>) — c'est ce
    /// qui fait que les recherches répétables ne sont plus remises à zéro par l'Ascension.
    ///
    /// <para>Une recherche ainsi restaurée compte comme complétée sans que ses prérequis le soient :
    /// les recherches qui en dépendent redeviennent donc accessibles dès le début du cycle, leur
    /// coût en points de recherche restant le seul verrou.</para>
    /// </summary>
    private void RestoreRepeatableResearchToBest()
    {
        if (_ascensionState == null || _godState?.PrestigeState == null) return;
        RestoreRepeatableResearchToBest(_ascensionState, _godState.PrestigeState.TechnologyTree);
    }

    private static void RestoreRepeatableResearchToBest(AscensionState ascensionState, TechnologyTree tree)
    {
        bool changed = false;
        foreach (var (techId, best) in ascensionState.BestRepeatCounts)
        {
            if (best <= 0 || TechnologyDefinitions.Get(techId)?.Repeatable != true) continue;

            int current = tree.RepeatCounts.TryGetValue(techId, out var c) ? c : 0;
            if (current >= best) continue;

            tree.RepeatCounts[techId] = best;
            if (!tree.CompletedTechnologies.Contains(techId))
                tree.CompletedTechnologies.Add(techId);
            changed = true;
        }

        if (changed)
            tree.RebuildModifiers();
    }

    /// <summary>Race jouée pendant le cycle d'Ascension en cours (Humains par défaut).</summary>
    public RaceId SelectedRace => _ascensionState?.SelectedRace ?? RaceId.Human;

    /// <summary>Races avec lesquelles une Ascension a déjà été effectuée (voir AscensionState.AscendedRaces).</summary>
    public IReadOnlyCollection<RaceId> AscendedRaces =>
        (IReadOnlyCollection<RaceId>?)_ascensionState?.AscendedRaces ?? Array.Empty<RaceId>();

    /// <summary>
    /// Colonnes de pouvoirs divins qui conditionnent le déblocage des races : les 4 premières
    /// (Main/Oeil/Marche/Bras de Dieu). Volontairement figé plutôt qu'aligné sur
    /// AscensionPowerDefinitions.ColumnCount : une colonne ajoutée après coup reverrouillerait le
    /// choix de race des joueurs qui l'ont déjà acquis.
    /// </summary>
    private const int RaceUnlockColumnCount = 4;

    /// <summary>
    /// Vrai si le choix de race est débloqué : toute la première rangée de pouvoirs divins achetée,
    /// c'est-à-dire le premier pouvoir de chacune des <see cref="RaceUnlockColumnCount"/> colonnes
    /// d'origine (Main/Oeil/Marche/Bras de Dieu).
    /// </summary>
    public bool IsRaceSelectionUnlocked =>
        Enumerable.Range(0, RaceUnlockColumnCount).All(col =>
        {
            var column = AscensionPowerDefinitions.GetColumn(col);
            return column.Count > 0 && IsPowerUnlocked(column[0].Id);
        });

    /// <summary>
    /// Vrai si les races avancées (Géants, Garudas, Sirènes, Elfes noirs) sont débloquées : toute la
    /// seconde rangée de pouvoirs divins achetée (le deuxième pouvoir de chacune de ces colonnes).
    /// </summary>
    public bool AreAdvancedRacesUnlocked =>
        IsRaceSelectionUnlocked &&
        Enumerable.Range(0, RaceUnlockColumnCount).All(col =>
        {
            var column = AscensionPowerDefinitions.GetColumn(col);
            return column.Count < 2 || IsPowerUnlocked(column[1].Id);
        });

    /// <summary>
    /// Races choisissables à la prochaine Ascension : Humains toujours ; les races de base une fois
    /// la première rangée de pouvoirs divins complète ; les races avancées une fois la seconde rangée
    /// complète. Toutes les races déclarées sont aujourd'hui implémentées ; un éventuel stub
    /// (RaceDefinition.IsImplemented faux, c'est-à-dire sans bâtiment racial) n'apparaîtrait jamais ici.
    /// </summary>
    public IReadOnlyList<RaceId> GetSelectableRaces()
    {
        if (!IsRaceSelectionUnlocked)
            return new[] { RaceId.Human };

        bool advancedUnlocked = AreAdvancedRacesUnlocked;
        return RaceDefinitions.All
            .Where(r => r.IsImplemented && (r.Tier == RaceTier.Base || advancedUnlocked))
            .Select(r => r.Id)
            .ToList();
    }

    /// <summary>Bâtiments uniques permanents actuellement choisis (voir SelectPermanentUniqueBuilding).</summary>
    public IReadOnlyCollection<BuildingType> PermanentUniqueBuildings =>
        (IReadOnlyCollection<BuildingType>?)_ascensionState?.PermanentUniqueBuildings ?? Array.Empty<BuildingType>();

    /// <summary>
    /// Nombre d'emplacements de bâtiments uniques permanents accordés par chaque Ascension déjà
    /// effectuée : 0 sans pouvoir divin, 1 avec Héritage Divin, 2 avec Héritage Éternel. C'est la
    /// colonne Héritage qui ouvre entièrement le système — aucun emplacement n'est gratuit.
    /// </summary>
    private int PermanentUniqueBuildingSlotsPerAscension =>
        (IsPowerUnlocked(AscensionPowerId.DivineLegacy) ? 1 : 0)
        + (IsPowerUnlocked(AscensionPowerId.EternalLegacy) ? 1 : 0);

    /// <summary>
    /// Nombre d'emplacements de bâtiments uniques permanents disponibles :
    /// <see cref="PermanentUniqueBuildingSlotsPerAscension"/> par Ascension déjà effectuée (voir
    /// AscensionState.AscensionsPerformed). Les deux pouvoirs de la colonne Héritage sont donc
    /// rétroactifs : ils comptent les Ascensions déjà accomplies, pas seulement les suivantes.
    /// </summary>
    public int PermanentUniqueBuildingSlots =>
        (_ascensionState?.AscensionsPerformed ?? 0) * PermanentUniqueBuildingSlotsPerAscension;

    /// <summary>
    /// Choisit un bâtiment unique permanent supplémentaire accordé par l'Ascension, tant qu'un
    /// emplacement libre reste disponible (voir <see cref="PermanentUniqueBuildingSlots"/>). Le choix
    /// est mémorisé cross-prestige (AscensionState.PermanentUniqueBuildings) mais ne prend effet qu'au
    /// prochain début d'île — voir <see cref="ApplyPermanentUniqueBuildingToCivilization"/>, appelé
    /// par MainGameController.InitializeControllersForCurrentIsland.
    /// </summary>
    public bool SelectPermanentUniqueBuilding(BuildingType type)
    {
        if (_ascensionState == null || !PermanentUniqueBuildingChoices.Contains(type)) return false;
        if (_ascensionState.PermanentUniqueBuildings.Contains(type)) return true;
        if (_ascensionState.PermanentUniqueBuildings.Count >= PermanentUniqueBuildingSlots) return false;

        _ascensionState.PermanentUniqueBuildings.Add(type);
        return true;
    }

    /// <summary>Retire un bâtiment unique permanent précédemment choisi, libérant son emplacement.</summary>
    public bool DeselectPermanentUniqueBuilding(BuildingType type)
        => _ascensionState?.PermanentUniqueBuildings.Remove(type) ?? false;

    /// <summary>
    /// Applique à la civilisation du joueur de l'île courante les bâtiments uniques permanents
    /// choisis (voir SelectPermanentUniqueBuilding). À appeler à chaque début d'île (nouvelle partie,
    /// prestige, ascension, redémarrage) — voir MainGameController.InitializeControllersForCurrentIsland.
    /// </summary>
    public void ApplyPermanentUniqueBuildingToCivilization()
    {
        if (_state == null) return;
        EnforcePermanentUniqueBuildingSlots();
        _state.PlayerCivilization.SetAscensionGrantedUniqueBuildings(PermanentUniqueBuildings);
    }

    /// <summary>
    /// Ramène le nombre de bâtiments choisis au nombre d'emplacements réellement disponibles. Sans
    /// cela, une sauvegarde antérieure à la colonne Héritage — où chaque Ascension donnait un
    /// emplacement gratuitement — continuerait d'accorder ses bâtiments alors que le pouvoir qui les
    /// autorise n'a pas été acheté. Les choix retirés ne coûtent rien à refaire : la sélection est
    /// gratuite et réversible (voir <see cref="DeselectPermanentUniqueBuilding"/>).
    /// </summary>
    private void EnforcePermanentUniqueBuildingSlots()
    {
        var chosen = _ascensionState?.PermanentUniqueBuildings;
        if (chosen == null) return;

        int slots = PermanentUniqueBuildingSlots;
        while (chosen.Count > slots)
            chosen.Remove(chosen.Last());
    }

    public bool CanAscend(GodState godState) => GetEffectiveDivineEssence(godState) >= MinDivineEssenceForAscension;

    /// <summary>
    /// Essence divine effective pour l'Ascension : la somme de GodState.DivineEssence (essences
    /// gagnées ce run) et de GodState.DivineEssenceReliquaryFloor (essences garanties par le
    /// Reliquaire). Les deux comptent identiquement pour le seuil d'Ascension (<see cref="CanAscend"/>)
    /// et les points divins gagnés (<see cref="GetGodPointsGain"/>) — c'est uniquement pour le
    /// plafond de corruption (<see cref="GetDivineEssenceCap"/>) et le coût de Purification des Os
    /// Divins que les essences du Reliquaire ne comptent pas (voir GodState.DivineEssence).
    /// </summary>
    public int GetEffectiveDivineEssence(GodState godState) => godState.DivineEssence + godState.DivineEssenceReliquaryFloor;

    /// <summary>
    /// Plafond d'essence divine gagnable ce cycle-ci en purifiant de nouveaux Os Divins
    /// (GodState.DivineEssence uniquement — voir <see cref="GetEffectiveDivineEssence"/>) : le niveau
    /// de corruption actuel de l'île (voir DivineBones.GetEssenceCap, PrestigeState.CurrentCorruptionLevel
    /// — chaque Os Divins fige ce niveau à sa génération, ce plafond est donc celui d'un Os Divins
    /// fraîchement apparu). Pour l'obtenir plus haut, il faut prestige pour augmenter la corruption.
    /// </summary>
    public int GetDivineEssenceCap() => Math.Max(0, _godState?.PrestigeState?.CurrentCorruptionLevel ?? 0);

    /// <summary>
    /// Vrai si le Reliquaire (Reliquaire Sacré/Renforcé) est débloqué, c'est-à-dire si la
    /// civilisation conserve au moins 1 essence divine au prestige — sert à n'afficher la réserve du
    /// Reliquaire sur l'écran Ascension que lorsqu'elle a un sens.
    /// </summary>
    public bool HasDivineEssenceReliquary => (_state?.PlayerCivilization.DivineEssenceKeptOnPrestige ?? 0) > 0;

    /// <summary>
    /// Essences divines actuellement garanties par le Reliquaire (voir GodState.DivineEssenceReliquaryFloor) :
    /// survivent à la perte de la dernière ville dans les Abysses, et ne comptent ni dans
    /// <see cref="GetDivineEssenceCap"/> ni dans le coût de Purification des Os Divins suivants.
    /// </summary>
    public int GetDivineEssenceReliquaryAmount(GodState godState) => godState.DivineEssenceReliquaryFloor;

    /// <summary>
    /// Niveau de la Nécropole bâtie sur l'île courante (0 s'il n'y en a pas) — chaque niveau majore
    /// de 15% les points divins de l'Ascension (voir <see cref="GetGodPointsGain"/>).
    /// </summary>
    public int GetNecropolisLevel() => _state?.Features.OfType<Necropolis>().FirstOrDefault()?.Level ?? 0;

    /// <summary>
    /// Bonus de points divins accordé par la Nécropole de l'île courante (0.15 par niveau, 0 sans
    /// Nécropole) — voir <see cref="GetGodPointsGain"/>.
    /// </summary>
    public double GetNecropolisAscensionBonus() => Necropolis.GetAscensionGainBonusForLevel(GetNecropolisLevel());

    /// <summary>
    /// Points divins qu'une Ascension immédiate rapporterait : 1 par essence divine effective détenue
    /// (<see cref="GetEffectiveDivineEssence"/>, Reliquaire inclus), majorés de 15% par niveau de la
    /// Nécropole bâtie sur l'île courante (arrondi à l'entier inférieur, voir <see cref="Necropolis"/>).
    /// Utilisé par <see cref="PerformAscension"/> et par l'écran Ascension, qui doivent afficher et
    /// créditer exactement le même nombre.
    /// </summary>
    public int GetGodPointsGain(GodState godState)
        => (int)Math.Floor(GetEffectiveDivineEssence(godState) * Necropolis.GetAscensionGainMultiplierForLevel(GetNecropolisLevel()));

    /// <summary>
    /// Convertit toute l'essence divine accumulée en points divins (1 pour 1, majoré par la Nécropole
    /// de l'île courante, voir <see cref="GetGodPointsGain"/>) — puis remet DivineEssence à zéro, ce
    /// qui réinitialise le coût de Purification des Os Divins
    /// (voir DivineBones.EssenceAlreadyCollected) — puis efface la progression de la partie en
    /// cours : le PrestigeState (recherches, points de prestige, niveau de corruption,
    /// historique...) est entièrement remplacé par un nouveau, câblé sur une toute nouvelle
    /// première île. GodState.AscensionState (pouvoirs débloqués) et les points divins survivent,
    /// seuls but de la manœuvre.
    /// </summary>
    public void PerformAscension(MainGameState mainGameState, IslandParameters firstIslandParameters)
        => PerformAscension(mainGameState, firstIslandParameters, SelectedRace);

    /// <summary>
    /// Comme <see cref="PerformAscension(MainGameState, IslandParameters)"/>, en choisissant la race
    /// du prochain cycle : la race jouée jusqu'ici rejoint AscendedRaces (débloquant définitivement
    /// son bâtiment racial dans les choix permanents), puis <paramref name="chosenRace"/> devient la
    /// race active. Le choix doit figurer dans <see cref="GetSelectableRaces"/>.
    /// </summary>
    public void PerformAscension(MainGameState mainGameState, IslandParameters firstIslandParameters, RaceId chosenRace)
    {
        var godState = mainGameState.GodState;
        if (!CanAscend(godState))
            throw new InvalidOperationException("Ascension is not available.");
        if (!GetSelectableRaces().Contains(chosenRace))
            throw new InvalidOperationException($"Race {chosenRace} is not selectable.");

        // La Nécropole de l'île courante majore la conversion (+15% par niveau) : elle doit donc être
        // lue avant que l'île ne soit remplacée plus bas.
        int godPointsGained = GetGodPointsGain(godState);
        godState.GodPoints += godPointsGained;
        godState.TotalGodPointsEarned += godPointsGained;
        godState.DivineEssence = 0;
        godState.DivineEssenceReliquaryFloor = 0;
        godState.AscensionState.AscensionsPerformed++;

        // La race qui accomplit l'Ascension marque l'histoire : son bâtiment racial devient un
        // choix permanent pour tous les cycles futurs, quelle que soit la race jouée.
        godState.AscensionState.AscendedRaces.Add(godState.AscensionState.SelectedRace);
        godState.AscensionState.SelectedRace = chosenRace;

        RecordAscensionCycleStats(mainGameState, godState);

        // Meilleurs paliers de recherches répétables du cycle qui s'achève : à relever avant que le
        // PrestigeState ne soit remplacé, c'est la mémoire sur laquelle s'appuie Mémoire de Dieu.
        if (godState.PrestigeState != null)
            RecordBestRepeatCounts(godState.AscensionState, godState.PrestigeState.TechnologyTree);

        var generator = new IslandMapGenerator(mainGameState.WorldPRNG);
        var worldState = generator.GenerateWorldState(
            firstIslandParameters,
            mainGameState.Clock.CurrentTick,
            startTick: mainGameState.Clock.CurrentTick,
            race: RaceDefinitions.Get(chosenRace))
            ?? throw new InvalidOperationException("Failed to generate island for ascension.");

        godState.PrestigeState = new PrestigeState(worldState);
        GrantFreePrestigeVertices(godState.PrestigeState, chosenRace);

        // Ascension Prestigieuse : le nouveau cycle ne repart plus la bourse vide, mais avec 1 point
        // de prestige par essence divine jamais gagnée.
        if (IsPowerUnlocked(AscensionPowerId.PrestigiousAscension))
            GrantPrestigiousAscensionPoints(godState);

        // Mémoire de Dieu : les recherches répétables ne sont pas remises à zéro par l'Ascension —
        // le nouvel arbre repart directement au meilleur palier jamais atteint.
        if (IsPowerUnlocked(AscensionPowerId.MemoryOfGod))
            RestoreRepeatableResearchToBest(godState.AscensionState, godState.PrestigeState.TechnologyTree);

        godState.AscensionState.CycleStartTick = mainGameState.Clock.CurrentTick;
        godState.AscensionState.CycleStartResearchCompleted = mainGameState.GameRecord.TotalResearchCompleted;
    }

    /// <summary>
    /// Archive les statistiques du cycle d'Ascension qui vient de se terminer (voir onglet Stats /
    /// Ascension) avant que PerformAscension ne remplace PrestigeState : historique plafonné à 5
    /// entrées, plus mise à jour des records cross-ascension (AscensionState.Max*).
    /// </summary>
    private static void RecordAscensionCycleStats(MainGameState mainGameState, GodState godState)
    {
        var prestigeState = godState.PrestigeState;
        if (prestigeState == null) return;

        var ascensionState = godState.AscensionState;
        var stats = new AscensionRunStats
        {
            MaxIslandTierReached = prestigeState.Tier,
            MaxCorruptionReached = prestigeState.CurrentCorruptionLevel,
            TickDuration = mainGameState.Clock.CurrentTick - ascensionState.CycleStartTick,
            ResearchCompleted = mainGameState.GameRecord.TotalResearchCompleted - ascensionState.CycleStartResearchCompleted,
            FinalPrestigePoints = prestigeState.TotalPrestigePointsEarned,
        };

        ascensionState.RunHistory.Add(stats);
        while (ascensionState.RunHistory.Count > 5)
            ascensionState.RunHistory.RemoveAt(0);

        ascensionState.MaxIslandTierReached = Math.Max(ascensionState.MaxIslandTierReached, stats.MaxIslandTierReached);
        ascensionState.MaxCorruptionReached = Math.Max(ascensionState.MaxCorruptionReached, stats.MaxCorruptionReached);
        ascensionState.MaxPlaytimeInSingleAscension = Math.Max(ascensionState.MaxPlaytimeInSingleAscension, stats.TickDuration);
        ascensionState.MaxResearchInSingleAscension = Math.Max(ascensionState.MaxResearchInSingleAscension, stats.ResearchCompleted);
        ascensionState.MaxPrestigePointsInSingleAscension = Math.Max(ascensionState.MaxPrestigePointsInSingleAscension, stats.FinalPrestigePoints);
    }

    /// <summary>
    /// Vertex de la carte de prestige offerts (ajoutés à PurchasedVertices sans coût) au début de
    /// chaque cycle d'Ascension : le vertex central dès que Foi (le premier pouvoir divin) est
    /// débloquée, plus ses 3 voisins (Caserne, Port &amp; Marché, Laboratoire) une fois le choix de
    /// race débloqué — le Marché de départ ainsi garanti permet d'acheter la ressource que le
    /// terrain de départ de la race ne produit pas (ex. la brique des Nains). S'y ajoutent les
    /// vertex propres à la race choisie (RaceDefinition.FreePrestigeVertices), eux aussi sans
    /// contrainte de contiguïté : les Elfes noirs partent ainsi avec Culture Fongique, seule source
    /// de nourriture viable pour un départ souterrain.
    /// </summary>
    private void GrantFreePrestigeVertices(PrestigeState prestigeState, RaceId race)
    {
        if (!IsPowerUnlocked(AscensionPowerId.Faith)) return;

        if (!prestigeState.PurchasedVertices.Contains(Model.Prestige.PrestigeMap.PrestigeMap.CentralVertex))
            prestigeState.PurchasedVertices.Add(Model.Prestige.PrestigeMap.PrestigeMap.CentralVertex);

        if (!IsRaceSelectionUnlocked) return;

        foreach (var neighbor in Expand.PrestigeMapController.DefaultMap.GetNeighbors(Model.Prestige.PrestigeMap.PrestigeMap.CentralVertex))
            if (!prestigeState.PurchasedVertices.Contains(neighbor.Coord))
                prestigeState.PurchasedVertices.Add(neighbor.Coord);

        foreach (var vertex in RaceDefinitions.Get(race).FreePrestigeVertices)
            if (!prestigeState.PurchasedVertices.Contains(vertex))
                prestigeState.PurchasedVertices.Add(vertex);
    }

    public IEnumerable<Modifier> GetModifiers()
    {
        if (IsPowerUnlocked(AscensionPowerId.Faith))
        {
            yield return new Modifier(Modifier.ECategory.BUILDING_MAX_LEVEL, "Temple", Modifier.EType.ADDITIVE, 3);
            yield return new Modifier(Modifier.ECategory.UNLOCK_DOMINION, Modifier.EType.ADDITIVE, 1.0);
        }

        if (IsPowerUnlocked(AscensionPowerId.DivineInventory))
            yield return new Modifier(Modifier.ECategory.STORAGE_CAPACITY_MULTIPLIER, Modifier.EType.ADDITIVE, 10.0);

        if (IsPowerUnlocked(AscensionPowerId.ArmOfGod))
            yield return new Modifier(Modifier.ECategory.SOLDIER_ATTACK_DAMAGE, Modifier.EType.ADDITIVE, 1);

        if (IsPowerUnlocked(AscensionPowerId.MemoryOfGod))
            yield return new Modifier(Modifier.ECategory.REPEATABLE_RESEARCH_SCALING_REDUCTION, Modifier.EType.ADDITIVE, 0.5);

        if (IsPowerUnlocked(AscensionPowerId.HornOfPlenty))
        {
            // +100% de rendement sans SubCategory : toute récolte automatique, quel que soit le
            // bâtiment, produit au moins double. Le bonus s'additionne à ceux des recherches et des
            // bâtiments et peut dépasser 100% — au-delà, la partie entière est garantie et seul le
            // reste est tiré au sort (voir HarvestController.PerformAutomaticProductionHarvests).
            yield return new Modifier(Modifier.ECategory.HARVEST_PRODUCTION_BONUS, Modifier.EType.ADDITIVE, 100);

            foreach (var resource in ResourceUtils.BasicResources)
                yield return new Modifier(Modifier.ECategory.PASSIVE_RESOURCE_GENERATION, resource.ToString(),
                    Modifier.EType.ADDITIVE, HornOfPlentyPassiveGenerationPerCycle);
        }

        if (IsPowerUnlocked(AscensionPowerId.WrathOfGod))
            yield return new Modifier(Modifier.ECategory.ATTACK_SPEED, Modifier.EType.ADDITIVE, 1.0);

        // Bonus/malus de la race jouée pendant ce cycle (voir RaceDefinitions).
        foreach (var modifier in RaceDefinitions.Get(SelectedRace).Modifiers)
            yield return modifier;
    }

    /// <summary>Niveau de Dominion minimum requis sur l'hex visé pour utiliser Marche de Dieu.</summary>
    public const int WalkOfGodMinDominionLevel = 2;

    /// <summary>
    /// Hexs ciblables par Marche de Dieu : les hexs de la carte de surface (Eau incluse) portant un
    /// Dominion de niveau <see cref="WalkOfGodMinDominionLevel"/> ou plus — Dieu ne marche que là où
    /// son emprise est déjà établie. Le Dominion pouvant s'étendre sur l'eau, c'est ainsi qu'on
    /// terraforme la mer : y faire croître le Dominion, puis y marcher.
    /// </summary>
    public IReadOnlyList<HexCoord> GetWalkOfGodTargetHexes()
    {
        if (_state == null) return Array.Empty<HexCoord>();

        var map = _state.GetMapForZ(IslandMap.SurfaceLayer);
        if (map == null) return Array.Empty<HexCoord>();

        return map.Tiles.Values
            .Select(t => t.Coord)
            .Where(c => GetWalkOfGodDominion(c) != null)
            .ToList();
    }

    /// <summary>Dominion de niveau suffisant (voir <see cref="WalkOfGodMinDominionLevel"/>) sur l'hex, ou null.</summary>
    private Dominion? GetWalkOfGodDominion(HexCoord hex) =>
        _state?.GetFeaturesAt(hex).OfType<Dominion>().FirstOrDefault(d => d.Level >= WalkOfGodMinDominionLevel);

    /// <summary>
    /// Coût en points de prestige de la prochaine utilisation d'un pouvoir divin ciblé (Marche,
    /// Présence, Poing de Dieu), calculé depuis le nombre d'usages déjà consommés depuis le dernier
    /// prestige : le premier usage est <b>gratuit</b>, le deuxième coûte 1, le troisième 2, puis le
    /// coût <b>double</b> à chaque usage supplémentaire (4, 8, 16...).
    ///
    /// <para>Le décalage est plafonné pour ne pas déborder l'int : passé une trentaine d'usages dans
    /// un même prestige, le coût est de toute façon hors d'atteinte de la cagnotte.</para>
    /// </summary>
    public static int TargetedPowerCost(int usesSinceLastPrestige)
        => usesSinceLastPrestige <= 0 ? 0 : 1 << Math.Min(usesSinceLastPrestige - 1, 30);

    /// <summary>
    /// Coût en points de prestige de la prochaine utilisation de Marche de Dieu : la première
    /// utilisation depuis le dernier prestige est <b>gratuite</b>, la deuxième coûte 1, la troisième 2,
    /// puis le coût double à chaque marche (4, 8, 16... — voir <see cref="TargetedPowerCost"/>,
    /// PrestigeState.WalkOfGodUsesSinceLastPrestige remis à zéro à chaque prestige).
    ///
    /// <para>La gratuité du premier usage n'est pas qu'un adoucissement : le coût se paie sur la
    /// cagnotte de prestige (PrestigeState.PrestigePoints), qui vaut zéro sur la première île d'un
    /// cycle d'Ascension. Sans elle, le pouvoir serait inutilisable précisément là où une race à
    /// terrain requis en a le plus besoin pour ouvrir son premier emplacement de ville.</para>
    /// </summary>
    public int GetWalkOfGodCost() => TargetedPowerCost(_godState?.PrestigeState?.WalkOfGodUsesSinceLastPrestige ?? 0);

    /// <summary>Vrai si Marche de Dieu est débloquée et que le joueur a assez de points de prestige pour son prochain coût.</summary>
    public bool CanUseWalkOfGod()
    {
        var prestigeState = _godState?.PrestigeState;
        return prestigeState != null && IsPowerUnlocked(AscensionPowerId.WalkOfGod) && prestigeState.PrestigePoints >= GetWalkOfGodCost();
    }

    /// <summary>
    /// Terrain de prédilection de la race jouée — celui dont ses villes exigent l'adjacence (voir
    /// <see cref="RaceDefinition.RequiredAdjacentTerrain"/> : Forêt pour les Elfes, Montagne pour les
    /// Nains, Eau pour les Sirènes). <c>null</c> pour les races sans contrainte de placement, qui
    /// n'ont donc rien de particulier à faire pousser.
    /// </summary>
    public TerrainType? FavouredTerrain => RaceDefinitions.Get(SelectedRace).RequiredAdjacentTerrain;

    /// <summary>
    /// Transforme le terrain d'un hex de surface portant un Dominion de niveau
    /// <see cref="WalkOfGodMinDominionLevel"/> ou plus, contre un coût en points de prestige croissant
    /// (voir <see cref="GetWalkOfGodCost"/>, premier usage gratuit) plus 1 niveau du Dominion de l'hex
    /// (jamais retiré : il reste au moins au niveau 1 après la marche).
    ///
    /// <para>Le terrain obtenu dépend de la race jouée. Pour une race à terrain de prédilection
    /// (<see cref="FavouredTerrain"/>), marcher sur <b>n'importe quel autre terrain</b> y fait pousser
    /// ce terrain — c'est le seul moyen pour un Elfe d'ouvrir un emplacement de ville là où il n'y a
    /// pas de forêt, et le pouvoir serait sans valeur pour lui s'il fallait retomber au hasard sur le
    /// bon terrain (1 chance sur 4). Marcher sur le terrain de prédilection lui-même retire ce
    /// déterminisme et retombe sur un terrain tiré au sort : la marche transforme, elle ne conserve
    /// pas. Pour une race sans contrainte, tout tirage est aléatoire, comme avant.</para>
    ///
    /// <para>Si l'hex ciblé était de l'eau, les hexs voisins qui n'existaient pas encore sont créés en
    /// tant qu'eau.</para>
    /// </summary>
    public bool ApplyWalkOfGod(HexCoord hex)
    {
        if (_state == null || _prng == null || !CanUseWalkOfGod()) return false;

        var map = _state.GetMapFor(hex);
        var tile = map?.GetTile(hex);
        if (tile == null) return false;

        var dominion = GetWalkOfGodDominion(hex);
        if (dominion == null) return false;

        bool wasWater = tile.TerrainType == TerrainType.Water;

        TerrainType newType;
        if (FavouredTerrain is { } favoured && tile.TerrainType != favoured)
        {
            newType = favoured;
        }
        else
        {
            do { newType = RandomTerrainPool[_prng.Next(RandomTerrainPool.Length)]; }
            while (newType == tile.TerrainType);
        }

        tile.TerrainType = newType;
        _state.NotifyTerrainChanged();

        if (wasWater)
        {
            bool addedTiles = false;
            foreach (var neighbor in hex.Neighbors())
            {
                if (!map!.HasTile(neighbor))
                {
                    map.AddTile(new HexTile(neighbor, TerrainType.Water));
                    addedTiles = true;
                }
            }

            if (addedTiles)
                _state.Visibility.RecalculateFor(_state.PlayerCivilization.Index);
        }

        dominion.Level--;

        var prestigeState = _godState!.PrestigeState!;
        prestigeState.PrestigePoints -= GetWalkOfGodCost();
        prestigeState.WalkOfGodUsesSinceLastPrestige++;

        // Le terrain façonné peut rendre une ville inoccupable — engloutie sous l'eau, ou privée du
        // terrain qu'exige sa race. Dieu ne fait pas d'exception pour ses fidèles : elle tombe.
        _cityBuilderController?.DestroyCitiesInvalidatedByTerrain();

        return true;
    }

    /// <summary>Points divins appliqués par Présence de Dieu sur l'hex visé.</summary>
    public const int PresenceOfGodCenterPoints = 5;

    /// <summary>Points divins appliqués par Présence de Dieu sur chacun des 6 hexs voisins.</summary>
    public const int PresenceOfGodNeighborPoints = 3;

    /// <summary>
    /// Hexs ciblables par Présence de Dieu : tous les hexs de la carte de surface, Eau incluse —
    /// la Corruption et le Dominion peuvent exister sur l'eau (voir CorruptionController.IsValidHex),
    /// et semer du Dominion en mer est le prélude à la terraformation par Marche de Dieu.
    /// </summary>
    public IReadOnlyList<HexCoord> GetPresenceOfGodTargetHexes()
    {
        if (_state == null) return Array.Empty<HexCoord>();

        var map = _state.GetMapForZ(IslandMap.SurfaceLayer);
        if (map == null) return Array.Empty<HexCoord>();

        return map.Tiles.Values
            .Select(t => t.Coord)
            .ToList();
    }

    /// <summary>
    /// Coût en points de prestige de la prochaine utilisation de Présence de Dieu : même modèle que
    /// Marche de Dieu, première manifestation gratuite depuis le dernier prestige, puis 1, 2, 4, 8...
    /// — voir PrestigeState.PresenceOfGodUsesSinceLastPrestige, remis à zéro à chaque prestige. La
    /// gratuité du premier usage a la même raison d'être que pour Marche de Dieu : la cagnotte de
    /// prestige vaut zéro sur la première île d'un cycle d'Ascension, où repousser la Corruption
    /// initiale est justement le plus utile.
    /// </summary>
    public int GetPresenceOfGodCost() => TargetedPowerCost(_godState?.PrestigeState?.PresenceOfGodUsesSinceLastPrestige ?? 0);

    /// <summary>Vrai si Présence de Dieu est débloquée et que le joueur a assez de points de prestige pour son prochain coût.</summary>
    public bool CanUsePresenceOfGod()
    {
        var prestigeState = _godState?.PrestigeState;
        return prestigeState != null && IsPowerUnlocked(AscensionPowerId.PresenceOfGod) && prestigeState.PrestigePoints >= GetPresenceOfGodCost();
    }

    /// <summary>
    /// Manifeste la Présence de Dieu sur une zone : <see cref="PresenceOfGodCenterPoints"/> points
    /// sur l'hex visé, <see cref="PresenceOfGodNeighborPoints"/> sur chacun de ses 6 voisins, contre
    /// un coût en points de prestige croissant (voir <see cref="GetPresenceOfGodCost"/>). Sur chaque
    /// hex, les points dissipent d'abord la Corruption niveau par niveau ; le reliquat pose ou
    /// renforce le Dominion d'autant.
    /// </summary>
    public bool ApplyPresenceOfGod(HexCoord hex)
    {
        if (_state == null || !CanUsePresenceOfGod()) return false;

        if (_state.GetMapFor(hex)?.GetTile(hex) == null) return false;

        ApplyPresencePoints(hex, PresenceOfGodCenterPoints);
        foreach (var neighbor in hex.Neighbors())
            ApplyPresencePoints(neighbor, PresenceOfGodNeighborPoints);

        var prestigeState = _godState!.PrestigeState!;
        prestigeState.PrestigePoints -= GetPresenceOfGodCost();
        prestigeState.PresenceOfGodUsesSinceLastPrestige++;

        return true;
    }

    private void ApplyPresencePoints(HexCoord hex, int points)
    {
        // Hexs hors carte : la Présence n'y a pas d'effet (voisins en bord de carte).
        if (_state!.GetMapFor(hex)?.GetTile(hex) == null) return;

        var corruption = _state.GetFeaturesAt(hex).OfType<Corruption>().FirstOrDefault();
        if (corruption != null)
        {
            int dispelled = Math.Min(points, corruption.Level);
            corruption.Level -= dispelled;
            points -= dispelled;
            if (corruption.Level <= 0)
                _state.RemoveFeature(corruption);
        }

        if (points <= 0) return;

        var dominion = _state.GetFeaturesAt(hex).OfType<Dominion>().FirstOrDefault();
        if (dominion != null)
            dominion.Level += points;
        else
            _state.AddFeature(new Dominion(hex, level: points));
    }

    /// <summary>Dégâts infligés par Poing de Dieu au monstre visé et à chaque ville ennemie adjacente.</summary>
    public const int FistOfGodDamage = 100;

    /// <summary>
    /// Hexs ciblables par Poing de Dieu : tous les hexs du calque actuellement affiché. Contrairement
    /// à Marche de Dieu et Présence de Dieu, le pouvoir n'est pas limité à la surface — les monstres
    /// des profondeurs sont précisément ceux contre lesquels il vaut le plus.
    /// </summary>
    public IReadOnlyList<HexCoord> GetFistOfGodTargetHexes()
    {
        if (_state == null) return Array.Empty<HexCoord>();

        var map = _state.GetMapForZ(_state.CurrentViewedLayer);
        if (map == null) return Array.Empty<HexCoord>();

        return map.Tiles.Values
            .Select(t => t.Coord)
            .ToList();
    }

    /// <summary>
    /// Coût en points de prestige de la prochaine utilisation de Poing de Dieu : même modèle que
    /// Marche de Dieu (premier coup gratuit depuis le dernier prestige, puis 1, 2, 4, 8... — voir
    /// PrestigeState.FistOfGodUsesSinceLastPrestige, remis à zéro à chaque prestige).
    /// </summary>
    public int GetFistOfGodCost() => TargetedPowerCost(_godState?.PrestigeState?.FistOfGodUsesSinceLastPrestige ?? 0);

    /// <summary>Vrai si Poing de Dieu est débloqué et que le joueur a assez de points de prestige pour son prochain coût.</summary>
    public bool CanUseFistOfGod()
    {
        var prestigeState = _godState?.PrestigeState;
        return prestigeState != null && IsPowerUnlocked(AscensionPowerId.FistOfGod) && prestigeState.PrestigePoints >= GetFistOfGodCost();
    }

    /// <summary>
    /// Abat le poing divin sur un hex : <see cref="FistOfGodDamage"/> dégâts au monstre qui s'y
    /// trouve (réduits par son armure, comme toute autre source de dégâts) et autant à chaque ville
    /// <b>ennemie</b> adjacente — les villes du joueur ne sont jamais touchées, le pouvoir est une
    /// arme, pas un châtiment. Les monstres « amis » (MonsterFeature.AttacksOtherMonsters, ex.
    /// l'Aventurier) sont épargnés, comme partout ailleurs.
    ///
    /// <para>Les dégâts sur une ville cascadent comme une attaque de monstre (voir
    /// MonsterController.ApplyMonsterAttack) : soldats, puis défense, puis niveaux d'Hôtel de ville ;
    /// une ville qui perd son Hôtel de ville est détruite.</para>
    /// </summary>
    public bool ApplyFistOfGod(HexCoord hex)
    {
        if (_state == null || _prng == null || !CanUseFistOfGod()) return false;

        if (_state.GetMapFor(hex)?.GetTile(hex) == null) return false;

        StrikeMonsterAt(hex);
        StrikeCitiesAdjacentTo(hex);

        var prestigeState = _godState!.PrestigeState!;
        prestigeState.PrestigePoints -= GetFistOfGodCost();
        prestigeState.FistOfGodUsesSinceLastPrestige++;

        return true;
    }

    private void StrikeMonsterAt(HexCoord hex)
    {
        var monster = _state!.GetFeaturesAt(hex).OfType<MonsterFeature>().FirstOrDefault(m => !m.AttacksOtherMonsters);
        if (monster == null) return;

        monster.Hp -= MonsterFeature.ApplyArmorReduction(FistOfGodDamage, monster.Armor, _prng!);
        if (monster.Hp > 0) return;

        monster.KilledByCivilizationIndex = _state.PlayerCivilization.Index;
        _state.RemoveFeature(monster);
        _state.EventLog.Add(monster.RemovedEventType);
    }

    private void StrikeCitiesAdjacentTo(HexCoord hex)
    {
        int playerIndex = _state!.PlayerCivilization.Index;

        // Liste matérialisée avant de frapper : une ville détruite est retirée de la civilisation
        // pendant l'itération.
        var targets = new List<City>();
        foreach (var civ in _state.Civilizations)
        {
            if (civ.Index == playerIndex) continue;
            foreach (var city in civ.Cities)
                if (city.Position.IsAdjacentTo(hex))
                    targets.Add(city);
        }

        foreach (var city in targets)
            StrikeCity(city, FistOfGodDamage);
    }

    /// <summary>
    /// Cascade de dégâts sur une ville : soldats, défense, niveaux d'Hôtel de ville, puis destruction
    /// si l'Hôtel de ville a disparu. Même ordre que MonsterController.ApplyMonsterAttack, sans les
    /// Armures d'Acier : rien ne pare le poing de Dieu.
    /// </summary>
    private void StrikeCity(City city, int damage)
    {
        var civ = _state!.GetCivilization(city.CivilizationIndex);
        if (civ == null) return;

        int soldierDmg = Math.Min(damage, city.Soldiers);
        city.Soldiers -= soldierDmg;
        damage -= soldierDmg;

        int defenseDmg = Math.Min(damage, city.CurrentDefense);
        city.CurrentDefense -= defenseDmg;
        damage -= defenseDmg;

        if (damage > 0)
        {
            var townHall = city.Buildings.OfType<TownHall>().FirstOrDefault();
            if (townHall != null)
            {
                townHall.Level -= Math.Min(damage, townHall.Level);
                if (townHall.Level <= 0)
                {
                    city.RemoveBuilding(townHall);
                    city.InvalidateLevelCache();
                }
                BuildingController.RecalculateStorageCapacity(civ);
                civ.TrimResourcesToMax();
            }
        }

        if (!city.Buildings.OfType<TownHall>().Any())
            _cityBuilderController?.DestroyCity(city, CityDestructionCause.Combat);
    }

    private void OnClockAdvanced(object? sender, GameClockAdvancedEventArgs e)
    {
        try { PerformHandOfGodHarvests(); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[AscensionController] {nameof(PerformHandOfGodHarvests)}: {ex}"); }
    }

    private void PerformHandOfGodHarvests()
    {
        if (_state == null || _harvestController == null || !IsPowerUnlocked(AscensionPowerId.HandOfGod)) return;

        var civ = _state.PlayerCivilization;
        var hexes = new HashSet<HexCoord>();
        foreach (var city in civ.Cities)
            foreach (var hex in city.Position.GetHexes())
                hexes.Add(hex);

        foreach (var hex in hexes)
            _harvestController.ManualHarvest(civ.Index, hex);
    }
}
