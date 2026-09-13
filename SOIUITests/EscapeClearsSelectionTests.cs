using System.Reflection;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using SettlersOfIdlestanSkia.Screens;
using SettlersOfIdlestanSkia.Services;
using SettlersOfIdlestanUI;
using SettlersOfIdlestanUI.Views;
using Xunit;

namespace SOIUITests;

/// <summary>
/// Echap n'a longtemps servi qu'a annuler un mode de ciblage : hors ciblage, la touche ne faisait
/// rien et la ville selectionnee ne se refermait qu'au clic. Ces tests verrouillent la regle
/// retenue — Echap retire la selection courante, mais seulement quand il n'a aucun ecran a fermer
/// (voir GameScreen.HandleKeyPressed).
///
/// Le test passe par une vraie GameView et un vrai evenement clavier : la touche part de la vue
/// racine et non du controle carte (voir GameRuntimeControl.MapKey), un chemin qu'un appel direct
/// a GameRuntimeHost.KeyPressed ne couvrirait pas.
/// </summary>
public class EscapeClearsSelectionTests
{
    private static GameControllerService GetGameControllerService(SkiaGameRuntime runtime)
    {
        var screenField = typeof(SkiaGameRuntime).GetField("_gameScreen", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var screen = (GameScreen)screenField.GetValue(runtime)!;
        var gcsField = typeof(GameScreen).GetField("_gameControllerService", BindingFlags.NonPublic | BindingFlags.Instance)!;
        return (GameControllerService)gcsField.GetValue(screen)!;
    }

    /// Le clavier suit le focus : sans cela l'evenement viserait la fenetre et ne traverserait
    /// jamais GameView. En jeu c'est le premier clic sur la carte qui lui donne le focus.
    private static void FocusMap(GameView view)
    {
        var mapField = typeof(GameView).GetField("_mapControl", BindingFlags.NonPublic | BindingFlags.Instance)!;
        ((Control)mapField.GetValue(view)!).Focus();
        Dispatcher.UIThread.RunJobs();
    }

    private static (SkiaGameRuntime Runtime, GameRuntimeHost Host, GameView View, Window Window) StartGame()
    {
        var runtime = new SkiaGameRuntime();
        runtime.Initialize(new FakeFileSystemService(), allowDebugMode: true);
        runtime.InvokeTitleAction(TitleScreenSnapshot.ActionPrimary);

        var host = new GameRuntimeHost(runtime);
        var view = new GameView(host);
        var window = new Window { Width = 1280, Height = 800, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return (runtime, host, view, window);
    }

    [AvaloniaFact]
    public void Echap_retire_la_ville_selectionnee_quand_aucun_ecran_n_est_ouvert()
    {
        var (runtime, host, view, window) = StartGame();
        using (host)
        using (view)
        {
            var gcs = GetGameControllerService(runtime);
            gcs.CityBuildingService.SetSelectedCity(gcs.PlayerCivilization!.Cities[0].Position);
            Assert.True(host.GetCityPanelSnapshot().IsVisible, "Le panneau de ville devrait etre ouvert avant l'appui.");

            FocusMap(view);
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();

            Assert.Null(gcs.CityBuildingService.SelectedCity);
            Assert.False(host.GetCityPanelSnapshot().IsVisible);
        }
    }

    /// Controle negatif : le menu de l'engrenage est ouvert, Echap a donc un ecran a fermer et ne
    /// doit pas vider la selection au passage.
    [AvaloniaFact]
    public void Echap_laisse_la_selection_intacte_tant_qu_un_ecran_est_ouvert()
    {
        var (runtime, host, view, window) = StartGame();
        using (host)
        using (view)
        {
            var gcs = GetGameControllerService(runtime);
            gcs.CityBuildingService.SetSelectedCity(gcs.PlayerCivilization!.Cities[0].Position);
            host.ToggleSettingsMenu();
            Assert.True(host.GetSettingsMenuSnapshot().IsOpen, "Le menu devrait etre ouvert avant l'appui.");

            FocusMap(view);
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();

            Assert.NotNull(gcs.CityBuildingService.SelectedCity);
            Assert.True(host.GetCityPanelSnapshot().IsVisible);
        }
    }
}
