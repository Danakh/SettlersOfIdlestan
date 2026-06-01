using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Controller;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Controller.Expand;
using SettlersOfIdlestan.Controller.Military;

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
        _mainGameController.MilitaryController.CityDestroyed += OnCityDestroyed;
    }

    private void OnCityDestroyed(object? sender, CityDestroyedEventArgs e)
    {
        if (SelectedCity?.Position.Equals(e.CityVertex) == true)
            ClearSelectedCity();
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

        return BuildingController.GetBuildingsAndBuildables(SelectedCity);
    }

    public IEnumerable<Building> SelectedCityUniqueBuildingsAndBuildables()
    {
        if (SelectedCity == null)
            return [];

        return BuildingController.GetUniqueBuildingsAndBuildables(SelectedCity!);
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
            BuildingController.BuildBuilding(SelectedCity, buildingType);
        }
    }

    public void ToggleBuildingActivation(BuildingType buildingType)
    {
        var building = SelectedCity?.Buildings.FirstOrDefault(b => b.Type == buildingType);
        if (building == null || building.ActivationStatus == ActivationStatus.NON_ACTIVABLE) return;
        building.ActivationStatus = building.ActivationStatus == ActivationStatus.ACTIVE
            ? ActivationStatus.INACTIVE
            : ActivationStatus.ACTIVE;
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
        return islandState.Civilizations[SelectedCity.CivilizationIndex].ForgeDoubleHarvestBonus * building.Level;
    }

    public long GetCurrentTick() => _mainGameController.CurrentMainState?.Clock?.CurrentTick ?? 0;

    public (int available, int max) GetSelectedCitySoldiers()
    {
        if (SelectedCity == null) return (0, 0);
        var mc = _mainGameController.MilitaryController;
        return (mc.GetAttackScore(SelectedCity), mc.GetMaximumSoldierCapacity(SelectedCity, IslandState.Civilizations[SelectedCity.CivilizationIndex]));
    }

    public (int current, int max) GetSelectedCityDefense()
    {
        if (SelectedCity == null) return (0, 0);
        var mc = _mainGameController.MilitaryController;
        var islandState = IslandState;
        if (SelectedCity.CivilizationIndex >= islandState.Civilizations.Count) return (0, 0);
        var civ = islandState.Civilizations[SelectedCity.CivilizationIndex];
        return (SelectedCity.CurrentDefense, mc.GetDefenseScore(SelectedCity, civ));
    }

    public long GetEffectiveSeaportGenerationCooldown(Seaport seaport)
    {
        return HarvestController.GetEffectiveSeaportGenerationCooldown(seaport);
    }

    public bool IsAtMaxLevel(Building building)
    {
        if (SelectedCity == null)
            return false;
        return building.Level >= BuildingController.GetMaxLevel(building, SelectedCity.CivilizationIndex);
    }

    /// <summary>
    /// Returns true if the building could be built/upgraded if resources were available,
    /// ignoring the resource check (but still checking unique constraints and prerequisites).
    /// </summary>
    public bool CanBuildOrUpgradeIgnoringResources(Building building)
    {
        if (SelectedCity == null) return false;
        var islandState = IslandState;
        if (SelectedCity.CivilizationIndex >= islandState.Civilizations.Count) return false;
        if (IsAtMaxLevel(building)) return false;
        if (building.Level == 0 && !building.HasBuildPrerequisites(SelectedCity)) return false;
        if (building.Level == 0 && building.IsUnique && SelectedCityHasAnyUniqueBuilding()) return false;
        return true;
    }
}
