using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using SettlersOfIdlestanSkia.Renderers.Overlay.Popup;
using SettlersOfIdlestanSkia.Services.Localization;
using SettlersOfIdlestanUI;
using SettlersOfIdlestanUI.Controls;
using SettlersOfIdlestanUI.ViewModels;
using Xunit;
using SkiaLayer = SettlersOfIdlestanSkia.Services;

namespace SOIUITests;

/// <summary>
/// Instantane et routage des boutons, testes directement sur les renderers : ils restent la
/// machine a etats, et c'est la que le clic de la vue Avalonia aboutit. Ces tests garantissent
/// que les cles annoncees par l'instantane sont exactement celles qu'InvokeButton accepte —
/// une divergence rendrait un bouton silencieusement inerte.
/// </summary>
public class ModalPopupRendererTests
{
    private static LocalizationService Localization() => new();

    [Fact]
    public void Une_modale_fermee_n_a_pas_d_instantane()
    {
        using var popup = new GameOverPopupRenderer(Localization(), () => { });

        Assert.False(popup.GetSnapshot().IsOpen);
    }

    [Fact]
    public void Chaque_bouton_annonce_par_l_instantane_est_accepte_par_InvokeButton()
    {
        int restarts = 0;
        using var popup = new GameOverPopupRenderer(Localization(), () => restarts++);
        popup.Open();

        var snapshot = popup.GetSnapshot();
        Assert.Equal(SkiaLayer.ModalPopupSnapshot.IdGameOver, snapshot.Id);
        var button = Assert.Single(snapshot.Buttons);

        popup.InvokeButton(button.Key);

        Assert.Equal(1, restarts);
        Assert.False(popup.IsOpen);
    }

    [Fact]
    public void Une_cle_inconnue_ne_declenche_rien()
    {
        int restarts = 0;
        using var popup = new GameOverPopupRenderer(Localization(), () => restarts++);
        popup.Open();

        popup.InvokeButton("cle-qui-n-existe-pas");

        Assert.Equal(0, restarts);
        Assert.True(popup.IsOpen);
    }

    [Fact]
    public void Annuler_une_remise_a_zero_ferme_sans_confirmer()
    {
        int confirms = 0;
        var files = new FakeFileSystemService();
        using var popup = new HardResetPopupRenderer(Localization(), files, () => confirms++);
        popup.Open();

        popup.InvokeButton(popup.GetSnapshot().Buttons[0].Key);

        Assert.False(popup.IsOpen);
        Assert.Equal(0, confirms);
        Assert.False(files.AutoDeleted);
    }

    [Fact]
    public void Confirmer_une_remise_a_zero_supprime_la_sauvegarde()
    {
        int confirms = 0;
        var files = new FakeFileSystemService();
        using var popup = new HardResetPopupRenderer(Localization(), files, () => confirms++);
        popup.Open();

        popup.InvokeButton(popup.GetSnapshot().Buttons[1].Key);

        Assert.False(popup.IsOpen);
        Assert.Equal(1, confirms);
        Assert.True(files.AutoDeleted);
    }

    /// Exporter la sauvegarde corrompue ne ferme pas la modale : le joueur doit encore choisir
    /// entre repartir de zero et quitter. Fermer ici le laisserait sans partie chargee.
    [Fact]
    public void Exporter_une_sauvegarde_corrompue_ne_ferme_pas_la_modale()
    {
        var files = new FakeFileSystemService();
        using var popup = new CorruptSavePopupRenderer(
            Localization(), files, "{}", onStartFresh: () => { }, onQuit: () => { });
        popup.Open();

        popup.InvokeButton(popup.GetSnapshot().Buttons[0].Key);

        Assert.True(popup.IsOpen);
        Assert.Contains("sauvegarde_corrompue.json", files.SavedFiles);
    }

    [Fact]
    public void La_croix_ferme_la_fin_de_demo_sans_relancer()
    {
        int replays = 0;
        using var popup = new DemoEndPopupRenderer(Localization(), () => replays++);
        popup.Open();

        Assert.True(popup.GetSnapshot().HasCloseButton);
        popup.InvokeButton(SkiaLayer.ModalPopupSnapshot.KeyClose);

        Assert.False(popup.IsOpen);
        Assert.Equal(0, replays);
    }
}

public class ModalPopupViewModelTests
{
    [Fact]
    public void Sans_partie_en_cours_aucune_modale_n_est_ouverte()
    {
        using var host = new GameRuntimeHost(new SkiaLayer.SkiaGameRuntime());
        var vm = new ModalPopupViewModel(host);

        vm.Refresh();

        Assert.False(vm.IsOpen);
        Assert.Empty(vm.Buttons);
        Assert.Empty(vm.Lines);
    }
}

public class ModalPopupViewTests
{
    /// Une modale est bloquante : c'est le voile plein ecran qui l'impose, la ou l'ancien code
    /// posait une garde en tete de chaque gestionnaire d'entree de GameScreen.
    [AvaloniaFact]
    public void Un_clic_n_importe_ou_n_atteint_pas_la_carte_quand_une_modale_est_ouverte()
    {
        var (window, map) = BuildProbeWindow(modalVisible: true);

        foreach (var point in new[] { new Point(80, 60), new Point(400, 300), new Point(750, 560) })
        {
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
        }
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(0, map.PointerPressedCount);
    }

    /// Controle negatif : sans lui, une modale restee visible en permanence passerait le test
    /// precedent tout en rendant le jeu injouable.
    [AvaloniaFact]
    public void Sans_modale_ouverte_les_clics_atteignent_la_carte()
    {
        var (window, map) = BuildProbeWindow(modalVisible: false);

        window.MouseDown(new Point(400, 300), MouseButton.Left);
        window.MouseUp(new Point(400, 300), MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(1, map.PointerPressedCount);
    }

    private static (Window Window, ProbeMapControl Map) BuildProbeWindow(bool modalVisible)
    {
        var host = new GameRuntimeHost(new SkiaLayer.SkiaGameRuntime());
        var map = new ProbeMapControl();

        // Pas de partie en cours : le VM masquerait la modale. On force la visibilite pour
        // eprouver le blocage, qui est l'objet de ces tests.
        var modal = new ModalPopupView(new ModalPopupViewModel(host)) { IsVisible = modalVisible };

        var window = new Window
        {
            Width = 800,
            Height = 600,
            Content = new Panel { Children = { map, modal } },
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return (window, map);
    }
}
