using System;
using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Controller.Expand;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Controller.Military;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.IslandFeatures;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Monsters;
using SettlersOfIdlestan.Model.Prestige.PrestigeMap;
using SettlersOfIdlestan.Model.Tasks;

namespace SettlersOfIdlestan.Controller.Tasks;

/// <summary>
/// S'abonne aux événements de tous les controllers et maintient GameRecord + RunRecord à jour.
/// Évalue les tâches tutoriel et émet OnTaskCompleted quand une tâche est complétée.
/// </summary>
public class TaskRecordController
{
    private GameRecord? _gameRecord;
    private RunRecord? _runRecord;
    private WorldState? _islandState;
    private int _playerCivIndex;

    private BuildingController? _buildingController;
    private RoadController? _roadController;
    private CityBuilderController? _cityBuilderController;
    private PrestigeMapController? _prestigeMapController;
    private ResearchController? _researchController;
    private MilitaryController? _militaryController;
    private HarvestController? _harvestController;
    private TradeController? _tradeController;
    private WonderController? _wonderController;

    public event EventHandler<TutorialTaskId>? OnTaskCompleted;

    internal TaskRecordController() { }

    internal void Initialize(
        GameRecord gameRecord,
        RunRecord runRecord,
        WorldState WorldState,
        BuildingController buildingController,
        RoadController roadController,
        CityBuilderController cityBuilderController,
        PrestigeMapController prestigeMapController,
        ResearchController researchController,
        MilitaryController militaryController,
        HarvestController harvestController,
        TradeController tradeController,
        WonderController wonderController)
    {
        Unsubscribe();

        _gameRecord = gameRecord;
        _runRecord = runRecord;
        _islandState = WorldState;
        _playerCivIndex = WorldState.PlayerCivilization.Index;
        _buildingController = buildingController;
        _roadController = roadController;
        _cityBuilderController = cityBuilderController;
        _prestigeMapController = prestigeMapController;
        _researchController = researchController;
        _militaryController = militaryController;
        _harvestController = harvestController;
        _tradeController = tradeController;
        _wonderController = wonderController;

        _buildingController.OnBuildingBuilt += HandleBuildingBuilt;
        _roadController.OnRoadBuilt += HandleRoadBuilt;
        _cityBuilderController.OnCityBuilt += HandleCityBuilt;
        _prestigeMapController.OnVertexPurchased += HandleVertexPurchased;
        _researchController.OnResearchCompleted += HandleResearchCompleted;
        _islandState.FeatureRemoved += HandleFeatureRemoved;
        _harvestController.OnHarvestCompleted += HandleHarvestCompleted;
        _tradeController.GoldObtainedFromTrade += HandleGoldObtainedFromTrade;
        _militaryController.ReinforcementSent += HandleReinforcementSent;
        _militaryController.CityDestroyed += HandleCityDestroyed;
        _wonderController.OnWonderPlaced += HandleWonderPlaced;
        _wonderController.OnWonderLevelUp += HandleWonderLevelUp;
    }

    private void Unsubscribe()
    {
        if (_buildingController != null) _buildingController.OnBuildingBuilt -= HandleBuildingBuilt;
        if (_roadController != null) _roadController.OnRoadBuilt -= HandleRoadBuilt;
        if (_cityBuilderController != null) _cityBuilderController.OnCityBuilt -= HandleCityBuilt;
        if (_prestigeMapController != null) _prestigeMapController.OnVertexPurchased -= HandleVertexPurchased;
        if (_researchController != null) _researchController.OnResearchCompleted -= HandleResearchCompleted;
        if (_islandState != null) _islandState.FeatureRemoved -= HandleFeatureRemoved;
        if (_harvestController != null) _harvestController.OnHarvestCompleted -= HandleHarvestCompleted;
        if (_tradeController != null) _tradeController.GoldObtainedFromTrade -= HandleGoldObtainedFromTrade;
        if (_militaryController != null) _militaryController.ReinforcementSent -= HandleReinforcementSent;
        if (_militaryController != null) _militaryController.CityDestroyed -= HandleCityDestroyed;
        if (_wonderController != null) _wonderController.OnWonderPlaced -= HandleWonderPlaced;
        if (_wonderController != null) _wonderController.OnWonderLevelUp -= HandleWonderLevelUp;
    }

    /// <summary>
    /// Appelé par MainGameController.PerformPrestige() avant la réinitialisation des controllers.
    /// </summary>
    internal void RecordPrestige()
    {
        if (_gameRecord == null) return;
        _gameRecord.TotalPrestigesPerformed++;
        CheckTaskCompletions();
    }

    private void HandleHarvestCompleted(object? sender, HarvestCompletedEventArgs e)
    {
        if (_gameRecord == null || _runRecord == null) return;
        if (e.CivilizationIndex != _playerCivIndex) return;

        foreach (var kv in e.Resources)
        {
            string key = kv.Key.ToString();
            _gameRecord.HarvestedResources[key] = _gameRecord.HarvestedResources.GetValueOrDefault(key) + kv.Value;
            _runRecord.HarvestedResources[key] = _runRecord.HarvestedResources.GetValueOrDefault(key) + kv.Value;
        }

        CheckTaskCompletions();
    }

    private static readonly HashSet<BuildingType> _productionBuildings = new()
    {
        BuildingType.Sawmill,
        BuildingType.Brickworks,
        BuildingType.Mill,
        BuildingType.Quarry,
        BuildingType.Mine,
        BuildingType.Seaport,
        BuildingType.GlassWorks,
    };

    private void HandleBuildingBuilt(object? sender, BuildingBuiltEventArgs e)
    {
        if (_gameRecord == null || _runRecord == null) return;
        if (e.City.CivilizationIndex != _playerCivIndex) return;

        if (e.IsNewBuilding)
        {
            _gameRecord.TotalBuildingsConstructed++;
            _runRecord.BuildingsConstructed++;
            string key = e.BuildingType.ToString();
            _gameRecord.BuildingCounts[key] = _gameRecord.BuildingCounts.GetValueOrDefault(key) + 1;
            _runRecord.BuildingCounts[key] = _runRecord.BuildingCounts.GetValueOrDefault(key) + 1;
        }
        else
        {
            _gameRecord.TotalBuildingsUpgraded++;
            _runRecord.BuildingsUpgraded++;

            if (e.Level == 2 && _productionBuildings.Contains(e.BuildingType))
                _gameRecord.ProductionBuildingsReachedLevel2++;

            if (e.Level == 4)
            {
                if (e.BuildingType == BuildingType.Seaport && !_gameRecord.HasSeaportLevel4)
                    _gameRecord.HasSeaportLevel4 = true;
                if (e.BuildingType == BuildingType.TownHall && !_gameRecord.HasTownHallLevel4)
                    _gameRecord.HasTownHallLevel4 = true;
            }

            if (!_gameRecord.HasSeaportAndTownHallLevel4SameCity
                && e.Level == 4
                && (e.BuildingType == BuildingType.Seaport || e.BuildingType == BuildingType.TownHall)
                && _islandState != null)
            {
                var city = e.City;
                if (city != null)
                {
                    bool hasSeaport4 = city.Buildings.Any(b => b.Type == BuildingType.Seaport && b.Level >= 4);
                    bool hasTownHall4 = city.Buildings.Any(b => b.Type == BuildingType.TownHall && b.Level >= 4);
                    if (hasSeaport4 && hasTownHall4)
                        _gameRecord.HasSeaportAndTownHallLevel4SameCity = true;
                }
            }
        }

        CheckTaskCompletions();
    }

    private void HandleRoadBuilt(object? sender, RoadAutoBuiltEventArgs e)
    {
        if (_gameRecord == null || _runRecord == null) return;
        if (e.CivilizationIndex != _playerCivIndex) return;

        _gameRecord.TotalRoadsBuilt++;
        _runRecord.RoadsBuilt++;
        CheckTaskCompletions();
    }

    private void HandleCityBuilt(object? sender, OutpostAutoBuiltEventArgs e)
    {
        if (_gameRecord == null || _runRecord == null) return;
        if (e.CivilizationIndex != _playerCivIndex) return;

        _gameRecord.TotalCitiesBuilt++;
        _runRecord.CitiesBuilt++;
        CheckTaskCompletions();
    }

    private void HandleVertexPurchased(object? sender, VertexPurchasedEventArgs e)
    {
        if (_gameRecord == null) return;
        _gameRecord.TotalPrestigeVerticesPurchased++;
        if (e.Vertex.Equals(PrestigeMap.BarracksVertex))
            _gameRecord.HasPurchasedBarracksVertex = true;
        CheckTaskCompletions();
    }

    private void HandleReinforcementSent(object? sender, ReinforcementEventArgs e)
    {
        if (_gameRecord == null) return;
        _gameRecord.HasCreatedReinforcementFlow = true;
        CheckTaskCompletions();
    }

    private void HandleCityDestroyed(object? sender, CityDestroyedEventArgs e)
    {
        if (_gameRecord == null) return;
        if (e.CivilizationIndex != _playerCivIndex && e.CivilizationIndex >= 0)
        {
            _gameRecord.TotalEnemyCitiesDestroyed++;
            CheckTaskCompletions();
        }
    }

    private void HandleWonderPlaced(object? sender, EventArgs e)
    {
        if (_gameRecord == null) return;
        _gameRecord.HasPlacedWonder = true;
        CheckTaskCompletions();
    }

    private void HandleWonderLevelUp(object? sender, int level)
    {
        if (_gameRecord == null) return;
        if (level >= 1)
            _gameRecord.HasBuiltWonder = true;
        CheckTaskCompletions();
    }

    private void HandleResearchCompleted(object? sender, TechnologyId e)
    {
        if (_gameRecord == null || _runRecord == null) return;
        _gameRecord.TotalResearchCompleted++;
        _runRecord.ResearchCompleted++;
        CheckTaskCompletions();
    }

    private void HandleFeatureRemoved(object? sender, IslandFeature e)
    {
        if (_gameRecord == null || _runRecord == null) return;
        if (e is Bandit)
        {
            _gameRecord.TotalBanditsDefeated++;
            _runRecord.BanditsDefeated++;
            CheckTaskCompletions();
        }
        else if (e is BanditHideout)
        {
            _gameRecord.TotalHideoutsDestroyed++;
            _runRecord.HideoutsDestroyed++;
            CheckTaskCompletions();
        }
        else if (e is Dragon)
        {
            _gameRecord.TotalDragonsDefeated++;
            _runRecord.DragonsDefeated++;
            CheckTaskCompletions();
        }
    }

    private void HandleGoldObtainedFromTrade(int amount)
    {
        if (_gameRecord == null) return;
        _gameRecord.TotalGoldObtainedFromTrade += amount;
        CheckTaskCompletions();
    }

    private void CheckTaskCompletions()
    {
        if (_gameRecord == null) return;
        foreach (var task in TutorialTaskDefinitions.All)
        {
            string key = task.Id.ToString();
            if (_gameRecord.CompletedTasks.Contains(key)) continue;
            if (task.IsCompleted(_gameRecord, _runRecord, _islandState))
            {
                _gameRecord.CompletedTasks.Add(key);
                OnTaskCompleted?.Invoke(this, task.Id);
            }
        }
    }

    public bool IsTaskCompleted(TutorialTaskId id)
        => _gameRecord?.CompletedTasks.Contains(id.ToString()) ?? false;

    public IReadOnlyList<TutorialTask> GetAllTasks() => TutorialTaskDefinitions.All;

    public IEnumerable<TutorialTask> GetIncompleteTasks()
    {
        if (_gameRecord == null) yield break;
        foreach (var task in TutorialTaskDefinitions.All)
            if (!_gameRecord.CompletedTasks.Contains(task.Id.ToString()))
                yield return task;
    }
}
