using SettlersOfIdlestan.Controller;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.Localization;
using Xunit;

namespace SOITests.ControllerTests;

/// <summary>
/// Reglages choisis avant de lancer la partie (ecran-titre). Ils doivent etre poses sur le
/// MainGameState avant la generation de l'ile, car son initialisation capture l'instance de
/// GameSettings : les remplacer apres coup laisserait le journal, les automatisations et la
/// recherche cables sur les reglages par defaut, sans aucune erreur visible.
/// </summary>
public class NewGameSettingsTests
{
    [Fact]
    public void CreateNewGame_ReprendLesReglagesFournis()
    {
        var settings = new GameSettings
        {
            ShowTutorial = false,
            Language = Language.French,
            UiScale = 1.5f,
            PauseAfterPrestige = true,
            AutomationsEnabled = false,
        };

        var controller = new MainGameController();
        controller.CreateNewGame(settings);

        var applied = controller.CurrentMainState!.Settings;
        Assert.False(applied.ShowTutorial);
        Assert.Equal(Language.French, applied.Language);
        Assert.Equal(1.5f, applied.UiScale);
        Assert.True(applied.PauseAfterPrestige);
        Assert.False(applied.AutomationsEnabled);
    }

    [Fact]
    public void CreateNewGame_SansReglages_UtiliseLesValeursParDefaut()
    {
        var controller = new MainGameController();
        controller.CreateNewGame();

        Assert.True(controller.CurrentMainState!.Settings.ShowTutorial);
    }

    [Fact]
    public void CreateNewGame_CableLesReglagesFournisSurLIle()
    {
        var settings = new GameSettings { AutomationsEnabled = false };
        settings.EventLogFilter.SetCategoryVisible(EventLogCategory.Dragon, false);

        var controller = new MainGameController();
        controller.CreateNewGame(settings);

        var state = controller.CurrentMainState!.CurrentWorldState!;

        // Le journal et les automatisations doivent voir l'instance fournie, pas une instance neuve.
        state.EventLog.Add(GameEventType.DragonDiscovered);
        Assert.Empty(state.EventLog.Entries);
        Assert.False(state.AutomationSettings.IsRoadAutomationActive);
    }
}
