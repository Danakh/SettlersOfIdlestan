using System;
using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Controller.Expand;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Controller.Military;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandFeatures;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Monsters;
using SettlersOfIdlestan.Model.Prestige;
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
    private GodState? _godState;
    private PlayerLifetimeStats? _lifetimeStats;
    private int _lastSyncedGodPointsEarned;

    private BuildingController? _buildingController;
    private RoadController? _roadController;
    private CityBuilderController? _cityBuilderController;
    private PrestigeMapController? _prestigeMapController;
    private ResearchController? _researchController;
    private MilitaryController? _militaryController;
    private HarvestController? _harvestController;
    private TradeController? _tradeController;
    private WonderController? _wonderController;
    private CorruptionSpireController? _corruptionSpireController;
    private DivineBonesController? _divineBonesController;

    public event EventHandler<TutorialTaskId>? OnTaskCompleted;
    public event EventHandler<GameRecord>? PrestigeRecorded;

    /// <summary>
    /// Émis après chaque mise à jour de GameRecord (tout événement de jeu suivi), permettant à
    /// l'AchievementController de valider les achievements en temps réel plutôt qu'au seul prestige.
    /// </summary>
    public event EventHandler<GameRecord>? GameRecordUpdated;

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
        WonderController wonderController,
        CorruptionSpireController corruptionSpireController,
        DivineBonesController divineBonesController,
        GodState godState,
        PlayerLifetimeStats lifetimeStats)
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
        _corruptionSpireController = corruptionSpireController;
        _divineBonesController = divineBonesController;
        _godState = godState;
        _lifetimeStats = lifetimeStats;
        _lastSyncedGodPointsEarned = godState.TotalGodPointsEarned;

        _buildingController.OnBuildingBuilt += HandleBuildingBuilt;
        _roadController.OnRoadBuilt += HandleRoadBuilt;
        _cityBuilderController.OnCityBuilt += HandleCityBuilt;
        _prestigeMapController.OnVertexPurchased += HandleVertexPurchased;
        _researchController.OnResearchCompleted += HandleResearchCompleted;
        _islandState.FeatureRemoved += HandleFeatureRemoved;
        _harvestController.OnHarvestCompleted += HandleHarvestCompleted;
        _tradeController.GoldObtainedFromTrade += HandleGoldObtainedFromTrade;
        _militaryController.ReinforcementSent += HandleReinforcementSent;
        _cityBuilderController.OnCityDestroyed += HandleCityDestroyed;
        _wonderController.OnWonderPlaced += HandleWonderPlaced;
        _wonderController.OnWonderLevelUp += HandleWonderLevelUp;
        _corruptionSpireController.OnCorruptionSpireBuilt += HandleCorruptionSpireBuilt;
        _divineBonesController.OnDivineBonesPurified += HandleDivineBonesPurified;

        RebuildPendingTaskIndices();
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
        if (_cityBuilderController != null) _cityBuilderController.OnCityDestroyed -= HandleCityDestroyed;
        if (_wonderController != null) _wonderController.OnWonderPlaced -= HandleWonderPlaced;
        if (_wonderController != null) _wonderController.OnWonderLevelUp -= HandleWonderLevelUp;
        if (_corruptionSpireController != null) _corruptionSpireController.OnCorruptionSpireBuilt -= HandleCorruptionSpireBuilt;
        if (_divineBonesController != null) _divineBonesController.OnDivineBonesPurified -= HandleDivineBonesPurified;
    }

    /// <summary>
    /// Appelé par MainGameController.PerformPrestige() avant la réinitialisation des controllers.
    /// <paramref name="earnedPrestigePoints"/> est le nombre de points gagnés pour cette partie.
    /// </summary>
    internal void RecordPrestige(int earnedPrestigePoints, bool corrupted = false)
    {
        if (_gameRecord == null) return;
        _gameRecord.TotalPrestigesPerformed++;
        if (corrupted)
            _gameRecord.TotalCorruptedPrestigesPerformed++;
        if (earnedPrestigePoints > _gameRecord.MaxPrestigePointsInSingleRun)
            _gameRecord.MaxPrestigePointsInSingleRun = earnedPrestigePoints;
        if (_lifetimeStats != null)
        {
            _lifetimeStats.TotalPrestigesPerformed++;
            _lifetimeStats.TotalPrestigePointsEarned += earnedPrestigePoints;
        }
        CheckTaskCompletions();
        PrestigeRecorded?.Invoke(this, _gameRecord);
    }

    /// <summary>
    /// Appelé par MainGameController.PerformAscension() avant la mutation de GodState (qui déclenche
    /// ensuite une réinitialisation des controllers). Comme RecordPrestige, ceci doit s'exécuter
    /// avant coup pour que le delta de points divins soit propagé vers PlayerLifetimeStats : sinon
    /// TaskRecordController.Initialize() (appelé par InitializeControllersForCurrentIsland) recale
    /// _lastSyncedGodPointsEarned sur la valeur déjà incrémentée, et le gain de cette Ascension ne
    /// serait jamais synchronisé.
    /// </summary>
    internal void RecordAscension(int godPointsGained)
    {
        if (_lifetimeStats != null)
            _lifetimeStats.TotalGodPointsEarned += godPointsGained;
        if (_gameRecord != null)
        {
            _gameRecord.HasPerformedAscension = true;
            CheckTaskCompletions();
        }
    }

    /// <summary>
    /// Met à jour les statistiques à vie (PlayerLifetimeStats) à partir de l'état courant.
    /// Les points divins sont propagés par delta (GodState.TotalGodPointsEarned ne diminue jamais
    /// au sein d'une partie, mais est réinitialisé à chaque "Nouvelle partie").
    /// Les records par run sont propagés par maximum.
    /// </summary>
    private void SyncLifetimeStats()
    {
        if (_lifetimeStats == null) return;

        if (_godState != null && _godState.TotalGodPointsEarned > _lastSyncedGodPointsEarned)
        {
            _lifetimeStats.TotalGodPointsEarned += _godState.TotalGodPointsEarned - _lastSyncedGodPointsEarned;
            _lastSyncedGodPointsEarned = _godState.TotalGodPointsEarned;
        }

        if (_runRecord != null)
        {
            int monstersThisRun = _runRecord.BanditsDefeated + _runRecord.DragonsDefeated
                + _runRecord.TrollsDefeated + _runRecord.OgresDefeated;
            if (monstersThisRun > _lifetimeStats.MaxMonstersDefeatedInSingleRun)
                _lifetimeStats.MaxMonstersDefeatedInSingleRun = monstersThisRun;

            if (_runRecord.CitiesBuilt > _lifetimeStats.MaxCitiesFoundedInSingleRun)
                _lifetimeStats.MaxCitiesFoundedInSingleRun = _runRecord.CitiesBuilt;
        }
    }

    /// <summary>
    /// Tient à jour GameRecord.MaxEffectiveDivineEssenceReached et HasChargedDivineReliquary depuis
    /// GodState (voir AscensionController.GetEffectiveDivineEssence) : appelé à chaque événement
    /// suivi plutôt que sur un événement dédié, faute d'événement quand le Reliquaire se charge
    /// (recalculé dans PrestigeController.PerformPrestige, avant même que TaskRecordController n'en
    /// soit informé — voir MainGameController.PerformPrestige, qui appelle RecordPrestige avant).
    /// </summary>
    private void SyncDivineEssenceRecord()
    {
        if (_gameRecord == null || _godState == null) return;

        int effective = _godState.DivineEssence + _godState.DivineEssenceReliquaryFloor;
        if (effective > _gameRecord.MaxEffectiveDivineEssenceReached)
            _gameRecord.MaxEffectiveDivineEssenceReached = effective;

        if (_godState.DivineEssenceReliquaryFloor > 0)
            _gameRecord.HasChargedDivineReliquary = true;
    }

    private void HandleDivineBonesPurified(object? sender, DivineBones e)
    {
        if (_gameRecord == null) return;
        _gameRecord.HasPurifiedDivineBones = true;
        CheckTaskCompletions();
    }

    /// <summary>Vrai si les deux hexes de cette arête sont du Vide (voir RoadController.IsEdgeBetweenVoidHexes, privée).</summary>
    private bool IsVoidRoad(Edge edge)
    {
        var map = _islandState?.GetMapFor(edge);
        if (map == null) return false;
        return map.GetTile(edge.Hex1)?.TerrainType == TerrainType.Void
            && map.GetTile(edge.Hex2)?.TerrainType == TerrainType.Void;
    }

    /// <summary>
    /// Nom persisté de chaque ressource. Même motif que <see cref="TaskKeys"/> : la récolte
    /// automatique lève un événement par ville et par hexagone à chaque tick, et
    /// <c>resource.ToString()</c> y allouait une chaîne à chaque fois.
    /// </summary>
    private static readonly Dictionary<Resource, string> ResourceKeys =
        Enum.GetValues<Resource>().ToDictionary(r => r, r => r.ToString());

    private void HandleHarvestCompleted(object? sender, HarvestCompletedEventArgs e)
    {
        if (_gameRecord == null || _runRecord == null) return;
        if (e.CivilizationIndex != _playerCivIndex) return;

        foreach (var kv in e.Resources)
        {
            string key = ResourceKeys[kv.Key];
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
            UpdateMaxUniqueBuildingTypesOnIsland();
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
        if (!_gameRecord.HasBuiltVoidRoad && IsVoidRoad(e.RoadPosition))
            _gameRecord.HasBuiltVoidRoad = true;
        CheckTaskCompletions();
    }

    private void UpdateMaxUniqueBuildingTypesOnIsland()
    {
        if (_gameRecord == null || _islandState == null) return;
        int uniqueTypes = _islandState.PlayerCivilization.Cities
            .SelectMany(c => c.Buildings)
            .Select(b => b.Type)
            .Distinct()
            .Count();
        if (uniqueTypes > _gameRecord.MaxUniqueBuildingTypesOnIsland)
            _gameRecord.MaxUniqueBuildingTypesOnIsland = uniqueTypes;
    }

    private void HandleCityBuilt(object? sender, OutpostAutoBuiltEventArgs e)
    {
        if (_gameRecord == null || _runRecord == null) return;
        if (e.CivilizationIndex != _playerCivIndex) return;

        _gameRecord.TotalCitiesBuilt++;
        _runRecord.CitiesBuilt++;
        if (e.Position.Z == LayerState.UnderworldZ)
            _gameRecord.HasFoundedUnderworldCity = true;
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
        // Only military conquest counts toward this achievement — monster-destroyed cities don't.
        if (e.Cause == CityDestructionCause.Combat && e.CivilizationIndex != _playerCivIndex && e.CivilizationIndex >= 0)
        {
            _gameRecord.TotalEnemyCitiesDestroyed++;

            var civ = _islandState?.GetCivilization(e.CivilizationIndex);
            if (civ != null && civ.IsNpc && civ.Cities.Count == 0 && _runRecord != null)
            {
                _runRecord.CivilizationsDestroyed++;
                _gameRecord.TotalCivilizationsDestroyed++;
            }

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
        if (level > _gameRecord.MaxWonderLevelReached)
            _gameRecord.MaxWonderLevelReached = level;
        CheckTaskCompletions();
    }

    private void HandleCorruptionSpireBuilt(object? sender, int sourceLevel)
    {
        if (_gameRecord == null) return;
        _gameRecord.HasBuiltCorruptionSpire = true;
        if (sourceLevel >= 4)
            _gameRecord.HasBuiltCorruptionSpireOnLevel4Source = true;
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

        // N'accorder les kills de monstres que si c'est le joueur qui a porté le coup fatal.
        bool killedByPlayer = e is MonsterFeature m && m.KilledByCivilizationIndex == _playerCivIndex;

        if (e is Bandit && killedByPlayer)
        {
            _gameRecord.TotalBanditsDefeated++;
            _runRecord.BanditsDefeated++;
            CheckTaskCompletions();
        }
        else if (e is BanditHideout && killedByPlayer)
        {
            _gameRecord.TotalHideoutsDestroyed++;
            _runRecord.HideoutsDestroyed++;
            CheckTaskCompletions();
        }
        else if (e is Dragon && killedByPlayer)
        {
            _gameRecord.TotalDragonsDefeated++;
            _runRecord.DragonsDefeated++;
            CheckTaskCompletions();
        }
        else if (e is Troll && killedByPlayer)
        {
            _gameRecord.TotalTrollsDefeated++;
            _runRecord.TrollsDefeated++;
            CheckTaskCompletions();
        }
        else if (e is Ogre && killedByPlayer)
        {
            _gameRecord.TotalOgresDefeated++;
            _runRecord.OgresDefeated++;
            CheckTaskCompletions();
        }
    }

    private void HandleGoldObtainedFromTrade(int amount, Resource _, int __)
    {
        if (_gameRecord == null) return;
        _gameRecord.TotalGoldObtainedFromTrade += amount;
        CheckTaskCompletions();
    }

    /// <summary>
    /// Clé persistée de chaque tâche, pré-calculée une fois pour toutes.
    /// <c>task.Id.ToString()</c> boxe l'enum et alloue une chaîne : le faire pour les 42 tâches à
    /// chaque appel de <see cref="CheckTaskCompletions"/> — donc à chaque récolte — représentait à
    /// lui seul plusieurs pourcents des allocations de la simulation en fin de partie.
    /// </summary>
    private static readonly string[] TaskKeys =
        TutorialTaskDefinitions.All.Select(t => t.Id.ToString()).ToArray();

    /// <summary>
    /// Indices, dans <see cref="TutorialTaskDefinitions.All"/>, des tâches pas encore complétées.
    /// Reconstruit au chargement puis entretenu au fil des complétions : une fois le tutoriel
    /// terminé — ce qui est le cas pendant l'essentiel d'une partie — la boucle de vérification
    /// devient vide au lieu de réévaluer 42 prédicats, dont plusieurs (CountBuilding,
    /// ComputePrestigePoints) parcourent tous les bâtiments de toutes les villes du joueur.
    /// </summary>
    private readonly List<int> _pendingTaskIndices = new();

    private List<TutorialTaskId>? _justCompletedTasks;

    private void RebuildPendingTaskIndices()
    {
        _pendingTaskIndices.Clear();
        if (_gameRecord == null) return;

        for (int i = 0; i < TutorialTaskDefinitions.All.Count; i++)
            if (!_gameRecord.CompletedTasks.Contains(TaskKeys[i]))
                _pendingTaskIndices.Add(i);
    }

    private void CheckTaskCompletions()
    {
        if (_gameRecord == null) return;
        SyncLifetimeStats();
        SyncDivineEssenceRecord();

        // Compactage en place, dans l'ordre de définition : l'ordre d'émission de OnTaskCompleted
        // est celui de l'ancienne boucle.
        int kept = 0;
        for (int read = 0; read < _pendingTaskIndices.Count; read++)
        {
            int index = _pendingTaskIndices[read];
            var task = TutorialTaskDefinitions.All[index];

            if (!task.IsCompleted(_gameRecord, _runRecord, _islandState))
            {
                _pendingTaskIndices[kept++] = index;
                continue;
            }

            _gameRecord.CompletedTasks.Add(TaskKeys[index]);
            (_justCompletedTasks ??= new List<TutorialTaskId>()).Add(task.Id);
        }
        _pendingTaskIndices.RemoveRange(kept, _pendingTaskIndices.Count - kept);

        // Notifié après le compactage : un abonné qui réentrerait ici verrait une liste cohérente.
        if (_justCompletedTasks is { Count: > 0 })
        {
            var completed = _justCompletedTasks;
            _justCompletedTasks = null;
            foreach (var id in completed)
                OnTaskCompleted?.Invoke(this, id);
        }

        GameRecordUpdated?.Invoke(this, _gameRecord);
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
