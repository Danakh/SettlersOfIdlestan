using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Controller;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.IslandMap;
using SOITests.TestUtilities;
using Xunit;

namespace SOITests.ControllerTests
{
    public class PriorityAutoplayStrategyTests
    {
        private class CountingObjective : IAutoplayObjective
        {
            private readonly int _target;
            public int Advances { get; private set; }
            public CountingObjective(int target) => _target = target;
            public bool IsComplete() => Advances >= _target;
            public bool TryAdvanceOnce() { Advances++; return true; }
        }

        [Fact]
        public void TryStepOnce_NeverAdvancesLaterObjectiveWhileEarlierIncomplete()
        {
            var first = new CountingObjective(3);
            var second = new CountingObjective(2);
            var strategy = new PriorityAutoplayStrategy(new IAutoplayObjective[] { first, second });

            for (int i = 0; i < 3; i++)
                strategy.TryStepOnce();

            Assert.Equal(3, first.Advances);
            Assert.Equal(0, second.Advances);
            Assert.False(strategy.IsComplete());

            for (int i = 0; i < 2; i++)
                strategy.TryStepOnce();

            Assert.Equal(2, second.Advances);
            Assert.True(strategy.IsComplete());
        }

        /// <summary>City "production levels" represented as a plain int list so the alternation between
        /// a production objective and an expansion objective can be asserted step by step, matching the
        /// scenario described when this class was designed: [prod lvl1] then [outposts] then [prod lvl2]
        /// equips each new outpost before moving on to the next one, instead of building all outposts
        /// first.</summary>
        private class ProductionLevelObjective : IAutoplayObjective
        {
            private readonly List<int> _cityLevels;
            private readonly int _targetLevel;
            public ProductionLevelObjective(List<int> cityLevels, int targetLevel)
            {
                _cityLevels = cityLevels;
                _targetLevel = targetLevel;
            }
            public bool IsComplete() => _cityLevels.All(l => l >= _targetLevel);
            public bool TryAdvanceOnce()
            {
                var idx = _cityLevels.FindIndex(l => l < _targetLevel);
                if (idx < 0) return false;
                _cityLevels[idx]++;
                return true;
            }
        }

        private class CityCountObjectiveStub : IAutoplayObjective
        {
            private readonly List<int> _cityLevels;
            private readonly int _targetCount;
            public CityCountObjectiveStub(List<int> cityLevels, int targetCount)
            {
                _cityLevels = cityLevels;
                _targetCount = targetCount;
            }
            public bool IsComplete() => _cityLevels.Count >= _targetCount;
            public bool TryAdvanceOnce() { _cityLevels.Add(0); return true; }
        }

        [Fact]
        public void TryStepOnce_AlternatesBetweenProductionAndExpansion_EquippingEachOutpostBeforeTheNext()
        {
            var cityLevels = new List<int> { 0 };
            var prodLevel1 = new ProductionLevelObjective(cityLevels, targetLevel: 1);
            var outposts = new CityCountObjectiveStub(cityLevels, targetCount: 3);
            var strategy = new PriorityAutoplayStrategy(new IAutoplayObjective[] { prodLevel1, outposts });

            strategy.TryStepOnce();
            Assert.Equal(new List<int> { 1 }, cityLevels);

            strategy.TryStepOnce();
            Assert.Equal(new List<int> { 1, 0 }, cityLevels);

            strategy.TryStepOnce();
            Assert.Equal(new List<int> { 1, 1 }, cityLevels);

            strategy.TryStepOnce();
            Assert.Equal(new List<int> { 1, 1, 0 }, cityLevels);

            strategy.TryStepOnce();
            Assert.Equal(new List<int> { 1, 1, 1 }, cityLevels);
            Assert.True(strategy.IsComplete());
        }

        [Fact]
        public void RunPriorityStrategyUntil_EquipsSecondCityBeforeReportingCompletion()
        {
            var mainController = new MainGameController();
            var atlas = new AtlasController();
            mainController.CreateNewGame(atlas.GetIslandParameters(atlas.GetFirstWorldId()), prngSeed: 42);

            var worldState = mainController.CurrentMainState!.CurrentWorldState!;
            var civ = worldState.Civilizations.First();

            var auto = new CivilizationAutoplayer(
                civ,
                worldState.GetMapForZ(IslandMap.SurfaceLayer)!,
                mainController.RoadController,
                mainController.HarvestController,
                mainController.BuildingController,
                mainController.CityBuilderController,
                mainController.TradeController,
                mainController.ResearchController,
                mainController.PrestigeController,
                mainController.PrestigeMapController,
                worldState,
                mainController.CurrentMainState!.PrestigeState,
                mainController.PerformPrestige);
            var runner = new CivilizationAutoplayerRunner(auto, civ, mainController);

            // Mirrors CivilizationAutoplayer's own Step1Buildings set: enough basic production to
            // sustain expansion (the Sawmill/Brickworks/Quarry/Mill carry passive harvest, unlike the
            // hand-harvest-only TownHall, which alone cannot fund the roads needed to reach a second city).
            var productionLevel1 = new BuildingLevelObjective(auto, mainController.BuildingController,
                new[]
                {
                    BuildingType.TownHall, BuildingType.Seaport, BuildingType.Market,
                    BuildingType.Sawmill, BuildingType.Brickworks, BuildingType.Quarry, BuildingType.Mill,
                },
                targetLevel: 1);
            var twoCities = new CityCountObjective(auto, targetCount: 2);
            var strategy = new PriorityAutoplayStrategy(new IAutoplayObjective[] { productionLevel1, twoCities });

            runner.RunPriorityStrategyUntil(strategy, () => false, maxIterations: 20000);

            Assert.True(strategy.IsComplete());
            Assert.Equal(2, civ.Cities.Count);
            Assert.All(civ.Cities, city =>
                Assert.Contains(city.Buildings, b => b.Type == BuildingType.TownHall && b.Level >= 1));
        }
    }
}
