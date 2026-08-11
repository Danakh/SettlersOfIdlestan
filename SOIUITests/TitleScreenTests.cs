using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SettlersOfIdlestanUI;
using SettlersOfIdlestanUI.Controls;
using SettlersOfIdlestanUI.ViewModels;
using Xunit;
using SkiaLayer = SettlersOfIdlestanSkia.Services;

namespace SOIUITests;

public class TitleScreenViewModelTests
{
    [Fact]
    public void En_partie_l_ecran_titre_est_masque()
    {
        // Le runtime n'est pas sur l'ecran-titre : son instantane est le seul gate a l'inverse
        // des autres, il doit donc rester masque ici.
        using var host = new GameRuntimeHost(new SkiaLayer.SkiaGameRuntime());
        var vm = new TitleScreenViewModel(host);

        vm.Refresh();

        Assert.False(vm.IsVisible);
    }

    /// Les trois onglets s'excluent : exactement un contenu est affiche.
    [Fact]
    public void Un_seul_onglet_est_affiche_a_la_fois()
    {
        using var host = new GameRuntimeHost(new SkiaLayer.SkiaGameRuntime());
        var vm = new TitleScreenViewModel(host);
        vm.Refresh();

        int shown = (vm.ShowingChangelog ? 1 : 0) + (vm.ShowingCredits ? 1 : 0) + (vm.ShowingSettings ? 1 : 0);
        Assert.Equal(1, shown);
    }
}

public class TitleScreenViewTests
{
    /// <summary>
    /// L'ecran-titre couvre tout : son fond opaque doit empecher les clics d'atteindre le
    /// canevas Skia, encore present dessous.
    /// </summary>
    [AvaloniaFact]
    public void Un_clic_n_atteint_pas_le_canevas_sous_l_ecran_titre()
    {
        var (window, map, _) = BuildProbeWindow(visible: true);

        foreach (var point in new[] { new Point(100, 100), new Point(400, 300), new Point(700, 560) })
        {
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
        }
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(0, map.PointerPressedCount);
    }

    /// Controle negatif : une fois en partie, le canevas redevient cliquable.
    [AvaloniaFact]
    public void Ecran_titre_masque_les_clics_atteignent_le_canevas()
    {
        var (window, map, _) = BuildProbeWindow(visible: false);

        window.MouseDown(new Point(400, 300), MouseButton.Left);
        window.MouseUp(new Point(400, 300), MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(1, map.PointerPressedCount);
    }

    /// <summary>
    /// Les boutons de depart sont conditionnels : sans sauvegarde il n'y a ni remise a zero ni
    /// chargement cloud, et le bouton principal change de libelle.
    /// </summary>
    [AvaloniaFact]
    public void Les_boutons_refletent_la_presence_d_une_sauvegarde()
    {
        var (_, _, withSave) = BuildProbeWindow(visible: true, actions:
        [
            new(SkiaLayer.TitleScreenSnapshot.ActionPrimary, "Continuer", SkiaLayer.TitleActionTone.Primary),
            new(SkiaLayer.TitleScreenSnapshot.ActionHardReset, "Nouvelle Partie", SkiaLayer.TitleActionTone.Danger),
        ]);
        Assert.Equal(2, ActionLabels(withSave).Count);
        Assert.Contains("Continuer", ActionLabels(withSave));

        var (_, _, withoutSave) = BuildProbeWindow(visible: true, actions:
        [
            new(SkiaLayer.TitleScreenSnapshot.ActionPrimary, "Nouvelle Partie", SkiaLayer.TitleActionTone.Primary),
        ]);
        Assert.Single(ActionLabels(withoutSave));
    }

    /// <summary>
    /// L'onglet des reglages reutilise le panneau du popup en jeu : c'est ce partage qui rend la
    /// version Skia commune supprimable.
    /// </summary>
    /// <remarks>
    /// La section est masquee tant que son onglet n'est pas actif, et l'onglet actif vient de
    /// l'instantane — qu'on ne peut pas forcer sans ecran-titre actif dans le runtime. On la rend
    /// donc visible a la main, ce qui reste fidele a l'objet du test.
    /// </remarks>
    [AvaloniaFact]
    public void L_onglet_des_reglages_reutilise_le_panneau_partage()
    {
        var (_, _, view) = BuildProbeWindow(visible: true);

        var section = view.GetVisualDescendants().OfType<ScrollViewer>()
            .First(s => s.Content is SettingsPanelView);
        section.IsVisible = true;
        Dispatcher.UIThread.RunJobs();

        Assert.Single(view.GetVisualDescendants().OfType<SettingsPanelView>());
    }

    private static List<string?> ActionLabels(TitleScreenView view) =>
        view.GetVisualDescendants().OfType<Button>()
            .Where(b => b.DataContext is TitleActionViewModel)
            .Select(b => b.Content as string)
            .ToList();

    private static (Window Window, ProbeMapControl Map, TitleScreenView View) BuildProbeWindow(
        bool visible, SkiaLayer.TitleActionSnapshot[]? actions = null)
    {
        var host = new GameRuntimeHost(new SkiaLayer.SkiaGameRuntime());
        var map = new ProbeMapControl();
        var vm = new TitleScreenViewModel(host);

        // Pas d'ecran-titre actif dans le runtime : les boutons sont alimentes a la main pour
        // eprouver la vue.
        foreach (var action in actions ?? []) vm.Actions.Add(new TitleActionViewModel(action));

        var view = new TitleScreenView(vm) { IsVisible = visible };

        var window = new Window
        {
            Width = 900,
            Height = 700,
            Content = new Panel { Children = { map, view } },
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return (window, map, view);
    }
}
