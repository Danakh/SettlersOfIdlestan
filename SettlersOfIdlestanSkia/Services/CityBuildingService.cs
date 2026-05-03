using SettlersOfIdlestan.Model.City;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestanSkia.Services;
using SettlersOfIdlestan.Controller;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SettlersOfIdlestanSkia.Services;

public class CityBuildingService
{
    private readonly MainGameController _mainGameController;
    public City? SelectedCity { get; private set; } = null;

    public CityBuildingService(MainGameController mainGameController)
    {
        _mainGameController = mainGameController ?? throw new ArgumentNullException(nameof(mainGameController));
    }

    public void SetSelectedCity(Vertex selectedCityVertex)
    {
        SelectedCity = _mainGameController.CurrentMainState?.CurrentIslandState?.FindCityAt(selectedCityVertex);
    }

    public IEnumerable<Building> SelectedCityBuildingsAndBuildables()
    {
        if (SelectedCity == null)
            return [];

        return _mainGameController.BuildingController.GetBuildingsAndBuildables(SelectedCity.CivilizationIndex, SelectedCity.Position);
    }

    public void TryExecuteSelectedCityBuildingAction(BuildingType buildingType)
    {
        if (SelectedCity != null)
        {
            _mainGameController.BuildingController.BuildBuilding(SelectedCity.CivilizationIndex, SelectedCity.Position, buildingType);
        }
    }
}
