using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Controller;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestanSkia.Services;

public class CityBuildingService
{
    private readonly IslandState _islandState;
    private readonly BuildingController _buildingController;
    public City? SelectedCity { get; private set; } = null;

    public CityBuildingService(MainGameController mainGameController)
    {
        _islandState = mainGameController.CurrentMainState?.CurrentIslandState ?? throw new InvalidOperationException("Island state is not available.");
        _buildingController = mainGameController.BuildingController ?? throw new InvalidOperationException("BuildingController is not available.");
    }

    public void SetSelectedCity(Vertex selectedCityVertex)
    {
        SelectedCity = _islandState.FindCityAt(selectedCityVertex);
    }

    public IEnumerable<Building> SelectedCityBuildingsAndBuildables()
    {
        if (SelectedCity == null)
            return [];

        return _buildingController.GetBuildingsAndBuildables(SelectedCity.CivilizationIndex, SelectedCity.Position);
    }

    public void TryExecuteSelectedCityBuildingAction(BuildingType buildingType)
    {
        if (SelectedCity != null)
        {
            _buildingController.BuildBuilding(SelectedCity.CivilizationIndex, SelectedCity.Position, buildingType);
        }
    }

    public bool CanBuildOrUpgrade(Building building)
    {
        if (SelectedCity == null)
            return false;

        var islandState = _islandState;
        if (islandState == null || SelectedCity.CivilizationIndex >= islandState.Civilizations.Count)
            return false;

        var civilization = islandState.Civilizations[SelectedCity.CivilizationIndex];

        // Check if at max level
        if (IsAtMaxLevel(building))
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
        return building.Level >= _buildingController.GetMaxLevel(building);
    }
}
