using System.Linq;
using Xunit;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.GameplayModifier;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SOITests.ModifierTests;

/// <summary>
/// Validates every technology modifier defined in TechnologyDefinitions.All.
/// Each test completes exactly one technology in isolation and asserts the expected
/// modifier value via ModifierAggregator, confirming the definition is correct.
/// Base value is always 0 / 0.0 so the result directly equals the modifier's value.
/// </summary>
public class TechnologyModifierTests
{
    private static ModifierAggregator BuildAggregator(params TechnologyId[] completed)
    {
        var tree = new TechnologyTree();
        foreach (var tech in completed)
            tree.CompleteResearch(tech);
        var aggregator = new ModifierAggregator();
        aggregator.Register(tree);
        return aggregator;
    }

    // ── HARVEST_SPEED ─────────────────────────────────────────────────────────

    [Fact]
    public void NoResearch_HarvestSpeed_IsUnchanged()
    {
        Assert.Equal(0.0, BuildAggregator().ApplyModifiers(ECategory.HARVEST_SPEED, "", 0.0), 5);
    }

    [Fact]
    public void HarvestEfficiency_HarvestSpeed_Plus0Point1()
    {
        Assert.Equal(0.1, BuildAggregator(TechnologyId.HarvestEfficiency).ApplyModifiers(ECategory.HARVEST_SPEED, "", 0.0), 5);
    }

    [Fact]
    public void MasterHarvest_HarvestSpeed_Plus0Point05PerCompletion()
    {
        // Répétable : +5% par complétion, pas une valeur fixe (voir CLAUDE.md / ResearchController).
        Assert.Equal(0.05, BuildAggregator(TechnologyId.MasterHarvest).ApplyModifiers(ECategory.HARVEST_SPEED, "", 0.0), 5);

        var tree = new TechnologyTree();
        tree.CompleteResearch(TechnologyId.MasterHarvest);
        tree.CompleteResearch(TechnologyId.MasterHarvest);
        var aggregator = new ModifierAggregator();
        aggregator.Register(tree);
        Assert.Equal(0.10, aggregator.ApplyModifiers(ECategory.HARVEST_SPEED, "", 0.0), 5);
    }

    // ── FORGE_DOUBLE_HARVEST_BONUS ────────────────────────────────────────────

    [Fact]
    public void Artisanat_ForgeDoubleHarvestBonus_Plus5()
    {
        Assert.Equal(5, BuildAggregator(TechnologyId.Artisanat).ApplyModifiers(ECategory.FORGE_DOUBLE_HARVEST_BONUS, "", 0));
    }

    [Fact]
    public void Metallurgy_ForgeDoubleHarvestBonus_Plus10()
    {
        Assert.Equal(10, BuildAggregator(TechnologyId.Metallurgy).ApplyModifiers(ECategory.FORGE_DOUBLE_HARVEST_BONUS, "", 0));
    }

    [Fact]
    public void MaitriseDesAlliages_ForgeDoubleHarvestBonus_Plus15()
    {
        Assert.Equal(15, BuildAggregator(TechnologyId.MaitriseDesAlliages).ApplyModifiers(ECategory.FORGE_DOUBLE_HARVEST_BONUS, "", 0));
    }

    // ── UNLOCK_WONDERS ────────────────────────────────────────────────────────

    [Fact]
    public void Architecture_UnlocksWonders()
    {
        Assert.True(BuildAggregator(TechnologyId.Architecture).HasModifier(ECategory.UNLOCK_WONDERS));
    }

    [Fact]
    public void NoResearch_WondersNotUnlocked()
    {
        Assert.False(BuildAggregator().HasModifier(ECategory.UNLOCK_WONDERS));
    }

    // ── UNIT_PRODUCTION_SPEED ─────────────────────────────────────────────────

    [Fact]
    public void MilitaryDiscipline_UnitProductionSpeed_Plus0Point1()
    {
        Assert.Equal(0.1, BuildAggregator(TechnologyId.MilitaryDiscipline).ApplyModifiers(ECategory.UNIT_PRODUCTION_SPEED, "", 0.0), 5);
    }

    [Fact]
    public void MilitaryTactics_UnitProductionSpeed_Plus0Point15()
    {
        Assert.Equal(0.15, BuildAggregator(TechnologyId.MilitaryTactics).ApplyModifiers(ECategory.UNIT_PRODUCTION_SPEED, "", 0.0), 5);
    }

    [Fact]
    public void AdvancedTactics_NoUnitProductionSpeedModifier()
    {
        Assert.Equal(0.0, BuildAggregator(TechnologyId.AdvancedTactics).ApplyModifiers(ECategory.UNIT_PRODUCTION_SPEED, "", 0.0), 5);
    }

    // ── CITY_ATTACK_RANGE ─────────────────────────────────────────────────────

    [Fact]
    public void Scouting_CityAttackRange_Plus1()
    {
        Assert.Equal(1, BuildAggregator(TechnologyId.Scouting).ApplyModifiers(ECategory.CITY_ATTACK_RANGE, "", 0));
    }

    // ── HARVEST_PRODUCTION_BONUS ──────────────────────────────────────────────

    [Fact]
    public void Agriculture_HarvestProductionBonus_Mill_Plus50()
    {
        Assert.Equal(50, BuildAggregator(TechnologyId.Agriculture).ApplyModifiers(ECategory.HARVEST_PRODUCTION_BONUS, "Mill", 0));
    }

    // ── STORAGE_CAPACITY_BASIC ────────────────────────────────────────────────

    [Fact]
    public void StorageOptimization_StorageCapacityBasic_Plus20()
    {
        Assert.Equal(20, BuildAggregator(TechnologyId.StorageOptimization).ApplyModifiers(ECategory.STORAGE_CAPACITY_BASIC, "", 0));
    }

    // ── RESEARCH_PRODUCTION_SPEED ────────────────────────────────────────────

    [Fact]
    public void Archivage_ResearchProductionSpeed_Plus0Point05()
    {
        Assert.Equal(0.05, BuildAggregator(TechnologyId.Archivage).ApplyModifiers(ECategory.RESEARCH_PRODUCTION_SPEED, "", 0.0), 5);
    }

    [Fact]
    public void ImprovedResearch_ResearchProductionSpeed_Plus0Point2()
    {
        Assert.Equal(0.2, BuildAggregator(TechnologyId.ImprovedResearch).ApplyModifiers(ECategory.RESEARCH_PRODUCTION_SPEED, "", 0.0), 5);
    }

    [Fact]
    public void MasterResearch_ResearchProductionSpeed_Plus0Point3()
    {
        Assert.Equal(0.3, BuildAggregator(TechnologyId.MasterResearch).ApplyModifiers(ECategory.RESEARCH_PRODUCTION_SPEED, "", 0.0), 5);
    }

    // ── MINE_GOLD_CHANCE_PERCENT ──────────────────────────────────────────────

    [Fact]
    public void Orpaillage_MineGoldChancePercent_Plus10()
    {
        Assert.Equal(10, BuildAggregator(TechnologyId.Orpaillage).ApplyModifiers(ECategory.MINE_GOLD_CHANCE_PERCENT, "", 0));
    }

    // ── RESEARCH_COST_REDUCTION ───────────────────────────────────────────────

    [Fact]
    public void ResearchMethods_ResearchCostReduction_Plus0Point1()
    {
        Assert.Equal(0.1, BuildAggregator(TechnologyId.ResearchMethods).ApplyModifiers(ECategory.RESEARCH_COST_REDUCTION, "", 0.0), 5);
    }


    // ── BUILDING_MAX_LEVEL ────────────────────────────────────────────────────

    [Fact]
    public void MilitaryBuildings_BarracksMaxLevel_Plus2()
    {
        Assert.Equal(2, BuildAggregator(TechnologyId.MilitaryBuildings).ApplyModifiers(ECategory.BUILDING_MAX_LEVEL, "Barracks", 0));
    }

    [Fact]
    public void HarvestTools_SawmillMaxLevel_Plus1()
    {
        Assert.Equal(1, BuildAggregator(TechnologyId.HarvestTools).ApplyModifiers(ECategory.BUILDING_MAX_LEVEL, "Sawmill", 0));
    }

    [Fact]
    public void HarvestTools_MillMaxLevel_Plus1()
    {
        Assert.Equal(1, BuildAggregator(TechnologyId.HarvestTools).ApplyModifiers(ECategory.BUILDING_MAX_LEVEL, "Mill", 0));
    }

    [Fact]
    public void HarvestTools_QuarryMaxLevel_Plus1()
    {
        Assert.Equal(1, BuildAggregator(TechnologyId.HarvestTools).ApplyModifiers(ECategory.BUILDING_MAX_LEVEL, "Quarry", 0));
    }

    [Fact]
    public void HarvestTools_BrickworksMaxLevel_Plus1()
    {
        Assert.Equal(1, BuildAggregator(TechnologyId.HarvestTools).ApplyModifiers(ECategory.BUILDING_MAX_LEVEL, "Brickworks", 0));
    }

    [Fact]
    public void AdvancedArchitecture_SawmillMaxLevel_Plus1()
    {
        Assert.Equal(1, BuildAggregator(TechnologyId.AdvancedArchitecture).ApplyModifiers(ECategory.BUILDING_MAX_LEVEL, "Sawmill", 0));
    }

    [Fact]
    public void AdvancedArchitecture_BrickworksMaxLevel_Plus1()
    {
        Assert.Equal(1, BuildAggregator(TechnologyId.AdvancedArchitecture).ApplyModifiers(ECategory.BUILDING_MAX_LEVEL, "Brickworks", 0));
    }

    [Fact]
    public void AdvancedArchitecture_QuarryMaxLevel_Plus1()
    {
        Assert.Equal(1, BuildAggregator(TechnologyId.AdvancedArchitecture).ApplyModifiers(ECategory.BUILDING_MAX_LEVEL, "Quarry", 0));
    }

    [Fact]
    public void AdvancedArchitecture_MillMaxLevel_Plus1()
    {
        Assert.Equal(1, BuildAggregator(TechnologyId.AdvancedArchitecture).ApplyModifiers(ECategory.BUILDING_MAX_LEVEL, "Mill", 0));
    }

    [Fact]
    public void GrandArchitecture_MarketMaxLevel_Plus1()
    {
        Assert.Equal(1, BuildAggregator(TechnologyId.GrandArchitecture).ApplyModifiers(ECategory.BUILDING_MAX_LEVEL, "Market", 0));
    }

    [Fact]
    public void GrandArchitecture_LibraryMaxLevel_Plus1()
    {
        Assert.Equal(1, BuildAggregator(TechnologyId.GrandArchitecture).ApplyModifiers(ECategory.BUILDING_MAX_LEVEL, "Library", 0));
    }

    [Fact]
    public void GrandArchitecture_BarracksMaxLevel_Plus1()
    {
        Assert.Equal(1, BuildAggregator(TechnologyId.GrandArchitecture).ApplyModifiers(ECategory.BUILDING_MAX_LEVEL, "Barracks", 0));
    }

    // ── CITY_DEFENSE ──────────────────────────────────────────────────────────

    [Fact]
    public void Patrol_NoCityDefenseModifier()
    {
        Assert.Equal(0, BuildAggregator(TechnologyId.Patrol).ApplyModifiers(ECategory.CITY_DEFENSE, "", 0));
    }

    // ── Branche des Abysses & capstones (tiers 8-13) ──────────────────────────

    [Fact]
    public void EtudeDesAbysses_UnderworldTreasureChance_Plus5()
    {
        Assert.Equal(5, BuildAggregator(TechnologyId.EtudeDesAbysses).ApplyModifiers(ECategory.UNDERWORLD_TREASURE_CHANCE_PERCENT, "", 0));
    }

    [Fact]
    public void Demonologie_UnderworldMonsterSpawnInterval_Plus0Point5()
    {
        Assert.Equal(0.5, BuildAggregator(TechnologyId.Demonologie).ApplyModifiers(ECategory.UNDERWORLD_MONSTER_SPAWN_INTERVAL, "", 0.0), 5);
    }

    [Fact]
    public void ResistanceALaCorruption_CorruptionLevelReduction_Plus1()
    {
        Assert.Equal(1, BuildAggregator(TechnologyId.ResistanceALaCorruption).ApplyModifiers(ECategory.CORRUPTION_LEVEL_REDUCTION, "", 0));
    }

    [Fact]
    public void ResistanceALaCorruptionEtSecretsDeLaFaille_CorruptionLevelReduction_Cumulent2()
    {
        Assert.Equal(2, BuildAggregator(TechnologyId.ResistanceALaCorruption, TechnologyId.SecretsDeLaFaille)
            .ApplyModifiers(ECategory.CORRUPTION_LEVEL_REDUCTION, "", 0));
    }

    [Fact]
    public void TheologieDeLAscension_PrestigeGain_Plus0Point5()
    {
        Assert.Equal(0.5, BuildAggregator(TechnologyId.TheologieDeLAscension).ApplyModifiers(ECategory.PRESTIGE_GAIN, "", 0.0), 5);
    }

    [Fact]
    public void AcierAbyssal_ForgeDoubleHarvestBonus_Plus25()
    {
        Assert.Equal(25, BuildAggregator(TechnologyId.AcierAbyssal).ApplyModifiers(ECategory.FORGE_DOUBLE_HARVEST_BONUS, "", 0));
    }

    [Fact]
    public void AcierAbyssal_SmelterProduction_Plus1()
    {
        Assert.Equal(1, BuildAggregator(TechnologyId.AcierAbyssal).ApplyModifiers(ECategory.BUILDING_PRODUCTION, "Smelter", 0));
    }

    [Fact]
    public void MagieDuVide_RitualTotalPower_Plus0Point25()
    {
        Assert.Equal(0.25, BuildAggregator(TechnologyId.MagieDuVide).ApplyModifiers(ECategory.RITUAL_TOTAL_POWER, "", 0.0), 5);
    }

    [Fact]
    public void CoeurDeLaTerre_MineHarvestSpeed_Plus0Point5()
    {
        Assert.Equal(0.5, BuildAggregator(TechnologyId.CoeurDeLaTerre).ApplyModifiers(ECategory.HARVEST_SPEED, "Mine", 0.0), 5);
    }

    [Fact]
    public void Omniscience_ResearchProductionSpeed_Plus0Point5()
    {
        Assert.Equal(0.5, BuildAggregator(TechnologyId.Omniscience).ApplyModifiers(ECategory.RESEARCH_PRODUCTION_SPEED, "", 0.0), 5);
    }

    [Fact]
    public void LegionEternelle_CityMaxSoldiers_Plus10()
    {
        Assert.Equal(10, BuildAggregator(TechnologyId.LegionEternelle).ApplyModifiers(ECategory.CITY_MAX_SOLDIERS_BONUS, "", 0));
    }

    // ── Suite de la ligne du Vide & branche de la Théocratie (tiers 12-15) ────

    [Fact]
    public void ReliquaireSacre_DivineBonesCostReduction_Plus0Point15()
    {
        Assert.Equal(0.15, BuildAggregator(TechnologyId.ReliquaireSacre).ApplyModifiers(ECategory.DIVINE_BONES_COST_REDUCTION, "", 0.0), 5);
    }

    [Fact]
    public void CartographieDuVide_UnlocksVoidRouteCostReduction()
    {
        Assert.True(BuildAggregator(TechnologyId.CartographieDuVide).HasModifier(ECategory.VOID_ROUTE_COST_REDUCTION));
    }

    [Fact]
    public void DogmeDeLEmprise_TempleDominionCap_Plus1()
    {
        Assert.Equal(1, BuildAggregator(TechnologyId.DogmeDeLEmprise).ApplyModifiers(ECategory.TEMPLE_DOMINION_CAP, "", 0));
    }

    [Fact]
    public void CommunionAbyssale_PrestigeGain_Plus1()
    {
        Assert.Equal(1.0, BuildAggregator(TechnologyId.CommunionAbyssale).ApplyModifiers(ECategory.PRESTIGE_GAIN, "", 0.0), 5);
    }

    [Fact]
    public void Evangelisation_DominionSpreadChance_Plus5()
    {
        Assert.Equal(5, BuildAggregator(TechnologyId.Evangelisation).ApplyModifiers(ECategory.DOMINION_SPREAD_CHANCE, "", 0));
    }

    [Fact]
    public void TerreConsacree_TempleDominionProtectionChance_Plus0Point5()
    {
        Assert.Equal(0.5, BuildAggregator(TechnologyId.TerreConsacree).ApplyModifiers(ECategory.TEMPLE_DOMINION_PROTECTION_CHANCE, "", 0.0), 5);
    }

    [Fact]
    public void BastionConsacre_UnlocksTempleDefenseBonus()
    {
        Assert.True(BuildAggregator(TechnologyId.BastionConsacre).HasModifier(ECategory.TEMPLE_DEFENSE_BONUS));
    }

    // ── RequiresDominionUnlock ────────────────────────────────────────────────
    // Les recherches du Dominion doivent rester verrouillées derrière le pouvoir divin Foi ;
    // celles qui n'en dépendent pas ne doivent pas porter le flag par accident.

    [Fact]
    public void DominionTechnologies_RequireDominionUnlock()
    {
        var expected = new[]
        {
            TechnologyId.DogmeDeLEmprise,
            TechnologyId.Evangelisation,
            TechnologyId.TerreConsacree,
            TechnologyId.BastionConsacre,
        };
        var actual = TechnologyDefinitions.All
            .Where(t => t.RequiresDominionUnlock)
            .Select(t => t.Id)
            .ToList();
        Assert.Equal(expected.OrderBy(x => x), actual.OrderBy(x => x));
    }

    // ── Layout integrity (Tier/Line) ──────────────────────────────────────────
    // ResearchRenderer positions every technology on a (Tier, Line) grid cell
    // (col, row) — two technologies sharing a cell would overlap on screen.
    // This loops over TechnologyDefinitions.All so it also covers future technologies.

    [Fact]
    public void AllTechnologies_HaveUniqueTierLinePosition()
    {
        var duplicates = TechnologyDefinitions.All
            .GroupBy(t => (t.Tier, t.Line))
            .Where(g => g.Count() > 1)
            .Select(g => $"(Tier {g.Key.Tier}, Line {g.Key.Line}): {string.Join(", ", g.Select(t => t.Id))}")
            .ToList();

        Assert.True(duplicates.Count == 0,
            "Technologies sharing the same (Tier, Line) position: " + string.Join(" | ", duplicates));
    }
}
