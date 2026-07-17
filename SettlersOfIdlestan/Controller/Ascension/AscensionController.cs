using System;
using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Controller.Generator;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Ascension;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandFeatures;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Prestige;
using SettlersOfIdlestan.Model.Races;

namespace SettlersOfIdlestan.Controller.Ascension;

/// <summary>
/// Gère les pouvoirs divins (GodState.AscensionState) : Foi est le pouvoir fondateur (toujours
/// disponible), qui déverrouille les 4 colonnes indépendantes (Main/Oeil/Marche/Bras de Dieu) ;
/// effets passifs (Main de Dieu, Oeil de Dieu, Bras de Dieu, Foi) et l'action ciblée Marche de Dieu.
/// Gère aussi l'Ascension elle-même (voir <see cref="PerformAscension"/>) : convertit l'essence
/// divine accumulée (DivineBonesController) en points divins et repart de zéro (île + prestige).
/// </summary>
public class AscensionController : IModifierProvider
{
    /// <summary>Nombre minimum d'essences divines requis pour pouvoir déclencher une Ascension.</summary>
    public const int MinDivineEssenceForAscension = 4;

    /// <summary>
    /// Bâtiments uniques toujours choisissables comme bâtiment permanent d'Ascension (voir
    /// <see cref="SelectPermanentUniqueBuilding"/>) : uniquement des IUniqueBuilding dont l'intégralité
    /// de l'effet est capturé par GetUniqueBuildingModifiers (pas d'automatisation liée à une
    /// présence physique en ville, pas de comportement par tick propre à l'instance).
    /// S'y ajoutent les bâtiments raciaux des races ayant déjà ascensionné — voir
    /// <see cref="PermanentUniqueBuildingChoices"/>.
    /// </summary>
    private static readonly IReadOnlyList<BuildingType> BasePermanentUniqueBuildingChoices = new[]
    {
        BuildingType.Academy,
        BuildingType.ArtisansGuild,
        BuildingType.BlastFurnace,
        BuildingType.HarvestersGuild,
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

    public event Action? OnModifiersChanged;

    public void Initialize(WorldState state, GameClock? clock, GamePRNG prng, HarvestController harvestController, GodState godState)
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
        OnModifiersChanged?.Invoke();
        return true;
    }

    /// <summary>Race jouée pendant le cycle d'Ascension en cours (Humains par défaut).</summary>
    public RaceId SelectedRace => _ascensionState?.SelectedRace ?? RaceId.Human;

    /// <summary>Races avec lesquelles une Ascension a déjà été effectuée (voir AscensionState.AscendedRaces).</summary>
    public IReadOnlyCollection<RaceId> AscendedRaces =>
        (IReadOnlyCollection<RaceId>?)_ascensionState?.AscendedRaces ?? Array.Empty<RaceId>();

    /// <summary>
    /// Vrai si le choix de race est débloqué : toute la première rangée de pouvoirs divins achetée,
    /// c'est-à-dire le premier pouvoir de chacune des 4 colonnes (Main/Oeil/Marche/Bras de Dieu).
    /// </summary>
    public bool IsRaceSelectionUnlocked =>
        Enumerable.Range(0, 4).All(col =>
        {
            var column = AscensionPowerDefinitions.GetColumn(col);
            return column.Count > 0 && IsPowerUnlocked(column[0].Id);
        });

    /// <summary>
    /// Vrai si les races avancées (Sirènes, Elfes noirs) sont débloquées : toute la seconde rangée
    /// de pouvoirs divins achetée (le deuxième pouvoir de chaque colonne qui en possède un).
    /// </summary>
    public bool AreAdvancedRacesUnlocked =>
        IsRaceSelectionUnlocked &&
        Enumerable.Range(0, 4).All(col =>
        {
            var column = AscensionPowerDefinitions.GetColumn(col);
            return column.Count < 2 || IsPowerUnlocked(column[1].Id);
        });

    /// <summary>
    /// Races choisissables à la prochaine Ascension : Humains toujours ; les races de base une fois
    /// la première rangée de pouvoirs divins complète. Les races avancées (Sirènes, Elfes noirs) ne
    /// sont pas encore implémentées et n'apparaissent jamais ici.
    /// </summary>
    public IReadOnlyList<RaceId> GetSelectableRaces()
    {
        if (!IsRaceSelectionUnlocked)
            return new[] { RaceId.Human };

        return RaceDefinitions.All
            .Where(r => r.Tier == RaceTier.Base)
            .Select(r => r.Id)
            .ToList();
    }

    /// <summary>Bâtiments uniques permanents actuellement choisis (voir SelectPermanentUniqueBuilding).</summary>
    public IReadOnlyCollection<BuildingType> PermanentUniqueBuildings =>
        (IReadOnlyCollection<BuildingType>?)_ascensionState?.PermanentUniqueBuildings ?? Array.Empty<BuildingType>();

    /// <summary>
    /// Nombre d'emplacements de bâtiments uniques permanents disponibles : 1 par Ascension déjà
    /// effectuée (voir AscensionState.AscensionsPerformed) — 0 tant qu'aucune Ascension n'a eu lieu.
    /// </summary>
    public int PermanentUniqueBuildingSlots => _ascensionState?.AscensionsPerformed ?? 0;

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
        _state.PlayerCivilization.SetAscensionGrantedUniqueBuildings(PermanentUniqueBuildings);
    }

    public bool CanAscend(GodState godState) => godState.DivineEssence >= MinDivineEssenceForAscension;

    /// <summary>
    /// Convertit toute l'essence divine accumulée en points divins (1 pour 1, cross-prestige) —
    /// remettant DivineEssence à zéro, ce qui réinitialise le coût de Purification des Os Divins
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

        int essenceGained = godState.DivineEssence;
        godState.GodPoints += essenceGained;
        godState.TotalGodPointsEarned += essenceGained;
        godState.DivineEssence = 0;
        godState.AscensionState.AscensionsPerformed++;

        // La race qui accomplit l'Ascension marque l'histoire : son bâtiment racial devient un
        // choix permanent pour tous les cycles futurs, quelle que soit la race jouée.
        godState.AscensionState.AscendedRaces.Add(godState.AscensionState.SelectedRace);
        godState.AscensionState.SelectedRace = chosenRace;

        RecordAscensionCycleStats(mainGameState, godState);

        var generator = new IslandMapGenerator(mainGameState.WorldPRNG);
        var worldState = generator.GenerateWorldState(
            firstIslandParameters,
            mainGameState.Clock.CurrentTick,
            startTick: mainGameState.Clock.CurrentTick,
            startVertexTerrain: RaceDefinitions.Get(chosenRace).StartVertexTerrain)
            ?? throw new InvalidOperationException("Failed to generate island for ascension.");

        godState.PrestigeState = new PrestigeState(worldState);
        GrantFreePrestigeVertices(godState.PrestigeState);

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
    /// terrain de départ de la race ne produit pas (ex. la brique des Nains).
    /// </summary>
    private void GrantFreePrestigeVertices(PrestigeState prestigeState)
    {
        if (!IsPowerUnlocked(AscensionPowerId.Faith)) return;

        if (!prestigeState.PurchasedVertices.Contains(Model.Prestige.PrestigeMap.PrestigeMap.CentralVertex))
            prestigeState.PurchasedVertices.Add(Model.Prestige.PrestigeMap.PrestigeMap.CentralVertex);

        if (!IsRaceSelectionUnlocked) return;

        foreach (var neighbor in Expand.PrestigeMapController.DefaultMap.GetNeighbors(Model.Prestige.PrestigeMap.PrestigeMap.CentralVertex))
            if (!prestigeState.PurchasedVertices.Contains(neighbor.Coord))
                prestigeState.PurchasedVertices.Add(neighbor.Coord);
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
    /// Coût en points de prestige de la prochaine utilisation de Marche de Dieu : 1 à la première
    /// utilisation depuis le dernier prestige, 2 à la deuxième, etc. (voir
    /// PrestigeState.WalkOfGodUsesSinceLastPrestige, remis à zéro à chaque prestige).
    /// </summary>
    public int GetWalkOfGodCost() => (_godState?.PrestigeState?.WalkOfGodUsesSinceLastPrestige ?? 0) + 1;

    /// <summary>Vrai si Marche de Dieu est débloquée et que le joueur a assez de points de prestige pour son prochain coût.</summary>
    public bool CanUseWalkOfGod()
    {
        var prestigeState = _godState?.PrestigeState;
        return prestigeState != null && IsPowerUnlocked(AscensionPowerId.WalkOfGod) && prestigeState.PrestigePoints >= GetWalkOfGodCost();
    }

    /// <summary>
    /// Assigne un terrain aléatoire (différent de l'actuel si possible) à un hex de surface portant
    /// un Dominion de niveau <see cref="WalkOfGodMinDominionLevel"/> ou plus, contre un coût en
    /// points de prestige croissant (voir <see cref="GetWalkOfGodCost"/>) plus 1 niveau du Dominion
    /// de l'hex (jamais retiré : il reste au moins au niveau 1 après la marche).
    /// Si l'hex ciblé était de l'eau, les hexs voisins qui n'existaient pas encore sont créés en tant qu'eau.
    /// </summary>
    public bool ChangeTerrainRandomly(HexCoord hex)
    {
        if (_state == null || _prng == null || !CanUseWalkOfGod()) return false;

        var map = _state.GetMapFor(hex);
        var tile = map?.GetTile(hex);
        if (tile == null) return false;

        var dominion = GetWalkOfGodDominion(hex);
        if (dominion == null) return false;

        bool wasWater = tile.TerrainType == TerrainType.Water;

        TerrainType newType;
        do { newType = RandomTerrainPool[_prng.Next(RandomTerrainPool.Length)]; }
        while (newType == tile.TerrainType);

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
    /// Marche de Dieu (1 à la première utilisation depuis le dernier prestige, 2 à la deuxième, etc.
    /// — voir PrestigeState.PresenceOfGodUsesSinceLastPrestige, remis à zéro à chaque prestige).
    /// </summary>
    public int GetPresenceOfGodCost() => (_godState?.PrestigeState?.PresenceOfGodUsesSinceLastPrestige ?? 0) + 1;

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
