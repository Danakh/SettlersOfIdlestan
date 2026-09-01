using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SOITests.TestUtilities;
using Xunit;

namespace SOITests.ModelTests;

/// <summary>
/// Régression : un bâtiment unique accordé en permanence par l'Ascension (voir
/// Civilization.SetAscensionGrantedUniqueBuildings) ne vit dans aucune ville, donc
/// Civilization.RebuildUniqueBuildingCache — appelé à chaque changement de bâtiments de n'importe
/// quelle ville de la civilisation, pas seulement à l'ajout/retrait d'une ville — recréait autrefois
/// une instance fraîche à chaque passage. Pour la Guilde des bâtisseurs, cela réinitialisait
/// silencieusement LastRoadBuildTick/LastOutpostBuildTick/LastTownHallBuildTick à chaque construction
/// ailleurs dans la civilisation, empêchant son cooldown de jamais s'écouler entièrement.
/// </summary>
public class AscensionGrantedUniqueBuildingCacheTests
{
    [Fact]
    public void RebuildUniqueBuildingCache_PreservesInstanceAcrossRebuilds()
    {
        var state = IslandTestFactory.CreateSevenHexIslandState();
        var civ = state.Civilizations[0];

        civ.SetAscensionGrantedUniqueBuildings(new[] { BuildingType.BuildersGuild });

        var guild = (BuildersGuild)civ.GetUniqueBuilding(BuildingType.BuildersGuild)!;
        Assert.NotNull(guild);
        guild.LastTownHallBuildTick = 12345;

        // Simule des changements de bâtiments ailleurs dans la civilisation (ce qui déclenche
        // RebuildUniqueBuildingCache via City.BuildingsChanged) sans rien toucher à la guilde.
        var city = civ.Cities[0];
        city.AddBuilding(new TownHall { Level = 1 });
        city.FindBuilding(BuildingType.TownHall)!.Level = 2;
        city.InvalidateLevelCache();
        civ.RebuildUniqueBuildingCache();

        var guildAfter = civ.GetUniqueBuilding(BuildingType.BuildersGuild);
        Assert.Same(guild, guildAfter);
        Assert.Equal(12345, ((BuildersGuild)guildAfter!).LastTownHallBuildTick);
    }

    [Fact]
    public void SetAscensionGrantedUniqueBuildings_NewIsland_ResetsInstance()
    {
        var state = IslandTestFactory.CreateSevenHexIslandState();
        var civ = state.Civilizations[0];

        civ.SetAscensionGrantedUniqueBuildings(new[] { BuildingType.BuildersGuild });
        var guild = (BuildersGuild)civ.GetUniqueBuilding(BuildingType.BuildersGuild)!;
        guild.LastTownHallBuildTick = 99999;

        // Nouvelle île : la liste accordée est réappliquée (même si son contenu ne change pas) —
        // une instance neuve est attendue ici, pas un report accidentel de l'ancienne île.
        civ.SetAscensionGrantedUniqueBuildings(new[] { BuildingType.BuildersGuild });
        var guildAfter = (BuildersGuild)civ.GetUniqueBuilding(BuildingType.BuildersGuild)!;

        Assert.NotSame(guild, guildAfter);
        Assert.Equal(0, guildAfter.LastTownHallBuildTick);
    }
}
