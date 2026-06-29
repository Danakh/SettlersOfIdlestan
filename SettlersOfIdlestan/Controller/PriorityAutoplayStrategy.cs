using System;
using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
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
