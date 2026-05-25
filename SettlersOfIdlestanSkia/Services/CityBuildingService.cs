using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Controller;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Controller.Expand;

namespace SettlersOfIdlestanSkia.Services;

public class CityBuildingService
{
    private readonly MainGameController _mainGameController;
    public City? SelectedCity { get; private set; } = null;
    private IslandState IslandState => _mainGameController.CurrentMainState?.CurrentIslandState ?? throw new InvalidOperationException("Island state is not available.");
    private BuildingController BuildingController => _mainGameController.BuildingController ?? throw new InvalidOperationException("BuildingController is not available.");
    public PrestigeController PrestigeController => _mainGameController.PrestigeController;

    public CityBuildingService(MainGameController mainGameController)
    {
        _mainGameController = mainGameController ?? throw new ArgumentNullException(nameof(mainGameController));
    }

    public void SetSelectedCity(Vertex selectedCityVertex)
    {
        SelectedCity = IslandState.FindCityAt(selectedCityVertex);
    }

    public void ClearSelectedCity()
    {
        SelectedCity = null;
    }

    public IEnumerable<Building> SelectedCityBuildingsAndBuildables()
    {
        if (SelectedCity == null)
            return [];

        return BuildingController.GetBuildingsAndBuildables(SelectedCity.CivilizationIndex, SelectedCity.Position);
    }

    public void TryExecuteSelectedCityBuildingAction(BuildingType buildingType)
    {
        if (SelectedCity != null)
        {
            BuildingController.BuildBuilding(SelectedCity.CivilizationIndex, SelectedCity.Position, buildingType);
        }
    }

    public bool CanBuildOrUpgrade(Building building)
    {
        if (SelectedCity == null)
            return false;

        var islandState = IslandState;
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

    public long GetCurrentTick() => _mainGameController.CurrentMainState?.Clock?.CurrentTick ?? 0;

    public bool IsAtMaxLevel(Building building)
    {
        if (SelectedCity == null)
            return false;
        return building.Level >= BuildingController.GetMaxLevel(building, SelectedCity.CivilizationIndex);
    }
}
