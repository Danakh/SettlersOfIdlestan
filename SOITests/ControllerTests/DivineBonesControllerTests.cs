using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandFeatures;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Prestige;
using SOITests.TestUtilities;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace SOITests.ControllerTests
{
    /// <summary>
    /// Tests du plafond d'essences divines (voir DivineBones.GetEssenceCap et
    /// DivineBonesController.ProcessInvestment) quand plusieurs Os Divins terminent leur
    /// Purification dans le même tick — le cas qui a motivé ces tests : le joueur purifiait
    /// plusieurs Os d'un coup et se demandait si le plafond affiché (corruption + pouvoirs divins,
    /// voir AscensionController.GetDivineEssenceCap) était bien respecté sans être sous-compté.
    /// </summary>
    public class DivineBonesControllerTests
    {
        /// <summary>Les 3 hexes touchés par la ville de l'île de test (voir IslandTestFactory.CreateSevenHexIslandState).</summary>
        private static HexCoord[] CityAdjacentHexes => new[]
        {
            new HexCoord(0, 0, IslandMap.SurfaceLayer),
            new HexCoord(0, 1, IslandMap.SurfaceLayer),
            new HexCoord(1, 0, IslandMap.SurfaceLayer),
        };

        private static (WorldState state, GameClock clock, GodState godState, DivineBonesController controller) CreateSetup(int divineEssence)
        {
            var state = IslandTestFactory.CreateSevenHexIslandState();

            var clock = new GameClock();
            clock.Start();

            var godState = new GodState { DivineEssence = divineEssence };
            var prng = new GamePRNG(1);

            var controller = new DivineBonesController();
            controller.Initialize(state, clock, godState, prng, harvestController: null);

            return (state, clock, godState, controller);
        }

        /// <summary>
        /// Investissement déjà couvert, quel que soit le coût réel : évite de dépendre du multiplicateur
        /// dynamique (DivineBones.GetCostMultiplier), qui bouge avec EssenceAlreadyCollected pendant la
        /// boucle du même tick (voir DivineBonesController.ProcessInvestment).
        /// </summary>
        private static void FillInvestment(DivineBones bones)
        {
            foreach (var resource in new[] { Resource.Crystal, Resource.Mithril, Resource.Steel })
            {
                bones.InvestedResources[resource] = long.MaxValue / 2;
                bones.InvestmentEnabled.Add(resource);
            }
        }

        private static List<DivineBones> AddThreeBonesAdjacentToCity(WorldState state, int corruptionLevel)
        {
            var bonesList = new List<DivineBones>();
            foreach (var hex in CityAdjacentHexes)
            {
                var bones = new DivineBones(hex, corruptionLevel);
                FillInvestment(bones);
                state.AddFeature(bones);
                bonesList.Add(bones);
            }
            return bonesList;
        }

        /// <summary>
        /// Trois Os Divins prêts à se terminer sont posés sur les 3 hexes touchant la ville, avec un
        /// plafond de 11 essences (niveau de corruption 11) : partant de 8 essences déjà détenues,
        /// les 3 Purifications simultanées doivent bien amener le total à 11, sans sous-compter à
        /// cause du traitement séquentiel des Os dans la même boucle de tick.
        /// </summary>
        [Fact]
        public void SimultaneousPurifications_InSameTick_ReachTheDisplayedCap()
        {
            var (state, clock, godState, controller) = CreateSetup(divineEssence: 8);
            var bonesList = AddThreeBonesAdjacentToCity(state, corruptionLevel: 11);

            Assert.All(bonesList, b => Assert.Equal(11, b.GetEssenceCap()));

            clock.SimulateAdvance(DivineBonesController.InvestmentIntervalTicks);

            Assert.Equal(11, godState.DivineEssence);
            Assert.Equal(3, godState.TotalDivineEssenceEarned);
            Assert.Empty(state.Features.OfType<DivineBones>());
            Assert.Equal(3, state.EventLog.Entries.Count(e => e.Type == GameEventType.DivineBonesPurified));
            Assert.DoesNotContain(state.EventLog.Entries, e => e.Type == GameEventType.DivineBonesPurifiedNoEssence);
        }

        /// <summary>
        /// Même scénario, mais en partant à 1 essence du plafond : sur les 3 Purifications simultanées,
        /// seules les 2 premières doivent obtenir une essence (8→9, 9→10... ici 9→10→11), la 3e
        /// franchissant le plafond ne doit rien accorder — le plafond ne doit être ni dépassé, ni
        /// sous-compté d'une unité par un effet de bord du traitement séquentiel.
        /// </summary>
        [Fact]
        public void SimultaneousPurifications_ExactlyAtCapBoundary_GrantsOnlyUpToCap()
        {
            var (state, clock, godState, controller) = CreateSetup(divineEssence: 9);
            AddThreeBonesAdjacentToCity(state, corruptionLevel: 11);

            clock.SimulateAdvance(DivineBonesController.InvestmentIntervalTicks);

            Assert.Equal(11, godState.DivineEssence);
            Assert.Equal(2, godState.TotalDivineEssenceEarned);
            Assert.Empty(state.Features.OfType<DivineBones>());
            Assert.Equal(2, state.EventLog.Entries.Count(e => e.Type == GameEventType.DivineBonesPurified));
            Assert.Single(state.EventLog.Entries.Where(e => e.Type == GameEventType.DivineBonesPurifiedNoEssence));
        }
    }
}
