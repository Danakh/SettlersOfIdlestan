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
    public CitySelectionInfo SelectionInfo { get; private set; } = CitySelectionInfo.Empty;

    public CityBuildingService(MainGameController mainGameController)
    {
        _mainGameController = mainGameController ?? throw new ArgumentNullException(nameof(mainGameController));
    }

    public void SetSelectedCity(Vertex selectedCityVertex)
    {
        var city = FindCityAt(selectedCityVertex);
        if (city == null)
        {
            SelectionInfo = CitySelectionInfo.Empty;
            return;
        }

        var buildable = GetBuildableBuildingsAtCity(selectedCityVertex)
            .Select(b => b.Type)
            .ToHashSet();
        var builtByType = city.Buildings.ToDictionary(b => b.Type, b => b);
        var allTypes = Enum.GetValues<BuildingType>()
            .OrderBy(t => t.ToString());
        var buildings = allTypes.Select(type =>
            {
                var isBuilt = builtByType.TryGetValue(type, out var built);
                return new CityBuildingListItem(
                    BuildingType: type.ToString(),
                    IsBuilt: isBuilt,
                    CanBuild: !isBuilt && buildable.Contains(type),
                    Level: isBuilt && built != null ? built.Level : 0
                );
            })
            .ToList();

        SelectionInfo = new CitySelectionInfo(
            selectedCityVertex,
            city.LevelName,
            buildings
        );
    }

    public bool TryExecuteSelectedCityBuildingAction(string buildingTypeName)
    {
        var selectedCityVertex = SelectionInfo.SelectedCityVertex;
        if (selectedCityVertex == null)
            return false;

        if (!Enum.TryParse<BuildingType>(buildingTypeName, out var type))
            return false;

        var city = FindCityAt(selectedCityVertex);
        if (city == null)
            return false;

        var existing = city.Buildings.FirstOrDefault(b => b.Type == type);
        var success = existing == null
            ? TryBuildBuildingAtCity(selectedCityVertex, type)
            : TryActivateBuildingAtCity(selectedCityVertex, type);

        if (success)
        {
            SetSelectedCity(selectedCityVertex);
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

    public List<Building> GetBuildableBuildingsAtCity(Vertex cityVertex)
    {
        var city = FindCityAt(cityVertex);
        if (city == null)
            return [];

        return _mainGameController.BuildingController.GetBuildableBuildings(city.CivilizationIndex, cityVertex);
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
