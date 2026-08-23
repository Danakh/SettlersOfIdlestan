using System.Reflection;
using SettlersOfIdlestan.Model.Ascension;
using SettlersOfIdlestanSkia.Screens;
using SettlersOfIdlestanSkia.Services;
using SettlersOfIdlestanUI;
using SettlersOfIdlestanUI.ViewModels;
using SkiaSharp;
using Xunit;
using SkiaLayer = SettlersOfIdlestanSkia.Services;

namespace SOIUITests;

/// <summary>
/// Regression pour "je n'ai pas d'overlay" pendant le choix de race differe (voir
/// AscensionController.IsAscensionPending) : contrairement a AscensionPendingRenderRepro (qui ne
/// teste que le rendu Skia), ce test exerce la boucle de synchronisation Avalonia reelle
/// (GameView.SyncFromGameState) - c'est a dire chaque ViewModel.Refresh() appele a chaque tick
/// pour peupler la barre du haut, les panneaux lateraux, etc. Si l'un d'eux leve une exception non
/// geree une fois PlayerCivilization/CurrentWorldState devenus nuls (l'ile est detruite des la
/// demande d'Ascension), le minuteur Avalonia s'arrete et plus rien de l'overlay ne se
/// resynchronise - ce qui ressemble a un overlay entierement absent, sans que Render() lui-meme
/// (teste par-dessus par AscensionPendingRenderRepro) ne soit en cause.
/// </summary>
public class AscensionPendingOverlayRefreshRepro
{
    private static GameControllerService GetGameControllerService(SkiaGameRuntime runtime)
    {
        var screenField = typeof(SkiaGameRuntime).GetField("_gameScreen", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("champ _gameScreen introuvable");
        var screen = (GameScreen)screenField.GetValue(runtime)!;

        var gcsField = typeof(GameScreen).GetField("_gameControllerService", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("champ _gameControllerService introuvable");
        return (GameControllerService)gcsField.GetValue(screen)!;
    }

    [Fact]
    public void AllOverlayViewModels_RefreshWithoutThrowing_WhileAscensionPending()
    {
        var runtime = new SkiaGameRuntime();
        runtime.Initialize(new FakeFileSystemService(), allowDebugMode: true);
        runtime.InvokeTitleAction(SkiaLayer.TitleScreenSnapshot.ActionPrimary);

        using var host = new GameRuntimeHost(runtime);

        var gcs = GetGameControllerService(runtime);
        var ascension = gcs.MainGameController.AscensionController;
        var godState = gcs.CurrentGameState!.GodState;

        godState.GodPoints = 100;
        Assert.True(ascension.PurchasePower(AscensionPowerId.Faith));
        Assert.True(ascension.PurchasePower(AscensionPowerId.HandOfGod));
        Assert.True(ascension.PurchasePower(AscensionPowerId.MemoryOfGod));
        Assert.True(ascension.PurchasePower(AscensionPowerId.WalkOfGod));
        Assert.True(ascension.PurchasePower(AscensionPowerId.ArmOfGod));
        Assert.True(ascension.IsRaceSelectionUnlocked);

        godState.DivineEssence = 10;
        Assert.True(ascension.CanAscend(godState));

        var tabs = new TabBarViewModel(host);
        var resources = new ResourceBarViewModel(host);
        var time = new TimeControlViewModel(host);
        var monument = new MonumentPanelViewModel(host);
        var city = new CityPanelViewModel(host);
        var civ = new CivPanelViewModel(host);
        var modal = new ModalPopupViewModel(host);
        var toast = new ToastViewModel(host);
        var events = new EventLogViewModel(host);
        var stats = new StatsViewModel(host);
        var rituals = new RitualsViewModel(host);
        var automation = new AutomationViewModel(host);
        var settingsMenu = new SettingsMenuViewModel(host);
        var trade = new TradePopupViewModel(host);
        var prestige = new PrestigePopupViewModel(host);
        var settings = new SettingsPopupViewModel(host);
        var title = new TitleScreenViewModel(host);

        var canvasSize = new SKSize(1280, 800);
        using var surface = SKSurface.Create(new SKImageInfo(1280, 800, SKColorType.Rgba8888, SKAlphaType.Premul));
        var lastCanvasSize = new SKSize(-1, -1);

        void RefreshAll()
        {
            // Le vrai jeu fait tourner le rendu Skia (qui alimente TabBarRenderer.Update) sur son
            // propre minuteur, independant de celui-ci : sans un Render() ici, GetTabBarSnapshot()
            // ne refleterait que l'etat jamais calcule (liste d'onglets vide par defaut).
            host.Render(surface.Canvas, canvasSize, ref lastCanvasSize);

            AssertRefreshDoesNotThrow("Tabs", tabs.Refresh);
            AssertRefreshDoesNotThrow("Resources", resources.Refresh);
            AssertRefreshDoesNotThrow("Time", time.Refresh);
            AssertRefreshDoesNotThrow("Monument", monument.Refresh);
            AssertRefreshDoesNotThrow("City", city.Refresh);
            AssertRefreshDoesNotThrow("Civ", civ.Refresh);
            AssertRefreshDoesNotThrow("Modal", modal.Refresh);
            AssertRefreshDoesNotThrow("Toast", toast.Refresh);
            AssertRefreshDoesNotThrow("Events", events.Refresh);
            AssertRefreshDoesNotThrow("Stats", stats.Refresh);
            AssertRefreshDoesNotThrow("Rituals", rituals.Refresh);
            AssertRefreshDoesNotThrow("Automation", automation.Refresh);
            AssertRefreshDoesNotThrow("SettingsMenu", settingsMenu.Refresh);
            AssertRefreshDoesNotThrow("Trade", trade.Refresh);
            AssertRefreshDoesNotThrow("Prestige", prestige.Refresh);
            AssertRefreshDoesNotThrow("Settings", settings.Refresh);
            AssertRefreshDoesNotThrow("Title", title.Refresh);
        }

        // Etat normal (avant Ascension) : sanity check du harnais.
        RefreshAll();

        gcs.RequestAscension();
        Assert.True(ascension.IsAscensionPending);
        Assert.Null(godState.PrestigeState);

        // Le meme enchainement, plusieurs fois (comme les ticks reels a 100ms), une fois l'ile
        // detruite : c'est ici que doit apparaitre l'exception reportee par l'utilisateur.
        for (int i = 0; i < 3; i++)
            RefreshAll();

        Assert.True(tabs.Tabs.Count >= 1, "La barre d'onglets ne devrait jamais rester vide.");
    }

    private static void AssertRefreshDoesNotThrow(string name, Action refresh)
    {
        var ex = Record.Exception(refresh);
        Assert.True(ex == null, $"{name}.Refresh() a leve : {ex}");
    }
}
