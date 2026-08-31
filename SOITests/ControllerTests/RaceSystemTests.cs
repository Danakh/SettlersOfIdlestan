using SettlersOfIdlestan.Controller;
using SettlersOfIdlestan.Controller.Ascension;
using SettlersOfIdlestan.Model.Ascension;
using SettlersOfIdlestan.Controller.Generator;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandFeatures;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Controller.Expand;
using SettlersOfIdlestan.Model.Prestige;
using SettlersOfIdlestan.Model.Prestige.PrestigeMap;
using SettlersOfIdlestan.Model.Races;
using SOITests.TestUtilities;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SOITests.ControllerTests;

/// <summary>
/// Système de races (choix à l'Ascension) : déblocage individuel de chaque race — base comme
/// avancée — par sa propre combinaison de 3 pouvoirs divins (AscensionController.IsRaceUnlocked /
/// IsRaceSelectionUnlocked / GetSelectableRaces), enregistrement des races ayant ascensionné et
/// bâtiments raciaux permanents, restrictions de placement (CITY_MIN_DISTANCE,
/// CITY_PLACEMENT_REQUIRES_TERRAIN, CITY_PLACEMENT_FLYING), réduction de coût de ville
/// (NEW_CITY_COST_REDUCTION) et effet Ziggourat (production instantanée de Dominion des Temples).
/// </summary>
public class RaceSystemTests
{
    // ── Déblocage des races par combinaison de pouvoirs divins ──────────────

    private static AscensionController CreateAscension(out GodState godState, int godPoints = 100)
    {
        var state = IslandTestFactory.CreateSevenHexIslandState();
        godState = new GodState { GodPoints = godPoints };
        var ascension = new AscensionController();
        ascension.Initialize(state, clock: null, new GamePRNG(1), new HarvestController(), godState);
        return ascension;
    }

    /// <summary>
    /// Achète les 6 pouvoirs divins de premier niveau (un par colonne 0 à 5) : l'union des
    /// combinaisons requises par chacune des 4 races de base (voir RaceDefinitions.All), donc les
    /// débloque toutes simultanément.
    /// </summary>
    private static void UnlockFirstRow(AscensionController ascension)
    {
        Assert.True(ascension.PurchasePower(AscensionPowerId.Faith));
        Assert.True(ascension.PurchasePower(AscensionPowerId.HandOfGod));
        Assert.True(ascension.PurchasePower(AscensionPowerId.MemoryOfGod));
        Assert.True(ascension.PurchasePower(AscensionPowerId.WalkOfGod));
        Assert.True(ascension.PurchasePower(AscensionPowerId.ArmOfGod));
        Assert.True(ascension.PurchasePower(AscensionPowerId.PrestigiousAscension));
        Assert.True(ascension.PurchasePower(AscensionPowerId.DivineLegacy));
    }

    /// <summary>
    /// Les 6 pouvoirs divins de second rang (le 2e de chaque colonne 0-5) : l'union des combinaisons
    /// requises par chacune des 4 races avancées (voir RaceDefinitions.All), donc les débloque toutes
    /// simultanément. Suppose UnlockFirstRow déjà appelée (chaque pouvoir de second rang exige le
    /// premier pouvoir de sa colonne).
    /// </summary>
    private static void UnlockSecondRow(AscensionController ascension)
    {
        Assert.True(ascension.PurchasePower(AscensionPowerId.DivineInventory));
        Assert.True(ascension.PurchasePower(AscensionPowerId.EyeOfGod));
        Assert.True(ascension.PurchasePower(AscensionPowerId.PresenceOfGod));
        Assert.True(ascension.PurchasePower(AscensionPowerId.FistOfGod));
        Assert.True(ascension.PurchasePower(AscensionPowerId.GreaterPurification));
        Assert.True(ascension.PurchasePower(AscensionPowerId.EternalLegacy));
    }

    /// <summary>
    /// Hand+Memory+Walk+Arm complète exactement la combinaison des Orcs (Mémoire, Main, Bras — Marche
    /// est en trop mais ne gêne pas) : IsRaceSelectionUnlocked (vraie dès qu'une race de base autre
    /// qu'Humains devient sélectionnable) ne bascule donc qu'au dernier des 4 pouvoirs.
    /// </summary>
    [Fact]
    public void IsRaceSelectionUnlocked_TogglesOnceAnyBaseRaceCombinationIsComplete()
    {
        var ascension = CreateAscension(out _);

        Assert.False(ascension.IsRaceSelectionUnlocked);

        ascension.PurchasePower(AscensionPowerId.Faith);
        ascension.PurchasePower(AscensionPowerId.HandOfGod);
        ascension.PurchasePower(AscensionPowerId.MemoryOfGod);
        ascension.PurchasePower(AscensionPowerId.WalkOfGod);
        Assert.False(ascension.IsRaceSelectionUnlocked);

        ascension.PurchasePower(AscensionPowerId.ArmOfGod);
        Assert.True(ascension.IsRaceSelectionUnlocked);
        Assert.Contains(RaceId.Orc, ascension.GetSelectableRaces());
    }

    /// <summary>
    /// Chaque race de base a sa propre combinaison de 3 pouvoirs (RaceDefinition.RequiredPowers),
    /// indépendante des autres : acheter uniquement celle des Orcs (Mémoire, Main, Bras) ne débloque ni
    /// les Elfes, ni les Nains, ni les Gobelins.
    /// </summary>
    [Fact]
    public void IsRaceUnlocked_EachBaseRaceHasItsOwnIndependentCombination()
    {
        var ascension = CreateAscension(out _);
        Assert.True(ascension.PurchasePower(AscensionPowerId.Faith));
        Assert.True(ascension.PurchasePower(AscensionPowerId.MemoryOfGod));
        Assert.True(ascension.PurchasePower(AscensionPowerId.HandOfGod));
        Assert.True(ascension.PurchasePower(AscensionPowerId.ArmOfGod));

        Assert.True(ascension.IsRaceUnlocked(RaceId.Orc));
        Assert.False(ascension.IsRaceUnlocked(RaceId.Elf));
        Assert.False(ascension.IsRaceUnlocked(RaceId.Dwarf));
        Assert.False(ascension.IsRaceUnlocked(RaceId.Goblin));

        var races = ascension.GetSelectableRaces();
        Assert.Equal(new[] { RaceId.Human, RaceId.Orc }, races);
    }

    /// <summary>
    /// Chaque race avancée a sa propre combinaison de 3 pouvoirs de second rang (voir
    /// RaceDefinitions.All), en graphe complet à 4 sommets : chaque pouvoir est partagé par exactement
    /// 2 races. Acheter uniquement la combinaison des Géants (Œil, Inventaire Divin, Poing de Dieu) ne
    /// débloque donc ni les Garudas, ni les Sirènes, ni les Elfes noirs — Œil de Dieu, commun aux
    /// Géants et aux Garudas, ne suffit pas à débloquer ces derniers sans Héritage Éternel et
    /// Présence de Dieu.
    /// </summary>
    [Fact]
    public void IsRaceUnlocked_EachAdvancedRaceHasItsOwnIndependentCombination()
    {
        var ascension = CreateAscension(out _);
        UnlockFirstRow(ascension);
        Assert.True(ascension.PurchasePower(AscensionPowerId.EyeOfGod));
        Assert.True(ascension.PurchasePower(AscensionPowerId.DivineInventory));
        Assert.True(ascension.PurchasePower(AscensionPowerId.FistOfGod));

        Assert.True(ascension.IsRaceUnlocked(RaceId.Giant));
        Assert.False(ascension.IsRaceUnlocked(RaceId.Garuda));
        Assert.False(ascension.IsRaceUnlocked(RaceId.Mermaid));
        Assert.False(ascension.IsRaceUnlocked(RaceId.DarkElf));

        var races = ascension.GetSelectableRaces();
        Assert.Contains(RaceId.Giant, races);
        Assert.DoesNotContain(RaceId.Garuda, races);
        Assert.DoesNotContain(RaceId.Mermaid, races);
        Assert.DoesNotContain(RaceId.DarkElf, races);
    }

    [Fact]
    public void GetSelectableRaces_LockedReturnsHumanOnly()
    {
        var ascension = CreateAscension(out _);

        Assert.Equal(new[] { RaceId.Human }, ascension.GetSelectableRaces());
    }

    [Fact]
    public void GetSelectableRaces_FirstRowOnly_ExcludesAdvancedRaces()
    {
        var ascension = CreateAscension(out _);
        UnlockFirstRow(ascension);

        var races = ascension.GetSelectableRaces();

        Assert.Contains(RaceId.Human, races);
        Assert.Contains(RaceId.Elf, races);
        Assert.Contains(RaceId.Dwarf, races);
        Assert.Contains(RaceId.Goblin, races);
        Assert.Contains(RaceId.Orc, races);
        // Races avancées : verrouillées tant qu'aucune de leurs combinaisons de second rang n'est complète.
        Assert.DoesNotContain(RaceId.Mermaid, races);
        Assert.DoesNotContain(RaceId.DarkElf, races);
        Assert.DoesNotContain(RaceId.Giant, races);
        Assert.DoesNotContain(RaceId.Garuda, races);
    }

    /// <summary>All 6 second-rank powers together cover every advanced race's own combination.</summary>
    [Fact]
    public void GetSelectableRaces_SecondRowComplete_AddsImplementedAdvancedRaces()
    {
        var ascension = CreateAscension(out _);
        UnlockFirstRow(ascension);
        UnlockSecondRow(ascension);

        var races = ascension.GetSelectableRaces();

        Assert.Contains(RaceId.Giant, races);
        Assert.Contains(RaceId.Garuda, races);
        Assert.Contains(RaceId.Mermaid, races);
        Assert.Contains(RaceId.DarkElf, races);
    }

    [Fact]
    public void GetModifiers_EmitsSelectedRaceModifiers()
    {
        var ascension = CreateAscension(out var godState);
        godState.AscensionState.SelectedRace = RaceId.Goblin;

        var modifiers = ascension.GetModifiers().ToList();

        Assert.Contains(modifiers, m => m.Category == ECategory.CITY_MIN_DISTANCE && m.Type == EType.REPLACER && (int)m.Value == 2);
        Assert.Contains(modifiers, m => m.Category == ECategory.BUILDING_MAX_LEVEL && m.SubCategory == nameof(BuildingType.Sawmill) && (int)m.Value == -1);
    }

    [Fact]
    public void GetModifiers_Garuda_EmitsFlightAndAttackRangeWithoutMinDistanceOverride()
    {
        var ascension = CreateAscension(out var godState);
        godState.AscensionState.SelectedRace = RaceId.Garuda;

        var modifiers = ascension.GetModifiers().ToList();

        Assert.Contains(modifiers, m => m.Category == ECategory.CITY_PLACEMENT_FLYING && m.Type == EType.ADDITIVE && (int)m.Value == 3);
        Assert.Contains(modifiers, m => m.Category == ECategory.CITY_ATTACK_RANGE && (int)m.Value == 1);
        Assert.Contains(modifiers, m => m.Category == ECategory.CITY_DEFENSE && (int)m.Value == -3);
        // Le Vol ne dispense plus du rapprochement standard : distance minimale de droit commun (3).
        Assert.DoesNotContain(modifiers, m => m.Category == ECategory.CITY_MIN_DISTANCE);
        Assert.Contains(modifiers, m => m.Category == ECategory.BUILDING_MAX_LEVEL && m.SubCategory == nameof(BuildingType.ThroneOfWinds) && (int)m.Value == 1);
    }

    /// <summary>
    /// Le malus de niveau max garuda ne touche que la production, la recherche et la magie. Le
    /// Comptoir en est exclu par construction : le Port Impérial exige un Comptoir niveau 4, qui est
    /// aussi son plafond par défaut, donc un -1 dessus rendrait le prestige inatteignable. La Verrerie
    /// (plafond par défaut 0) est en revanche incluse : BuildingController.GetMaxLevel applique le
    /// malus en dernier et le plafonne à 1 minimum, jamais 0 par sa seule faute.
    /// </summary>
    [Fact]
    public void GetModifiers_Garuda_LowersProductionResearchAndMagicButNotSeaportOrWarehouse()
    {
        var ascension = CreateAscension(out var godState);
        godState.AscensionState.SelectedRace = RaceId.Garuda;

        var modifiers = ascension.GetModifiers().ToList();

        foreach (var lowered in new[]
                 {
                     BuildingType.Sawmill, BuildingType.Brickworks, BuildingType.Mill, BuildingType.Quarry,
                     BuildingType.Mine, BuildingType.Forge, BuildingType.Smelter,
                     BuildingType.Library, BuildingType.Laboratory,
                     BuildingType.MageTower, BuildingType.AlchimistHut, BuildingType.GlassWorks,
                 })
            Assert.Contains(modifiers, m => m.Category == ECategory.BUILDING_MAX_LEVEL
                                            && m.SubCategory == lowered.ToString() && (int)m.Value == -1);

        foreach (var spared in new[]
                 {
                     BuildingType.Seaport, BuildingType.Warehouse, BuildingType.Market, BuildingType.Temple,
                     BuildingType.TownHall, BuildingType.Palisade, BuildingType.Barracks,
                 })
            Assert.DoesNotContain(modifiers, m => m.Category == ECategory.BUILDING_MAX_LEVEL
                                                  && m.SubCategory == spared.ToString() && (int)m.Value < 0);
    }

    [Fact]
    public void ThroneOfWinds_ExtendsAttackRangeInsteadOfDefense()
    {
        var throne = new ThroneOfWinds { Level = 1 };

        var modifiers = throne.GetUniqueBuildingModifiers().ToList();

        Assert.Contains(modifiers, m => m.Category == ECategory.CITY_ATTACK_RANGE && (int)m.Value == 1);
        Assert.DoesNotContain(modifiers, m => m.Category == ECategory.CITY_DEFENSE);
    }

    [Fact]
    public void PermanentUniqueBuildingChoices_IncludesRacialBuildingsOfAscendedRaces()
    {
        var ascension = CreateAscension(out var godState);

        Assert.DoesNotContain(BuildingType.Ziggurat, ascension.PermanentUniqueBuildingChoices);

        godState.AscensionState.AscendedRaces.Add(RaceId.Human);
        godState.AscensionState.AscendedRaces.Add(RaceId.Elf);
        godState.AscensionState.AscendedRaces.Add(RaceId.Garuda);

        var choices = ascension.PermanentUniqueBuildingChoices;
        Assert.Contains(BuildingType.Ziggurat, choices);
        Assert.Contains(BuildingType.HeartTree, choices);
        Assert.Contains(BuildingType.ThroneOfWinds, choices);
        Assert.DoesNotContain(BuildingType.RunicForge, choices);
    }

    // ── PerformAscension : enregistrement de la race et validation du choix ──

    [Fact]
    public void PerformAscension_RecordsPreviousRaceAndAppliesChosenRace()
    {
        var controller = new MainGameController();
        controller.CreateNewGame();
        var godState = controller.CurrentMainState!.GodState;
        godState.GodPoints = 100;
        godState.DivineEssence = 5;
        UnlockFirstRow(controller.AscensionController);

        // Premier cycle (Humains par défaut, pas un vrai choix) : ne compte pas dans AscendedRaces —
        // voir PerformAscension_SecondAscension_AddsPlayedRaceToAscendedRaces_EvenWithoutAnyRaceUnlocked.
        controller.PerformAscension();
        godState.DivineEssence = 5;

        controller.PerformAscension(RaceId.Elf);

        Assert.Equal(RaceId.Elf, godState.AscensionState.SelectedRace);
        Assert.Contains(RaceId.Human, godState.AscensionState.AscendedRaces);
        // Ascension effectuée en Humain : la Ziggourat rejoint définitivement les choix permanents.
        Assert.Contains(BuildingType.Ziggurat, controller.AscensionController.PermanentUniqueBuildingChoices);
        // La nouvelle île démarre avec les modifiers elfes actifs sur la civilisation du joueur.
        var playerCiv = controller.CurrentMainState.CurrentWorldState!.PlayerCivilization;
        Assert.True(playerCiv.ModifierAggregator.HasModifier(ECategory.CITY_PLACEMENT_REQUIRES_TERRAIN, nameof(TerrainType.Forest)));
    }

    [Fact]
    public void PerformAscension_NonSelectableRace_Throws()
    {
        var controller = new MainGameController();
        controller.CreateNewGame();
        var godState = controller.CurrentMainState!.GodState;
        godState.DivineEssence = 5;

        // Choix de race non débloqué : seule Human est acceptée.
        Assert.Throws<InvalidOperationException>(() => controller.PerformAscension(RaceId.Elf));
        // L'échec ne doit rien avoir consommé.
        Assert.Equal(5, godState.DivineEssence);
    }

    [Fact]
    public void PerformAscension_WithoutRaceUnlock_KeepsHumanFlow()
    {
        var controller = new MainGameController();
        controller.CreateNewGame();
        var godState = controller.CurrentMainState!.GodState;
        godState.DivineEssence = 5;

        controller.PerformAscension();

        Assert.Equal(RaceId.Human, godState.AscensionState.SelectedRace);
        // Choix de race encore verrouillé : Humains n'est que la valeur par défaut, pas un choix du
        // joueur — ne doit donc pas débloquer la Ziggourat via AscendedRaces.
        Assert.DoesNotContain(RaceId.Human, godState.AscensionState.AscendedRaces);
        Assert.Equal(5, godState.GodPoints);
        // Sans Foi débloquée, aucun vertex de prestige n'est offert.
        Assert.Empty(controller.CurrentMainState.PrestigeState!.PurchasedVertices);
    }

    /// <summary>
    /// Régression : MainGameController.SetupModifierAggregators réenregistre AscensionController sur
    /// la même Civilization chaque fois qu'InitializeControllersForCurrentIsland est rappelé sans
    /// régénérer l'île (ex. SetGame/SetGameFromSave rappelé sur le même WorldState après une reprise de
    /// partie). Avant correctif, ModifierAggregator.Register n'était pas idempotent : un second
    /// enregistrement doublait tous les modifiers additifs fournis par la race jouée. Symptôme
    /// observable : le bonus racial Humain BUILDING_MAX_LEVEL Ziggurat +1 passait à +2, rendant la
    /// Ziggourat améliorable au niveau 2 alors que son maximum doit rester 1 en Humain (0 pour toute
    /// autre race). Reproduit ici directement au niveau ModifierAggregator/Civilization, sans passer
    /// par MainGameController, pour ne pas dépendre de PrestigeMapController.DefaultMap (carte statique
    /// partagée entre tests, source d'instabilité si on la sollicite plusieurs fois par test).
    /// </summary>
    [Fact]
    public void RegisteringSameAscensionControllerTwice_DoesNotDoubleRaceModifiers()
    {
        var ascension = CreateAscension(out var godState);
        Assert.Equal(RaceId.Human, godState.AscensionState.SelectedRace);

        var civ = new Civilization();
        civ.AddCustomAggregator(ascension);

        int maxLevelBefore = civ.ModifierAggregator.ApplyModifiers(
            ECategory.BUILDING_MAX_LEVEL, nameof(BuildingType.Ziggurat), new Ziggurat().GetDefaultMaxLevel());
        Assert.Equal(1, maxLevelBefore);

        // Simule SetupModifierAggregators rappelé sur la même civilisation (même instance d'AscensionController).
        civ.AddCustomAggregator(ascension);

        int maxLevelAfter = civ.ModifierAggregator.ApplyModifiers(
            ECategory.BUILDING_MAX_LEVEL, nameof(BuildingType.Ziggurat), new Ziggurat().GetDefaultMaxLevel());
        Assert.Equal(1, maxLevelAfter);
    }

    /// <summary>
    /// AscendedRaces ne dépend plus de IsRaceSelectionUnlocked (voir
    /// AscensionController.CreditAscensionPointsAndArchiveCycle) : seul le tout premier cycle est
    /// exclu, qu'une autre race ait ou non été débloquée entretemps — sans quoi un joueur qui ne
    /// débloque jamais aucune autre race resterait indéfiniment sans son bâtiment racial Humain.
    /// </summary>
    [Fact]
    public void PerformAscension_SecondAscension_AddsPlayedRaceToAscendedRaces_EvenWithoutAnyRaceUnlocked()
    {
        var controller = new MainGameController();
        controller.CreateNewGame();
        var godState = controller.CurrentMainState!.GodState;
        godState.DivineEssence = 5;

        // Premier cycle : choix de race verrouillé, ne compte pas dans AscendedRaces.
        controller.PerformAscension();
        Assert.DoesNotContain(RaceId.Human, godState.AscensionState.AscendedRaces);
        Assert.False(controller.AscensionController.IsRaceSelectionUnlocked);

        // Deuxième cycle : marque un vrai choix, même sans qu'aucune autre race n'ait jamais été débloquée.
        godState.DivineEssence = 5;
        controller.PerformAscension(RaceId.Human);

        Assert.Contains(RaceId.Human, godState.AscensionState.AscendedRaces);
    }

    // ── Vertex de prestige offerts à l'Ascension ─────────────────────────────

    [Fact]
    public void PerformAscension_WithFaithOnly_GrantsCentralPrestigeVertexOnly()
    {
        var controller = new MainGameController();
        controller.CreateNewGame();
        var godState = controller.CurrentMainState!.GodState;
        godState.GodPoints = 100;
        godState.DivineEssence = 5;
        Assert.True(controller.AscensionController.PurchasePower(AscensionPowerId.Faith));

        controller.PerformAscension();

        var purchased = controller.CurrentMainState.PrestigeState!.PurchasedVertices;
        Assert.Contains(PrestigeMap.CentralVertex, purchased);
        Assert.Single(purchased);
    }

    [Fact]
    public void PerformAscension_WithRacesUnlocked_GrantsCentralVertexAndItsThreeNeighborsFree()
    {
        var controller = new MainGameController();
        controller.CreateNewGame();
        var godState = controller.CurrentMainState!.GodState;
        godState.GodPoints = 100;
        godState.DivineEssence = 5;
        UnlockFirstRow(controller.AscensionController);

        controller.PerformAscension(RaceId.Dwarf);

        var prestigeState = controller.CurrentMainState.PrestigeState!;
        var purchased = prestigeState.PurchasedVertices;
        Assert.Contains(PrestigeMap.CentralVertex, purchased);
        var neighbors = PrestigeMapController.DefaultMap.GetNeighbors(PrestigeMap.CentralVertex);
        Assert.Equal(3, neighbors.Count);
        foreach (var neighbor in neighbors)
            Assert.Contains(neighbor.Coord, purchased);
        Assert.Equal(4, purchased.Count);
        // Gratuit = aucun point de prestige dépensé.
        Assert.Equal(0, prestigeState.PrestigePoints);
        // Le voisin Port & Marché garantit un Marché de départ : la civilisation peut acheter la
        // ressource que son terrain de départ ne produit pas (ex. la brique des Nains).
        var startingCity = controller.CurrentMainState.CurrentWorldState!.PlayerCivilization.Cities[0];
        Assert.Contains(startingCity.Buildings, b => b.Type == BuildingType.Market);
    }

    [Fact]
    public void PerformAscension_Dwarf_StartVertexTouchesMountainForestAndWater()
    {
        var controller = new MainGameController();
        controller.CreateNewGame();
        var godState = controller.CurrentMainState!.GodState;
        godState.GodPoints = 100;
        godState.DivineEssence = 5;
        UnlockFirstRow(controller.AscensionController);

        controller.PerformAscension(RaceId.Dwarf);

        // Le générateur remplace la Colline par la Montagne dans la paire de départ : la capitale
        // naine respecte sa propre restriction de placement.
        var worldState = controller.CurrentMainState.CurrentWorldState!;
        var startingCity = worldState.PlayerCivilization.Cities[0];
        var map = worldState.GetMapFor(startingCity.Position)!;
        Assert.True(map.VertexHasTerrainType(startingCity.Position, TerrainType.Mountain));
        Assert.True(map.VertexHasTerrainType(startingCity.Position, TerrainType.Forest));
        Assert.True(map.VertexHasTerrainType(startingCity.Position, TerrainType.Water));
    }

    // ── Restrictions de placement (CityBuilderController) ────────────────────
    //
    // Layout "ruban" : h1(0,0) — h2(1,0) — h3(0,1) — h4(1,1) — h5(0,2) — h6(1,2)
    //   v1(h1,h2,h3), vMiddle(h2,h3,h4) à distance 1, v2(h3,h4,h5) à distance 2,
    //   v3(h4,h5,h6) à distance 3 de v1. h5 est la seule Forêt.

    private static HexCoord H(int q, int r) => new(q, r, IslandMap.SurfaceLayer);

    private static (WorldState state, Civilization civ, Vertex v1, Vertex v2, Vertex v3) RibbonIsland()
    {
        var h1 = H(0, 0);
        var h2 = H(1, 0);
        var h3 = H(0, 1);
        var h4 = H(1, 1);
        var h5 = H(0, 2);
        var h6 = H(1, 2);

        var map = new IslandMap(new HexTile[]
        {
            new(h1, TerrainType.Plain),
            new(h2, TerrainType.Plain),
            new(h3, TerrainType.Plain),
            new(h4, TerrainType.Plain),
            new(h5, TerrainType.Forest),
            new(h6, TerrainType.Plain),
        });

        var civ = new Civilization { Index = 0 };
        var state = new WorldState(map, new List<Civilization> { civ }, AtlasController.InvalidIslandId);

        civ.AddRoad(new Road(Edge.Create(h2, h3)) { CivilizationIndex = 0 });
        civ.AddRoad(new Road(Edge.Create(h3, h4)) { CivilizationIndex = 0 });
        civ.AddRoad(new Road(Edge.Create(h4, h5)) { CivilizationIndex = 0 });

        return (state, civ, Vertex.Create(h1, h2, h3), Vertex.Create(h3, h4, h5), Vertex.Create(h4, h5, h6));
    }

    private static CityBuilderController Controller(WorldState state)
    {
        var controller = new CityBuilderController();
        controller.Initialize(state);
        return controller;
    }

    private static void AddRaceModifiers(Civilization civ, params Modifier[] modifiers)
        => civ.AddCustomAggregator(new StaticModifierProvider(modifiers));

    [Fact]
    public void GetBuildableVertices_GoblinMinDistance2_AllowsCityAtDistance2()
    {
        var (state, civ, v1, v2, _) = RibbonIsland();
        civ.AddCity(new City(v1) { CivilizationIndex = 0 });

        // Sans modifier : distance 2 < 3, bloqué.
        Assert.DoesNotContain(Controller(state).GetBuildableVertices(0), v => v.Equals(v2));

        AddRaceModifiers(civ, new Modifier(ECategory.CITY_MIN_DISTANCE, EType.REPLACER, 2));
        Assert.Contains(Controller(state).GetBuildableVertices(0), v => v.Equals(v2));
    }

    [Fact]
    public void GetBuildableVertices_GiantMinDistance4_BlocksCityAtDistance3()
    {
        var (state, civ, v1, _, v3) = RibbonIsland();
        civ.AddCity(new City(v1) { CivilizationIndex = 0 });

        // Sans modifier : distance 3 >= 3, constructible.
        Assert.Contains(Controller(state).GetBuildableVertices(0), v => v.Equals(v3));

        AddRaceModifiers(civ, new Modifier(ECategory.CITY_MIN_DISTANCE, EType.REPLACER, 4));
        Assert.DoesNotContain(Controller(state).GetBuildableVertices(0), v => v.Equals(v3));
    }

    [Fact]
    public void GetBuildableVertices_ElfForestRestriction_OnlyKeepsForestAdjacentVertices()
    {
        var (state, civ, v1, v2, v3) = RibbonIsland();
        AddRaceModifiers(civ, new Modifier(ECategory.CITY_PLACEMENT_REQUIRES_TERRAIN, nameof(TerrainType.Forest), EType.ADDITIVE, 1));

        var vertices = Controller(state).GetBuildableVertices(0);

        // v1 ne touche que des Plaines ; v2 et v3 touchent h5 (Forêt).
        Assert.DoesNotContain(vertices, v => v.Equals(v1));
        Assert.Contains(vertices, v => v.Equals(v2));
        Assert.Contains(vertices, v => v.Equals(v3));
    }

    // ── Restriction de terrain en Inframonde (Elfes / Caverne aux champignons) ──────────
    //
    // Même layout "ruban" que RibbonIsland, mais sur la couche Inframonde et avec la Caverne aux
    // champignons à la place de la Forêt (absente sous terre — voir TerrainTypeExtensions.UnderworldEquivalent).

    private static HexCoord HU(int q, int r) => new(q, r, LayerState.UnderworldZ);

    private static (WorldState state, Civilization civ, Vertex v1, Vertex v2) UnderworldRibbonIsland()
    {
        var h1 = HU(0, 0);
        var h2 = HU(1, 0);
        var h3 = HU(0, 1);
        var h4 = HU(1, 1);
        var h5 = HU(0, 2);

        var underworldMap = new IslandMap(new HexTile[]
        {
            new(h1, TerrainType.Mountain),
            new(h2, TerrainType.Mountain),
            new(h3, TerrainType.Mountain),
            new(h4, TerrainType.Mountain),
            new(h5, TerrainType.MushroomCave),
        });

        var civ = new Civilization { Index = 0 };
        var state = new WorldState(new IslandMap(Array.Empty<HexTile>()), new List<Civilization> { civ }, AtlasController.InvalidIslandId);
        state.AddLayer(LayerState.UnderworldZ, new LayerState(underworldMap));

        return (state, civ, Vertex.Create(h1, h2, h3), Vertex.Create(h3, h4, h5));
    }

    [Fact]
    public void BuildCityPlacementTerrainFilter_ElfInUnderworld_RequiresMushroomCaveAdjacency()
    {
        var (state, civ, v1, v2) = UnderworldRibbonIsland();
        AddRaceModifiers(civ, new Modifier(ECategory.CITY_PLACEMENT_REQUIRES_TERRAIN, nameof(TerrainType.Forest), EType.ADDITIVE, 1));

        var filter = Controller(state).BuildCityPlacementTerrainFilter(civ);

        // v1 ne touche aucune Caverne aux champignons ; v2 touche h5 (Caverne aux champignons).
        Assert.False(filter(v1));
        Assert.True(filter(v2));
    }

    [Fact]
    public void BuildCityPlacementTerrainFilter_DwarfInUnderworld_RemainsUnrestricted()
    {
        // Comportement préservé : seule la Forêt (Elfes) a un équivalent souterrain. La Montagne
        // (Nains) existe déjà sous terre littéralement, mais aucune restriction n'y est ajoutée —
        // seul le cas explicitement demandé (Elfes) change de comportement.
        var (state, civ, v1, _) = UnderworldRibbonIsland();
        AddRaceModifiers(civ, new Modifier(ECategory.CITY_PLACEMENT_REQUIRES_TERRAIN, nameof(TerrainType.Mountain), EType.ADDITIVE, 1));

        var filter = Controller(state).BuildCityPlacementTerrainFilter(civ);

        Assert.True(filter(v1));
    }

    [Fact]
    public void GetBuildableVertices_TerrainRestriction_CacheInvalidatedByNotifyTerrainChanged()
    {
        var (state, civ, _, v2, _) = RibbonIsland();
        AddRaceModifiers(civ, new Modifier(ECategory.CITY_PLACEMENT_REQUIRES_TERRAIN, nameof(TerrainType.Mountain), EType.ADDITIVE, 1));
        var controller = Controller(state);

        Assert.Empty(controller.GetBuildableVertices(0));

        // Marche de Dieu transforme un terrain sans toucher aux compteurs de routes/villes : le
        // cache de GetBuildableVertices est invalidé via WorldState.NotifyTerrainChanged
        // (TerrainVersion), appelé par tous les mutateurs de terrain.
        var h5 = H(0, 2);
        state.GetMapFor(h5)!.GetTile(h5)!.TerrainType = TerrainType.Mountain;
        state.NotifyTerrainChanged();

        Assert.Contains(controller.GetBuildableVertices(0), v => v.Equals(v2));
    }

    [Fact]
    public void NewCityBuildingCostFor_GreatBurrowReduction_LowersCost()
    {
        var (state, civ, v1, _, _) = RibbonIsland();
        var controller = Controller(state);

        var baseCost = controller.NewCityBuildingCostFor(v1, civ);
        Assert.Equal(10, baseCost[Resource.Brick]);
        Assert.Equal(10, baseCost[Resource.Wood]);
        Assert.Equal(15, baseCost[Resource.Food]);

        AddRaceModifiers(civ, new Modifier(ECategory.NEW_CITY_COST_REDUCTION, EType.ADDITIVE, 0.25));
        var reducedCost = controller.NewCityBuildingCostFor(v1, civ);
        Assert.Equal(8, reducedCost[Resource.Brick]);
        Assert.Equal(8, reducedCost[Resource.Wood]);
        Assert.Equal(11, reducedCost[Resource.Food]);
    }

    // ── Vol (CITY_PLACEMENT_FLYING, Garudas) ─────────────────────────────────
    //
    // Ruban sans aucune route : seul le Vol peut fournir des candidats. Prolongé d'un hex
    // h7(0,3) pour obtenir v4 à distance 4 de v1 (hors portée de vol 3).

    private static (WorldState state, Civilization civ, Vertex v1, Vertex vMiddle, Vertex v2, Vertex v3, Vertex v4)
        RoadlessRibbonIsland(bool waterStrait = false)
    {
        var h1 = H(0, 0);
        var h2 = H(1, 0);
        var h3 = H(0, 1);
        var h4 = H(1, 1);
        var h5 = H(0, 2);
        var h6 = H(1, 2);
        var h7 = H(0, 3);

        // waterStrait : h3/h4/h5 en Eau — v2(h3,h4,h5) devient un vertex tout-eau, v3 reste
        // terrestre de l'autre côté du bras de mer.
        var strait = waterStrait ? TerrainType.Water : TerrainType.Plain;
        var map = new IslandMap(new HexTile[]
        {
            new(h1, TerrainType.Plain),
            new(h2, TerrainType.Plain),
            new(h3, strait),
            new(h4, strait),
            new(h5, strait),
            new(h6, TerrainType.Plain),
            new(h7, TerrainType.Plain),
        });

        var civ = new Civilization { Index = 0 };
        var state = new WorldState(map, new List<Civilization> { civ }, AtlasController.InvalidIslandId);

        return (state, civ,
            Vertex.Create(h1, h2, h3),
            Vertex.Create(h2, h3, h4),
            Vertex.Create(h3, h4, h5),
            Vertex.Create(h4, h5, h6),
            Vertex.Create(h5, h6, h7));
    }

    /// <summary>
    /// Vol garuda + distance minimale ramenée à 2. Le rapprochement n'est <b>pas</b> un modificateur
    /// garuda (la race est à la distance standard 3) : c'est le ruban de test qui est trop court pour
    /// que 3 laisse le moindre candidat. Seul le Vol est ici la mécanique sous test.
    /// </summary>
    private static void AddFlightModifiers(Civilization civ)
        => AddRaceModifiers(civ,
            new Modifier(ECategory.CITY_PLACEMENT_FLYING, EType.ADDITIVE, 3),
            new Modifier(ECategory.CITY_MIN_DISTANCE, EType.REPLACER, 2));

    [Fact]
    public void GetBuildableVertices_Flight_AllowsRoadlessVerticesWithinRange()
    {
        var (state, civ, v1, _, v2, v3, _) = RoadlessRibbonIsland();
        civ.AddCity(new City(v1) { CivilizationIndex = 0 });

        // Sans Vol : aucune route, aucun candidat.
        Assert.Empty(Controller(state).GetBuildableVertices(0));

        AddFlightModifiers(civ);
        var vertices = Controller(state).GetBuildableVertices(0);
        Assert.Contains(vertices, v => v.Equals(v2));
        Assert.Contains(vertices, v => v.Equals(v3));
    }

    [Fact]
    public void GetBuildableVertices_Flight_RespectsMinDistanceAndRange()
    {
        var (state, civ, v1, vMiddle, _, _, v4) = RoadlessRibbonIsland();
        civ.AddCity(new City(v1) { CivilizationIndex = 0 });
        AddFlightModifiers(civ);

        var vertices = Controller(state).GetBuildableVertices(0);

        // Distance 1 < distance minimale 2 : trop proche même en volant.
        Assert.DoesNotContain(vertices, v => v.Equals(vMiddle));
        // Distance 4 > portée de vol 3 : hors d'atteinte.
        Assert.DoesNotContain(vertices, v => v.Equals(v4));
    }

    [Fact]
    public void GetBuildableVertices_Flight_FliesOverWaterButNeverLandsOnAllWaterVertex()
    {
        var (state, civ, v1, _, v2, v3, _) = RoadlessRibbonIsland(waterStrait: true);
        civ.AddCity(new City(v1) { CivilizationIndex = 0 });
        AddFlightModifiers(civ);

        var vertices = Controller(state).GetBuildableVertices(0);

        // v2 ne touche que de l'Eau : pas d'atterrissage en pleine mer.
        Assert.DoesNotContain(vertices, v => v.Equals(v2));
        // v3, terrestre de l'autre côté du bras de mer, est atteint en le survolant.
        Assert.Contains(vertices, v => v.Equals(v3));
    }

    [Fact]
    public void GetBuildableVertices_Flight_SurfaceOnly()
    {
        var (state, civ, _, _, _, _, _) = RoadlessRibbonIsland();

        // Ville d'Inframonde : le Vol ne part que des villes de surface — aucun candidat.
        var hu1 = new HexCoord(0, 0, LayerState.UnderworldZ);
        var hu2 = new HexCoord(1, 0, LayerState.UnderworldZ);
        var hu3 = new HexCoord(0, 1, LayerState.UnderworldZ);
        civ.AddCity(new City(Vertex.Create(hu1, hu2, hu3)) { CivilizationIndex = 0 });
        AddFlightModifiers(civ);

        Assert.Empty(Controller(state).GetBuildableVertices(0));
    }

    [Fact]
    public void PerformAscension_Garuda_AfterSecondRow_AppliesFlightToPlayerCivilization()
    {
        var controller = new MainGameController();
        controller.CreateNewGame();
        var godState = controller.CurrentMainState!.GodState;
        godState.GodPoints = 100;
        godState.DivineEssence = 5;
        UnlockFirstRow(controller.AscensionController);
        UnlockSecondRow(controller.AscensionController);

        controller.PerformAscension(RaceId.Garuda);

        Assert.Equal(RaceId.Garuda, godState.AscensionState.SelectedRace);
        var playerCiv = controller.CurrentMainState.CurrentWorldState!.PlayerCivilization;
        Assert.True(playerCiv.ModifierAggregator.HasModifier(ECategory.CITY_PLACEMENT_FLYING));
    }

    // ── Portée de terrain / plafond par ville (Sirènes) ─────────────────────
    //
    // Sur RibbonIsland (h1 muté en Eau) : v1(h1,h2,h3) touche l'Eau directement (distance 0) ;
    // v2(h3,h4,h5) est à distance 2 de v1 ; v3(h4,h5,h6) est à distance 3 de v1 (mêmes distances
    // que celles déjà exploitées par les tests CITY_MIN_DISTANCE ci-dessus).

    [Fact]
    public void GetBuildableVertices_MermaidTerrainRange2_AllowsUpTo2EdgesFromWater()
    {
        var (state, civ, v1, v2, v3) = RibbonIsland();
        var h1 = H(0, 0);
        state.GetMapFor(h1)!.GetTile(h1)!.TerrainType = TerrainType.Water;
        state.NotifyTerrainChanged();

        AddRaceModifiers(civ, new Modifier(ECategory.CITY_PLACEMENT_TERRAIN_RANGE, nameof(TerrainType.Water), EType.ADDITIVE, 2));

        var vertices = Controller(state).GetBuildableVertices(0);

        Assert.Contains(vertices, v => v.Equals(v1)); // touche l'Eau directement (portée 0)
        Assert.Contains(vertices, v => v.Equals(v2)); // à 2 arêtes de l'Eau : dans la portée
        Assert.DoesNotContain(vertices, v => v.Equals(v3)); // à 3 arêtes : hors portée
    }

    [Fact]
    public void GetBuildableVertices_TerrainRange_CacheInvalidatedByNotifyTerrainChanged()
    {
        var (state, civ, v1, _, _) = RibbonIsland();
        AddRaceModifiers(civ, new Modifier(ECategory.CITY_PLACEMENT_TERRAIN_RANGE, nameof(TerrainType.Water), EType.ADDITIVE, 2));
        var controller = Controller(state);

        // Aucune Eau sur la carte : aucun candidat ne peut satisfaire la restriction.
        Assert.Empty(controller.GetBuildableVertices(0));

        var h1 = H(0, 0);
        state.GetMapFor(h1)!.GetTile(h1)!.TerrainType = TerrainType.Water;
        state.NotifyTerrainChanged();

        Assert.Contains(controller.GetBuildableVertices(0), v => v.Equals(v1));
    }

    [Fact]
    public void PerformAscension_Mermaid_AfterSecondRow_AppliesModifiersToPlayerCivilization()
    {
        var controller = new MainGameController();
        controller.CreateNewGame();
        var godState = controller.CurrentMainState!.GodState;
        godState.GodPoints = 100;
        godState.DivineEssence = 5;
        UnlockFirstRow(controller.AscensionController);
        UnlockSecondRow(controller.AscensionController);

        controller.PerformAscension(RaceId.Mermaid);

        Assert.Equal(RaceId.Mermaid, godState.AscensionState.SelectedRace);
        var playerCiv = controller.CurrentMainState.CurrentWorldState!.PlayerCivilization;
        Assert.True(playerCiv.ModifierAggregator.HasModifier(ECategory.CITY_PLACEMENT_TERRAIN_RANGE, nameof(TerrainType.Water)));
        Assert.True(playerCiv.ModifierAggregator.HasModifier(ECategory.INLAND_CITY_LEVEL_CAP, nameof(TerrainType.Water)));
    }

    // ── Plafond de l'Hôtel de Ville par ville (INLAND_CITY_LEVEL_CAP, Sirènes) ──
    //
    // v1(h1,h2,h3) touche l'Eau (h1 muté) : ville côtière, aucun plafond. v3(h4,h5,h6) ne touche
    // jamais l'Eau : ville en retrait, plafonnée. h6 est en plus muté en Montagne pour que la Mine
    // y soit constructible côté terrain (seul le niveau doit alors l'en empêcher).

    private static (WorldState state, Civilization civ, City coastalCity, City inlandCity) MermaidCitiesSetup()
    {
        var (state, civ, v1, _, v3) = RibbonIsland();
        var h1 = H(0, 0);
        var h6 = H(1, 2);
        state.GetMapFor(h1)!.GetTile(h1)!.TerrainType = TerrainType.Water;
        state.GetMapFor(h6)!.GetTile(h6)!.TerrainType = TerrainType.Mountain;
        state.NotifyTerrainChanged();

        AddRaceModifiers(civ, new Modifier(ECategory.INLAND_CITY_LEVEL_CAP, nameof(TerrainType.Water), EType.ADDITIVE, 2));

        var coastalCity = new City(v1) { CivilizationIndex = 0 };
        var inlandCity = new City(v3) { CivilizationIndex = 0 };
        coastalCity.AddBuilding(new TownHall { Level = 1 });
        inlandCity.AddBuilding(new TownHall { Level = 1 });
        civ.AddCity(coastalCity);
        civ.AddCity(inlandCity);

        return (state, civ, coastalCity, inlandCity);
    }

    [Fact]
    public void GetMaxLevel_InlandCityLevelCap_CapsOnlyCitiesNotTouchingRequiredTerrain()
    {
        var (state, civ, coastalCity, inlandCity) = MermaidCitiesSetup();
        var controller = new BuildingController(state);

        var coastalTownHall = coastalCity.Buildings.OfType<TownHall>().Single();
        var inlandTownHall = inlandCity.Buildings.OfType<TownHall>().Single();

        // Côtière : aucun plafond, garde le maximum normal de l'Hôtel de Ville (4).
        Assert.Equal(4, controller.GetMaxLevel(coastalTownHall, civ, coastalCity));
        // En retrait : plafonnée à 2 par INLAND_CITY_LEVEL_CAP.
        Assert.Equal(2, controller.GetMaxLevel(inlandTownHall, civ, inlandCity));
    }

    [Fact]
    public void GetMaxLevel_InlandCityLevelCap_ExcludesMineAtCappedLevelButAllowsBeyondIt()
    {
        var (state, _, _, inlandCity) = MermaidCitiesSetup();
        var mine = new Mine();
        var inlandMap = state.GetMapFor(inlandCity.Position)!;
        var inlandTownHall = inlandCity.Buildings.OfType<TownHall>().Single();

        // Touche la Montagne (h6) : côté terrain la Mine serait constructible, seul le niveau
        // (AvailableAtLevel = 3) doit encore bloquer.
        inlandTownHall.Level = 2; // maximum atteignable pour cette ville d'après INLAND_CITY_LEVEL_CAP
        inlandCity.InvalidateLevelCache();
        Assert.False(mine.IsBuildingAvailableForCity(inlandMap, inlandCity, null));

        // Preuve différentielle : sans le plafond (simulé ici en dépassant volontairement la
        // valeur qu'INLAND_CITY_LEVEL_CAP autoriserait), la Mine deviendrait disponible — c'est
        // donc bien le plafond, pas le terrain, qui l'exclut ci-dessus.
        inlandTownHall.Level = 3;
        inlandCity.InvalidateLevelCache();
        Assert.True(mine.IsBuildingAvailableForCity(inlandMap, inlandCity, null));
    }

    [Fact]
    public void GetMaxLevel_WithCityOverload_MatchesCivWideOverload_ForRacesWithoutInlandCap()
    {
        // Non-régression : sans INLAND_CITY_LEVEL_CAP (toute race autre que Sirènes), le surcharge
        // à 3 arguments doit rendre exactement le même résultat que la version civ-wide existante.
        var (state, civ, v1, _, _) = RibbonIsland();
        var city = new City(v1) { CivilizationIndex = 0 };
        city.AddBuilding(new TownHall { Level = 1 });
        civ.AddCity(city);
        AddRaceModifiers(civ, new Modifier(ECategory.CITY_MIN_DISTANCE, EType.REPLACER, 2)); // ex. Gobelins

        var controller = new BuildingController(state);
        var townHall = city.Buildings.OfType<TownHall>().Single();

        Assert.Equal(controller.GetMaxLevel(townHall, civ), controller.GetMaxLevel(townHall, civ, city));
    }

    // ── Définitions des races ────────────────────────────────────────────────

    [Fact]
    public void RaceDefinitions_RacialBuildingsAreUniqueModifierOnlyBuildingsGatedByRace()
    {
        foreach (var race in RaceDefinitions.All.Where(r => r.RacialBuilding != null))
        {
            var prototype = BuildingFactory.Create(race.RacialBuilding!.Value);
            Assert.NotNull(prototype);
            Assert.True(prototype!.IsUnique, $"{race.RacialBuilding} doit être unique");
            Assert.IsAssignableFrom<IUniqueBuilding>(prototype);
            // Niveau max par défaut 0 : seul le modifier +1 de la race le rend constructible.
            Assert.Equal(0, prototype.GetDefaultMaxLevel());
            Assert.Contains(race.Modifiers, m =>
                m.Category == ECategory.BUILDING_MAX_LEVEL &&
                m.SubCategory == race.RacialBuilding.Value.ToString() &&
                (int)m.Value == 1);
        }
    }

    [Fact]
    public void RaceDefinitions_GoblinMaxLevelMalus_SparesTownHallAndUniqueBuildings()
    {
        var goblin = RaceDefinitions.Get(RaceId.Goblin);
        var malus = goblin.Modifiers
            .Where(m => m.Category == ECategory.BUILDING_MAX_LEVEL && (int)m.Value == -1)
            .Select(m => m.SubCategory)
            .ToList();

        Assert.NotEmpty(malus);
        Assert.Contains(nameof(BuildingType.Sawmill), malus);
        Assert.DoesNotContain(nameof(BuildingType.TownHall), malus);
        Assert.DoesNotContain(nameof(BuildingType.WarRoom), malus);
        Assert.DoesNotContain(nameof(BuildingType.GreatBurrow), malus);
        // Temple : niveau max par défaut 1 (le bonus vient de Foi) — inclus malgré ce départ bas,
        // BuildingController.GetMaxLevel applique le malus en dernier et le plafonne à 1 minimum
        // (voir GetMaxLevel_GoblinMalus_NeverDropsBuildingBelowOneButCapsTempleWithFaith).
        Assert.Contains(nameof(BuildingType.Temple), malus);
    }

    /// <summary>
    /// Le malus racial (BUILDING_MAX_LEVEL négatif, ex. Gobelins -1 sur les bâtiments standards)
    /// s'applique en dernier dans BuildingController.GetMaxLevel et ne peut jamais rendre
    /// inconstructible un bâtiment par ailleurs atteignable (plafonné à 1 minimum) ni faire apparaître
    /// un bâtiment jamais débloqué par une autre source (reste à 0, inchangé).
    /// </summary>
    [Fact]
    public void GetMaxLevel_GoblinMalus_NeverDropsBuildingBelowOneButCapsTempleWithFaith()
    {
        var state = IslandTestFactory.CreateSevenHexIslandState();
        var civ = state.Civilizations[0];
        var controller = new BuildingController(state);

        AddRaceModifiers(civ, new Modifier(ECategory.BUILDING_MAX_LEVEL, nameof(BuildingType.Temple), EType.ADDITIVE, -1));

        // Sans Foi : Temple reste à son niveau de base (1), le malus ne le fait pas passer à 0.
        Assert.Equal(1, controller.GetMaxLevel(new Temple(), civ));

        // Avec Foi (+3, simulé ici directement) : 1 + 3 - 1 = 3 au lieu de 4. Enregistrer un nouveau
        // provider invalide automatiquement le cache de niveau max (ModifierAggregator.Changed).
        AddRaceModifiers(civ, new Modifier(ECategory.BUILDING_MAX_LEVEL, nameof(BuildingType.Temple), EType.ADDITIVE, 3));
        Assert.Equal(3, controller.GetMaxLevel(new Temple(), civ));

        // Un bâtiment jamais débloqué par ailleurs (base 0, aucun bonus positif) reste à 0 : le malus
        // ne le fait pas apparaître artificiellement à 1.
        AddRaceModifiers(civ, new Modifier(ECategory.BUILDING_MAX_LEVEL, nameof(BuildingType.Library), EType.ADDITIVE, -1));
        Assert.Equal(0, controller.GetMaxLevel(new Library(), civ));
    }

    // ── Grand Terrier : réduction des prérequis des bâtiments uniques ───────
    //
    // Académie comme cobaye : Hôtel de Ville 3 et Bibliothèque 4, aucune exigence de terrain à
    // satisfaire en plus (contrairement au Port Impérial, qui veut de l'Eau).

    /// <summary>
    /// Prépare une ville de niveau <paramref name="townHallLevel"/> avec une Bibliothèque du niveau
    /// donné, et débloque l'Académie (niveau max 0 par défaut) pour que seuls les prérequis décident.
    /// </summary>
    private static (WorldState state, BuildingController controller, City city, Civilization civ)
        AcademyCandidateCity(int townHallLevel, int libraryLevel)
    {
        var state = IslandTestFactory.CreateSevenHexIslandState();
        var civ = state.Civilizations[0];
        var city = civ.Cities[0];
        city.AddBuilding(new TownHall { Level = townHallLevel });
        city.AddBuilding(new Library { Level = libraryLevel });

        AddRaceModifiers(civ, new Modifier(ECategory.BUILDING_MAX_LEVEL, nameof(BuildingType.Academy), EType.ADDITIVE, 1));
        StockResources(civ);

        return (state, new BuildingController(state), city, civ);
    }

    /// <summary>
    /// Remplit la trésorerie bien au-delà de tout coût de construction. Écriture directe plutôt que
    /// AddResource : ce dernier plafonne au stockage disponible, qui sans Entrepôt ne couvre pas les
    /// centaines d'unités que coûte un bâtiment unique — et c'est le prérequis qu'on teste ici, pas
    /// la capacité de stockage.
    /// </summary>
    private static void StockResources(Civilization civ)
    {
        foreach (Resource resource in Enum.GetValues<Resource>())
            civ.Resources[resource] = 1000;
    }

    [Fact]
    public void GreatBurrow_RequiresTownHallLevel3()
    {
        Assert.Equal(3, new GreatBurrow().AvailableAtLevel);
    }

    [Fact]
    public void GetUniqueBuildingsAndBuildables_PrerequisiteReduction_LowersRequiredCityLevel()
    {
        // Hôtel de Ville 2 : sous le seuil de 3 de l'Académie, elle n'est pas proposée.
        var (_, controller, city, civ) = AcademyCandidateCity(townHallLevel: 2, libraryLevel: 4);
        Assert.DoesNotContain(controller.GetUniqueBuildingsAndBuildables(city), b => b.Type == BuildingType.Academy);

        AddRaceModifiers(civ, new Modifier(ECategory.UNIQUE_BUILDING_PREREQUISITE_REDUCTION, EType.ADDITIVE, 1));
        Assert.Contains(controller.GetUniqueBuildingsAndBuildables(city), b => b.Type == BuildingType.Academy);
    }

    [Fact]
    public void HasBuildPrerequisites_PrerequisiteReduction_LowersRequiredBuildingLevel()
    {
        // Bibliothèque 3 : sous les 4 exigés — exactement la situation d'un Gobelin, dont le malus
        // BUILDING_MAX_LEVEL -1 plafonne les bâtiments standards un niveau trop bas.
        var (state, controller, city, civ) = AcademyCandidateCity(townHallLevel: 3, libraryLevel: 3);
        var academy = new Academy();

        Assert.False(academy.HasBuildPrerequisites(city, state));

        AddRaceModifiers(civ, new Modifier(ECategory.UNIQUE_BUILDING_PREREQUISITE_REDUCTION, EType.ADDITIVE, 1));
        Assert.True(controller.BuildBuilding(city, BuildingType.Academy));
        Assert.Equal(1, city.FindBuilding(BuildingType.Academy)!.Level);
    }

    [Fact]
    public void HasBuildPrerequisites_PrerequisiteReduction_NeverRemovesTheRequirementEntirely()
    {
        // Un prérequis de niveau 1 tombe à 0 si on le décrémente sans plancher — et un seuil de 0 est
        // satisfait par une ville qui n'a pas le bâtiment du tout. La réduction allège, elle ne
        // supprime pas : la Salle de Guerre continue d'exiger une Garnison.
        var state = IslandTestFactory.CreateSevenHexIslandState();
        var civ = state.Civilizations[0];
        var city = civ.Cities[0];
        city.AddBuilding(new TownHall { Level = 4 });
        AddRaceModifiers(civ,
            new Modifier(ECategory.BUILDING_MAX_LEVEL, nameof(BuildingType.WarRoom), EType.ADDITIVE, 1),
            new Modifier(ECategory.UNIQUE_BUILDING_PREREQUISITE_REDUCTION, EType.ADDITIVE, 1));
        StockResources(civ);

        var controller = new BuildingController(state);
        Assert.DoesNotContain(controller.GetUniqueBuildingsAndBuildables(city),
            b => b.Type == BuildingType.WarRoom && b.Level == 0 && new WarRoom().HasBuildPrerequisites(city, state));
        Assert.False(controller.BuildBuilding(city, BuildingType.WarRoom));

        city.AddBuilding(new Garrison { Level = 1 });
        Assert.True(controller.BuildBuilding(city, BuildingType.WarRoom));
    }

}
