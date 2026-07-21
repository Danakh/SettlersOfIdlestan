using System;
using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandFeatures;
using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestan.Controller
{
    /// <summary>
    /// A single growth goal that a <see cref="PriorityAutoplayStrategy"/> can sequence. Implementations
    /// decide both when the goal is reached (<see cref="IsComplete"/>) and how to make one unit of
    /// progress toward it (<see cref="TryAdvanceOnce"/>), using <see cref="CivilizationAutoplayer"/>'s
    /// existing primitive methods. None of these advance the game clock.
    /// </summary>
    public interface IAutoplayObjective
    {
        bool IsComplete();
        bool TryAdvanceOnce();
    }

    /// <summary>
    /// Satisfied once every current city has each of the given building types at (at least) the target
    /// level. A building that is unavailable to a city (terrain/prerequisites not met) or already at its
    /// max level is treated as already satisfied for that city, since the autoplayer cannot do anything
    /// more about it.
    /// </summary>
    public class BuildingLevelObjective : IAutoplayObjective
    {
        private readonly CivilizationAutoplayer _autoplayer;
        private readonly BuildingController _buildingController;
        private readonly BuildingType[] _buildingTypes;
        private readonly int _targetLevel;

        public BuildingLevelObjective(
            CivilizationAutoplayer autoplayer,
            BuildingController buildingController,
            IEnumerable<BuildingType> buildingTypes,
            int targetLevel)
        {
            _autoplayer = autoplayer ?? throw new ArgumentNullException(nameof(autoplayer));
            _buildingController = buildingController ?? throw new ArgumentNullException(nameof(buildingController));
            _buildingTypes = buildingTypes?.ToArray() ?? throw new ArgumentNullException(nameof(buildingTypes));
            _targetLevel = targetLevel;
        }

        public bool IsComplete() =>
            _autoplayer.Civilization.Cities.All(city => _buildingTypes.All(bt => IsDone(city, bt)));

        public bool TryAdvanceOnce()
        {
            // Protection globale calculée une fois : coût max de chaque bâtiment en attente dans
            // toutes les villes. Évite qu'une ville trade une ressource dont une autre ville a besoin.
            var globalProtect = new ResourceSet();
            foreach (var city in _autoplayer.Civilization.Cities)
            {
                foreach (var bt in _buildingTypes)
                {
                    if (IsDone(city, bt)) continue;
                    var building = _buildingController.GetBuildingOrBuildable(city, bt);
                    if (building == null) continue;
                    var cost = building.Level == 0 ? building.GetBuildCost() : building.GetUpgradeCost(building.Level + 1);
                    foreach (var (resource, amount) in cost)
                        globalProtect[resource] = Math.Max(globalProtect[resource], amount);
                }
            }

            foreach (var city in _autoplayer.Civilization.Cities.ToList())
            {
                // Réserve par ville : uniquement les bâtiments effectivement constructibles ici.
                // On trade pour ce qu'on peut construire dans cette ville, mais on protège
                // globalement pour ne pas brader ce que d'autres villes attendent encore.
                var cityReserve = new ResourceSet();
                foreach (var bt in _buildingTypes)
                {
                    if (IsDone(city, bt)) continue;
                    var building = _buildingController.GetBuildingOrBuildable(city, bt);
                    if (building == null) continue;
                    var cost = building.Level == 0 ? building.GetBuildCost() : building.GetUpgradeCost(building.Level + 1);
                    foreach (var (resource, amount) in cost)
                        cityReserve[resource] = Math.Max(cityReserve[resource], amount);
                }
                _autoplayer.TryGrindOnce(cityReserve, resourcesToKeep: globalProtect);

                foreach (var bt in _buildingTypes)
                    if (!IsDone(city, bt) && _autoplayer.TryBuildBuildingOnce(city, bt, withGrind: false))
                        return true;
            }
            return false;
        }

        private bool IsDone(City city, BuildingType bt)
        {
            var building = _buildingController.GetBuildingOrBuildable(city, bt);
            if (building == null) return true;
            var maxLevel = _buildingController.GetMaxLevel(building, _autoplayer.Civilization);
            return building.Level >= Math.Min(_targetLevel, maxLevel);
        }
    }

    /// <summary>
    /// Wraps a <see cref="BuildingLevelObjective"/> behind a runtime predicate: while the predicate is
    /// false, this objective reports itself as already complete (a no-op — control passes straight to
    /// the next stage) and never touches the autoplayer. Once the predicate becomes true it behaves
    /// exactly like the inner objective. Re-evaluated on every call since
    /// <see cref="PriorityAutoplayStrategy.TryStepOnce"/> re-scans from the top each time, so e.g. a
    /// stage gated on "a Bandit has been spotted" naturally reopens mid-run the moment one is, even if
    /// later stages had already started.
    /// </summary>
    public class ConditionalBuildingLevelObjective : IAutoplayObjective
    {
        private readonly Func<bool> _predicate;
        private readonly BuildingLevelObjective _inner;

        public ConditionalBuildingLevelObjective(Func<bool> predicate, BuildingLevelObjective inner)
        {
            _predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public bool IsComplete() => !_predicate() || _inner.IsComplete();
        public bool TryAdvanceOnce() => _inner.TryAdvanceOnce();
    }

    /// <summary>
    /// Satisfied once the civilization owns at least the target number of cities. Advances by delegating
    /// to <see cref="CivilizationAutoplayer.TryExpandOnce"/> (pure expansion: an outpost when a buildable
    /// vertex exists, otherwise a road toward the nearest prospective vertex).
    /// </summary>
    public class CityCountObjective : IAutoplayObjective
    {
        private readonly CivilizationAutoplayer _autoplayer;
        private readonly int _targetCount;

        public CityCountObjective(CivilizationAutoplayer autoplayer, int targetCount)
        {
            _autoplayer = autoplayer ?? throw new ArgumentNullException(nameof(autoplayer));
            _targetCount = targetCount;
        }

        public bool IsComplete() => _autoplayer.Civilization.Cities.Count >= _targetCount;

        public bool TryAdvanceOnce() => _autoplayer.TryExpandOnce();
    }

    /// <summary>
    /// Satisfied once the civilization has built the (unique) Imperial Port. Advances by delegating to
    /// <see cref="CivilizationAutoplayer.TryBuildImperialPortOnce"/>, which focuses exclusively on the
    /// first coastal city rather than spreading Seaport/Warehouse/TownHall levels across every city the
    /// way <see cref="BuildingLevelObjective"/> would — unique buildings are never returned as buildable
    /// by <see cref="BuildingController.GetBuildingOrBuildable"/>, so BuildingLevelObjective can't drive
    /// this on its own regardless of which building types are listed.
    /// </summary>
    public class ImperialPortObjective : IAutoplayObjective
    {
        private readonly CivilizationAutoplayer _autoplayer;

        public ImperialPortObjective(CivilizationAutoplayer autoplayer)
        {
            _autoplayer = autoplayer ?? throw new ArgumentNullException(nameof(autoplayer));
        }

        public bool IsComplete() => _autoplayer.Civilization.UniqueBuildings.Contains(BuildingType.ImperialPort);

        public bool TryAdvanceOnce() => _autoplayer.TryBuildImperialPortOnce();
    }

    /// <summary>
    /// Satisfied once the civilization has built the given (unique) building anywhere. Advances by trying
    /// <see cref="CivilizationAutoplayer.TryBuildUniqueBuildingOnce"/> across every city in turn — unlike
    /// <see cref="BuildingLevelObjective"/>'s multi-building-type grind thrash, every city here chases the
    /// exact same build cost, so grinding from each one is harmless. Unique buildings are never returned
    /// as buildable by <see cref="BuildingController.GetBuildingOrBuildable"/>, so BuildingLevelObjective
    /// can't drive them regardless of which building types are listed.
    /// </summary>
    public class UniqueBuildingObjective : IAutoplayObjective
    {
        private readonly CivilizationAutoplayer _autoplayer;
        private readonly BuildingType _buildingType;

        public UniqueBuildingObjective(CivilizationAutoplayer autoplayer, BuildingType buildingType)
        {
            _autoplayer = autoplayer ?? throw new ArgumentNullException(nameof(autoplayer));
            _buildingType = buildingType;
        }

        public bool IsComplete() => _autoplayer.Civilization.UniqueBuildings.Contains(_buildingType);

        public bool TryAdvanceOnce()
        {
            bool didSomething = false;
            foreach (var city in _autoplayer.Civilization.Cities.ToList())
            {
                if (_autoplayer.TryBuildUniqueBuildingOnce(city, _buildingType, withGrind: true))
                    didSomething = true;
            }
            return didSomething;
        }
    }

    /// <summary>
    /// Maintenance objective that keeps research flowing: starts the cheapest available research
    /// whenever none is in progress, and sets the cheapest queueable research whenever the queue slot
    /// is empty (once the research-queue prestige perk is unlocked). No-ops entirely until research is
    /// unlocked. <see cref="IsComplete"/> delegates to <see cref="CivilizationAutoplayer.HasResearchActionAvailable"/>,
    /// a side-effect-free check, so — like <see cref="BarracksActivationObjective"/> — this only ever
    /// blocks the strategy for the tick(s) needed to (re)start research, never for the whole research
    /// duration, which is what lets it sit early in a priority list without starving building progress.
    /// </summary>
    public class ResearchObjective : IAutoplayObjective
    {
        private readonly CivilizationAutoplayer _autoplayer;

        public ResearchObjective(CivilizationAutoplayer autoplayer)
        {
            _autoplayer = autoplayer ?? throw new ArgumentNullException(nameof(autoplayer));
        }

        public bool IsComplete() => !_autoplayer.HasResearchActionAvailable();

        public bool TryAdvanceOnce() => _autoplayer.TryResearchOnce();
    }

    /// <summary>
    /// Satisfied once every one of the four basic terrain types (Forest, Hill, Plain, Mountain) has at
    /// least one city adjacent to a non-contested hex of that type on the surface layer. Reactivates
    /// automatically if a terrain type later becomes fully contested (e.g. disputed zone). When a
    /// terrain type is missing, tries to (1) build an outpost on a road-connected vertex adjacent to
    /// that terrain, then (2) extend the road network toward unexplored hexes within edge distance 1–2
    /// in hope of discovering it. Treats itself as complete (pass-through) when neither action is
    /// possible, so it never blocks the rest of the strategy.
    /// </summary>
    public class ResourceCoverageObjective : IAutoplayObjective
    {
        private static readonly TerrainType[] ResourceTerrains =
            { TerrainType.Forest, TerrainType.Hill, TerrainType.Plain, TerrainType.Mountain };

        private static readonly HashSet<TerrainType> ResourceTerrainSet = new()
            { TerrainType.Forest, TerrainType.Hill, TerrainType.Plain, TerrainType.Mountain };

        private readonly CivilizationAutoplayer _autoplayer;

        public ResourceCoverageObjective(CivilizationAutoplayer autoplayer)
        {
            _autoplayer = autoplayer ?? throw new ArgumentNullException(nameof(autoplayer));
        }

        public bool IsComplete()
        {
            var missing = GetMissingTerrains();
            if (missing.Count == 0) return true;

            foreach (var terrain in missing)
                if (_autoplayer.GetBuildableVertexForTerrain(terrain) != null)
                    return false;

            return !_autoplayer.HasUnexploredHexesWithinTwoRoads();
        }

        public bool TryAdvanceOnce()
        {
            var missing = GetMissingTerrains();
            if (missing.Count == 0) return false;

            foreach (var terrain in missing)
            {
                var vertex = _autoplayer.GetBuildableVertexForTerrain(terrain);
                if (vertex != null)
                    return _autoplayer.TryBuildOutpostOnce(vertex, withGrind: true);
            }

            return _autoplayer.TryExtendRoadTowardUnexploredOnce();
        }

        private List<TerrainType> GetMissingTerrains()
        {
            var ws = _autoplayer.WorldState;
            if (ws == null) return new List<TerrainType>();

            var map = ws.GetMapForZ(IslandMap.SurfaceLayer);
            if (map == null) return new List<TerrainType>();

            var contestedHexes = new HashSet<HexCoord>(
                ws.Features.OfType<ContestedTerritory>().Select(ct => ct.Position));

            var covered = new HashSet<TerrainType>();
            foreach (var city in _autoplayer.Civilization.Cities)
            {
                if (city.Position.Z != IslandMap.SurfaceLayer) continue;
                foreach (var hex in city.Position.GetHexes())
                {
                    if (contestedHexes.Contains(hex)) continue;
                    var terrain = map.GetTile(hex)?.TerrainType;
                    if (terrain.HasValue && ResourceTerrainSet.Contains(terrain.Value))
                        covered.Add(terrain.Value);
                }
            }

            return ResourceTerrains.Where(t => !covered.Contains(t)).ToList();
        }
    }

    /// <summary>
    /// Objective de maintenance qui active ou désactive les Casernes selon l'équilibre alimentaire.
    /// Quand les soldats consomment plus de <see cref="FoodConsumptionThreshold"/> du gain de nourriture
    /// par seconde, les Casernes sont désactivées (arrêt du recrutement). Quand la consommation
    /// redescend sous le seuil, elles sont réactivées. <paramref name="forceActive"/>, si fourni et vrai,
    /// court-circuite ce calcul et force l'activation quel que soit l'équilibre alimentaire — utilisé en
    /// temps de guerre (voir <see cref="CivilizationAutoplayerPriorities.Unified"/>) : une expansion
    /// illimitée en fin de liste peut faire grimper la consommation de nourriture bien au-delà du seuil,
    /// et désactiver les Casernes à ce moment-là couperait le recrutement de soldats en pleine attaque.
    /// <see cref="IsComplete"/> retourne true dès que l'état actuel est déjà correct, ce qui garantit
    /// que cet objectif ne bloque jamais la stratégie plus d'un tick.
    /// </summary>
    public class BarracksActivationObjective : IAutoplayObjective
    {
        private readonly CivilizationAutoplayer _autoplayer;
        private readonly Func<bool>? _forceActive;
        private const double FoodConsumptionThreshold = 0.5;

        public BarracksActivationObjective(CivilizationAutoplayer autoplayer, Func<bool>? forceActive = null)
        {
            _autoplayer = autoplayer ?? throw new ArgumentNullException(nameof(autoplayer));
            _forceActive = forceActive;
        }

        public bool IsComplete()
        {
            bool shouldBeActive = ShouldBarracksBeActive();
            return _autoplayer.Civilization.Cities.All(city =>
            {
                var barracks = city.Buildings.OfType<Barracks>().FirstOrDefault();
                if (barracks == null || barracks.Level == 0) return true;
                return barracks.ActivationStatus == (shouldBeActive ? ActivationStatus.ACTIVE : ActivationStatus.INACTIVE);
            });
        }

        public bool TryAdvanceOnce()
        {
            bool shouldBeActive = ShouldBarracksBeActive();
            var targetStatus = shouldBeActive ? ActivationStatus.ACTIVE : ActivationStatus.INACTIVE;
            bool didSomething = false;
            foreach (var city in _autoplayer.Civilization.Cities)
            {
                var barracks = city.Buildings.OfType<Barracks>().FirstOrDefault();
                if (barracks == null || barracks.Level == 0) continue;
                if (barracks.ActivationStatus == targetStatus) continue;
                barracks.ActivationStatus = targetStatus;
                didSomething = true;
            }
            return didSomething;
        }

        private bool ShouldBarracksBeActive()
        {
            if (_forceActive != null && _forceActive()) return true;

            int civIndex = _autoplayer.Civilization.Index;
            var production = _autoplayer.HarvestController.GetAverageProductionRatesPerSecond(civIndex);
            var consumption = _autoplayer.HarvestController.GetAverageConsumptionRatesPerSecond(civIndex);
            production.TryGetValue(Resource.Food, out double foodGain);
            consumption.TryGetValue(Resource.Food, out double foodCost);
            if (foodGain <= 0) return foodCost <= 0;
            return foodCost <= foodGain * FoodConsumptionThreshold;
        }
    }

    /// <summary>
    /// Maintenance objective that keeps <see cref="CivilizationAutoplayer.PriorityTargetCivilization"/>
    /// pointed at the nearest visible enemy civilization (via
    /// <see cref="CivilizationAutoplayer.FindNearestVisibleEnemy"/>) and refreshes attack/reinforcement
    /// flows through <see cref="CivilizationAutoplayer.TryUpdatePriorityTargetFlowsOnce"/>. Gated by
    /// <paramref name="shouldAttack"/> so it stays a no-op until, e.g., a city-count threshold is
    /// reached. <see cref="IsComplete"/> re-checks with <c>apply: false</c> on every call, so this only
    /// ever blocks the strategy for the tick(s) needed to (re)point flows at the current target —
    /// unlike <see cref="BuildingLevelObjective"/> it never waits for a whole war to be won, which is
    /// what lets it sit early in a priority list without starving later objectives like expansion.
    /// </summary>
    public class AttackNeighborsObjective : IAutoplayObjective
    {
        private readonly CivilizationAutoplayer _autoplayer;
        private readonly Func<bool> _shouldAttack;

        public AttackNeighborsObjective(CivilizationAutoplayer autoplayer, Func<bool> shouldAttack)
        {
            _autoplayer = autoplayer ?? throw new ArgumentNullException(nameof(autoplayer));
            _shouldAttack = shouldAttack ?? throw new ArgumentNullException(nameof(shouldAttack));
        }

        public bool IsComplete()
        {
            if (!_shouldAttack()) return true;
            var target = SyncTarget();
            return target == null || !_autoplayer.TryUpdatePriorityTargetFlowsOnce(apply: false);
        }

        public bool TryAdvanceOnce()
        {
            var target = SyncTarget();
            if (target == null) return false;
            return _autoplayer.TryUpdatePriorityTargetFlowsOnce();
        }

        private Civilization? SyncTarget()
        {
            var target = _autoplayer.FindNearestVisibleEnemy();
            _autoplayer.PriorityTargetCivilization = target;
            return target;
        }
    }

    /// <summary>
    /// Drives a <see cref="CivilizationAutoplayer"/> through an ordered list of <see cref="IAutoplayObjective"/>s,
    /// never acting on objective N+1 while objective N still has actionable progress to make. Each call to
    /// <see cref="TryStepOnce"/> re-scans the list from the top, so an event that re-opens an earlier
    /// objective (e.g. a freshly built outpost that lacks the production buildings a prior objective
    /// already finished elsewhere) automatically pulls focus back to it on the next call. This is what
    /// produces "finish step before moving on" sequencing: e.g. [all level-1 production] then
    /// [5 outposts] then [all level-2 production] will fully equip each outpost before the next one is
    /// built, rather than building all 5 outposts first.
    /// Does not advance the game clock — pair with a time-advancing loop the way CivilizationAutoplayer's
    /// own Step methods are paired with CivilizationAutoplayerRunner.
    /// </summary>
    public class PriorityAutoplayStrategy
    {
        private readonly IReadOnlyList<IAutoplayObjective> _objectives;

        public PriorityAutoplayStrategy(IReadOnlyList<IAutoplayObjective> objectives)
        {
            _objectives = objectives ?? throw new ArgumentNullException(nameof(objectives));
        }

        public bool IsComplete() => _objectives.All(o => o.IsComplete());

        public bool TryStepOnce()
        {
            foreach (var objective in _objectives)
            {
                if (objective.IsComplete()) continue;
                return objective.TryAdvanceOnce();
            }
            return false;
        }
    }
}
