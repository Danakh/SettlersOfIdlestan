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
        SelectedCity = FindCityAt(selectedCityVertex);
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
            ? TryBuildBuildingAtCity(SelectedCity.Position, type)
            : TryActivateBuildingAtCity(SelectedCity.Position, type);

        if (success)
        {
            SetSelectedCity(SelectedCity.Position);
        }

        return success;
    }

    public City? FindCityAt(Vertex vertex)
    {
        return GetAllCities().FirstOrDefault(c => c.Position.Equals(vertex));
    }

    public IReadOnlyList<City> GetAllCities()
    {
        return _mainGameController.CurrentMainState?.CurrentIslandState?.Civilizations
            .SelectMany(c => c.Cities)
            .ToList() ?? [];
    }


    public bool TryBuildBuildingAtCity(Vertex cityVertex, BuildingType buildingType)
    {
        var city = FindCityAt(cityVertex);
        if (city == null)
            return false;

        try
        {
            _mainGameController.BuildingController.BuildBuilding(city.CivilizationIndex, cityVertex, buildingType);
            if (_mainGameController.CurrentMainState != null)
            {
                _mainGameController.SetGame(_mainGameController.CurrentMainState);
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool TryActivateBuildingAtCity(Vertex cityVertex, BuildingType buildingType)
    {
        var city = FindCityAt(cityVertex);
        if (city == null)
            return false;

        var existing = city.Buildings.FirstOrDefault(b => b.Type == buildingType);
        if (existing == null)
            return false;

        // Placeholder: les actions actives par bâtiment (Prestige, etc.) ne sont pas encore branchées.
        return true;
    }
}
