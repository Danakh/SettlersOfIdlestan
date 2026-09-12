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
    /// Depuis que BuildStandardMaxLevelModifiers couvre aussi les bâtiments à plafond par défaut 0/1
    /// (BuildingController.GetMaxLevel garantit qu'un malus négatif ne les rend jamais inconstructibles),
    /// les Géants (+2) touchent aussi ces deux types et deviennent la meilleure race sur les deux.
    /// </summary>
    [Theory]
    [InlineData(BuildingType.MushroomFarm, 4)]  // 0 (défaut) + 2 (vertex Culture Fongique) + 2 (Géant, meilleur que Garuda/Gobelin -1)
    [InlineData(BuildingType.MageTower, 7)]     // 0 (défaut) + 1+1+2 (3 vertex) + 1 (Magisterium Divin) + 2 (Géant, meilleur que Garuda/Gobelin -1)
    public void TheoreticalMaxLevel_IgnoresRacePenaltyWhenNoRaceGrantsABonus(BuildingType type, int expected)
    {
        Assert.Equal(expected, BuildingMaxLevelCalculator.GetTheoreticalMaxLevel(type));
    }

    [Theory]
    [MemberData(nameof(AllBuildingTypes))]
    public void TheoreticalMaxLevel_MatchesIndependentSumOfAllBonusSources(BuildingType type)
    {
        var prototype = BuildingFactory.Create(type)!;
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
            if (BuildingFactory.Create(uniqueType) is not IUniqueBuilding unique) continue;
            ((Building)unique).Level = 1;
            expected += unique.GetUniqueBuildingModifiers().Where(Matches).Sum(m => (int)m.Value);
        }

        // Pouvoirs divins d'Ascension : la table AscensionBuildingMaxLevelGrants est la source unique
        // de ces bonus ; le test AscensionPowers_GrantExactlyTheBuildingMaxLevelGrantsTable ci-dessous
        // vérifie que le contrôleur n'en émet pas un seul en dehors d'elle.
        expected += AscensionBuildingMaxLevelGrants.All.Where(g => g.Type == type).Sum(g => g.Bonus);

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
    /// Garde-fou sur la table AscensionBuildingMaxLevelGrants, seule source des bonus de niveau max
    /// accordés par les pouvoirs divins : si un pouvoir en émet un hors de la table, le calculateur
    /// (qui ne lit que la table) sous-évaluerait le plafond théorique sans la moindre erreur — les
    /// plafonds des presets d'automatisation seraient alors faux. Tous les pouvoirs sont débloqués
    /// ici, donc GetModifiers() doit rendre exactement la table, ligne pour ligne.
    /// </summary>
    [Fact]
    public void AscensionPowers_GrantExactlyTheBuildingMaxLevelGrantsTable()
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

        var emitted = ascension.GetModifiers()
            .Where(m => m.Category == ECategory.BUILDING_MAX_LEVEL)
            .Where(m => !raceModifiers.Contains(m))
            .Select(m => (m.SubCategory, Value: (int)m.Value))
            .OrderBy(g => g.SubCategory).ThenBy(g => g.Value)
            .ToList();

        var expected = AscensionBuildingMaxLevelGrants.All
            .Select(g => (SubCategory: BuildingTypeNames.Of(g.Type), Value: g.Bonus))
            .OrderBy(g => g.SubCategory).ThenBy(g => g.Value)
            .ToList();

        Assert.Equal(expected, emitted);
    }

    /// <summary>
    /// Un bonus de niveau max n'a de sens que si le palier qu'il ouvre se paie : un
    /// GetUpgradeCost() vide à ce niveau offrirait l'amélioration, silencieusement. Vérifié pour
    /// chaque type touché par la table (la Spire de Défense, la Tour des Arcanes et l'Académie
    /// n'avaient aucun coût défini au-delà de leur ancien plafond).
    /// </summary>
    [Fact]
    public void EveryAscensionGrantedLevelHasAnUpgradeCost()
    {
        foreach (var type in AscensionBuildingMaxLevelGrants.All.Select(g => g.Type).Distinct())
        {
            var prototype = BuildingFactory.Create(type)!;
            int max = prototype.GetAbsoluteMaxLevel();

            for (int level = 2; level <= max; level++)
                Assert.True(prototype.GetUpgradeCost(level).Count > 0,
                    $"{type} niveau {level} (plafond absolu {max}) n'a aucun coût d'amélioration.");
        }
    }
}
