using System.Reflection;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using SettlersOfIdlestan.Model.Ascension;
using SettlersOfIdlestanSkia.Screens;
using SettlersOfIdlestanSkia.Services;
using SettlersOfIdlestanUI;
using SettlersOfIdlestanUI.Views;
using Xunit;
using SkiaLayer = SettlersOfIdlestanSkia.Services;

namespace SOIUITests;

/// <summary>
/// Régression pour "je n'ai pas d'overlay" pendant le choix de race différé (voir
/// AscensionController.IsAscensionPending) : GameView.SyncFromGameState liait la visibilité de
/// TOUTE la barre du haut (onglets compris) à ResourceBarViewModel.IsAvailable, un proxy pour
/// "une partie tourne" qui devenait faux dès que RequestAscension détruisait
/// PlayerCivilization — cachant onglets, ressources, horloge et engrenage d'un coup, précisément
/// au moment où le joueur doit cliquer sur l'onglet Île pour choisir sa race. Ni
/// AscensionPendingRenderRepro (rendu Skia seul) ni AscensionPendingOverlayRefreshRepro
/// (ViewModels seuls, sans la vue GameView réelle) ne couvraient ce couplage : ce test construit
/// une vraie GameView pour vérifier que la barre du haut reste visible.
/// </summary>
public class AscensionPendingTopBarVisibilityTests
{
    private static GameControllerService GetGameControllerService(SkiaGameRuntime runtime)
    {
        var screenField = typeof(SkiaGameRuntime).GetField("_gameScreen", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var screen = (GameScreen)screenField.GetValue(runtime)!;
        var gcsField = typeof(GameScreen).GetField("_gameControllerService", BindingFlags.NonPublic | BindingFlags.Instance)!;
        return (GameControllerService)gcsField.GetValue(screen)!;
    }

    [AvaloniaFact]
    public void TopBar_StaysVisible_WhileAscensionPending_EvenThoughPlayerCivilizationIsGone()
    {
        var runtime = new SkiaGameRuntime();
        runtime.Initialize(new FakeFileSystemService(), allowDebugMode: true);
        runtime.InvokeTitleAction(SkiaLayer.TitleScreenSnapshot.ActionPrimary);

        using var host = new GameRuntimeHost(runtime);
        using var view = new GameView(host);

        var window = new Window { Width = 1280, Height = 800, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var syncMethod = typeof(GameView).GetMethod("SyncFromGameState", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var topBarField = typeof(GameView).GetField("_topBar", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var topBar = (Control)topBarField.GetValue(view)!;

        syncMethod.Invoke(view, null);
        Assert.True(topBar.IsVisible, "La barre du haut devrait deja etre visible en partie normale.");

        var gcs = GetGameControllerService(runtime);
        var ascension = gcs.MainGameController.AscensionController;
        var godState = gcs.CurrentGameState!.GodState;

        godState.GodPoints = 100;
        Assert.True(ascension.PurchasePower(AscensionPowerId.Faith));
        Assert.True(ascension.PurchasePower(AscensionPowerId.HandOfGod));
        Assert.True(ascension.PurchasePower(AscensionPowerId.MemoryOfGod));
        Assert.True(ascension.PurchasePower(AscensionPowerId.WalkOfGod));
        Assert.True(ascension.PurchasePower(AscensionPowerId.ArmOfGod));
        godState.DivineEssence = 10;

        gcs.RequestAscension();
        Assert.True(ascension.IsAscensionPending);
        Assert.Null(gcs.CurrentGameState!.PrestigeState);
        Assert.Null(gcs.PlayerCivilization);

        syncMethod.Invoke(view, null);

        Assert.True(topBar.IsVisible,
            "La barre du haut (onglets, ressources, horloge, engrenage) a disparu pendant le choix de race differe.");

        gcs.ConfirmAscensionRace(SettlersOfIdlestan.Model.Races.RaceId.Human);
        syncMethod.Invoke(view, null);

        Assert.True(topBar.IsVisible, "La barre du haut devrait rester visible une fois la race confirmee.");
    }
}
