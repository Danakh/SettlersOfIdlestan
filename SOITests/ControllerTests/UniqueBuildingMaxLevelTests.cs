using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Prestige.PrestigeMap;
using SettlersOfIdlestan.Model.Races;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SOITests.ControllerTests;

/// <summary>
/// Garde-fou anti-régression pour Building.GetAbsoluteMaxLevel() : pour chaque bâtiment unique
/// vivant, vérifie que le niveau max codé en dur correspond bien à GetDefaultMaxLevel() + la somme
/// de tous les bonus BUILDING_MAX_LEVEL additifs réellement présents dans les définitions de
/// recherche, de prestige et de race — collectés dynamiquement depuis les sources, pas recopiés.
/// Si une future recherche/vertex/race ajoute un bonus BUILDING_MAX_LEVEL pour un unique sans que
/// son override de GetAbsoluteMaxLevel() soit mis à jour en conséquence, ce test échoue.
/// </summary>
public class UniqueBuildingMaxLevelTests
{
    private static int SumOfAdditiveBonuses(BuildingType type)
    {
        string subCategory = type.ToString();

        IEnumerable<SettlersOfIdlestan.Model.GameplayModifier.Modifier> techModifiers =
            TechnologyDefinitions.All.SelectMany(t => t.Modifiers);

        IEnumerable<SettlersOfIdlestan.Model.GameplayModifier.Modifier> prestigeModifiers =
            PrestigeMapFactory.CreateDefault().Vertices.SelectMany(v => v.Modifiers);

        IEnumerable<SettlersOfIdlestan.Model.GameplayModifier.Modifier> raceModifiers =
            RaceDefinitions.All.SelectMany(r => r.Modifiers);

        return techModifiers.Concat(prestigeModifiers).Concat(raceModifiers)
            .Where(m => m.Category == ECategory.BUILDING_MAX_LEVEL
                && m.Type == EType.ADDITIVE
                && m.SubCategory == subCategory)
            .Sum(m => (int)m.Value);
    }

    [Theory]
    [InlineData(BuildingType.Academy)]
    [InlineData(BuildingType.AdventurersGuild)]
    [InlineData(BuildingType.ArcaneTower)]
    [InlineData(BuildingType.ArtisansGuild)]
    [InlineData(BuildingType.BlastFurnace)]
    [InlineData(BuildingType.BuildersGuild)]
    [InlineData(BuildingType.ColossusWorkshop)]
    [InlineData(BuildingType.GrandTemple)]
    [InlineData(BuildingType.GreatBurrow)]
    [InlineData(BuildingType.HarvestersGuild)]
    [InlineData(BuildingType.HeartTree)]
    [InlineData(BuildingType.ImperialPort)]
    [InlineData(BuildingType.PearlGrotto)]
    [InlineData(BuildingType.RunicForge)]
    [InlineData(BuildingType.SkullPit)]
    [InlineData(BuildingType.SpiderShrine)]
    [InlineData(BuildingType.ThroneOfWinds)]
    [InlineData(BuildingType.TraderGuild)]
    [InlineData(BuildingType.VolcanicForge)]
    [InlineData(BuildingType.WarRoom)]
    [InlineData(BuildingType.Ziggurat)]
    public void AbsoluteMaxLevel_MatchesSumOfAllBonusSources(BuildingType type)
    {
        var prototype = BuildingController.CreateBuilding(type)!;
        Assert.True(prototype.IsUnique, $"{type} should be IsUnique — this test only covers unique buildings");

        int expected = prototype.GetDefaultMaxLevel() + SumOfAdditiveBonuses(type);

        Assert.Equal(expected, prototype.GetAbsoluteMaxLevel());
    }
}
