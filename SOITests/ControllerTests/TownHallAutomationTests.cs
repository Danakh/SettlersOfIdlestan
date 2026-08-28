using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.IslandMap;
using SOITests.TestUtilities;
using Xunit;

namespace SOITests.ControllerTests;

public class TownHallAutomationTests
{
    private static (WorldState state, BuildingController controller, Civilization civ) CreateSetup()
    {
        var state = IslandTestFactory.CreateSevenHexIslandState();
        var civ = state.Civilizations[0];
        var city = civ.Cities[0];
        city.AddBuilding(new TownHall { Level = 1 });
        city.AddBuilding(new BuildersGuild { Level = 1 });

        var controller = new BuildingController(state);
        return (state, controller, civ);
    }

    [Fact]
    public void TownHall_AutomationUpgradesTownHall()
    {
        var (state, controller, civ) = CreateSetup();
        var city = civ.Cities[0];

        state.AutomationSettings.TownHallAutomationEnabled = true;

        var clock = new GameClock();
        clock.Start();
        controller.Initialize(state, clock);

        // Coût du niveau 2 (TownHall.GetUpgradeCost) : 2*(2*2+1) = 10 par ressource.
        civ.AddResource(Resource.Food, 10);
        civ.AddResource(Resource.Wood, 10);
        civ.AddResource(Resource.Brick, 10);
        civ.AddResource(Resource.Stone, 10);

        clock.SimulateAdvance(10);
        Assert.Equal(1, city.FindBuilding(BuildingType.TownHall)!.Level);

        clock.SimulateAdvance(1000);
        Assert.Equal(2, city.FindBuilding(BuildingType.TownHall)!.Level);
    }

    /// <summary>
    /// Régression : un premier passage sans assez de ressources ne doit pas figer l'automatisation
    /// pour de bon. Avant la correction, <c>TickGuildAutomation</c> mettait en cache « rien à faire »
    /// dès le premier échec — y compris un échec dû au seul coût non couvert — sous une clé
    /// (BuildingsVersion, ModifierVersion, PresetsVersion) qu'aucune accumulation de ressources ne
    /// fait bouger, bloquant l'amélioration jusqu'à un événement sans rapport.
    /// </summary>
    [Fact]
    public void TownHall_AutomationRetriesAfterInsufficientResourcesOnFirstAttempt()
    {
        var (state, controller, civ) = CreateSetup();
        var city = civ.Cities[0];

        state.AutomationSettings.TownHallAutomationEnabled = true;

        var clock = new GameClock();
        clock.Start();
        controller.Initialize(state, clock);

        // Pas de ressources au premier cooldown écoulé : l'action échoue faute de coût couvert.
        clock.SimulateAdvance(10);
        clock.SimulateAdvance(1000);
        Assert.Equal(1, city.FindBuilding(BuildingType.TownHall)!.Level);

        // Les ressources s'accumulent ensuite (production classique), sans qu'aucun bâtiment,
        // modificateur ou preset ne change entretemps.
        civ.AddResource(Resource.Food, 10);
        civ.AddResource(Resource.Wood, 10);
        civ.AddResource(Resource.Brick, 10);
        civ.AddResource(Resource.Stone, 10);

        clock.SimulateAdvance(1000);
        Assert.Equal(2, city.FindBuilding(BuildingType.TownHall)!.Level);
    }
}
