using SettlersOfIdlestanSkia.Services;
using Xunit;

namespace SOIUITests;

/// <summary>
/// Un changement de monde (chargement, redemarrage d'ile, prestige, ascension) remplace le
/// WorldState et toutes les civilisations. Les services UI qui survivent a ce changement doivent
/// etre nettoyes, pas remplaces : plusieurs renderers recoivent CityBuildingService dans leur
/// constructeur et gardaient l'ancienne instance -- et sa selection perimee -- quand le service la
/// recreait.
/// </summary>
public class WorldSwapUiStateTests
{
    [Fact]
    public void Redemarrer_l_ile_garde_le_meme_CityBuildingService_et_vide_la_selection()
    {
        var service = new GameControllerService();
        service.InitializeNewGame();

        var cityBuildingService = service.CityBuildingService;
        var startingCity = service.PlayerCivilization!.Cities[0];
        cityBuildingService.SetSelectedCity(startingCity.Position);
        Assert.NotNull(cityBuildingService.SelectedCity);

        service.RestartIsland();

        Assert.Same(cityBuildingService, service.CityBuildingService);
        Assert.Null(cityBuildingService.SelectedCity);
    }
}
