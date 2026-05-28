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

    public IEnumerable<Building> SelectedCityUniqueBuildingsAndBuildables()
    {
        if (SelectedCity == null)
            return [];

        return BuildingController.GetUniqueBuildingsAndBuildables(SelectedCity.CivilizationIndex, SelectedCity.Position);
    }

    public bool HasUniqueBuildingsUnlocked()
    {
        return SelectedCity?.Level >= 4;
    }

    public bool IsBuiltInSelectedCity(Building building)
    {
        return SelectedCity != null && SelectedCity.Buildings.Any(b => b.Type == building.Type);
    }

    public bool SelectedCityHasAnyUniqueBuilding()
    {
        return SelectedCity != null && SelectedCity.Buildings.Any(b => b.IsUnique);
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

        // Check build prerequisites (e.g. required other buildings)
        if (building.Level == 0 && !building.HasBuildPrerequisites(SelectedCity))
            return false;

        // Only one unique building per city
        if (building.Level == 0 && building.IsUnique && SelectedCityHasAnyUniqueBuilding())
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

    public int GetSelectedCivilizationForgeBonus(Forge building)
    {
        if (SelectedCity == null)
            return 0;
        var islandState = IslandState;
        if (islandState == null || SelectedCity.CivilizationIndex >= islandState.Civilizations.Count)
            return 0;
        return islandState.Civilizations[SelectedCity.CivilizationIndex].ForgeDoubleProdBonus * building.Level;
    }

    public long GetCurrentTick() => _mainGameController.CurrentMainState?.Clock?.CurrentTick ?? 0;

    public long GetEffectiveMarketCooldown(Market market)
    {
        if (SelectedCity == null) return HarvestController.MarketGenerationCooldownTicks;
        var islandState = IslandState;
        if (SelectedCity.CivilizationIndex >= islandState.Civilizations.Count)
            return HarvestController.MarketGenerationCooldownTicks;
        var civ = islandState.Civilizations[SelectedCity.CivilizationIndex];
        return HarvestController.GetEffectiveMarketCooldown(market, civ);
    }

    public bool IsAtMaxLevel(Building building)
    {
        if (SelectedCity == null)
            return false;
        return building.Level >= BuildingController.GetMaxLevel(building, SelectedCity.CivilizationIndex);
    }
}
