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

    public IEnumerable<Building> SelectedCityBuildings()
    {
        if (SelectedCity == null)
            return [];

        return _mainGameController.BuildingController.GetBuildableBuildings(SelectedCity.CivilizationIndex, SelectedCity.Position);
    }

    public bool TryExecuteSelectedCityBuildingAction(string buildingTypeName)
    {
        if (SelectedCity == null)
            return false;

        if (!Enum.TryParse<BuildingType>(buildingTypeName, out var type))
            return false;

        var existing = SelectedCity.Buildings.FirstOrDefault(b => b.Type == type);
        var success = existing == null
            ? TryBuildBuilding(type)
            : TryActivateBuilding(type);

        return success;
    }

    public bool TryBuildBuilding(BuildingType buildingType)
    {
        if (SelectedCity != null)
        {
            _mainGameController.BuildingController.BuildBuilding(SelectedCity.CivilizationIndex, SelectedCity.Position, buildingType);
            if (_mainGameController.CurrentMainState != null)
            {
                _mainGameController.SetGame(_mainGameController.CurrentMainState);
            }
            return true;
        }
        return false;
    }

    public bool TryActivateBuilding(BuildingType buildingType)
    {
        if (SelectedCity != null)
        {

            var existing = SelectedCity.Buildings.FirstOrDefault(b => b.Type == buildingType);
            if (existing == null)
                return false;

            // Placeholder: les actions actives par bâtiment (Prestige, etc.) ne sont pas encore branchées.
            return true;
        }
        return false;
    }
}
