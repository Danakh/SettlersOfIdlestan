using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.IslandMap;
using SOITests.TestUtilities;
using Xunit;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SOITests.ControllerTests;

/// <summary>
/// Vérifie que le bonus de chaque bâtiment unique sans automatisation (BlastFurnace + les bâtiments
/// raciaux) atteint réellement la civilisation une fois construit — pas seulement que
/// GetUniqueBuildingModifiers() le retourne en isolation, mais que Civilization.RebuildUniqueBuildingsModifiers
/// le propage jusqu'aux propriétés/agrégateur civ-wide effectivement consultés par le reste du jeu.
/// Ziggurat (déclenchement TEMPLE_INSTANT_DOMINION), ThroneOfWinds (portée d'attaque) et GreatBurrow
/// (coût des nouvelles villes) sont déjà couverts par RaceSystemTests.cs.
/// </summary>
public class UniqueBuildingBonusTests
{
    private static (WorldState state, City city, Civilization civ) CreateSetup()
    {
        var state = IslandTestFactory.CreateSevenHexIslandState();
        var civ = state.Civilizations[0];
        var city = civ.Cities[0];
        return (state, city, civ);
    }

    private static void BuildUnique<T>(City city, Civilization civ, T building) where T : Building, IUniqueBuilding
    {
        city.AddBuilding(building);
        civ.RegisterUniqueBuildingInCache(building);
        civ.RebuildUniqueBuildingsModifiers();
    }

    [Fact]
    public void BlastFurnace_BoostsSmelterProduction()
    {
        var (_, city, civ) = CreateSetup();
        Assert.Equal(0, civ.ModifierAggregator.ApplyModifiers(ECategory.BUILDING_PRODUCTION, "Smelter", 0));

        BuildUnique(city, civ, new BlastFurnace { Level = 1 });

        Assert.Equal(BlastFurnace.BonusSteelPerSmelterCycle,
            civ.ModifierAggregator.ApplyModifiers(ECategory.BUILDING_PRODUCTION, "Smelter", 0));
    }

    [Fact]
    public void HeartTree_BoostsResearchSpeedAndGeneratesWood()
    {
        var (_, city, civ) = CreateSetup();
        Assert.Equal(1.0, civ.ResearchProductionSpeed, precision: 5);

        BuildUnique(city, civ, new HeartTree { Level = 1 });

        Assert.Equal(1.25, civ.ResearchProductionSpeed, precision: 5);
        Assert.Equal(5, civ.ModifierAggregator.ApplyModifiers(ECategory.PASSIVE_RESOURCE_GENERATION, nameof(Resource.Wood), 0));
    }

    [Fact]
    public void RunicForge_BoostsForgeMineAndSmelter()
    {
        var (_, city, civ) = CreateSetup();
        Assert.Equal(0, civ.ForgeDoubleHarvestBonus);
        Assert.Equal(0, civ.MineGoldChancePercent);
        Assert.Equal(0, civ.ModifierAggregator.ApplyModifiers(ECategory.BUILDING_PRODUCTION, "Smelter", 0));

        BuildUnique(city, civ, new RunicForge { Level = 1 });

        Assert.Equal(15, civ.ForgeDoubleHarvestBonus);
        Assert.Equal(10, civ.MineGoldChancePercent);
        Assert.Equal(1, civ.ModifierAggregator.ApplyModifiers(ECategory.BUILDING_PRODUCTION, "Smelter", 0));
    }

    [Fact]
    public void ColossusWorkshop_BoostsAutomaticHarvestForEveryBuilding()
    {
        var (_, city, civ) = CreateSetup();
        Assert.Equal(0, civ.GetHarvestProductionBonus(nameof(BuildingType.Mine)));
        Assert.Equal(0, civ.GetHarvestProductionBonus(nameof(BuildingType.Sawmill)));

        BuildUnique(city, civ, new ColossusWorkshop { Level = 1 });

        // Pas de SubCategory sur ce modifier : le bonus s'applique à tous les bâtiments récolteurs.
        Assert.Equal(10, civ.GetHarvestProductionBonus(nameof(BuildingType.Mine)));
        Assert.Equal(10, civ.GetHarvestProductionBonus(nameof(BuildingType.Sawmill)));
    }

    [Fact]
    public void SkullPit_GrantsFreeSoldierFoodPerCity()
    {
        var (_, city, civ) = CreateSetup();
        Assert.Equal(0, civ.ModifierAggregator.ApplyModifiers(ECategory.SOLDIER_FOOD_FREE_PER_CITY, "", 0));

        BuildUnique(city, civ, new SkullPit { Level = 1 });

        Assert.Equal(5, civ.ModifierAggregator.ApplyModifiers(ECategory.SOLDIER_FOOD_FREE_PER_CITY, "", 0));
    }

    [Fact]
    public void PearlGrotto_BoostsCityDefenseAndGeneratesFood()
    {
        var (_, city, civ) = CreateSetup();
        Assert.Equal(0, civ.ModifierAggregator.ApplyModifiers(ECategory.CITY_DEFENSE, "", 0));

        BuildUnique(city, civ, new PearlGrotto { Level = 1 });

        Assert.Equal(3, civ.ModifierAggregator.ApplyModifiers(ECategory.CITY_DEFENSE, "", 0));
        Assert.Equal(5, civ.ModifierAggregator.ApplyModifiers(ECategory.PASSIVE_RESOURCE_GENERATION, nameof(Resource.Food), 0));
    }

    [Fact]
    public void SpiderShrine_GrantsMonsterAttackImmunityToRatsAndMinorDemons()
    {
        var (_, city, civ) = CreateSetup();
        Assert.False(civ.ModifierAggregator.HasModifier(ECategory.MONSTER_ATTACK_IMMUNITY, "Rats"));
        Assert.False(civ.ModifierAggregator.HasModifier(ECategory.MONSTER_ATTACK_IMMUNITY, "MinorDemon"));

        BuildUnique(city, civ, new SpiderShrine { Level = 1 });

        Assert.True(civ.ModifierAggregator.HasModifier(ECategory.MONSTER_ATTACK_IMMUNITY, "Rats"));
        Assert.True(civ.ModifierAggregator.HasModifier(ECategory.MONSTER_ATTACK_IMMUNITY, "MinorDemon"));
    }
}
