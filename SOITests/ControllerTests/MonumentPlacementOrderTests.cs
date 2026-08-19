using SettlersOfIdlestan.Controller.Expand;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using SOITests.TestUtilities;
using Xunit;

namespace SOITests.ControllerTests;

/// <summary>
/// MonumentInvestment.OrderByLeastSacrifice : un Monument stérilise la récolte de son hexagone, donc
/// le choix de l'hexagone se paie en production perdue. L'ordre rendu doit proposer d'abord ce qui ne
/// coûte rien, puis ce qui ne prive qu'une seule ville, et à sacrifice égal la ressource dont la
/// civilisation a déjà le plus.
/// </summary>
public class MonumentPlacementOrderTests
{
    private static HexCoord Center => new(0, 0, IslandMap.SurfaceLayer);
    private static HexCoord East   => new(1, 0, IslandMap.SurfaceLayer);
    private static HexCoord West   => new(-1, 0, IslandMap.SurfaceLayer);
    private static HexCoord NorthEast => new(0, 1, IslandMap.SurfaceLayer);
    private static HexCoord NorthWest => new(-1, 1, IslandMap.SurfaceLayer);

    /// <summary>
    /// Île à sept hexagones : la ville de départ touche Center (Plaine), NorthEast (Plaine) et East
    /// (Forêt), avec un Moulin (Nourriture sur la Plaine) et une Scierie (Bois sur la Forêt).
    /// </summary>
    private static WorldState CreateSetup()
    {
        var state = IslandTestFactory.CreateSevenHexIslandState();
        var civ = state.PlayerCivilization;
        civ.SetStorageCapacityCache(1000, 1000);

        var city = civ.Cities[0];
        city.AddBuilding(new Mill { Level = 1 });
        city.AddBuilding(new Sawmill { Level = 1 });

        return state;
    }

    [Fact]
    public void OrderByLeastSacrifice_PutsHexesNoCityHarvestsFirst()
    {
        var state = CreateSetup();
        var civ = state.PlayerCivilization;
        civ.AddResource(Resource.Food, 100);
        civ.AddResource(Resource.Wood, 100);

        // West ne touche aucune ville : le sacrifier ne coûte rien.
        var ordered = MonumentInvestment.OrderByLeastSacrifice(new[] { Center, East, West }, civ, state);

        Assert.Equal(West, ordered[0]);
    }

    [Fact]
    public void OrderByLeastSacrifice_PrefersTheResourceTheCivilizationHasMostOf()
    {
        var state = CreateSetup();
        var civ = state.PlayerCivilization;
        civ.AddResource(Resource.Food, 100);
        civ.AddResource(Resource.Wood, 5);

        // Les deux ne privent qu'une ville : on sacrifie la Nourriture (100 en stock) plutôt que le
        // Bois (5), qui est la ressource rare.
        var ordered = MonumentInvestment.OrderByLeastSacrifice(new[] { East, Center }, civ, state);

        Assert.Equal(new[] { Center, East }, ordered);
    }

    [Fact]
    public void OrderByLeastSacrifice_PrefersDeprivingASingleCity()
    {
        var state = CreateSetup();
        var civ = state.PlayerCivilization;
        civ.AddResource(Resource.Food, 100);
        civ.AddResource(Resource.Wood, 5);

        // Deuxième ville de l'autre côté de NorthEast : Center et NorthEast sont désormais récoltés
        // par deux villes, East par une seule.
        var second = new City(Vertex.Create(Center, NorthEast, NorthWest)) { CivilizationIndex = civ.Index };
        second.AddBuilding(new Mill { Level = 1 });
        civ.AddCity(second);

        var ordered = MonumentInvestment.OrderByLeastSacrifice(new[] { Center, NorthEast, East }, civ, state);

        // East passe devant malgré sa ressource plus rare : le nombre de villes privées prime.
        Assert.Equal(East, ordered[0]);
    }
}
