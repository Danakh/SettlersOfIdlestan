using System.Reflection;
using SettlersOfIdlestan.Controller;
using SettlersOfIdlestan.Model.Ascension;
using SettlersOfIdlestan.Model.Races;
using SettlersOfIdlestanSkia.Screens;
using SettlersOfIdlestanSkia.Services;
using SettlersOfIdlestanSkia.Services.Localization;
using SkiaSharp;
using Xunit;

namespace SOIUITests;

/// <summary>
/// Régression pour "Save File Load Error" au redémarrage complet de l'application pendant
/// l'intervalle de choix de race différé (voir AscensionController.IsAscensionPending) :
/// contrairement à AscensionPendingRenderRepro (qui part d'une GameScreen déjà construite avec une
/// île, et n'entre dans l'attente qu'ensuite via RequestAscension), ce test construit une GameScreen
/// à froid — saveJson déjà en attente de choix de race — exactement le chemin emprunté par
/// SkiaGameRuntime.OnContinueRequested après un redémarrage complet du process.
/// </summary>
public class AscensionPendingColdLoadRepro
{
    private static string CreateAscensionPendingSave()
    {
        var controller = new MainGameController();
        var parameters = SettlersOfIdlestan.Controller.Generator.DebugMapGenerator.CreateParameters();
        controller.CreateNewGame(parameters, prngSeed: 1);

        var godState = controller.CurrentMainState!.GodState;
        godState.GodPoints = 100;
        Assert.True(controller.AscensionController.PurchasePower(AscensionPowerId.Faith));
        godState.DivineEssence = 10;
        Assert.True(controller.AscensionController.CanAscend(godState));

        controller.RequestAscension();
        Assert.True(controller.AscensionController.IsAscensionPending);
        Assert.Null(controller.CurrentMainState!.PrestigeState);

        return controller.ExportMainState();
    }

    private static bool GetCorruptSavePending(GameScreen screen)
    {
        var field = typeof(GameScreen).GetField("_corruptSavePending", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("champ _corruptSavePending introuvable");
        return (bool)field.GetValue(screen)!;
    }

    private static GameControllerService GetGameControllerService(GameScreen screen)
    {
        var field = typeof(GameScreen).GetField("_gameControllerService", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("champ _gameControllerService introuvable");
        return (GameControllerService)field.GetValue(screen)!;
    }

    [Fact]
    public void ConstructGameScreen_FromSaveMadeDuringAscensionPending_DoesNotFallBackToCorruptSave()
    {
        var saveJson = CreateAscensionPendingSave();

        var fs = new FakeFileSystemService();
        var loc = new LocalizationService();
        var uiLayout = new UILayoutService();
        var resources = new ResourceManager();

        var ex = Record.Exception(() => new GameScreen(fs, loc, uiLayout, resources, saveJson, allowDebugMode: true));
        Assert.Null(ex);
    }

    [Fact]
    public void ConstructGameScreen_FromSaveMadeDuringAscensionPending_KeepsAscensionPendingState()
    {
        var saveJson = CreateAscensionPendingSave();

        var fs = new FakeFileSystemService();
        var loc = new LocalizationService();
        var uiLayout = new UILayoutService();
        var resources = new ResourceManager();

        var screen = new GameScreen(fs, loc, uiLayout, resources, saveJson, allowDebugMode: true);

        Assert.False(GetCorruptSavePending(screen));

        var gcs = GetGameControllerService(screen);
        Assert.True(gcs.MainGameController.AscensionController.IsAscensionPending);
        Assert.Null(gcs.CurrentGameState!.PrestigeState);
    }

    /// <summary>
    /// Couvre la suite du parcours réel de l'utilisateur : rendu effectif (pas seulement absence
    /// d'exception dans le constructeur) puis reprise via ConfirmAscensionRace, exactement comme
    /// AscensionPendingRenderRepro le fait pour le cas "devient en attente en cours de partie".
    /// </summary>
    [Fact]
    public void ConstructGameScreen_FromSaveMadeDuringAscensionPending_RendersAndCanResume()
    {
        var saveJson = CreateAscensionPendingSave();

        var fs = new FakeFileSystemService();
        var loc = new LocalizationService();
        var uiLayout = new UILayoutService();
        var resources = new ResourceManager();

        var screen = new GameScreen(fs, loc, uiLayout, resources, saveJson, allowDebugMode: true);
        Assert.False(GetCorruptSavePending(screen));

        var canvasSize = new SKSize(1280, 800);
        screen.EnsureCanvasInitialized(canvasSize);
        using var surface = SKSurface.Create(new SKImageInfo(1280, 800, SKColorType.Rgba8888, SKAlphaType.Premul));

        var exRender = Record.Exception(() => screen.Render(surface.Canvas));
        Assert.Null(exRender);

        var gcs = GetGameControllerService(screen);
        gcs.ConfirmAscensionRace(RaceId.Human);
        Assert.False(gcs.MainGameController.AscensionController.IsAscensionPending);
        Assert.NotNull(gcs.CurrentGameState!.PrestigeState);

        var exRenderAfterConfirm = Record.Exception(() => screen.Render(surface.Canvas));
        Assert.Null(exRenderAfterConfirm);
    }
}
