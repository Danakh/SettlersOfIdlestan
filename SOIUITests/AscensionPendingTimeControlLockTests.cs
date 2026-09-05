using System.Reflection;
using Avalonia.Headless.XUnit;
using SettlersOfIdlestan.Model.Ascension;
using SettlersOfIdlestanSkia.Screens;
using SettlersOfIdlestanSkia.Services;
using SettlersOfIdlestanUI;
using SettlersOfIdlestanUI.ViewModels;
using Xunit;

namespace SOIUITests;

/// <summary>
/// Verrou du controle de temps pendant le choix de race d'une Ascension (voir
/// GameScreen.IsTimeControlLocked). La barre du haut, bouton lecture compris, reste volontairement
/// visible pendant cette attente — c'est meme garanti par AscensionPendingTopBarVisibilityTests —
/// alors qu'il n'existe plus aucune ile a simuler : RequestAscension a detruit le PrestigeState et
/// le WorldState avec.
///
/// Un appui sur lecture relancait donc une horloge qui ne pouvait plus rien faire avancer, et
/// surtout arretait d'alimenter la banque hors-ligne (GameClock.Advance ne verse le temps ecoule
/// dans OfflineBankTicks que pendant la pause) — le temps passe a choisir sa race etait perdu, et
/// au-dela de x1 la banque deja accumulee se vidait par-dessus. Cote modele, ce meme appui
/// remettait l'ile detruite en marche (voir
/// MainGameControllerTests.RequestAscension_ThenResumingTheClock_...).
/// </summary>
public class AscensionPendingTimeControlLockTests
{
    private static GameControllerService GetGameControllerService(SkiaGameRuntime runtime)
    {
        var screenField = typeof(SkiaGameRuntime).GetField("_gameScreen", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var screen = (GameScreen)screenField.GetValue(runtime)!;
        var gcsField = typeof(GameScreen).GetField("_gameControllerService", BindingFlags.NonPublic | BindingFlags.Instance)!;
        return (GameControllerService)gcsField.GetValue(screen)!;
    }

    [AvaloniaFact]
    public void TimeControl_IsLockedOnPause_WhileAscensionPending_AndTimeGoesToTheOfflineBank()
    {
        var runtime = new SkiaGameRuntime();
        runtime.Initialize(new FakeFileSystemService(), allowDebugMode: true);
        runtime.InvokeTitleAction(TitleScreenSnapshot.ActionPrimary);

        using var host = new GameRuntimeHost(runtime);
        var viewModel = new TimeControlViewModel(host);

        var gcs = GetGameControllerService(runtime);
        var ascension = gcs.MainGameController.AscensionController;
        var godState = gcs.CurrentGameState!.GodState;
        var clock = gcs.CurrentGameState!.Clock;

        viewModel.Refresh();
        Assert.False(viewModel.IsLocked, "Le controle de temps doit etre libre en partie normale.");
        Assert.True(viewModel.IsUnlocked);

        godState.GodPoints = 100;
        Assert.True(ascension.PurchasePower(AscensionPowerId.Faith));
        godState.DivineEssence = 10;

        gcs.RequestAscension();
        Assert.True(ascension.IsAscensionPending);

        viewModel.Refresh();
        Assert.True(viewModel.IsLocked, "Le controle de temps doit etre verrouille pendant le choix de race.");
        Assert.False(viewModel.IsUnlocked);
        Assert.True(viewModel.IsPaused);

        // Le joueur appuie sur lecture, puis tente une vitesse : les deux doivent etre sans effet.
        viewModel.TogglePause();
        host.TogglePause();
        host.SetGameSpeed(10);

        Assert.Equal(0, clock.SpeedMultiplier);
        viewModel.Refresh();
        Assert.True(viewModel.IsPaused, "L'horloge ne doit pas avoir redemarre pendant le choix de race.");

        // Puisque la pause tient, le temps qui passe s'accumule bien dans la banque.
        long bankBefore = clock.OfflineBankTicks;
        long tickBefore = clock.CurrentTick;
        var now = DateTimeOffset.UtcNow;
        clock.Advance(now);
        clock.Advance(now.AddSeconds(5));

        Assert.True(clock.OfflineBankTicks > bankBefore,
            "Le temps passe a choisir sa race doit alimenter la banque hors-ligne.");
        Assert.Equal(tickBefore, clock.CurrentTick);

        // Une fois la race confirmee, le controle redevient libre : ConfirmAscensionRace relance
        // l'horloge, et le bouton reprend la main dans les deux sens.
        gcs.ConfirmAscensionRace(SettlersOfIdlestan.Model.Races.RaceId.Human);
        viewModel.Refresh();

        Assert.False(viewModel.IsLocked);
        Assert.False(viewModel.IsPaused);

        viewModel.TogglePause();
        Assert.True(viewModel.IsPaused, "Le bouton pause doit refonctionner une fois la race choisie.");

        viewModel.TogglePause();
        Assert.False(viewModel.IsPaused, "Le bouton lecture doit refonctionner une fois la race choisie.");
    }
}
