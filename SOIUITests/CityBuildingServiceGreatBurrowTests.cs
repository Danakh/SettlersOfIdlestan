using SettlersOfIdlestan.Controller;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestanSkia.Services;
using Xunit;

namespace SOIUITests;

/// <summary>
/// Régression : avec le Grand Terrier gobelin (UNIQUE_BUILDING_PREREQUISITE_REDUCTION +1) posé dans
/// une ville, le Port Impérial devait rester constructible dans une autre ville portuaire dont le
/// Comptoir plafonne à 3 (au lieu des 4 exigés sans réduction). En jeu, le bouton restait grisé sans
/// aucun tooltip de prérequis manquant : <see cref="CityBuildingService.CanBuildOrUpgrade"/>
/// interrogeait la ville brute au lieu du contexte réduit de
/// <see cref="BuildingController.BuildPrerequisiteContext"/>, contrairement à
/// <see cref="CityBuildingService.GetMissingPrerequisiteKey"/> et à
/// <see cref="CityBuildingService.CanBuildOrUpgradeIgnoringResources"/> qui, eux, l'utilisaient déjà
/// — d'où l'absence de tooltip malgré le bouton grisé.
/// </summary>
public class CityBuildingServiceGreatBurrowTests
{
    private static (MainGameController controller, CityBuildingService service, Vertex portCityVertex)
        BuildGoblinTwoCityIsland()
    {
        var center = new HexCoord(0, 0, IslandMap.SurfaceLayer);
        var e = new HexCoord(1, 0, IslandMap.SurfaceLayer);
        var w = new HexCoord(-1, 0, IslandMap.SurfaceLayer);
        var ne = new HexCoord(0, 1, IslandMap.SurfaceLayer);
        var sw = new HexCoord(0, -1, IslandMap.SurfaceLayer);
        var nw = new HexCoord(-1, 1, IslandMap.SurfaceLayer);
        var se = new HexCoord(1, -1, IslandMap.SurfaceLayer);

        var tiles = new List<HexTile>
        {
            new HexTile(center, TerrainType.Plain),
            new HexTile(e, TerrainType.Forest),
            new HexTile(w, TerrainType.Hill),
            new HexTile(ne, TerrainType.Plain),
            // Converti en Eau (Montagne à l'origine) : le Port Impérial exige un hex Eau adjacent.
            new HexTile(sw, TerrainType.Water),
            new HexTile(nw, TerrainType.Forest),
            new HexTile(se, TerrainType.Plain),
        };
        var map = new IslandMap(tiles);

        var civ = new Civilization { Index = 0 };

        // Ville avec le Grand Terrier : la réduction de prérequis qu'il accorde est portée par la
        // civilisation, pas par cette ville — elle doit bénéficier à l'autre ville tout autant.
        var burrowCityVertex = Vertex.Create(center, ne, e);
        var burrowCity = new City(burrowCityVertex) { CivilizationIndex = civ.Index };
        civ.AddCity(burrowCity);
        burrowCity.AddBuilding(new GreatBurrow { Level = 1 });

        // Ville portuaire : Hôtel de Ville 3 (Niveau 4 une fois réduit) et Comptoir 3 (le seuil réel
        // de 4 abaissé d'un cran) — exactement la situation décrite en jeu.
        var portCityVertex = Vertex.Create(center, w, sw);
        var portCity = new City(portCityVertex) { CivilizationIndex = civ.Index };
        civ.AddCity(portCity);
        portCity.AddBuilding(new TownHall { Level = 3 });
        portCity.AddBuilding(new Seaport { Level = 3 });

        foreach (Resource resource in Enum.GetValues<Resource>())
            civ.Resources[resource] = 1000;

        var state = new WorldState(map, new List<Civilization> { civ }, AtlasController.InvalidIslandId);

        var mainState = new MainGameState(state, new GameClock(), new GamePRNG(1));
        var controller = new MainGameController();
        controller.SetGame(mainState);

        var service = new CityBuildingService(controller);
        service.SetSelectedCity(portCityVertex);

        return (controller, service, portCityVertex);
    }

    [Fact]
    public void GetUniqueBuildingsAndBuildables_GreatBurrowReduction_OffersImperialPortInOtherCity()
    {
        var (controller, _, portCityVertex) = BuildGoblinTwoCityIsland();
        var portCity = controller.CurrentMainState!.CurrentWorldState!.FindCityAt(portCityVertex)!;

        // Confirme au niveau modèle que le Port Impérial est bien un candidat valide : si ce n'est
        // pas le cas, le bug est ailleurs que dans CityBuildingService.
        Assert.Contains(controller.BuildingController.GetUniqueBuildingsAndBuildables(portCity),
            b => b.Type == BuildingType.ImperialPort);
    }

    [Fact]
    public void GetMissingPrerequisiteKey_GreatBurrowReduction_ReportsNoMissingPrerequisite()
    {
        var (_, service, _) = BuildGoblinTwoCityIsland();

        Assert.Null(service.GetMissingPrerequisiteKey(new ImperialPort()));
    }

    [Fact]
    public void CanBuildOrUpgrade_GreatBurrowReduction_ImperialPortIsBuildable()
    {
        var (_, service, _) = BuildGoblinTwoCityIsland();

        // Reproduit le bug rapporté en jeu : le bouton restait grisé (CanBuildOrUpgrade == false)
        // alors qu'aucun tooltip de prérequis manquant ne s'affichait (test précédent) et que les
        // ressources étaient disponibles.
        Assert.True(service.CanBuildOrUpgrade(new ImperialPort()));
    }
}
