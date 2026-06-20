using System;
using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;

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
            foreach (var city in _autoplayer.Civilization.Cities.ToList())
                foreach (var bt in _buildingTypes)
                    if (!IsDone(city, bt) && _autoplayer.TryBuildBuildingOnce(city, bt))
                        return true;
            return false;
        }

        private bool IsDone(City city, BuildingType bt)
        {
            var building = _buildingController.GetBuildingOrBuildable(city, bt);
            if (building == null) return true;
            var maxLevel = _buildingController.GetMaxLevel(building, _autoplayer.Civilization.Index);
            return building.Level >= Math.Min(_targetLevel, maxLevel);
        }
    }

    /// <summary>
    /// Satisfied once the civilization owns at least the target number of cities. Advances by delegating
    /// to <see cref="CivilizationAutoplayer.TryStep0Once"/> (pure expansion: an outpost when a buildable
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

        public bool TryAdvanceOnce() => _autoplayer.TryStep0Once();
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
