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

    public bool CanBuildOrUpgrade(Building building)
    {
        if (SelectedCity == null)
            return false;

        var islandState = _mainGameController.CurrentMainState?.CurrentIslandState;
        if (islandState == null || SelectedCity.CivilizationIndex >= islandState.Civilizations.Count)
            return false;

        var civilization = islandState.Civilizations[SelectedCity.CivilizationIndex];

        // Check if at max level
        var isAtMaxLevel = building.Level >= building.MaxLevel;
        if (isAtMaxLevel)
            return false;

        // Get the cost for this action
        var cost = building.Level == 0 ? building.GetBuildCost() : building.GetUpgradeCost(building.Level + 1);

        // Check if we have enough resources
        foreach (var (resource, amount) in cost)
        {
            if (civilization.GetResourceQuantity(resource) < amount)
                return false;
        }

        return true;
    }

    public bool IsAtMaxLevel(Building building)
    {
        return building.Level >= building.MaxLevel;
    }
}
