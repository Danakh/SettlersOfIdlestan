using System;
using SettlersOfIdlestan.Controller;
using SettlersOfIdlestan.Controller.Ascension;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Prestige;
using SOITests.TestUtilities;
using Xunit;

namespace SOITests.ControllerTests;

/// <summary>
/// Presets d'automatisation de construction (voir AutomationPresetSettings et
/// TechnologyId.AutomationPreset) : plafonds par bâtiment/preset, respect du plafond par
/// BuildingController.TickGuildAutomation, et survie au prestige/ascension (porté par GodState).
/// </summary>
public class AutomationPresetTests
{
    [Fact]
    public void GetCap_UnsetEntry_DefaultsTo10()
    {
        var presets = new AutomationPresetSettings();

        Assert.Equal(10, presets.GetCap(1, BuildingType.Sawmill));
        Assert.Equal(10, presets.GetActiveCap(BuildingType.Sawmill));
    }

    [Fact]
    public void ClampToTheoreticalMax_LowersStoredValueAboveMax()
    {
        var presets = new AutomationPresetSettings();
        int max = BuildingMaxLevelCalculator.GetTheoreticalMaxLevel(BuildingType.Sawmill);
        presets.SetCap(1, BuildingType.Sawmill, Math.Min(AutomationPresetSettings.MaxCap, max + 5));
        // SetCap borne déjà à 10 : on force directement le dictionnaire pour simuler une valeur
        // décodée d'une ancienne sauvegarde, supérieure au max théorique mais valide à l'époque.
        presets.Preset1Caps[BuildingType.Sawmill] = max + 5;

        presets.ClampToTheoreticalMax();

        Assert.Equal(max, presets.GetCap(1, BuildingType.Sawmill));
    }

    [Fact]
    public void ClampToTheoreticalMax_LeavesValueWithinRangeUnchanged()
    {
        var presets = new AutomationPresetSettings();
        int max = BuildingMaxLevelCalculator.GetTheoreticalMaxLevel(BuildingType.Sawmill);
        Assert.True(max > 0, "Test assumes Sawmill has a positive theoretical max level.");
        presets.SetCap(2, BuildingType.Sawmill, max - 1 >= 0 ? max - 1 : 0);
        int expected = presets.GetCap(2, BuildingType.Sawmill);

        presets.ClampToTheoreticalMax();

        Assert.Equal(expected, presets.GetCap(2, BuildingType.Sawmill));
    }

    [Fact]
    public void SetCap_ClampsToValidRange()
    {
        var presets = new AutomationPresetSettings();

        presets.SetCap(1, BuildingType.Sawmill, -5);
        Assert.Equal(0, presets.GetCap(1, BuildingType.Sawmill));

        presets.SetCap(1, BuildingType.Sawmill, 999);
        Assert.Equal(10, presets.GetCap(1, BuildingType.Sawmill));
    }

    [Fact]
    public void GetActiveCap_FollowsActivePreset()
    {
        var presets = new AutomationPresetSettings();
        presets.SetCap(1, BuildingType.Sawmill, 2);
        presets.SetCap(2, BuildingType.Sawmill, 7);

        Assert.Equal(2, presets.GetActiveCap(BuildingType.Sawmill));

        presets.SetActivePreset(2);
        Assert.Equal(7, presets.GetActiveCap(BuildingType.Sawmill));
    }

    [Fact]
    public void SetActivePreset_ClampsBetween1And3()
    {
        var presets = new AutomationPresetSettings();

        presets.SetActivePreset(0);
        Assert.Equal(1, presets.ActivePreset);

        presets.SetActivePreset(99);
        Assert.Equal(3, presets.ActivePreset);
    }

    private static (WorldState state, BuildingController controller, Civilization civ, GodState godState) CreateAutomationSetup()
    {
        var state = IslandTestFactory.CreateSevenHexIslandState();
        var civ = state.Civilizations[0];
        var city = civ.Cities[0];
        city.AddBuilding(new TownHall { Level = 20 }); // city.Level = 20 → Sawmill available (E hex = Forest)
        city.AddBuilding(new HarvestersGuild { Level = 4 });

        var godState = new GodState();
        state.AutomationSettings.BindPresets(godState);
        state.AutomationSettings.ProductionBuildingAutomationEnabled = true;

        var controller = new BuildingController(state);
        return (state, controller, civ, godState);
    }

    [Fact]
    public void TickGuildAutomation_CapZero_NeverBuildsTheType()
    {
        var (state, controller, civ, godState) = CreateAutomationSetup();
        var city = civ.Cities[0];
        godState.AutomationPresets.SetCap(1, BuildingType.Sawmill, 0);

        var clock = new GameClock();
        clock.Start();
        controller.Initialize(state, clock);

        civ.AddResource(Resource.Wood, 200);
        civ.AddResource(Resource.Brick, 100);

        clock.SimulateAdvance(5000);

        Assert.DoesNotContain(city.Buildings, b => b.Type == BuildingType.Sawmill);
    }

    [Fact]
    public void TickGuildAutomation_StopsUpgradingOncePresetCapReached()
    {
        var (state, controller, civ, godState) = CreateAutomationSetup();
        var city = civ.Cities[0];
        godState.AutomationPresets.SetCap(1, BuildingType.Sawmill, 2);

        var clock = new GameClock();
        clock.Start();
        controller.Initialize(state, clock);

        // Assez de ressources pour largement dépasser le plafond de 2 si le preset n'était pas respecté.
        civ.AddResource(Resource.Wood, 100000);
        civ.AddResource(Resource.Brick, 100000);

        for (int i = 0; i < 50; i++) clock.SimulateAdvance(1000);

        var sawmill = Assert.Single(city.Buildings, b => b.Type == BuildingType.Sawmill);
        Assert.Equal(2, sawmill.Level);
    }

    [Fact]
    public void AutomationPresets_SurvivePrestigeAndAscension()
    {
        var controller = new MainGameController();
        controller.CreateNewGame();
        var godState = controller.CurrentMainState!.GodState;
        var civ = controller.CurrentMainState!.CurrentWorldState!.PlayerCivilization;
        civ.AddUniqueBuilding(BuildingType.ImperialPort);
        for (int i = 0; i < 20; i++) civ.Cities[0].AddBuilding(new Temple());

        godState.AutomationPresets.SetActivePreset(2);
        godState.AutomationPresets.SetCap(2, BuildingType.Sawmill, 3);

        controller.PerformPrestige();

        Assert.Equal(2, controller.CurrentMainState!.GodState.AutomationPresets.ActivePreset);
        Assert.Equal(3, controller.CurrentMainState.GodState.AutomationPresets.GetCap(2, BuildingType.Sawmill));

        godState.GodPoints = 100;
        godState.DivineEssence = AscensionController.MinDivineEssenceForAscension;
        controller.PerformAscension();

        Assert.Equal(2, controller.CurrentMainState!.GodState.AutomationPresets.ActivePreset);
        Assert.Equal(3, controller.CurrentMainState.GodState.AutomationPresets.GetCap(2, BuildingType.Sawmill));
    }

    /// <summary>
    /// Simule une sauvegarde plus ancienne où un plafond dépassait le max théorique courant : la
    /// prochaine initialisation (chargement, prestige, ascension — voir
    /// MainGameController.InitializeControllersForCurrentIsland) doit le ramener au max plutôt que
    /// de le laisser tel quel.
    /// </summary>
    [Fact]
    public void InitializeControllersForCurrentIsland_ClampsStalePresetCapsFromOlderSaves()
    {
        var controller = new MainGameController();
        controller.CreateNewGame();
        var godState = controller.CurrentMainState!.GodState;
        var civ = controller.CurrentMainState!.CurrentWorldState!.PlayerCivilization;
        civ.AddUniqueBuilding(BuildingType.ImperialPort);
        for (int i = 0; i < 20; i++) civ.Cities[0].AddBuilding(new Temple());

        int max = BuildingMaxLevelCalculator.GetTheoreticalMaxLevel(BuildingType.Sawmill);
        godState.AutomationPresets.Preset1Caps[BuildingType.Sawmill] = max + 5;

        controller.PerformPrestige();

        Assert.Equal(max, controller.CurrentMainState!.GodState.AutomationPresets.GetCap(1, BuildingType.Sawmill));
    }
}
