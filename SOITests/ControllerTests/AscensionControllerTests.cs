using SettlersOfIdlestan.Controller;
using SettlersOfIdlestan.Controller.Ascension;
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
using SOITests.TestUtilities;
using System.Linq;
using Xunit;

namespace SOITests.ControllerTests;

/// <summary>
/// Tests des pouvoirs divins et du bâtiment unique permanent d'Ascension (voir AscensionController.
/// CanPurchasePower/PurchasePower, PermanentUniqueBuildingChoices/SelectPermanentUniqueBuilding/
/// ApplyPermanentUniqueBuildingToCivilization) : coût en points divins, emplacements de bâtiment
/// permanent (1 par Ascension effectuée et par pouvoir de la colonne Héritage, rétroactivement,
/// aucun sans ces pouvoirs), application à la civilisation sans occuper d'emplacement
/// en ville, blocage de la construction manuelle, survie à la perte de toutes les villes, et cumul
/// avec un bâtiment unique physiquement construit.
/// </summary>
public class AscensionControllerTests
{
    private static (WorldState state, City city, Civilization civ, AscensionController ascension, GodState godState) CreateTestSetup(
        int godPoints = 100, int ascensionsPerformed = 1, int? prestigePoints = null, GameClock? clock = null)
    {
        var state = IslandTestFactory.CreateSevenHexIslandState();
        var civ = state.Civilizations[0];
        var city = civ.Cities[0];

        var godState = new GodState { GodPoints = godPoints };
        godState.AscensionState.AscensionsPerformed = ascensionsPerformed;
        if (prestigePoints.HasValue)
            godState.PrestigeState = new PrestigeState(state) { PrestigePoints = prestigePoints.Value };

        var ascension = new AscensionController();
        ascension.Initialize(state, clock, new GamePRNG(1), new HarvestController(), godState);

        return (state, city, civ, ascension, godState);
    }

    private static void UnlockWalkOfGod(AscensionController ascension)
    {
        Assert.True(ascension.PurchasePower(AscensionPowerId.Faith));
        Assert.True(ascension.PurchasePower(AscensionPowerId.WalkOfGod));
    }

    private static void UnlockPresenceOfGod(AscensionController ascension)
    {
        UnlockWalkOfGod(ascension);
        Assert.True(ascension.PurchasePower(AscensionPowerId.PresenceOfGod));
    }

    [Fact]
    public void PermanentUniqueBuildingChoices_AreAllValidUniqueBuildingTypes()
    {
        // Contrairement à une exigence antérieure, tous les choix ne sont pas forcément
        // IUniqueBuilding (BuildersGuild/ImperialPort/AdventurersGuild n'apportent pas de modifier
        // civ-wide) : seul IsUnique + une factory valide sont garantis.
        foreach (var type in new AscensionController().PermanentUniqueBuildingChoices)
        {
            var prototype = BuildingFactory.Create(type);
            Assert.NotNull(prototype);
            Assert.True(prototype!.IsUnique, $"{type} should be IsUnique to be a valid ascension choice");
        }
    }

    /// <summary>
    /// Tous les bâtiments uniques non-raciaux sont éligibles comme choix permanent, sans exception :
    /// leur automatisation éventuelle interroge déjà Civilization.GetUniqueBuilding plutôt que de
    /// parcourir les bâtiments physiques des villes, donc rien ne dépend d'une présence physique en
    /// ville (voir le commentaire de BasePermanentUniqueBuildingChoices).
    /// </summary>
    [Fact]
    public void PermanentUniqueBuildingChoices_IncludesAllNonRacialUniqueBuildingsWithoutException()
    {
        var choices = new AscensionController().PermanentUniqueBuildingChoices;
        Assert.Contains(BuildingType.Academy, choices);
        Assert.Contains(BuildingType.AdventurersGuild, choices);
        Assert.Contains(BuildingType.ArcaneTower, choices);
        Assert.Contains(BuildingType.ArtisansGuild, choices);
        Assert.Contains(BuildingType.BlastFurnace, choices);
        Assert.Contains(BuildingType.BuildersGuild, choices);
        Assert.Contains(BuildingType.GrandTemple, choices);
        Assert.Contains(BuildingType.HarvestersGuild, choices);
        Assert.Contains(BuildingType.ImperialPort, choices);
        Assert.Contains(BuildingType.TraderGuild, choices);
        Assert.Contains(BuildingType.VolcanicForge, choices);
        Assert.Contains(BuildingType.WarRoom, choices);
    }

    [Fact]
    public void PermanentUniqueBuildingSlots_GrantsTwoSlotsPerAscension()
    {
        // Jalon Héritage Ancestral (voir AscensionMilestoneId.PermanentUniqueBuildings) : 2
        // emplacements par Ascension effectuée, gratuitement, dès la première Ascension accomplie.
        var (_, _, _, ascension, godState) = CreateTestSetup(ascensionsPerformed: 3);

        Assert.Equal(6, ascension.PermanentUniqueBuildingSlots);
        Assert.Equal(3, godState.AscensionState.AscensionsPerformed);
    }

    [Fact]
    public void PermanentUniqueBuildingSlots_BeforeFirstAscension_IsZero()
    {
        var (_, _, _, ascension, _) = CreateTestSetup(ascensionsPerformed: 0);

        Assert.Equal(0, ascension.PermanentUniqueBuildingSlots);
    }

    [Fact]
    public void ApplyPermanentUniqueBuildingToCivilization_MoreChoicesThanSlots_TrimsExcessAndGrantsOnlyWhatIsUnlocked()
    {
        // Avant toute Ascension, le jalon Héritage Ancestral (qui exige AscensionsPerformed ≥ 1)
        // n'ouvre le moindre emplacement — des bâtiments choisis dans cet état (ex. sauvegarde
        // corrompue) doivent donc être intégralement retirés.
        var (_, _, civ, ascension, godState) = CreateTestSetup(ascensionsPerformed: 0);
        godState.AscensionState.PermanentUniqueBuildings.Add(BuildingType.WarRoom);
        godState.AscensionState.PermanentUniqueBuildings.Add(BuildingType.Academy);

        ascension.ApplyPermanentUniqueBuildingToCivilization();

        Assert.Empty(ascension.PermanentUniqueBuildings);
        Assert.Empty(civ.UniqueBuildings);
    }

    [Fact]
    public void SelectPermanentUniqueBuilding_ValidCandidateWithSlotAvailable_ReturnsTrueAndPersistsToState()
    {
        var (_, _, _, ascension, godState) = CreateTestSetup(ascensionsPerformed: 1);

        var result = ascension.SelectPermanentUniqueBuilding(BuildingType.WarRoom);

        Assert.True(result);
        Assert.Contains(BuildingType.WarRoom, ascension.PermanentUniqueBuildings);
        Assert.Contains(BuildingType.WarRoom, godState.AscensionState.PermanentUniqueBuildings);
    }

    [Fact]
    public void SelectPermanentUniqueBuilding_NonCandidateType_ReturnsFalseAndLeavesStateUnset()
    {
        var (_, _, _, ascension, godState) = CreateTestSetup(ascensionsPerformed: 1);

        var result = ascension.SelectPermanentUniqueBuilding(BuildingType.TownHall);

        Assert.False(result);
        Assert.Empty(ascension.PermanentUniqueBuildings);
        Assert.Empty(godState.AscensionState.PermanentUniqueBuildings);
    }

    [Fact]
    public void SelectPermanentUniqueBuilding_NoSlotsAvailable_ReturnsFalse()
    {
        var (_, _, _, ascension, _) = CreateTestSetup(ascensionsPerformed: 0);

        var result = ascension.SelectPermanentUniqueBuilding(BuildingType.WarRoom);

        Assert.False(result);
        Assert.Empty(ascension.PermanentUniqueBuildings);
    }

    [Fact]
    public void SelectPermanentUniqueBuilding_ExceedingSlotCount_ReturnsFalseAndLeavesFirstChoiceUnchanged()
    {
        // ascensionsPerformed: 1 -> exactement 2 emplacements (jalon Héritage Ancestral, voir
        // AscensionMilestoneId.PermanentUniqueBuildings) : les deux premiers choix passent, le
        // troisième doit échouer sans rien changer aux deux déjà choisis.
        var (_, _, _, ascension, _) = CreateTestSetup(ascensionsPerformed: 1);
        ascension.SelectPermanentUniqueBuilding(BuildingType.WarRoom);
        ascension.SelectPermanentUniqueBuilding(BuildingType.Academy);

        var result = ascension.SelectPermanentUniqueBuilding(BuildingType.ArcaneTower);

        Assert.False(result);
        Assert.Equal(new[] { BuildingType.WarRoom, BuildingType.Academy }, ascension.PermanentUniqueBuildings);
    }

    [Fact]
    public void SelectPermanentUniqueBuilding_WithMultipleSlots_AllowsDistinctChoicesUpToLimit()
    {
        var (_, _, _, ascension, _) = CreateTestSetup(ascensionsPerformed: 2);

        Assert.True(ascension.SelectPermanentUniqueBuilding(BuildingType.WarRoom));
        Assert.True(ascension.SelectPermanentUniqueBuilding(BuildingType.Academy));

        Assert.Equal(2, ascension.PermanentUniqueBuildings.Count);
        Assert.Contains(BuildingType.WarRoom, ascension.PermanentUniqueBuildings);
        Assert.Contains(BuildingType.Academy, ascension.PermanentUniqueBuildings);
    }

    [Fact]
    public void DeselectPermanentUniqueBuilding_FreesSlotForAnotherChoice()
    {
        var (_, _, _, ascension, _) = CreateTestSetup(ascensionsPerformed: 1);
        ascension.SelectPermanentUniqueBuilding(BuildingType.WarRoom);

        var deselectResult = ascension.DeselectPermanentUniqueBuilding(BuildingType.WarRoom);
        var selectResult = ascension.SelectPermanentUniqueBuilding(BuildingType.Academy);

        Assert.True(deselectResult);
        Assert.True(selectResult);
        Assert.Equal(new[] { BuildingType.Academy }, ascension.PermanentUniqueBuildings);
    }

    [Fact]
    public void ApplyPermanentUniqueBuildingToCivilization_NoneSelected_GrantsNothing()
    {
        var (_, _, civ, ascension, _) = CreateTestSetup();

        ascension.ApplyPermanentUniqueBuildingToCivilization();

        Assert.Empty(civ.UniqueBuildings);
    }

    [Fact]
    public void ApplyPermanentUniqueBuildingToCivilization_RegistersBuildingAtLevelOneWithoutPhysicalInstance()
    {
        var (_, city, civ, ascension, _) = CreateTestSetup(ascensionsPerformed: 1);
        ascension.SelectPermanentUniqueBuilding(BuildingType.WarRoom);

        ascension.ApplyPermanentUniqueBuildingToCivilization();

        Assert.Contains(BuildingType.WarRoom, civ.UniqueBuildings);
        var granted = civ.GetUniqueBuilding(BuildingType.WarRoom);
        Assert.NotNull(granted);
        Assert.Equal(1, granted!.Level);
        Assert.DoesNotContain(city.Buildings, b => b.Type == BuildingType.WarRoom);
    }

    [Fact]
    public void ApplyPermanentUniqueBuildingToCivilization_ContributesUniqueBuildingModifiers()
    {
        var (_, _, civ, ascension, _) = CreateTestSetup(ascensionsPerformed: 1);
        ascension.SelectPermanentUniqueBuilding(BuildingType.WarRoom);

        ascension.ApplyPermanentUniqueBuildingToCivilization();

        // WarRoom.GetUniqueBuildingModifiers() : UNIT_PRODUCTION_SPEED +0.5 additif (base 1.0)
        Assert.Equal(1.5, civ.UnitProductionSpeed, precision: 5);
    }

    [Fact]
    public void ApplyPermanentUniqueBuildingToCivilization_MultipleGrantedBuildings_BothContributeModifiers()
    {
        var (_, _, civ, ascension, _) = CreateTestSetup(ascensionsPerformed: 2);
        ascension.SelectPermanentUniqueBuilding(BuildingType.WarRoom);
        ascension.SelectPermanentUniqueBuilding(BuildingType.Academy);

        ascension.ApplyPermanentUniqueBuildingToCivilization();

        // WarRoom : UNIT_PRODUCTION_SPEED +0.5 (base 1.0). Academy accordée à son niveau max absolu
        // (5, voir Academy.GetAbsoluteMaxLevel) : RESEARCH_PRODUCTION_SPEED +0.1*5 = +0.5 (base 1.0).
        // Les deux doivent s'appliquer.
        Assert.Equal(1.5, civ.UnitProductionSpeed, precision: 5);
        Assert.Equal(1.5, civ.ResearchProductionSpeed, precision: 5);
    }

    [Fact]
    public void ApplyPermanentUniqueBuildingToCivilization_BlocksManualConstruction()
    {
        var (state, city, civ, ascension, _) = CreateTestSetup(ascensionsPerformed: 1);
        ascension.SelectPermanentUniqueBuilding(BuildingType.WarRoom);
        ascension.ApplyPermanentUniqueBuildingToCivilization();

        civ.AddResource(Resource.Stone, 1000);
        civ.AddResource(Resource.Gold, 1000);
        civ.AddResource(Resource.Ore, 1000);

        var buildingController = new BuildingController(state);
        var result = buildingController.BuildBuilding(city, BuildingType.WarRoom);

        Assert.False(result);
        Assert.DoesNotContain(city.Buildings, b => b.Type == BuildingType.WarRoom);
    }

    [Fact]
    public void ApplyPermanentUniqueBuildingToCivilization_SurvivesLossOfAllCities()
    {
        var (_, city, civ, ascension, _) = CreateTestSetup(ascensionsPerformed: 1);
        ascension.SelectPermanentUniqueBuilding(BuildingType.WarRoom);
        ascension.ApplyPermanentUniqueBuildingToCivilization();

        civ.RemoveCity(city);

        Assert.Contains(BuildingType.WarRoom, civ.UniqueBuildings);
        Assert.NotNull(civ.GetUniqueBuilding(BuildingType.WarRoom));
        Assert.Equal(1.5, civ.UnitProductionSpeed, precision: 5);
    }

    [Fact]
    public void ApplyPermanentUniqueBuildingToCivilization_CombinesWithDifferentPhysicallyBuiltUniqueBuilding()
    {
        var (_, city, civ, ascension, _) = CreateTestSetup(ascensionsPerformed: 1);
        ascension.SelectPermanentUniqueBuilding(BuildingType.WarRoom);
        ascension.ApplyPermanentUniqueBuildingToCivilization();

        // Bâtiment unique différent, construit normalement dans une ville.
        var academy = new Academy { Level = 2 };
        city.AddBuilding(academy);
        civ.RegisterUniqueBuildingInCache(academy);
        civ.RebuildUniqueBuildingsModifiers();

        // WarRoom (Ascension) : UNIT_PRODUCTION_SPEED +0.5. Academy niveau 2 (physique) :
        // RESEARCH_PRODUCTION_SPEED +0.1*2 = +0.2 (base 1.0). Les deux doivent s'appliquer sans se
        // marcher dessus.
        Assert.Equal(1.5, civ.UnitProductionSpeed, precision: 5);
        Assert.Equal(1.2, civ.ResearchProductionSpeed, precision: 5);
    }

    /// <summary>
    /// Bâtiment unique déjà construit en ville, puis choisi comme bâtiment permanent : l'exemplaire
    /// physique est détruit au début d'île suivant (voir
    /// Civilization.SetAscensionGrantedUniqueBuildings) et remplacé par le bâtiment accordé, au
    /// niveau max absolu et sans occuper d'emplacement de ville.
    /// </summary>
    [Fact]
    public void ApplyPermanentUniqueBuildingToCivilization_SameUniqueBuildingAlreadyBuiltInCity_DestroysThePhysicalCopy()
    {
        var (_, city, civ, ascension, _) = CreateTestSetup(ascensionsPerformed: 1);

        // Académie construite normalement dans une ville, avant tout choix de bâtiment permanent.
        var academy = new Academy { Level = 2 };
        city.AddBuilding(academy);
        civ.RegisterUniqueBuildingInCache(academy);
        civ.RebuildUniqueBuildingsModifiers();
        Assert.Contains(city.Buildings, b => b.Type == BuildingType.Academy);

        ascension.SelectPermanentUniqueBuilding(BuildingType.Academy);
        ascension.ApplyPermanentUniqueBuildingToCivilization();

        // L'emplacement de ville est libéré : l'exemplaire construit n'existe plus.
        Assert.DoesNotContain(city.Buildings, b => b.Type == BuildingType.Academy);

        // Le bâtiment accordé prend sa place : instance neuve, au niveau max absolu, hors des villes.
        int grantedLevel = ascension.GetPermanentUniqueBuildingLevel(BuildingType.Academy);
        var granted = civ.GetUniqueBuilding(BuildingType.Academy);
        Assert.NotNull(granted);
        Assert.NotSame(academy, granted);
        Assert.Equal(grantedLevel, granted!.Level);
        Assert.True(civ.IsAscensionGrantedUniqueBuilding(BuildingType.Academy));

        // Les modifiers viennent du seul bâtiment accordé — ni ceux de l'exemplaire détruit
        // (niveau 2), ni les deux cumulés.
        Assert.Equal(1.0 + 0.1 * grantedLevel, civ.ResearchProductionSpeed, precision: 5);
    }

    /// <summary>
    /// Après destruction de l'exemplaire construit (voir le test précédent), le type reste bloqué à
    /// la construction manuelle : le bâtiment accordé compte comme l'unique exemplaire.
    /// </summary>
    [Fact]
    public void ApplyPermanentUniqueBuildingToCivilization_AfterDestroyingPhysicalCopy_BlocksRebuildingIt()
    {
        var (state, city, civ, ascension, _) = CreateTestSetup(ascensionsPerformed: 1);

        city.AddBuilding(new Academy { Level = 2 });
        civ.RebuildUniqueBuildingCache();
        civ.RebuildUniqueBuildingsModifiers();

        ascension.SelectPermanentUniqueBuilding(BuildingType.Academy);
        ascension.ApplyPermanentUniqueBuildingToCivilization();

        civ.AddResource(Resource.Brick, 1000);
        civ.AddResource(Resource.Stone, 1000);
        civ.AddResource(Resource.Glass, 1000);

        var buildingController = new BuildingController(state);
        var result = buildingController.BuildBuilding(city, BuildingType.Academy);

        Assert.False(result);
        Assert.DoesNotContain(city.Buildings, b => b.Type == BuildingType.Academy);
        Assert.Contains(BuildingType.Academy, civ.UniqueBuildings);
    }

    [Fact]
    public void CanPurchasePower_Faith_CostsOneGodPointAndRequiresEnoughPoints()
    {
        var (_, _, _, ascension, _) = CreateTestSetup(godPoints: 0);
        Assert.False(ascension.CanPurchasePower(AscensionPowerId.Faith));

        var (_, _, _, ascensionWithPoints, _) = CreateTestSetup(godPoints: 1);
        Assert.True(ascensionWithPoints.CanPurchasePower(AscensionPowerId.Faith));
    }

    [Fact]
    public void PurchasePower_DeductsGodPointCostOnSuccess()
    {
        var (_, _, _, ascension, godState) = CreateTestSetup(godPoints: 5);

        var result = ascension.PurchasePower(AscensionPowerId.Faith);

        Assert.True(result);
        Assert.True(ascension.IsPowerUnlocked(AscensionPowerId.Faith));
        Assert.Equal(4, godState.GodPoints);
    }

    [Fact]
    public void PurchasePower_InsufficientGodPoints_FailsAndLeavesPointsUntouched()
    {
        var (_, _, _, ascension, godState) = CreateTestSetup(godPoints: 0);

        var result = ascension.PurchasePower(AscensionPowerId.Faith);

        Assert.False(result);
        Assert.False(ascension.IsPowerUnlocked(AscensionPowerId.Faith));
        Assert.Equal(0, godState.GodPoints);
    }

    [Fact]
    public void PurchasePower_SecondTierColumnPower_RequiresFirstTierUnlockedRegardlessOfPoints()
    {
        var (_, _, _, ascension, godState) = CreateTestSetup(godPoints: 100);

        // DivineInventory (colonne 0, coût 5) nécessite HandOfGod (colonne 0, coût 2) déjà débloqué,
        // même avec largement assez de points divins.
        Assert.False(ascension.CanPurchasePower(AscensionPowerId.DivineInventory));

        ascension.PurchasePower(AscensionPowerId.Faith);
        ascension.PurchasePower(AscensionPowerId.HandOfGod);

        Assert.True(ascension.CanPurchasePower(AscensionPowerId.DivineInventory));
        Assert.True(ascension.PurchasePower(AscensionPowerId.DivineInventory));
        Assert.Equal(100 - 1 - 2 - 5, godState.GodPoints);
    }

    /// <summary>Pose un Dominion sur un hex de l'île de test — Marche de Dieu ne cible que les hexs sous Dominion de niveau 2+.</summary>
    private static (HexCoord hex, Dominion dominion) SeedDominion(WorldState state, int level, int q = 0, int r = 0)
    {
        var hex = new HexCoord(q, r, SettlersOfIdlestan.Model.IslandMap.IslandMap.SurfaceLayer);
        var dominion = new Dominion(hex, level);
        state.AddFeature(dominion);
        return (hex, dominion);
    }

    /// <summary>
    /// Barème commun aux trois pouvoirs ciblés : gratuit, 1, 2, puis doublement à chaque usage.
    /// Le plafond du décalage évite un débordement d'int sur les nombres d'usages absurdes.
    /// </summary>
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 4)]
    [InlineData(4, 8)]
    [InlineData(5, 16)]
    [InlineData(10, 512)]
    [InlineData(31, 1 << 30)]
    [InlineData(60, 1 << 30)]
    public void TargetedPowerCost_DoublesAfterTwo(int uses, int expectedCost)
    {
        Assert.Equal(expectedCost, AscensionController.TargetedPowerCost(uses));
    }

    [Fact]
    public void GetWalkOfGodCost_FirstUseIsFreeThenDoubles()
    {
        var (state, _, _, ascension, godState) = CreateTestSetup(godPoints: 100, prestigePoints: 10);
        UnlockWalkOfGod(ascension);
        var (hex, dominion) = SeedDominion(state, level: 5);
        Assert.Contains(hex, ascension.GetWalkOfGodTargetHexes());

        // Première marche depuis le dernier prestige : gratuite (seul le Dominion est consommé).
        Assert.Equal(0, ascension.GetWalkOfGodCost());
        Assert.True(ascension.ApplyWalkOfGod(hex));
        Assert.Equal(10, godState.PrestigeState!.PrestigePoints);
        Assert.Equal(4, dominion.Level);

        Assert.Equal(1, ascension.GetWalkOfGodCost());
        Assert.True(ascension.ApplyWalkOfGod(hex));
        Assert.Equal(9, godState.PrestigeState!.PrestigePoints);
        Assert.Equal(3, dominion.Level);

        Assert.Equal(2, ascension.GetWalkOfGodCost());
        Assert.True(ascension.ApplyWalkOfGod(hex));
        Assert.Equal(7, godState.PrestigeState!.PrestigePoints);
        Assert.Equal(2, dominion.Level);

        // Passé 2, le coût double à chaque marche suivante : 4, 8, 16...
        Assert.Equal(4, ascension.GetWalkOfGodCost());
    }

    /// <summary>
    /// La gratuité ne vaut que pour la première marche : une fois celle-ci consommée, une cagnotte
    /// vide bloque bien le pouvoir (c'est le cas nominal sur la première île d'un cycle d'Ascension,
    /// où PrestigeState.PrestigePoints part de zéro).
    /// </summary>
    [Fact]
    public void ApplyWalkOfGod_InsufficientPrestigePointsAfterTheFreeUse_FailsAndLeavesStateUntouched()
    {
        var (state, _, _, ascension, godState) = CreateTestSetup(godPoints: 100, prestigePoints: 0);
        UnlockWalkOfGod(ascension);
        godState.PrestigeState!.WalkOfGodUsesSinceLastPrestige = 1;
        var (hex, dominion) = SeedDominion(state, level: 2);
        var terrainBefore = state.GetMapFor(hex)!.GetTile(hex)!.TerrainType;

        var result = ascension.ApplyWalkOfGod(hex);

        Assert.False(result);
        Assert.Equal(0, godState.PrestigeState!.PrestigePoints);
        Assert.Equal(1, godState.PrestigeState!.WalkOfGodUsesSinceLastPrestige);
        Assert.Equal(terrainBefore, state.GetMapFor(hex)!.GetTile(hex)!.TerrainType);
        Assert.Equal(2, dominion.Level);
    }

    /// <summary>Le premier usage gratuit fonctionne avec une cagnotte à zéro — l'île 1 d'une Ascension.</summary>
    [Fact]
    public void ApplyWalkOfGod_WithNoPrestigePointsAtAll_StillWorksOnce()
    {
        var (state, _, _, ascension, godState) = CreateTestSetup(godPoints: 100, prestigePoints: 0);
        UnlockWalkOfGod(ascension);
        var (hex, _) = SeedDominion(state, level: 5);

        Assert.True(ascension.CanUseWalkOfGod());
        Assert.True(ascension.ApplyWalkOfGod(hex));
        Assert.Equal(0, godState.PrestigeState!.PrestigePoints);

        Assert.False(ascension.CanUseWalkOfGod());
        Assert.False(ascension.ApplyWalkOfGod(hex));
    }

    [Fact]
    public void ApplyWalkOfGod_NoPrestigeState_Fails()
    {
        var (state, _, _, ascension, _) = CreateTestSetup(godPoints: 100, prestigePoints: null);
        UnlockWalkOfGod(ascension);
        var (hex, _) = SeedDominion(state, level: 2);

        Assert.False(ascension.ApplyWalkOfGod(hex));
    }

    /// <summary>
    /// Race à terrain de prédilection : marcher sur n'importe quel autre terrain y fait pousser ce
    /// terrain, de façon déterministe. C'est ce qui rend le pouvoir utile à un Elfe — retomber au
    /// hasard sur la Forêt (1 chance sur 4) ne lui ouvrirait pratiquement jamais d'emplacement.
    /// </summary>
    [Fact]
    public void ApplyWalkOfGod_RaceWithFavouredTerrain_GrowsItOnAnyOtherTerrain()
    {
        var (state, _, _, ascension, godState) = CreateTestSetup(godPoints: 100, prestigePoints: 10);
        godState.AscensionState.SelectedRace = RaceId.Elf;
        UnlockWalkOfGod(ascension);
        var (hex, _) = SeedDominion(state, level: 5);
        state.GetMapFor(hex)!.GetTile(hex)!.TerrainType = TerrainType.Desert;

        Assert.Equal(TerrainType.Forest, ascension.FavouredTerrain);
        Assert.True(ascension.ApplyWalkOfGod(hex));
        Assert.Equal(TerrainType.Forest, state.GetMapFor(hex)!.GetTile(hex)!.TerrainType);
    }

    /// <summary>
    /// Sur le terrain de prédilection lui-même, le déterminisme tombe : la marche transforme, elle ne
    /// conserve pas. Le terrain obtenu est tiré au sort, et n'est donc jamais celui de départ.
    /// </summary>
    [Fact]
    public void ApplyWalkOfGod_RaceWithFavouredTerrain_OnThatTerrain_IsRandom()
    {
        var (state, _, _, ascension, godState) = CreateTestSetup(godPoints: 100, prestigePoints: 10);
        godState.AscensionState.SelectedRace = RaceId.Elf;
        UnlockWalkOfGod(ascension);
        var (hex, _) = SeedDominion(state, level: 5);
        state.GetMapFor(hex)!.GetTile(hex)!.TerrainType = TerrainType.Forest;

        Assert.True(ascension.ApplyWalkOfGod(hex));
        Assert.NotEqual(TerrainType.Forest, state.GetMapFor(hex)!.GetTile(hex)!.TerrainType);
    }

    /// <summary>
    /// Comme <see cref="CreateTestSetup"/>, mais avec le CityBuilderController câblé : c'est lui qui
    /// détruit les villes que le terrain transformé ne permet plus d'occuper (voir
    /// AscensionController.ApplyWalkOfGod / CityBuilderController.DestroyCitiesInvalidatedByTerrain).
    /// </summary>
    private static (WorldState state, City city, Civilization civ, AscensionController ascension) CreateWalkOfGodSetupWithCityDestruction(RaceId race)
    {
        var state = IslandTestFactory.CreateSevenHexIslandState();
        var civ = state.Civilizations[0];
        var city = civ.Cities[0];

        var godState = new GodState { GodPoints = 100 };
        godState.PrestigeState = new PrestigeState(state) { PrestigePoints = 10 };
        godState.AscensionState.SelectedRace = race;

        // En jeu, les modifiers de la race arrivent sur la civilisation via AscensionController
        // (IModifierProvider) enregistré par MainGameController.SetupModifierAggregators ; ici on
        // les pose directement, sinon CITY_PLACEMENT_REQUIRES_TERRAIN n'existe pas côté civ et la
        // règle de validité de terrain n'a rien à vérifier.
        civ.AddCustomAggregator(new StaticModifierProvider(RaceDefinitions.Get(race).Modifiers));

        var cityBuilder = new CityBuilderController();
        cityBuilder.Initialize(state);

        var ascension = new AscensionController();
        ascension.Initialize(state, clock: null, new GamePRNG(1), new HarvestController(), godState, cityBuilder);
        UnlockWalkOfGod(ascension);

        return (state, city, civ, ascension);
    }

    private static void SetTerrain(WorldState state, HexCoord hex, TerrainType terrain)
        => state.GetMapFor(hex)!.GetTile(hex)!.TerrainType = terrain;

    /// <summary>
    /// Une ville naine dont on efface la Montagne adjacente n'a plus de quoi tenir : elle est détruite
    /// par la marche qui a transformé le terrain.
    /// </summary>
    [Fact]
    public void ApplyWalkOfGod_DestroysCityLeftWithoutItsRaceRequiredTerrain()
    {
        var (state, city, civ, ascension) = CreateWalkOfGodSetupWithCityDestruction(RaceId.Dwarf);

        // La ville tient sur center/NE/E : on fait de E sa seule Montagne, puis on y marche. Pour un
        // Nain, la Montagne est le terrain de prédilection : la marche y tire donc un terrain au hasard,
        // qui n'est jamais la Montagne.
        var mountainHex = new HexCoord(1, 0, SettlersOfIdlestan.Model.IslandMap.IslandMap.SurfaceLayer);
        SetTerrain(state, mountainHex, TerrainType.Mountain);
        Assert.Contains(city, civ.Cities);

        SeedDominion(state, level: 5, q: mountainHex.Q, r: mountainHex.R);
        Assert.True(ascension.ApplyWalkOfGod(mountainHex));

        Assert.NotEqual(TerrainType.Mountain, state.GetMapFor(mountainHex)!.GetTile(mountainHex)!.TerrainType);
        Assert.DoesNotContain(city, civ.Cities);
    }

    /// <summary>Une ville dont les trois hexs deviennent de l'eau est engloutie, race sans contrainte comprise.</summary>
    [Fact]
    public void ApplyWalkOfGod_DestroysCitySurroundedByWater()
    {
        var (state, city, civ, ascension) = CreateWalkOfGodSetupWithCityDestruction(RaceId.Mermaid);

        // Deux des trois hexs de la ville sont déjà noyés ; le troisième le devient par la marche —
        // l'Eau étant le terrain de prédilection des Sirènes, la transformation est déterministe.
        var lastLandHex = new HexCoord(0, 0, SettlersOfIdlestan.Model.IslandMap.IslandMap.SurfaceLayer);
        SetTerrain(state, new HexCoord(0, 1, SettlersOfIdlestan.Model.IslandMap.IslandMap.SurfaceLayer), TerrainType.Water);
        SetTerrain(state, new HexCoord(1, 0, SettlersOfIdlestan.Model.IslandMap.IslandMap.SurfaceLayer), TerrainType.Water);
        Assert.Contains(city, civ.Cities);

        SeedDominion(state, level: 5, q: lastLandHex.Q, r: lastLandHex.R);
        Assert.True(ascension.ApplyWalkOfGod(lastLandHex));

        Assert.Equal(TerrainType.Water, state.GetMapFor(lastLandHex)!.GetTile(lastLandHex)!.TerrainType);
        Assert.DoesNotContain(city, civ.Cities);
    }

    /// <summary>
    /// Le garde-fou de la mécanique : une ville qui reste occupable survit à la transformation d'un de
    /// ses propres hexs. Sans ça, toute marche à proximité raserait la ville.
    /// </summary>
    [Fact]
    public void ApplyWalkOfGod_LeavesStillValidCityStanding()
    {
        var (state, city, civ, ascension) = CreateWalkOfGodSetupWithCityDestruction(RaceId.Human);

        var cityHex = new HexCoord(0, 0, SettlersOfIdlestan.Model.IslandMap.IslandMap.SurfaceLayer);
        SeedDominion(state, level: 5, q: cityHex.Q, r: cityHex.R);

        Assert.True(ascension.ApplyWalkOfGod(cityHex));

        // Les deux autres hexs de la ville restent terrestres, et les Humains n'exigent aucun terrain.
        Assert.Contains(city, civ.Cities);
    }

    /// <summary>
    /// Île à 3 hexs d'eau (vertex unique) avec MaritimeBeaconController/WarFleetController câblés sur
    /// AscensionController : c'est eux qui détruisent la balise/la flotte que le terrain transformé
    /// prive de leurs 3 hexs d'eau (voir AscensionController.ApplyWalkOfGod /
    /// MaritimeBeaconController.DestroyBeaconsInvalidatedByTerrain).
    /// </summary>
    private static (WorldState state, Civilization civ, HexCoord hex, Vertex vertex, AscensionController ascension) CreateWalkOfGodSetupWithBeaconAndFleetDestruction()
    {
        var h1 = new HexCoord(0, 0, SettlersOfIdlestan.Model.IslandMap.IslandMap.SurfaceLayer);
        var h2 = new HexCoord(1, 0, SettlersOfIdlestan.Model.IslandMap.IslandMap.SurfaceLayer);
        var h3 = new HexCoord(0, 1, SettlersOfIdlestan.Model.IslandMap.IslandMap.SurfaceLayer);

        var map = new SettlersOfIdlestan.Model.IslandMap.IslandMap(new[]
        {
            new SettlersOfIdlestan.Model.IslandMap.HexTile(h1, TerrainType.Water),
            new SettlersOfIdlestan.Model.IslandMap.HexTile(h2, TerrainType.Water),
            new SettlersOfIdlestan.Model.IslandMap.HexTile(h3, TerrainType.Water),
        });

        var civ = new Civilization { Index = 0 };
        var state = new WorldState(map, new System.Collections.Generic.List<Civilization> { civ }, AtlasController.InvalidIslandId);

        var vertex = Vertex.Create(h1, h2, h3);
        civ.AddMaritimeBeacon(new MaritimeBeacon(vertex) { CivilizationIndex = 0 });
        civ.AddFleet(new WarFleet(vertex) { CivilizationIndex = 0 });
        // Marche de Dieu ne cible que le brouillard de guerre levé (IsVisibleToPlayer) : une route
        // touchant le vertex suffit à révéler ses 3 hexs (voir VisibleIslandMap).
        civ.AddRoad(new Road(Edge.Create(h1, h2)) { CivilizationIndex = 0 });
        state.Visibility.Recalculate();

        var godState = new GodState { GodPoints = 100 };
        godState.PrestigeState = new PrestigeState(state) { PrestigePoints = 10 };
        godState.AscensionState.SelectedRace = RaceId.Human;

        var maritimeBeaconController = new MaritimeBeaconController();
        maritimeBeaconController.Initialize(state);
        var warFleetController = new WarFleetController();
        warFleetController.Initialize(state);

        var ascension = new AscensionController();
        ascension.Initialize(state, clock: null, new GamePRNG(1), new HarvestController(), godState,
            cityBuilderController: null, maritimeBeaconController, warFleetController);
        UnlockWalkOfGod(ascension);

        return (state, civ, h1, vertex, ascension);
    }

    /// <summary>
    /// Marcher sur un hex d'eau (Humain, sans terrain de prédilection) le transforme toujours en
    /// terrain terrestre au hasard (Water absent de RandomTerrainPool) : la balise posée sur ce vertex
    /// perd un de ses 3 hexs d'eau, et la flotte posée dessus tombe avec elle.
    /// </summary>
    [Fact]
    public void ApplyWalkOfGod_DestroysBeaconAndFleetLosingWaterSurround()
    {
        var (state, civ, hex, vertex, ascension) = CreateWalkOfGodSetupWithBeaconAndFleetDestruction();
        SeedDominion(state, level: 5, q: hex.Q, r: hex.R);

        Assert.True(ascension.ApplyWalkOfGod(hex));

        Assert.NotEqual(TerrainType.Water, state.GetMapFor(hex)!.GetTile(hex)!.TerrainType);
        Assert.Empty(civ.MaritimeBeacons);
        Assert.Empty(civ.Fleets);
    }

    /// <summary>
    /// Marcher sur l'un des deux hexs d'eau d'un vertex de ville rend constructible l'arête maritime
    /// restante (bloquée jusque-là faute d'UNLOCK_MARITIME_ROUTES, une fois l'un des deux hexs d'eau
    /// devenu terrestre) sans que le nombre de villes/balises de la civilisation — seule clé du cache
    /// de <see cref="RoadController"/> — ne bouge : sans le câblage de RoadController dans
    /// AscensionController.Initialize, le cache resterait figé sur l'état d'avant la marche.
    /// </summary>
    [Fact]
    public void ApplyWalkOfGod_InvalidatesBuildableRoadsCacheForAffectedLayer()
    {
        var h1 = new HexCoord(0, 0, SettlersOfIdlestan.Model.IslandMap.IslandMap.SurfaceLayer);
        var h2 = new HexCoord(1, 0, SettlersOfIdlestan.Model.IslandMap.IslandMap.SurfaceLayer);
        var h3 = new HexCoord(0, 1, SettlersOfIdlestan.Model.IslandMap.IslandMap.SurfaceLayer);

        var map = new SettlersOfIdlestan.Model.IslandMap.IslandMap(new[]
        {
            new SettlersOfIdlestan.Model.IslandMap.HexTile(h1, TerrainType.Plain),
            new SettlersOfIdlestan.Model.IslandMap.HexTile(h2, TerrainType.Water),
            new SettlersOfIdlestan.Model.IslandMap.HexTile(h3, TerrainType.Water),
        });

        var civ = new Civilization { Index = 0 };
        var state = new WorldState(map, new System.Collections.Generic.List<Civilization> { civ }, AtlasController.InvalidIslandId);

        var vertex = Vertex.Create(h1, h2, h3);
        new SettlersOfIdlestan.Controller.Generator.IslandMapGenerator(new GamePRNG(1)).PopulatePlayerCivilization(map, civ, vertex);
        // Marche de Dieu ne cible que le brouillard de guerre levé : une route touchant le vertex
        // suffit à révéler ses 3 hexs (voir VisibleIslandMap).
        civ.AddRoad(new Road(Edge.Create(h1, h2)) { CivilizationIndex = 0 });
        state.Visibility.Recalculate();

        var roadController = new RoadController(state);
        var waterEdge = Edge.Create(h2, h3);
        Assert.DoesNotContain(roadController.GetBuildableRoads(0), r => r.Position.Equals(waterEdge));

        var godState = new GodState { GodPoints = 100 };
        godState.PrestigeState = new PrestigeState(state) { PrestigePoints = 10 };
        godState.AscensionState.SelectedRace = RaceId.Human;

        var ascension = new AscensionController();
        ascension.Initialize(state, clock: null, new GamePRNG(1), new HarvestController(), godState, roadController: roadController);
        UnlockWalkOfGod(ascension);
        SeedDominion(state, level: 5, q: h2.Q, r: h2.R);

        Assert.True(ascension.ApplyWalkOfGod(h2));
        Assert.NotEqual(TerrainType.Water, state.GetMapFor(h2)!.GetTile(h2)!.TerrainType);

        Assert.Contains(roadController.GetBuildableRoads(0), r => r.Position.Equals(waterEdge));
    }

    /// <summary>
    /// Île à 3 hexs de terre (vertex unique) avec MobileCampController câblé sur AscensionController :
    /// c'est lui qui détruit le camp englouti sous 3 hexs d'eau par le terrain transformé (voir
    /// AscensionController.ApplyWalkOfGod / MobileCampController.DestroyCampsInvalidatedByTerrain).
    /// </summary>
    private static (WorldState state, Civilization civ, HexCoord[] hexes, Vertex vertex, AscensionController ascension) CreateWalkOfGodSetupWithMobileCampDestruction()
    {
        var h1 = new HexCoord(0, 0, SettlersOfIdlestan.Model.IslandMap.IslandMap.SurfaceLayer);
        var h2 = new HexCoord(1, 0, SettlersOfIdlestan.Model.IslandMap.IslandMap.SurfaceLayer);
        var h3 = new HexCoord(0, 1, SettlersOfIdlestan.Model.IslandMap.IslandMap.SurfaceLayer);

        var map = new SettlersOfIdlestan.Model.IslandMap.IslandMap(new[]
        {
            new SettlersOfIdlestan.Model.IslandMap.HexTile(h1, TerrainType.Plain),
            new SettlersOfIdlestan.Model.IslandMap.HexTile(h2, TerrainType.Plain),
            new SettlersOfIdlestan.Model.IslandMap.HexTile(h3, TerrainType.Plain),
        });

        var civ = new Civilization { Index = 0 };
        var state = new WorldState(map, new System.Collections.Generic.List<Civilization> { civ }, AtlasController.InvalidIslandId);

        var vertex = Vertex.Create(h1, h2, h3);
        civ.AddMobileCamp(new MobileCamp(vertex) { CivilizationIndex = 0 });
        // Marche de Dieu ne cible que le brouillard de guerre levé (IsVisibleToPlayer) : une route
        // touchant le vertex suffit à révéler ses 3 hexs (voir VisibleIslandMap).
        civ.AddRoad(new Road(Edge.Create(h1, h2)) { CivilizationIndex = 0 });
        state.Visibility.Recalculate();

        var godState = new GodState { GodPoints = 100 };
        godState.PrestigeState = new PrestigeState(state) { PrestigePoints = 10 };
        // Sirène : l'Eau est son terrain de prédilection, donc marcher sur un hex de terre y fait
        // toujours pousser de l'Eau de façon déterministe (voir ApplyWalkOfGod).
        godState.AscensionState.SelectedRace = RaceId.Mermaid;

        var cityBuilderController = new CityBuilderController();
        cityBuilderController.Initialize(state);
        var mobileCampController = new MobileCampController();
        mobileCampController.Initialize(state, cityBuilderController);

        var ascension = new AscensionController();
        ascension.Initialize(state, clock: null, new GamePRNG(1), new HarvestController(), godState,
            cityBuilderController, mobileCampController: mobileCampController);
        UnlockWalkOfGod(ascension);

        return (state, civ, new[] { h1, h2, h3 }, vertex, ascension);
    }

    /// <summary>
    /// Les 3 hexs du camp tournent à l'Eau un par un ; le camp ne tombe qu'une fois les 3 engloutis,
    /// jamais avant (garde-fou symétrique à ApplyWalkOfGod_LeavesStillValidCityStanding).
    /// </summary>
    [Fact]
    public void ApplyWalkOfGod_DestroysMobileCampOnceAllThreeHexesBecomeWater()
    {
        var (state, civ, hexes, vertex, ascension) = CreateWalkOfGodSetupWithMobileCampDestruction();

        SeedDominion(state, level: 2, q: hexes[0].Q, r: hexes[0].R);
        Assert.True(ascension.ApplyWalkOfGod(hexes[0]));
        Assert.Single(civ.MobileCamps);

        SeedDominion(state, level: 2, q: hexes[1].Q, r: hexes[1].R);
        Assert.True(ascension.ApplyWalkOfGod(hexes[1]));
        Assert.Single(civ.MobileCamps);

        SeedDominion(state, level: 2, q: hexes[2].Q, r: hexes[2].R);
        Assert.True(ascension.ApplyWalkOfGod(hexes[2]));

        Assert.All(vertex.GetHexes(), h => Assert.Equal(TerrainType.Water, state.GetMapFor(h)!.GetTile(h)!.TerrainType));
        Assert.Empty(civ.MobileCamps);
    }

    /// <summary>Race sans contrainte de placement : aucun terrain privilégié, tirage aléatoire comme avant.</summary>
    [Fact]
    public void ApplyWalkOfGod_RaceWithoutFavouredTerrain_JustChangesTheTerrain()
    {
        var (state, _, _, ascension, godState) = CreateTestSetup(godPoints: 100, prestigePoints: 10);
        godState.AscensionState.SelectedRace = RaceId.Human;
        UnlockWalkOfGod(ascension);
        var (hex, _) = SeedDominion(state, level: 5);
        state.GetMapFor(hex)!.GetTile(hex)!.TerrainType = TerrainType.Desert;

        Assert.Null(ascension.FavouredTerrain);
        Assert.True(ascension.ApplyWalkOfGod(hex));
        Assert.NotEqual(TerrainType.Desert, state.GetMapFor(hex)!.GetTile(hex)!.TerrainType);
    }

    [Fact]
    public void GetWalkOfGodTargetHexes_OnlyIncludesHexesWithDominionLevel2OrMore()
    {
        var (state, _, _, ascension, _) = CreateTestSetup(godPoints: 100, prestigePoints: 10);
        UnlockWalkOfGod(ascension);

        Assert.Empty(ascension.GetWalkOfGodTargetHexes());

        var (weakHex, _) = SeedDominion(state, level: 1, q: 1, r: 0);
        // NE (0,1) plutôt que Ouest (-1,0) : ce dernier n'est pas dans le champ de vision de la
        // ville de test (voir IsVisibleToPlayer), qui ne couvre que centre/NE/Est.
        var (strongHex, _) = SeedDominion(state, level: 2, q: 0, r: 1);

        var targets = ascension.GetWalkOfGodTargetHexes();
        Assert.DoesNotContain(weakHex, targets);
        Assert.Contains(strongHex, targets);
        Assert.Single(targets);
    }

    [Fact]
    public void GetWalkOfGodTargetHexes_ExcludesDeepWaterAndVoid()
    {
        var (state, _, _, ascension, _) = CreateTestSetup(godPoints: 100, prestigePoints: 10);
        UnlockWalkOfGod(ascension);

        var (deepWaterHex, _) = SeedDominion(state, level: 2, q: 0, r: 0);
        state.GetMapFor(deepWaterHex)!.GetTile(deepWaterHex)!.TerrainType = TerrainType.DeepWater;
        var (voidHex, _) = SeedDominion(state, level: 2, q: 0, r: 1);
        state.GetMapFor(voidHex)!.GetTile(voidHex)!.TerrainType = TerrainType.Void;

        Assert.Empty(ascension.GetWalkOfGodTargetHexes());
    }

    [Fact]
    public void ApplyWalkOfGod_OnDeepWaterOrVoid_FailsAndCostsNothing()
    {
        var (state, _, _, ascension, godState) = CreateTestSetup(godPoints: 100, prestigePoints: 10);
        UnlockWalkOfGod(ascension);

        var (deepWaterHex, _) = SeedDominion(state, level: 2, q: 0, r: 0);
        state.GetMapFor(deepWaterHex)!.GetTile(deepWaterHex)!.TerrainType = TerrainType.DeepWater;
        var (voidHex, _) = SeedDominion(state, level: 2, q: 0, r: 1);
        state.GetMapFor(voidHex)!.GetTile(voidHex)!.TerrainType = TerrainType.Void;

        Assert.False(ascension.ApplyWalkOfGod(deepWaterHex));
        Assert.False(ascension.ApplyWalkOfGod(voidHex));
        Assert.Equal(10, godState.PrestigeState!.PrestigePoints);
        Assert.Equal(0, godState.PrestigeState!.WalkOfGodUsesSinceLastPrestige);
    }

    [Fact]
    public void ApplyWalkOfGod_WithoutDominionLevel2_FailsAndCostsNothing()
    {
        var (state, _, _, ascension, godState) = CreateTestSetup(godPoints: 100, prestigePoints: 10);
        UnlockWalkOfGod(ascension);
        var (hex, _) = SeedDominion(state, level: 1);

        Assert.False(ascension.ApplyWalkOfGod(hex));
        Assert.Equal(10, godState.PrestigeState!.PrestigePoints);
        Assert.Equal(0, godState.PrestigeState!.WalkOfGodUsesSinceLastPrestige);
    }

    [Fact]
    public void ApplyWalkOfGod_OnWaterHexWithDominion_TransformsTerrainAndReducesDominion()
    {
        var (state, _, _, ascension, godState) = CreateTestSetup(godPoints: 100, prestigePoints: 10);
        UnlockWalkOfGod(ascension);
        var (hex, dominion) = SeedDominion(state, level: 2);
        state.GetMapFor(hex)!.GetTile(hex)!.TerrainType = TerrainType.Water;

        Assert.True(ascension.ApplyWalkOfGod(hex));

        Assert.NotEqual(TerrainType.Water, state.GetMapFor(hex)!.GetTile(hex)!.TerrainType);
        Assert.Equal(1, dominion.Level);
        // Première marche : gratuite, la cagnotte est intacte.
        Assert.Equal(10, godState.PrestigeState!.PrestigePoints);

        // Retombé au niveau 1 : l'hex n'est plus ciblable tant que le Dominion n'a pas regagné du niveau.
        Assert.DoesNotContain(hex, ascension.GetWalkOfGodTargetHexes());
        Assert.False(ascension.ApplyWalkOfGod(hex));
    }

    [Theory]
    [InlineData(LayerState.AbyssZ)]
    [InlineData(LayerState.PandemoniumZ)]
    public void ApplyWalkOfGod_OnAbyssOrPandemoniumHex_WithSufficientDominion_Succeeds(int z)
    {
        var (state, _, _, ascension, godState) = CreateTestSetup(godPoints: 100, prestigePoints: 10);
        UnlockWalkOfGod(ascension);

        var layer = LayerState.EstablishOupostInNewAutoExpandLayer(state.PlayerCivilization, z, surroundWithVoid: true);
        state.AddLayer(z, layer);

        var hex = new HexCoord(0, 0, z);
        state.AddFeature(new Dominion(hex, level: 5));

        Assert.Contains(hex, ascension.GetWalkOfGodTargetHexes());
        Assert.True(ascension.ApplyWalkOfGod(hex));
        Assert.NotEqual(TerrainType.Mountain, state.GetMapFor(hex)!.GetTile(hex)!.TerrainType);
        Assert.Equal(10, godState.PrestigeState!.PrestigePoints); // Première marche : gratuite.
    }

    /// <summary>Le Void, quel que soit le calque, reste hors de portée même avec un Dominion suffisant — voir aussi ApplyWalkOfGod_OnVoidHexWithDominion_IsRejected pour le Void de surface.</summary>
    [Theory]
    [InlineData(LayerState.AbyssZ)]
    [InlineData(LayerState.PandemoniumZ)]
    public void ApplyWalkOfGod_OnAbyssOrPandemoniumVoidHexWithDominion_IsRejected(int z)
    {
        var (state, _, _, ascension, godState) = CreateTestSetup(godPoints: 100, prestigePoints: 10);
        UnlockWalkOfGod(ascension);

        var layer = LayerState.EstablishOupostInNewAutoExpandLayer(state.PlayerCivilization, z, surroundWithVoid: true);
        state.AddLayer(z, layer);

        // (1, -1, z) fait partie de l'anneau de Void entourant le triangle (0,0)/(1,0)/(0,1).
        var voidHex = new HexCoord(1, -1, z);
        Assert.Equal(TerrainType.Void, state.GetMapFor(voidHex)!.GetTile(voidHex)!.TerrainType);
        state.AddFeature(new Dominion(voidHex, level: 5));

        Assert.DoesNotContain(voidHex, ascension.GetWalkOfGodTargetHexes());
        Assert.False(ascension.ApplyWalkOfGod(voidHex));
        Assert.Equal(10, godState.PrestigeState!.PrestigePoints);
    }

    /// <summary>Crée une couche Inframonde (triangle Montagne par défaut) et l'attache à l'état de test.</summary>
    private static HexCoord AddUnderworldLayerWithDominion(WorldState state, int dominionLevel)
    {
        var layer = LayerState.EstablishOupostInNewAutoExpandLayer(state.PlayerCivilization, LayerState.UnderworldZ);
        state.AddLayer(LayerState.UnderworldZ, layer);

        var hex = new HexCoord(0, 0, LayerState.UnderworldZ);
        state.AddFeature(new Dominion(hex, dominionLevel));
        return hex;
    }

    [Fact]
    public void ApplyWalkOfGod_OnUnderworldHex_WithSufficientDominion_Succeeds()
    {
        var (state, _, _, ascension, godState) = CreateTestSetup(godPoints: 100, prestigePoints: 10);
        UnlockWalkOfGod(ascension);
        var hex = AddUnderworldLayerWithDominion(state, dominionLevel: 5);

        Assert.Contains(hex, ascension.GetWalkOfGodTargetHexes());
        Assert.True(ascension.ApplyWalkOfGod(hex));
        Assert.NotEqual(TerrainType.Mountain, state.GetMapFor(hex)!.GetTile(hex)!.TerrainType);
        Assert.Equal(10, godState.PrestigeState!.PrestigePoints); // Première marche : gratuite.
    }

    /// <summary>Le pool aléatoire souterrain (AscensionController.UnderworldRandomTerrainPool) ne contient ni Forêt ni Plaine, absentes sous terre.</summary>
    [Fact]
    public void ApplyWalkOfGod_OnUnderworldHex_NeverGrowsForestOrPlain()
    {
        for (int seed = 1; seed <= 10; seed++)
        {
            var state = IslandTestFactory.CreateSevenHexIslandState();
            var godState = new GodState { GodPoints = 100 };
            godState.PrestigeState = new PrestigeState(state) { PrestigePoints = 10 };
            var ascension = new AscensionController();
            ascension.Initialize(state, clock: null, new GamePRNG(seed), new HarvestController(), godState);
            UnlockWalkOfGod(ascension);
            var hex = AddUnderworldLayerWithDominion(state, dominionLevel: 5);

            Assert.True(ascension.ApplyWalkOfGod(hex));
            var terrain = state.GetMapFor(hex)!.GetTile(hex)!.TerrainType;
            Assert.NotEqual(TerrainType.Forest, terrain);
            Assert.NotEqual(TerrainType.Plain, terrain);
        }
    }

    /// <summary>
    /// Sous terre, il n'y a pas de Forêt : pour un Elfe, le terrain de prédilection traduit via
    /// TerrainTypeExtensions.UnderworldEquivalent devient la Caverne aux champignons.
    /// </summary>
    [Fact]
    public void ApplyWalkOfGod_ElfInUnderworld_GrowsMushroomCaveInsteadOfForest()
    {
        var (state, _, _, ascension, godState) = CreateTestSetup(godPoints: 100, prestigePoints: 10);
        godState.AscensionState.SelectedRace = RaceId.Elf;
        UnlockWalkOfGod(ascension);
        var hex = AddUnderworldLayerWithDominion(state, dominionLevel: 5);

        Assert.Equal(TerrainType.Mountain, state.GetMapFor(hex)!.GetTile(hex)!.TerrainType);
        Assert.True(ascension.ApplyWalkOfGod(hex));
        Assert.Equal(TerrainType.MushroomCave, state.GetMapFor(hex)!.GetTile(hex)!.TerrainType);
    }

    [Fact]
    public void GetWalkOfGodTargetHexes_IncludesUnderworldHexesWithSufficientDominion()
    {
        var (state, _, _, ascension, _) = CreateTestSetup(godPoints: 100, prestigePoints: 10);
        UnlockWalkOfGod(ascension);
        var hex = AddUnderworldLayerWithDominion(state, dominionLevel: 2);

        Assert.Contains(hex, ascension.GetWalkOfGodTargetHexes());
    }

    [Fact]
    public void GetWalkOfGodCost_DoublesFromWalkOfGodUsesSinceLastPrestige()
    {
        var (_, _, _, ascension, godState) = CreateTestSetup(godPoints: 100, prestigePoints: 10);
        UnlockWalkOfGod(ascension);

        godState.PrestigeState!.WalkOfGodUsesSinceLastPrestige = 4;

        Assert.Equal(8, ascension.GetWalkOfGodCost());
    }

    [Fact]
    public void PresenceOfGod_RequiresWalkOfGodFirstInColumn()
    {
        var (_, _, _, ascension, _) = CreateTestSetup(godPoints: 100);
        Assert.True(ascension.PurchasePower(AscensionPowerId.Faith));

        Assert.False(ascension.CanPurchasePower(AscensionPowerId.PresenceOfGod));

        Assert.True(ascension.PurchasePower(AscensionPowerId.WalkOfGod));
        Assert.True(ascension.CanPurchasePower(AscensionPowerId.PresenceOfGod));
    }

    [Fact]
    public void ApplyPresenceOfGod_DispelsCorruptionThenSeedsDominionOnAreaAndCostsPrestige()
    {
        var (state, _, _, ascension, godState) = CreateTestSetup(godPoints: 100, prestigePoints: 10);
        UnlockPresenceOfGod(ascension);

        var center = new HexCoord(0, 0, SettlersOfIdlestan.Model.IslandMap.IslandMap.SurfaceLayer);
        var east = new HexCoord(1, 0, SettlersOfIdlestan.Model.IslandMap.IslandMap.SurfaceLayer);
        var west = new HexCoord(-1, 0, SettlersOfIdlestan.Model.IslandMap.IslandMap.SurfaceLayer);
        state.AddFeature(new Corruption(center, level: 2));
        state.AddFeature(new Corruption(east, level: 10));

        Assert.True(ascension.ApplyPresenceOfGod(center));

        // Hex visé (5 points) : corruption niveau 2 dissipée, reliquat 3 en Dominion.
        Assert.Empty(state.GetFeaturesAt(center).OfType<Corruption>());
        Assert.Equal(3, state.GetFeaturesAt(center).OfType<Dominion>().Single().Level);

        // Voisin corrompu (3 points) : corruption 10 → 7, pas de Dominion.
        Assert.Equal(7, state.GetFeaturesAt(east).OfType<Corruption>().Single().Level);
        Assert.Empty(state.GetFeaturesAt(east).OfType<Dominion>());

        // Voisin vide (3 points) : Dominion niveau 3.
        Assert.Equal(3, state.GetFeaturesAt(west).OfType<Dominion>().Single().Level);

        // Première manifestation depuis le dernier prestige : gratuite.
        Assert.Equal(10, godState.PrestigeState!.PrestigePoints);
        Assert.Equal(1, godState.PrestigeState!.PresenceOfGodUsesSinceLastPrestige);
    }

    [Fact]
    public void GetPresenceOfGodCost_FirstUseIsFreeThenDoubles()
    {
        var (_, _, _, ascension, godState) = CreateTestSetup(godPoints: 100, prestigePoints: 10);
        UnlockPresenceOfGod(ascension);
        var hex = ascension.GetPresenceOfGodTargetHexes()[0];

        Assert.Equal(0, ascension.GetPresenceOfGodCost());
        Assert.True(ascension.ApplyPresenceOfGod(hex));
        Assert.Equal(10, godState.PrestigeState!.PrestigePoints);

        Assert.Equal(1, ascension.GetPresenceOfGodCost());
        Assert.True(ascension.ApplyPresenceOfGod(hex));
        Assert.Equal(9, godState.PrestigeState!.PrestigePoints);

        Assert.Equal(2, ascension.GetPresenceOfGodCost());
        Assert.True(ascension.ApplyPresenceOfGod(hex));
        Assert.Equal(7, godState.PrestigeState!.PrestigePoints);

        // Passé 2, le coût double à chaque manifestation suivante : 4, 8, 16...
        Assert.Equal(4, ascension.GetPresenceOfGodCost());
    }

    [Fact]
    public void ApplyPresenceOfGod_InsufficientPrestigePoints_FailsAndLeavesStateUntouched()
    {
        var (state, _, _, ascension, godState) = CreateTestSetup(godPoints: 100, prestigePoints: 0);
        UnlockPresenceOfGod(ascension);
        // La première manifestation étant gratuite, il faut en avoir déjà consommé une pour que
        // l'absence de points de prestige bloque le pouvoir.
        godState.PrestigeState!.PresenceOfGodUsesSinceLastPrestige = 1;
        var hex = ascension.GetPresenceOfGodTargetHexes()[0];

        Assert.False(ascension.ApplyPresenceOfGod(hex));

        Assert.Equal(0, godState.PrestigeState!.PrestigePoints);
        Assert.Equal(1, godState.PrestigeState!.PresenceOfGodUsesSinceLastPrestige);
        Assert.Empty(state.Features.OfType<Dominion>());
    }

    [Fact]
    public void GetPresenceOfGodCost_DoublesFromPresenceOfGodUsesSinceLastPrestige()
    {
        var (_, _, _, ascension, godState) = CreateTestSetup(godPoints: 100, prestigePoints: 10);
        UnlockPresenceOfGod(ascension);

        godState.PrestigeState!.PresenceOfGodUsesSinceLastPrestige = 4;

        Assert.Equal(8, ascension.GetPresenceOfGodCost());
    }

    [Fact]
    public void GetPresenceOfGodTargetHexes_IncludesWater()
    {
        var (state, _, _, ascension, _) = CreateTestSetup(godPoints: 100, prestigePoints: 10);
        UnlockPresenceOfGod(ascension);

        // Est (1,0) plutôt qu'Ouest (-1,0) : ce dernier n'est pas dans le champ de vision de la
        // ville de test (voir IsVisibleToPlayer), qui ne couvre que centre/NE/Est.
        var east = new HexCoord(1, 0, SettlersOfIdlestan.Model.IslandMap.IslandMap.SurfaceLayer);
        var west = new HexCoord(-1, 0, SettlersOfIdlestan.Model.IslandMap.IslandMap.SurfaceLayer);
        state.GetMapFor(east)!.GetTile(east)!.TerrainType = SettlersOfIdlestan.Model.IslandMap.TerrainType.Water;

        var targets = ascension.GetPresenceOfGodTargetHexes();
        Assert.Contains(east, targets);
        // Hex hors du brouillard de guerre découvert : jamais ciblable, même sur la surface.
        Assert.DoesNotContain(west, targets);
    }

    [Fact]
    public void ApplyPresenceOfGod_OnWaterHex_SeedsDominion()
    {
        var (state, _, _, ascension, _) = CreateTestSetup(godPoints: 100, prestigePoints: 10);
        UnlockPresenceOfGod(ascension);

        // Est (1,0) plutôt qu'Ouest (-1,0) : ce dernier n'est pas dans le champ de vision de la
        // ville de test (voir IsVisibleToPlayer), qui ne couvre que centre/NE/Est.
        var east = new HexCoord(1, 0, SettlersOfIdlestan.Model.IslandMap.IslandMap.SurfaceLayer);
        state.GetMapFor(east)!.GetTile(east)!.TerrainType = SettlersOfIdlestan.Model.IslandMap.TerrainType.Water;

        Assert.True(ascension.ApplyPresenceOfGod(east));

        // L'eau est un hex valide : le Dominion y naît comme sur la terre (prélude à Marche de Dieu).
        Assert.Equal(5, state.GetFeaturesAt(east).OfType<Dominion>().Single().Level);
    }

    [Fact]
    public void GetPresenceOfGodTargetHexes_ExcludesDeepWater()
    {
        var (state, _, _, ascension, _) = CreateTestSetup(godPoints: 100, prestigePoints: 10);
        UnlockPresenceOfGod(ascension);

        var east = new HexCoord(1, 0, SettlersOfIdlestan.Model.IslandMap.IslandMap.SurfaceLayer);
        state.GetMapFor(east)!.GetTile(east)!.TerrainType = TerrainType.DeepWater;

        Assert.DoesNotContain(east, ascension.GetPresenceOfGodTargetHexes());
    }

    [Fact]
    public void ApplyPresenceOfGod_OnDeepWater_FailsAndCostsNothing()
    {
        var (state, _, _, ascension, godState) = CreateTestSetup(godPoints: 100, prestigePoints: 10);
        UnlockPresenceOfGod(ascension);

        var east = new HexCoord(1, 0, SettlersOfIdlestan.Model.IslandMap.IslandMap.SurfaceLayer);
        state.GetMapFor(east)!.GetTile(east)!.TerrainType = TerrainType.DeepWater;

        Assert.False(ascension.ApplyPresenceOfGod(east));
        Assert.Equal(10, godState.PrestigeState!.PrestigePoints);
        Assert.Equal(0, godState.PrestigeState!.PresenceOfGodUsesSinceLastPrestige);
    }

    /// <summary>Crée une couche Inframonde et la rend courante, sans y poser de Dominion.</summary>
    private static HexCoord AddViewedUnderworldLayer(WorldState state)
    {
        var layer = LayerState.EstablishOupostInNewAutoExpandLayer(state.PlayerCivilization, LayerState.UnderworldZ);
        state.AddLayer(LayerState.UnderworldZ, layer);
        state.CurrentViewedLayer = LayerState.UnderworldZ;
        return new HexCoord(0, 0, LayerState.UnderworldZ);
    }

    /// <summary>
    /// Présence de Dieu suit le calque affiché et n'est pas réservée à la surface : la liste de cibles
    /// ne contient que des hexs de l'Inframonde quand c'est lui qu'on regarde, et aucun de la surface.
    /// </summary>
    [Fact]
    public void GetPresenceOfGodTargetHexes_CoversTheViewedLayerNotOnlyTheSurface()
    {
        var (state, _, _, ascension, _) = CreateTestSetup(godPoints: 100, prestigePoints: 10);
        UnlockPresenceOfGod(ascension);
        var underworld = AddViewedUnderworldLayer(state);

        var targets = ascension.GetPresenceOfGodTargetHexes();

        Assert.Contains(underworld, targets);
        Assert.All(targets, hex => Assert.Equal(LayerState.UnderworldZ, hex.Z));
    }

    /// <summary>
    /// L'enchaînement qui donne son intérêt à la levée de la restriction de calque : Poing de Dieu ne
    /// frappe que sous Dominion, et en profondeur la Présence est la seule source de Dominion que le
    /// joueur puisse viser. Sans elle, le poing n'a aucune cible hors de la surface.
    /// </summary>
    [Fact]
    public void ApplyPresenceOfGod_OnUnderworldHex_SeedsTheDominionFistOfGodNeeds()
    {
        var (state, _, _, ascension, _) = CreateTestSetup(godPoints: 100, prestigePoints: 10);
        UnlockPresenceOfGod(ascension);
        Assert.True(ascension.PurchasePower(AscensionPowerId.ArmOfGod));
        Assert.True(ascension.PurchasePower(AscensionPowerId.FistOfGod));

        var underworld = AddViewedUnderworldLayer(state);
        Assert.Empty(ascension.GetFistOfGodTargetHexes());

        Assert.True(ascension.ApplyPresenceOfGod(underworld));

        Assert.Equal(AscensionController.PresenceOfGodCenterPoints,
            state.GetFeaturesAt(underworld).OfType<Dominion>().Single().Level);
        Assert.Contains(underworld, ascension.GetFistOfGodTargetHexes());
    }

    [Fact]
    public void GetModifiers_ArmOfGod_GrantsSoldierAttackDamageBonus()
    {
        var (_, _, _, ascension, _) = CreateTestSetup(godPoints: 100);
        Assert.True(ascension.PurchasePower(AscensionPowerId.Faith));

        Assert.DoesNotContain(ascension.GetModifiers(), m => m.Category == Modifier.ECategory.SOLDIER_ATTACK_DAMAGE);

        Assert.True(ascension.PurchasePower(AscensionPowerId.ArmOfGod));

        var modifier = Assert.Single(ascension.GetModifiers()
            .Where(m => m.Category == Modifier.ECategory.SOLDIER_ATTACK_DAMAGE));
        Assert.Equal(Modifier.EType.ADDITIVE, modifier.Type);
        Assert.Equal(1, modifier.Value);
    }

    [Fact]
    public void GetModifiers_Faith_GrantsFlatTempleBonusRegardlessOfRace()
    {
        var (_, _, _, ascension, _) = CreateTestSetup(godPoints: 100);
        Assert.True(ascension.PurchasePower(AscensionPowerId.Faith));

        // Le bonus de Foi lui-même ne dépend pas de la race : c'est le malus standard des Gobelins
        // (RaceDefinitions, appliqué en dernier par BuildingController.GetMaxLevel) qui plafonne
        // ensuite leur Temple à 3 au lieu de 4 — voir
        // RaceSystemTests.GetMaxLevel_GoblinMalus_NeverDropsBuildingBelowOneButCapsTempleWithFaith.
        var modifier = Assert.Single(ascension.GetModifiers()
            .Where(m => m.Category == Modifier.ECategory.BUILDING_MAX_LEVEL && m.SubCategory == "Temple"));
        Assert.Equal(3, modifier.Value);
    }

    [Fact]
    public void GetModifiers_WrathOfGod_GrantsAttackSpeedBonus()
    {
        var (_, _, _, ascension, _) = CreateTestSetup(godPoints: 100);
        Assert.True(ascension.PurchasePower(AscensionPowerId.Faith));
        Assert.True(ascension.PurchasePower(AscensionPowerId.ArmOfGod));
        Assert.True(ascension.PurchasePower(AscensionPowerId.FistOfGod));

        Assert.DoesNotContain(ascension.GetModifiers(), m => m.Category == Modifier.ECategory.ATTACK_SPEED);

        Assert.True(ascension.PurchasePower(AscensionPowerId.WrathOfGod));

        var modifier = Assert.Single(ascension.GetModifiers()
            .Where(m => m.Category == Modifier.ECategory.ATTACK_SPEED));
        Assert.Equal(Modifier.EType.ADDITIVE, modifier.Type);
        Assert.Equal(1.0, modifier.Value);
    }

    [Fact]
    public void WrathOfGod_RequiresFistOfGodFirstInColumn()
    {
        var (_, _, _, ascension, _) = CreateTestSetup(godPoints: 100);
        Assert.True(ascension.PurchasePower(AscensionPowerId.Faith));
        Assert.True(ascension.PurchasePower(AscensionPowerId.ArmOfGod));

        Assert.False(ascension.CanPurchasePower(AscensionPowerId.WrathOfGod));

        Assert.True(ascension.PurchasePower(AscensionPowerId.FistOfGod));
        Assert.True(ascension.CanPurchasePower(AscensionPowerId.WrathOfGod));
    }

    [Fact]
    public void GetModifiers_HornOfPlenty_DoublesEveryAutomaticHarvestAndGrantsBasicResources()
    {
        var (_, _, _, ascension, _) = CreateTestSetup(godPoints: 100);
        Assert.True(ascension.PurchasePower(AscensionPowerId.Faith));
        Assert.True(ascension.PurchasePower(AscensionPowerId.HandOfGod));
        Assert.True(ascension.PurchasePower(AscensionPowerId.DivineInventory));

        Assert.DoesNotContain(ascension.GetModifiers(), m => m.Category == Modifier.ECategory.HARVEST_PRODUCTION_BONUS);

        Assert.True(ascension.PurchasePower(AscensionPowerId.HornOfPlenty));

        // Sans SubCategory : le doublement vaut pour tous les bâtiments récolteurs.
        var doubling = Assert.Single(ascension.GetModifiers()
            .Where(m => m.Category == Modifier.ECategory.HARVEST_PRODUCTION_BONUS));
        Assert.Equal("", doubling.SubCategory);
        Assert.Equal(Modifier.EType.ADDITIVE, doubling.Type);
        Assert.Equal(100, doubling.Value);

        var passive = ascension.GetModifiers()
            .Where(m => m.Category == Modifier.ECategory.PASSIVE_RESOURCE_GENERATION)
            .ToList();
        Assert.Equal(ResourceUtils.BasicResources.Count, passive.Count);
        foreach (var resource in ResourceUtils.BasicResources)
            Assert.Contains(passive, m => m.SubCategory == resource.ToString()
                && m.Value == AscensionController.HornOfPlentyPassiveGenerationPerCycle);
    }

    [Fact]
    public void HornOfPlenty_RequiresDivineInventoryFirstInColumn()
    {
        var (_, _, _, ascension, _) = CreateTestSetup(godPoints: 100);
        Assert.True(ascension.PurchasePower(AscensionPowerId.Faith));
        Assert.True(ascension.PurchasePower(AscensionPowerId.HandOfGod));

        Assert.False(ascension.CanPurchasePower(AscensionPowerId.HornOfPlenty));

        Assert.True(ascension.PurchasePower(AscensionPowerId.DivineInventory));
        Assert.True(ascension.CanPurchasePower(AscensionPowerId.HornOfPlenty));
    }

    // ── Poing de Dieu ────────────────────────────────────────────────────────

    private static void UnlockFistOfGod(AscensionController ascension)
    {
        Assert.True(ascension.PurchasePower(AscensionPowerId.Faith));
        Assert.True(ascension.PurchasePower(AscensionPowerId.ArmOfGod));
        Assert.True(ascension.PurchasePower(AscensionPowerId.FistOfGod));
    }

    private static readonly HexCoord Center = new(0, 0, SettlersOfIdlestan.Model.IslandMap.IslandMap.SurfaceLayer);

    /// <summary>
    /// Pose un Dominion sur un hex : condition de ciblage de Poing de Dieu, dont chaque coup consomme
    /// aussi 1 niveau (voir AscensionController.ApplyFistOfGod). Niveau large par défaut, pour que les
    /// tests qui frappent plusieurs fois ne s'arrêtent pas sur cette condition.
    /// </summary>
    private static Dominion AddDominion(WorldState state, HexCoord hex, int level = 10)
    {
        var dominion = new Dominion(hex, level);
        state.AddFeature(dominion);
        return dominion;
    }

    /// <summary>Ville ennemie posée sur un vertex adjacent au hex central, avec son Hôtel de ville.</summary>
    private static City AddEnemyCityAdjacentToCenter(WorldState state, int townHallLevel = 2, int soldiers = 0, int defense = 0)
    {
        var enemyCiv = new Civilization { Index = 1 };
        var vertex = Vertex.Create(
            Center,
            new HexCoord(1, 0, SettlersOfIdlestan.Model.IslandMap.IslandMap.SurfaceLayer),
            new HexCoord(1, -1, SettlersOfIdlestan.Model.IslandMap.IslandMap.SurfaceLayer));
        var city = new City(vertex) { CivilizationIndex = enemyCiv.Index, Soldiers = soldiers, CurrentDefense = defense };
        city.AddBuilding(new TownHall { Level = townHallLevel });
        enemyCiv.AddCity(city);
        state.AddCivilization(enemyCiv);
        return city;
    }

    [Fact]
    public void FistOfGod_RequiresArmOfGodFirstInColumn()
    {
        var (_, _, _, ascension, _) = CreateTestSetup(godPoints: 100);
        Assert.True(ascension.PurchasePower(AscensionPowerId.Faith));

        Assert.False(ascension.CanPurchasePower(AscensionPowerId.FistOfGod));

        Assert.True(ascension.PurchasePower(AscensionPowerId.ArmOfGod));
        Assert.True(ascension.CanPurchasePower(AscensionPowerId.FistOfGod));
    }

    [Fact]
    public void GetFistOfGodCost_FirstUseIsFreeThenDoubles()
    {
        var (state, _, _, ascension, godState) = CreateTestSetup(godPoints: 100, prestigePoints: 10);
        UnlockFistOfGod(ascension);
        AddDominion(state, Center);

        Assert.Equal(0, ascension.GetFistOfGodCost());
        Assert.True(ascension.ApplyFistOfGod(Center));
        Assert.Equal(10, godState.PrestigeState!.PrestigePoints);

        Assert.Equal(1, ascension.GetFistOfGodCost());
        Assert.True(ascension.ApplyFistOfGod(Center));
        Assert.Equal(9, godState.PrestigeState!.PrestigePoints);

        Assert.Equal(2, ascension.GetFistOfGodCost());
        Assert.True(ascension.ApplyFistOfGod(Center));
        Assert.Equal(7, godState.PrestigeState!.PrestigePoints);

        // Passé 2, le coût double à chaque coup suivant : 4, 8, 16...
        Assert.Equal(4, ascension.GetFistOfGodCost());
    }

    [Fact]
    public void ApplyFistOfGod_DamagesMonsterOnTargetedHex()
    {
        var (state, _, _, ascension, _) = CreateTestSetup(godPoints: 100, prestigePoints: 10);
        UnlockFistOfGod(ascension);
        AddDominion(state, Center);

        // Rats niveau 30 : 5 + 5 × 29 = 150 PV, sans armure.
        var rats = new Rats(Center, level: 30);
        state.AddFeature(rats);

        Assert.True(ascension.ApplyFistOfGod(Center));

        Assert.Equal(150 - AscensionController.FistOfGodDamage, rats.Hp);
        Assert.Contains(rats, state.Features);
    }

    [Fact]
    public void ApplyFistOfGod_KillsAndRemovesMonsterItBringsToZero()
    {
        var (state, _, _, ascension, _) = CreateTestSetup(godPoints: 100, prestigePoints: 10);
        UnlockFistOfGod(ascension);
        AddDominion(state, Center);

        var rats = new Rats(Center);
        state.AddFeature(rats);

        Assert.True(ascension.ApplyFistOfGod(Center));

        Assert.DoesNotContain(rats, state.Features);
        Assert.Equal(state.PlayerCivilization.Index, rats.KilledByCivilizationIndex);
    }

    [Fact]
    public void ApplyFistOfGod_SparesFriendlyMonsters()
    {
        var (state, _, _, ascension, _) = CreateTestSetup(godPoints: 100, prestigePoints: 10);
        UnlockFistOfGod(ascension);
        AddDominion(state, Center);

        // L'Aventurier (AttacksOtherMonsters) est un allié : jamais ciblé, ici comme ailleurs.
        var adventurer = new Adventurer(Center);
        state.AddFeature(adventurer);
        int hpBefore = adventurer.Hp;

        Assert.True(ascension.ApplyFistOfGod(Center));

        Assert.Equal(hpBefore, adventurer.Hp);
        Assert.Contains(adventurer, state.Features);
    }

    [Fact]
    public void ApplyFistOfGod_CascadesDamageOnAdjacentEnemyCity()
    {
        var (state, _, _, ascension, _) = CreateTestSetup(godPoints: 100, prestigePoints: 10);
        UnlockFistOfGod(ascension);
        AddDominion(state, Center);

        // 100 dégâts : 60 soldats, puis 38 de défense, puis les 2 restants sur l'Hôtel de ville.
        var enemyCity = AddEnemyCityAdjacentToCenter(state, townHallLevel: 4, soldiers: 60, defense: 38);

        Assert.True(ascension.ApplyFistOfGod(Center));

        Assert.Equal(0, enemyCity.Soldiers);
        Assert.Equal(0, enemyCity.CurrentDefense);
        Assert.Equal(2, enemyCity.Buildings.OfType<TownHall>().Single().Level);
    }

    [Fact]
    public void ApplyFistOfGod_LeavesEnemyCityStandingWhenDamageIsAbsorbed()
    {
        var (state, _, _, ascension, _) = CreateTestSetup(godPoints: 100, prestigePoints: 10);
        UnlockFistOfGod(ascension);
        AddDominion(state, Center);

        var enemyCity = AddEnemyCityAdjacentToCenter(state, townHallLevel: 3, soldiers: 200, defense: 50);

        Assert.True(ascension.ApplyFistOfGod(Center));

        Assert.Equal(100, enemyCity.Soldiers);
        Assert.Equal(50, enemyCity.CurrentDefense);
        Assert.Equal(3, enemyCity.Buildings.OfType<TownHall>().Single().Level);
    }

    [Fact]
    public void ApplyFistOfGod_DestroysEnemyCityThatLosesItsTownHall()
    {
        var (state, _, _, ascension, godState) = CreateTestSetup(godPoints: 100, prestigePoints: 10);
        var cityBuilder = new CityBuilderController();
        cityBuilder.Initialize(state, clock: null, new GamePRNG(1));
        ascension.Initialize(state, clock: null, new GamePRNG(1), new HarvestController(), godState, cityBuilder);
        UnlockFistOfGod(ascension);
        AddDominion(state, Center);

        var enemyCity = AddEnemyCityAdjacentToCenter(state, townHallLevel: 2);

        Assert.True(ascension.ApplyFistOfGod(Center));

        Assert.DoesNotContain(enemyCity, state.Civilizations[1].Cities);
    }

    [Fact]
    public void ApplyFistOfGod_NeverDamagesPlayerCities()
    {
        var (state, city, _, ascension, _) = CreateTestSetup(godPoints: 100, prestigePoints: 10);
        UnlockFistOfGod(ascension);
        AddDominion(state, Center);

        city.AddBuilding(new TownHall { Level = 3 });
        city.Soldiers = 5;
        city.CurrentDefense = 7;

        // La ville du joueur est elle aussi adjacente au hex central.
        Assert.True(city.Position.IsAdjacentTo(Center));
        Assert.True(ascension.ApplyFistOfGod(Center));

        Assert.Equal(5, city.Soldiers);
        Assert.Equal(7, city.CurrentDefense);
        Assert.Equal(3, city.Buildings.OfType<TownHall>().Single().Level);
    }

    [Fact]
    public void ApplyFistOfGod_InsufficientPrestigePoints_FailsAndLeavesStateUntouched()
    {
        var (state, _, _, ascension, godState) = CreateTestSetup(godPoints: 100, prestigePoints: 0);
        UnlockFistOfGod(ascension);
        AddDominion(state, Center);
        // Premier coup gratuit : il faut en avoir déjà porté un pour que le manque de points bloque.
        godState.PrestigeState!.FistOfGodUsesSinceLastPrestige = 1;

        var rats = new Rats(Center);
        state.AddFeature(rats);

        Assert.False(ascension.ApplyFistOfGod(Center));

        Assert.Equal(rats.MaxHp, rats.Hp);
        Assert.Equal(1, godState.PrestigeState!.FistOfGodUsesSinceLastPrestige);
    }

    [Fact]
    public void GetFistOfGodTargetHexes_OnlyCoversVisibleHexesUnderDominion()
    {
        var (state, _, _, ascension, _) = CreateTestSetup(godPoints: 100, prestigePoints: 10);
        UnlockFistOfGod(ascension);

        // Sans le moindre Dominion, le poing n'a nulle part où s'abattre.
        Assert.Empty(ascension.GetFistOfGodTargetHexes());

        // Carte de 7 hexs, ville sans Tour de Guet (rayon de vision 1) : seuls les 3 hexs du sommet
        // de la ville (Center, NE, E) sont visibles. Un Dominion sur un hex visible est ciblable, un
        // Dominion sur un hex encore sous brouillard de guerre ne l'est pas.
        var w = new HexCoord(-1, 0, SettlersOfIdlestan.Model.IslandMap.IslandMap.SurfaceLayer);
        Assert.DoesNotContain(w, state.Visibility.GetForZ(state.CurrentViewedLayer)[state.PlayerCivilization.Index].Tiles.Keys);
        AddDominion(state, Center);
        AddDominion(state, w);

        var targets = ascension.GetFistOfGodTargetHexes();

        Assert.Equal(new[] { Center }, targets);
    }

    [Fact]
    public void ApplyFistOfGod_ConsumesOneDominionLevelOnTheTargetedHex()
    {
        var (state, _, _, ascension, _) = CreateTestSetup(godPoints: 100, prestigePoints: 10);
        UnlockFistOfGod(ascension);
        var dominion = AddDominion(state, Center, level: 3);

        Assert.True(ascension.ApplyFistOfGod(Center));

        Assert.Equal(2, dominion.Level);
        Assert.Contains(dominion, state.Features);
    }

    /// <summary>
    /// Contrairement à Marche de Dieu (niveau 2 minimum, le Dominion survit toujours à la marche), le
    /// poing frappe dès le niveau 1 et emporte alors le Dominion avec lui.
    /// </summary>
    [Fact]
    public void ApplyFistOfGod_RemovesDominionItBringsToZero()
    {
        var (state, _, _, ascension, _) = CreateTestSetup(godPoints: 100, prestigePoints: 10);
        UnlockFistOfGod(ascension);
        var dominion = AddDominion(state, Center, level: 1);

        Assert.True(ascension.ApplyFistOfGod(Center));

        Assert.DoesNotContain(dominion, state.Features);
        Assert.Empty(ascension.GetFistOfGodTargetHexes());
    }

    [Fact]
    public void ApplyFistOfGod_HexWithoutDominion_FailsAndLeavesStateUntouched()
    {
        var (state, _, _, ascension, godState) = CreateTestSetup(godPoints: 100, prestigePoints: 10);
        UnlockFistOfGod(ascension);

        var rats = new Rats(Center);
        state.AddFeature(rats);

        Assert.False(ascension.ApplyFistOfGod(Center));

        Assert.Equal(rats.MaxHp, rats.Hp);
        Assert.Equal(0, godState.PrestigeState!.FistOfGodUsesSinceLastPrestige);
    }

    [Fact]
    public void ApplyFistOfGod_HexNotVisible_FailsAndLeavesStateUntouched()
    {
        var (state, _, _, ascension, godState) = CreateTestSetup(godPoints: 100, prestigePoints: 10);
        UnlockFistOfGod(ascension);

        // W est hors de la vision de la ville (rayon 1, pas de Tour de Guet) sur cette carte de 7 hexs.
        var w = new HexCoord(-1, 0, SettlersOfIdlestan.Model.IslandMap.IslandMap.SurfaceLayer);
        var dominion = AddDominion(state, w);
        var rats = new Rats(w);
        state.AddFeature(rats);

        Assert.False(ascension.ApplyFistOfGod(w));

        Assert.Equal(rats.MaxHp, rats.Hp);
        Assert.Equal(10, dominion.Level);
        Assert.Equal(0, godState.PrestigeState!.FistOfGodUsesSinceLastPrestige);
    }

    // ── Temps de recharge des pouvoirs divins ciblés ─────────────────────────

    /// <summary>Avance l'horloge d'un temps de recharge complet, en un seul événement de tick.</summary>
    private static void AdvanceOneCooldown(GameClock clock) =>
        clock.SimulateAdvance(AscensionController.TargetedPowerCostDecayTicks,
            chunkTicks: AscensionController.TargetedPowerCostDecayTicks);

    [Fact]
    public void TargetedPowerCooldown_HalvesTheCostAndNeverGoesBackBelowOne()
    {
        var clock = new GameClock();
        var (state, _, _, ascension, _) = CreateTestSetup(godPoints: 100, prestigePoints: 100, clock: clock);
        UnlockFistOfGod(ascension);
        AddDominion(state, Center);

        for (int i = 0; i < 4; i++)
            Assert.True(ascension.ApplyFistOfGod(Center));
        Assert.Equal(8, ascension.GetFistOfGodCost());

        AdvanceOneCooldown(clock);
        Assert.Equal(4, ascension.GetFistOfGodCost());

        AdvanceOneCooldown(clock);
        Assert.Equal(2, ascension.GetFistOfGodCost());

        AdvanceOneCooldown(clock);
        Assert.Equal(1, ascension.GetFistOfGodCost());

        // Plancher : la gratuité du premier usage ne se regagne jamais en attendant.
        AdvanceOneCooldown(clock);
        AdvanceOneCooldown(clock);
        Assert.Equal(1, ascension.GetFistOfGodCost());
    }

    [Fact]
    public void TargetedPowerCooldown_NeverUsedPower_StaysFree()
    {
        var clock = new GameClock();
        var (_, _, _, ascension, _) = CreateTestSetup(godPoints: 100, prestigePoints: 100, clock: clock);
        UnlockFistOfGod(ascension);

        AdvanceOneCooldown(clock);

        Assert.Equal(0, ascension.GetFistOfGodCost());
        Assert.Null(ascension.GetFistOfGodCostDecayRemainingTicks());
    }

    /// <summary>
    /// Un saut de temps (banque hors-ligne, vitesse ×10) peut couvrir plusieurs recharges d'un coup :
    /// chacune doit compter.
    /// </summary>
    [Fact]
    public void TargetedPowerCooldown_LongTimeJump_CatchesUpEveryElapsedCooldown()
    {
        var clock = new GameClock();
        var (_, _, _, ascension, _) = CreateTestSetup(godPoints: 100, prestigePoints: 100, clock: clock);
        UnlockPresenceOfGod(ascension);

        for (int i = 0; i < 5; i++)
            Assert.True(ascension.ApplyPresenceOfGod(Center));
        Assert.Equal(16, ascension.GetPresenceOfGodCost());

        clock.SimulateAdvance(3 * AscensionController.TargetedPowerCostDecayTicks,
            chunkTicks: 3 * AscensionController.TargetedPowerCostDecayTicks);

        Assert.Equal(2, ascension.GetPresenceOfGodCost());
    }

    /// <summary>
    /// Sauvegarde antérieure au temps de recharge (compteur d'usages non nul, aucune échéance
    /// armée) : la première avance de temps arme la recharge sans rien réduire, plutôt que de rendre
    /// d'un coup tous les paliers écoulés depuis la dernière utilisation.
    /// </summary>
    [Fact]
    public void TargetedPowerCooldown_LegacySaveWithoutDecayTick_ArmsBeforeReducing()
    {
        var clock = new GameClock();
        var (_, _, _, ascension, godState) = CreateTestSetup(godPoints: 100, prestigePoints: 100, clock: clock);
        UnlockFistOfGod(ascension);
        godState.PrestigeState!.FistOfGodUsesSinceLastPrestige = 4;
        godState.PrestigeState!.FistOfGodNextCostDecayTick = 0;

        AdvanceOneCooldown(clock);
        Assert.Equal(8, ascension.GetFistOfGodCost());

        AdvanceOneCooldown(clock);
        Assert.Equal(4, ascension.GetFistOfGodCost());
    }

    /// <summary>
    /// Seul le premier lancement payant arme le temps de recharge : réutiliser le pouvoir ensuite
    /// augmente le coût mais ne repousse pas l'échéance en cours, sinon enchaîner les lancements
    /// suffirait à empêcher toute décroissance.
    /// </summary>
    [Fact]
    public void TargetedPowerCooldown_LaterUseDoesNotPushBackTheRunningCooldown()
    {
        var clock = new GameClock();
        var (state, _, _, ascension, _) = CreateTestSetup(godPoints: 100, prestigePoints: 100, clock: clock);
        UnlockFistOfGod(ascension);
        AddDominion(state, Center);

        // Deuxième usage : c'est lui qui arme la recharge.
        Assert.True(ascension.ApplyFistOfGod(Center));
        Assert.True(ascension.ApplyFistOfGod(Center));

        long half = AscensionController.TargetedPowerCostDecayTicks / 2;
        clock.SimulateAdvance(half, chunkTicks: half);
        Assert.Equal(half, ascension.GetFistOfGodCostDecayRemainingTicks());

        // Troisième usage à mi-parcours : le coût double, l'échéance ne bouge pas.
        Assert.True(ascension.ApplyFistOfGod(Center));
        Assert.Equal(4, ascension.GetFistOfGodCost());
        Assert.Equal(half, ascension.GetFistOfGodCostDecayRemainingTicks());

        clock.SimulateAdvance(half, chunkTicks: half);
        Assert.Equal(2, ascension.GetFistOfGodCost());
    }

    [Fact]
    public void GetFistOfGodCostDecayRemainingTicks_CountsDownFromAFullCooldown()
    {
        var clock = new GameClock();
        var (state, _, _, ascension, _) = CreateTestSetup(godPoints: 100, prestigePoints: 100, clock: clock);
        UnlockFistOfGod(ascension);
        AddDominion(state, Center);

        // Un seul coup : le coût est déjà au plancher de 1, aucune recharge n'a lieu d'être.
        Assert.True(ascension.ApplyFistOfGod(Center));
        Assert.Null(ascension.GetFistOfGodCostDecayRemainingTicks());

        Assert.True(ascension.ApplyFistOfGod(Center));
        Assert.Equal(AscensionController.TargetedPowerCostDecayTicks, ascension.GetFistOfGodCostDecayRemainingTicks());

        clock.SimulateAdvance(100, chunkTicks: 100);
        Assert.Equal(AscensionController.TargetedPowerCostDecayTicks - 100, ascension.GetFistOfGodCostDecayRemainingTicks());
    }

    // ── Mémoire de Dieu ──────────────────────────────────────────────────────

    private static void UnlockMemoryOfGod(AscensionController ascension)
    {
        Assert.True(ascension.PurchasePower(AscensionPowerId.Faith));
        Assert.True(ascension.PurchasePower(AscensionPowerId.MemoryOfGod));
    }

    [Fact]
    public void EyeOfGod_RequiresMemoryOfGodFirstInColumn()
    {
        var (_, _, _, ascension, _) = CreateTestSetup(godPoints: 100);
        Assert.True(ascension.PurchasePower(AscensionPowerId.Faith));

        Assert.False(ascension.CanPurchasePower(AscensionPowerId.EyeOfGod));

        Assert.True(ascension.PurchasePower(AscensionPowerId.MemoryOfGod));
        Assert.True(ascension.CanPurchasePower(AscensionPowerId.EyeOfGod));
    }

    [Fact]
    public void GetModifiers_MemoryOfGod_HalvesRepeatableResearchScaling()
    {
        var (_, _, _, ascension, _) = CreateTestSetup(godPoints: 100, prestigePoints: 0);
        Assert.True(ascension.PurchasePower(AscensionPowerId.Faith));

        Assert.DoesNotContain(ascension.GetModifiers(),
            m => m.Category == Modifier.ECategory.REPEATABLE_RESEARCH_SCALING_REDUCTION);

        Assert.True(ascension.PurchasePower(AscensionPowerId.MemoryOfGod));

        var modifier = Assert.Single(ascension.GetModifiers()
            .Where(m => m.Category == Modifier.ECategory.REPEATABLE_RESEARCH_SCALING_REDUCTION));
        Assert.Equal(Modifier.EType.ADDITIVE, modifier.Type);
        Assert.Equal(0.5, modifier.Value);
    }

    /// <summary>
    /// L'achat du pouvoir rend immédiatement les paliers perdus : les recherches répétables remontent
    /// au meilleur rang jamais atteint, modificateurs cumulés compris.
    /// </summary>
    [Fact]
    public void PurchaseMemoryOfGod_RestoresRepeatableResearchToItsBestRankEverReached()
    {
        var (_, _, _, ascension, godState) = CreateTestSetup(godPoints: 100, prestigePoints: 0);
        godState.AscensionState.BestRepeatCounts[TechnologyId.MasterHarvest] = 3;

        UnlockMemoryOfGod(ascension);

        var tree = godState.PrestigeState!.TechnologyTree;
        Assert.Equal(3, tree.RepeatCounts[TechnologyId.MasterHarvest]);
        Assert.Contains(TechnologyId.MasterHarvest, tree.CompletedTechnologies);
        // MasterHarvest : +5% HARVEST_SPEED par complétion, donc 3 rangs = +15%.
        Assert.Equal(0.15, tree.ApplyModifiers(Modifier.ECategory.HARVEST_SPEED, "", 0.0), 3);
    }

    /// <summary>Un palier déjà atteint dans le cycle en cours ne doit jamais être rabaissé par la restauration.</summary>
    [Fact]
    public void PurchaseMemoryOfGod_NeverLowersARankAlreadyHigherThanTheRecord()
    {
        var (_, _, _, ascension, godState) = CreateTestSetup(godPoints: 100, prestigePoints: 0);
        godState.AscensionState.BestRepeatCounts[TechnologyId.MasterHarvest] = 1;
        var tree = godState.PrestigeState!.TechnologyTree;
        tree.CompleteResearch(TechnologyId.MasterHarvest);
        tree.CompleteResearch(TechnologyId.MasterHarvest);
        tree.CompleteResearch(TechnologyId.MasterHarvest);

        UnlockMemoryOfGod(ascension);

        Assert.Equal(3, tree.RepeatCounts[TechnologyId.MasterHarvest]);
    }

    /// <summary>
    /// Le relevé du meilleur palier a lieu à chaque Ascension, pouvoir acquis ou non — sans quoi
    /// Mémoire de Dieu, achetée plus tard, ne saurait rien des cycles déjà joués.
    /// </summary>
    [Fact]
    public void PerformAscension_WithoutMemoryOfGod_ResetsRepeatableResearchButRecordsItsBestRank()
    {
        var controller = CreateAscendableGame(out var godState);
        var tree = controller.CurrentMainState!.PrestigeState!.TechnologyTree;
        tree.CompleteResearch(TechnologyId.MasterHarvest);
        tree.CompleteResearch(TechnologyId.MasterHarvest);

        controller.PerformAscension();

        var newTree = controller.CurrentMainState.PrestigeState!.TechnologyTree;
        Assert.DoesNotContain(TechnologyId.MasterHarvest, newTree.CompletedTechnologies);
        Assert.Equal(2, godState.AscensionState.BestRepeatCounts[TechnologyId.MasterHarvest]);
    }

    [Fact]
    public void PerformAscension_WithMemoryOfGod_KeepsRepeatableResearchAtItsBestRank()
    {
        var controller = CreateAscendableGame(out var godState);
        var ascension = controller.AscensionController;
        Assert.True(ascension.PurchasePower(AscensionPowerId.Faith));
        Assert.True(ascension.PurchasePower(AscensionPowerId.MemoryOfGod));

        var tree = controller.CurrentMainState!.PrestigeState!.TechnologyTree;
        tree.CompleteResearch(TechnologyId.MasterHarvest);
        tree.CompleteResearch(TechnologyId.MasterHarvest);

        controller.PerformAscension();

        var newTree = controller.CurrentMainState.PrestigeState!.TechnologyTree;
        Assert.NotSame(tree, newTree);
        Assert.Equal(2, newTree.RepeatCounts[TechnologyId.MasterHarvest]);
        Assert.Contains(TechnologyId.MasterHarvest, newTree.CompletedTechnologies);
        Assert.Equal(0.10, newTree.ApplyModifiers(Modifier.ECategory.HARVEST_SPEED, "", 0.0), 3);
        Assert.Equal(2, godState.AscensionState.BestRepeatCounts[TechnologyId.MasterHarvest]);
    }

    // ── Jalon Ascension Prestigieuse (AscensionMilestoneId.PrestigiousAscension) ─────────────

    [Fact]
    public void PerformAscension_WithPrestigiousAscensionMilestone_StartsTheNewCycleWithPrestigePoints()
    {
        var controller = CreateAscendableGame(out var godState);
        // Simule le jalon déjà débloqué par une Ascension précédente : au moins 1 race différente
        // ayant déjà accompli une Ascension (voir AscensionMilestoneDefinitions).
        godState.AscensionState.AscensionsPerformed = 1;
        godState.AscensionState.AscendedRaces.Add(RaceId.Elf);

        controller.PerformAscension();

        // Le PrestigeState du cycle qui commence est neuf : ces points ne peuvent venir que de la
        // dotation, versée avec le total de points divins déjà gagnés — ici uniquement ceux de cette
        // Ascension (GetGodPointsGain, sans Nécropole : 5 essences -> 5 points).
        int expectedPoints = AscensionController.MinDivineEssenceForAscension;
        Assert.Equal(expectedPoints, controller.CurrentMainState!.PrestigeState!.PrestigePoints);
        Assert.Equal(expectedPoints, controller.CurrentMainState.PrestigeState.TotalPrestigePointsEarned);
    }

    [Fact]
    public void PerformAscension_WithoutPrestigiousAscensionMilestone_StartsTheNewCycleAtZero()
    {
        var controller = CreateAscendableGame(out _);

        controller.PerformAscension();

        Assert.Equal(0, controller.CurrentMainState!.PrestigeState!.PrestigePoints);
    }

    /// <summary>Partie complète prête à ascensionner (essence divine et points divins fournis).</summary>
    private static MainGameController CreateAscendableGame(out GodState godState)
    {
        var controller = new MainGameController();
        controller.CreateNewGame();
        godState = controller.CurrentMainState!.GodState;
        godState.GodPoints = 100;
        godState.DivineEssence = AscensionController.MinDivineEssenceForAscension;
        return controller;
    }

    // ── Construction Divine / Conquête Divine ───────────────────────────────
    // Le comportement d'octroi (bâtiments réellement constructibles, cumul de la Palissade) vit dans
    // CityBuilderController.CreateCityAt — voir CityBuilderControllerTests. Ces tests-ci ne couvrent
    // que le drapeau de modifier et l'ordre de déblocage de la colonne.

    [Fact]
    public void GetModifiers_DivineConstruction_GrantsNewCityDivineConstructionFlag()
    {
        var (_, _, _, ascension, _) = CreateTestSetup(godPoints: 100);
        Assert.True(ascension.PurchasePower(AscensionPowerId.Faith));

        Assert.DoesNotContain(ascension.GetModifiers(), m => m.Category == Modifier.ECategory.NEW_CITY_DIVINE_CONSTRUCTION);

        Assert.True(ascension.PurchasePower(AscensionPowerId.DivineConstruction));

        Assert.Contains(ascension.GetModifiers(), m => m.Category == Modifier.ECategory.NEW_CITY_DIVINE_CONSTRUCTION);
    }

    [Fact]
    public void GetModifiers_DivineConquest_GrantsNewCityDivineConquestFlag()
    {
        var (_, _, _, ascension, _) = CreateTestSetup(godPoints: 100);
        Assert.True(ascension.PurchasePower(AscensionPowerId.Faith));
        Assert.True(ascension.PurchasePower(AscensionPowerId.DivineConstruction));

        Assert.DoesNotContain(ascension.GetModifiers(), m => m.Category == Modifier.ECategory.NEW_CITY_DIVINE_CONQUEST);

        Assert.True(ascension.PurchasePower(AscensionPowerId.DivineConquest));

        Assert.Contains(ascension.GetModifiers(), m => m.Category == Modifier.ECategory.NEW_CITY_DIVINE_CONQUEST);
    }

    [Fact]
    public void DivineConquest_RequiresDivineConstructionFirstInColumn()
    {
        var (_, _, _, ascension, _) = CreateTestSetup(godPoints: 100);
        Assert.True(ascension.PurchasePower(AscensionPowerId.Faith));

        Assert.False(ascension.CanPurchasePower(AscensionPowerId.DivineConquest));

        Assert.True(ascension.PurchasePower(AscensionPowerId.DivineConstruction));
        Assert.True(ascension.CanPurchasePower(AscensionPowerId.DivineConquest));
    }

    [Fact]
    public void DivineMagic_IsPurchasableRightAfterFaithAndActivatesInAscensionState()
    {
        var (_, _, _, ascension, godState) = CreateTestSetup(godPoints: 100);

        Assert.False(ascension.CanPurchasePower(AscensionPowerId.DivineMagic));
        Assert.False(godState.AscensionState.IsDivineMagicActive);

        Assert.True(ascension.PurchasePower(AscensionPowerId.Faith));
        Assert.True(ascension.CanPurchasePower(AscensionPowerId.DivineMagic));

        Assert.True(ascension.PurchasePower(AscensionPowerId.DivineMagic));
        Assert.True(godState.AscensionState.IsDivineMagicActive);
    }

    [Fact]
    public void DivineRituals_RequiresDivineMagicFirstAndActivatesInAscensionState()
    {
        var (_, _, _, ascension, godState) = CreateTestSetup(godPoints: 100);

        Assert.True(ascension.PurchasePower(AscensionPowerId.Faith));
        Assert.False(ascension.CanPurchasePower(AscensionPowerId.DivineRituals));
        Assert.False(godState.AscensionState.IsDivineRitualsActive);

        Assert.True(ascension.PurchasePower(AscensionPowerId.DivineMagic));
        Assert.True(ascension.CanPurchasePower(AscensionPowerId.DivineRituals));

        Assert.True(ascension.PurchasePower(AscensionPowerId.DivineRituals));
        Assert.True(godState.AscensionState.IsDivineRitualsActive);
    }
}
