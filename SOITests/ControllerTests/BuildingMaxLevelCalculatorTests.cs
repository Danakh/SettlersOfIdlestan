using SettlersOfIdlestan.Controller.Ascension;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Ascension;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
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
/// Garde-fou anti-régression pour BuildingMaxLevelCalculator.GetTheoreticalMaxLevel (utilisé par
/// Building.GetAbsoluteMaxLevel et par le tableau des presets d'automatisation, voir
/// AutomationRenderer.GetAutomationPresetPopupSnapshot) : pour CHAQUE type de bâtiment, unique ou
/// non, recalcule indépendamment la somme de tous les bonus BUILDING_MAX_LEVEL additifs jamais
/// atteignables et vérifie qu'elle correspond au calcul de production. Remplace l'ancien
/// UniqueBuildingMaxLevelTests, limité aux 21 bâtiments uniques et à des overrides codés en dur qui
/// n'existent plus.
/// </summary>
public class BuildingMaxLevelCalculatorTests
{
    public static IEnumerable<object[]> AllBuildingTypes() =>
        Enum.GetValues<BuildingType>().Select(t => new object[] { t });

    /// <summary>
    /// Cas concrets qui ont révélé le bug corrigé par GetBestRaceBonusBySubCategory : Garuda inflige
    /// -1 (GarudaLightBuildings) à des types que seule cette race touche (voir RaceDefinitions.cs).
    /// L'ancienne règle "une seule race le définit -> on l'ajoute" retenait ce malus au lieu de
    /// l'ignorer, sous-évaluant le plafond de tous les bâtiments de cette liste d'exactement 1.
    /// </summary>
    [Theory]
    [InlineData(BuildingType.MushroomFarm, 2)]  // 0 (défaut) + 2 (vertex Culture Fongique) + 0 (Garuda -1 ignoré)
    [InlineData(BuildingType.MageTower, 4)]     // 0 (défaut) + 1+1+2 (3 vertex) + 0 (Garuda -1 ignoré)
    public void TheoreticalMaxLevel_IgnoresRacePenaltyWhenNoRaceGrantsABonus(BuildingType type, int expected)
    {
        Assert.Equal(expected, BuildingMaxLevelCalculator.GetTheoreticalMaxLevel(type));
    }

    [Theory]
    [MemberData(nameof(AllBuildingTypes))]
    public void TheoreticalMaxLevel_MatchesIndependentSumOfAllBonusSources(BuildingType type)
    {
        var prototype = BuildingController.CreateBuilding(type)!;
        string subCategory = type.ToString();

        bool Matches(SettlersOfIdlestan.Model.GameplayModifier.Modifier m) =>
            m.Category == ECategory.BUILDING_MAX_LEVEL && m.Type == EType.ADDITIVE && m.SubCategory == subCategory;

        int expected = prototype.GetDefaultMaxLevel();

        expected += TechnologyDefinitions.All.SelectMany(t => t.Modifiers).Where(Matches).Sum(m => (int)m.Value);

        var prestigeMap = PrestigeMapFactory.CreateDefault();
        expected += prestigeMap.Vertices.SelectMany(v => v.Modifiers).Where(Matches).Sum(m => (int)m.Value);
        expected += prestigeMap.Hexes.Sum(h =>
            h.PerVertexModifiers.Where(Matches).Sum(m => (int)m.Value) * h.AdjacentVertices.Count);

        // Bonus accordés par d'autres bâtiments uniques une fois construits (Level 1 suffit, voir
        // HarvestersGuild/ArtisansGuild/TraderGuild.GetUniqueBuildingModifiers).
        foreach (BuildingType uniqueType in Enum.GetValues<BuildingType>())
        {
            if (BuildingController.CreateBuilding(uniqueType) is not IUniqueBuilding unique) continue;
            ((Building)unique).Level = 1;
            expected += unique.GetUniqueBuildingModifiers().Where(Matches).Sum(m => (int)m.Value);
        }

        // Foi (pouvoir divin d'Ascension) accorde Temple +3 — seule source d'Ascension touchant
        // BUILDING_MAX_LEVEL aujourd'hui (voir AscensionController.GetModifiers(), et le test
        // OnlyFaithGrantsBuildingMaxLevelAmongAscensionPowers ci-dessous qui garde ce fait à jour).
        if (subCategory == nameof(BuildingType.Temple)) expected += 3;

        // Bonus de race : le meilleur parmi toutes les races pour ce type, jamais negatif — un
        // seul choix de race est actif par partie, donc jamais deux bonus de races differentes
        // cumules, mais rien n'oblige a subir le malus d'une race qu'on n'a pas choisie (ex.
        // Garuda -1 sur la Tour de Mage/Champignonniere : une autre race l'evite simplement).
        var raceValuesForThisType = RaceDefinitions.All
            .Select(r => r.Modifiers.Where(Matches).Sum(m => (int)m.Value))
            .Where(v => v != 0)
            .ToList();
        if (raceValuesForThisType.Count > 0)
            expected += Math.Max(0, raceValuesForThisType.Max());

        Assert.Equal(expected, prototype.GetAbsoluteMaxLevel());
    }

    /// <summary>
    /// Garde-fou spécifique sur le seul fait codé en dur du calculateur
    /// (BuildingMaxLevelCalculator.GetAscensionBonus) : si un futur pouvoir divin ajoute un bonus
    /// BUILDING_MAX_LEVEL sans mise à jour correspondante, ce test échoue.
    /// </summary>
    [Fact]
    public void OnlyFaithGrantsBuildingMaxLevelAmongAscensionPowers()
    {
        var state = IslandTestFactory.CreateSevenHexIslandState();
        var godState = new GodState();
        godState.AscensionState.UnlockedPowers.UnionWith(Enum.GetValues<AscensionPowerId>());

        var ascension = new AscensionController();
        ascension.Initialize(state, clock: null, new GamePRNG(1), new HarvestController(), godState);

        // SelectedRace (par défaut Humaine) verse aussi ses propres modifiers dans GetModifiers() :
        // on les exclut par identité de référence, ils sont couverts par le test générique
        // ci-dessus, pas par celui-ci qui ne porte que sur les pouvoirs divins.
        var raceModifiers = new HashSet<SettlersOfIdlestan.Model.GameplayModifier.Modifier>(
            RaceDefinitions.Get(ascension.SelectedRace).Modifiers);

        var buildingMaxLevelGrants = ascension.GetModifiers()
            .Where(m => m.Category == ECategory.BUILDING_MAX_LEVEL)
            .Where(m => !raceModifiers.Contains(m))
            .ToList();

        var grant = Assert.Single(buildingMaxLevelGrants);
        Assert.Equal(nameof(BuildingType.Temple), grant.SubCategory);
        Assert.Equal(3, grant.Value);
    }
}
