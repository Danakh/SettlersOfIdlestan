using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Controller;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Controller.Expand;
using SettlersOfIdlestan.Controller.Military;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SettlersOfIdlestanSkia.Services;

public class CityBuildingService
{
    private readonly MainGameController _mainGameController;
    public event EventHandler? SelectionChanged;

    public City? SelectedCity { get; private set; } = null;
    private WorldState State => _mainGameController.CurrentMainState?.CurrentWorldState ?? throw new InvalidOperationException("Island state is not available.");
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
        var newCity = State.FindCityAt(selectedCityVertex);
        bool changed = newCity != SelectedCity;
        SelectedCity = newCity;
        if (changed && SelectedCity != null)
            SelectionChanged?.Invoke(this, EventArgs.Empty);
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

        var worldState = State;
        if (worldState == null || SelectedCity.CivilizationIndex >= worldState.Civilizations.Count)
            return false;

        var civilization = worldState.Civilizations[SelectedCity.CivilizationIndex];

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
        var worldState = State;
        if (worldState == null || SelectedCity.CivilizationIndex >= worldState.Civilizations.Count)
            return 0;
        return worldState.Civilizations[SelectedCity.CivilizationIndex].ForgeDoubleHarvestBonus * building.Level;
    }

    public int GetSelectedCivilizationMineGoldChancePercent()
    {
        if (SelectedCity == null)
            return 0;
        var worldState = State;
        if (worldState == null || SelectedCity.CivilizationIndex >= worldState.Civilizations.Count)
            return 0;
        return worldState.Civilizations[SelectedCity.CivilizationIndex].MineGoldChancePercent;
    }

    public long GetCurrentTick() => _mainGameController.CurrentMainState?.Clock?.CurrentTick ?? 0;

    public (int available, int max) GetSelectedCitySoldiers()
    {
        if (SelectedCity == null) return (0, 0);
        var mc = _mainGameController.MilitaryController;
        return (mc.GetAttackScore(SelectedCity), mc.GetMaximumSoldierCapacity(SelectedCity));
    }

    public (int current, int max) GetSelectedCityDefense()
    {
        if (SelectedCity == null) return (0, 0);
        var mc = _mainGameController.MilitaryController;
        return (SelectedCity.CurrentDefense, mc.GetDefenseScore(SelectedCity));
    }

    public long GetEffectiveSeaportGenerationCooldown(Seaport seaport)
    {
        return HarvestController.GetEffectiveSeaportGenerationCooldown(seaport);
    }

    private Civilization? SelectedCivilization
    {
        get
        {
            if (SelectedCity == null) return null;
            var worldState = State;
            if (worldState == null || SelectedCity.CivilizationIndex >= worldState.Civilizations.Count) return null;
            return worldState.Civilizations[SelectedCity.CivilizationIndex];
        }
    }

    /// <summary>Cooldown effectif du cycle de la Fonderie de la civilisation sélectionnée.</summary>
    public long GetSmelterEffectiveCooldown()
    {
        var civ = SelectedCivilization;
        var smelter = SelectedCity?.Buildings.OfType<Smelter>().FirstOrDefault();
        return civ == null || smelter == null ? Smelter.ProductionCooldownTicks : HarvestController.GetEffectiveSmelterCooldown(civ, smelter);
    }

    /// <summary>Minerai consommé par coulée de la Fonderie de la civilisation sélectionnée.</summary>
    public int GetSmelterOreInput()
    {
        var civ = SelectedCivilization;
        return civ == null ? Smelter.OreInputPerCycle : HarvestController.GetSmelterOreInput(civ);
    }

    /// <summary>Acier produit par coulée de la Fonderie de la civilisation sélectionnée.</summary>
    public int GetSmelterSteelOutput()
    {
        var civ = SelectedCivilization;
        return civ == null ? Smelter.SteelOutputPerCycle : HarvestController.GetSmelterSteelOutput(civ);
    }

    /// <summary>Vrai si la recherche Armures d'Acier est complétée pour la civilisation sélectionnée.</summary>
    public bool IsSteelArmorUnlocked()
        => SelectedCivilization?.ModifierAggregator.HasModifier(ECategory.UNLOCK_STEEL_ARMOR) ?? false;

    /// <summary>Multiplicateur de vitesse de récolte auto pour ce bâtiment (modificateurs civ inclus).</summary>
    public double GetHarvestSpeedMultiplier(Building building)
    {
        var civ = SelectedCivilization;
        if (civ == null) return 1.0;
        return civ.ModifierAggregator.ApplyModifiers(ECategory.HARVEST_SPEED, building.Type.ToString(), 1.0);
    }

    /// <summary>Bonus de récolte double de la Forge par niveau, venant des modificateurs de civilisation.</summary>
    public int GetCivilizationForgeHarvestBonusPerLevel()
        => SelectedCivilization?.ForgeDoubleHarvestBonus ?? 0;

    public bool IsAtMaxLevel(Building building)
    {
        if (SelectedCity == null)
            return false;
        return building.Level >= BuildingController.GetMaxLevel(building, SelectedCity.CivilizationIndex);
    }

    public int GetMaxLevel(Building building)
    {
        if (SelectedCity == null) return building.GetDefaultMaxLevel();
        return BuildingController.GetMaxLevel(building, SelectedCity.CivilizationIndex);
    }

    /// <summary>
    /// Returns true if the building could be built/upgraded if resources were available,
    /// ignoring the resource check (but still checking unique constraints and prerequisites).
    /// </summary>
    public bool CanBuildOrUpgradeIgnoringResources(Building building)
    {
        if (SelectedCity == null) return false;
        var worldState = State;
        if (SelectedCity.CivilizationIndex >= worldState.Civilizations.Count) return false;
        if (IsAtMaxLevel(building)) return false;
        if (building.Level == 0 && !building.HasBuildPrerequisites(SelectedCity)) return false;
        if (building.Level == 0 && building.IsUnique && SelectedCityHasAnyUniqueBuilding()) return false;
        return true;
    }
}
