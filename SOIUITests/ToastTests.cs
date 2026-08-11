using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SettlersOfIdlestanSkia.Renderers.Overlay;
using SettlersOfIdlestanSkia.Services;
using SettlersOfIdlestanUI;
using SettlersOfIdlestanUI.Controls;
using SettlersOfIdlestanUI.ViewModels;
using Xunit;
using SkiaLayer = SettlersOfIdlestanSkia.Services;

namespace SOIUITests;

public class NotificationToastRendererTests
{
    private static NotificationToastRenderer Renderer() => new(new UILayoutService());

    [Fact]
    public void Sans_toast_l_instantane_est_vide()
    {
        using var renderer = Renderer();

        Assert.Empty(renderer.GetSnapshot().Toasts);
    }

    /// <summary>
    /// Le decompte vivait autrefois dans Render. Ce renderer ne dessine plus rien : sans Advance
    /// appele par la boucle de jeu, les toasts resteraient affiches pour toujours. Ce test
    /// echoue si le decompte repart vers un chemin de rendu.
    /// </summary>
    [Fact]
    public void Les_toasts_expirent_sans_qu_aucun_rendu_n_ait_lieu()
    {
        using var renderer = Renderer();
        renderer.ShowNotification("Titre", "Message");

        Assert.Single(renderer.GetSnapshot().Toasts);

        renderer.Advance(6f);   // duree de vie : 5 s

        Assert.Empty(renderer.GetSnapshot().Toasts);
    }

    [Fact]
    public void Le_plus_recent_est_en_tete_de_l_instantane()
    {
        using var renderer = Renderer();
        renderer.ShowNotification("Premier", "a");
        renderer.ShowNotification("Second", "b");

        var toasts = renderer.GetSnapshot().Toasts;

        // La pile Avalonia est ancree en bas et se remplit de haut en bas : le plus recent doit
        // donc venir en premier pour finir en haut, comme dans le rendu Skia.
        Assert.Equal("Second", toasts[0].Title);
        Assert.Equal("Premier", toasts[1].Title);
    }

    [Fact]
    public void Au_dela_de_trois_toasts_le_plus_ancien_disparait()
    {
        using var renderer = Renderer();
        for (int i = 1; i <= 4; i++) renderer.ShowNotification($"T{i}", "m");

        var toasts = renderer.GetSnapshot().Toasts;

        Assert.Equal(3, toasts.Count);
        Assert.DoesNotContain(toasts, t => t.Title == "T1");
    }

    [Fact]
    public void Fermer_un_toast_le_retire_par_son_identifiant()
    {
        using var renderer = Renderer();
        renderer.ShowNotification("Premier", "a");
        renderer.ShowNotification("Second", "b");

        var target = renderer.GetSnapshot().Toasts[0];
        renderer.Dismiss(target.Id);

        var remaining = renderer.GetSnapshot().Toasts;
        Assert.Equal("Premier", Assert.Single(remaining).Title);
    }

    [Fact]
    public void Le_fondu_d_entree_puis_de_sortie_borne_l_opacite()
    {
        using var renderer = Renderer();
        renderer.ShowNotification("Titre", "Message");

        // A peine apparu : le fondu d'entree n'est pas termine.
        Assert.True(renderer.GetSnapshot().Toasts[0].Opacity < 1d);

        renderer.Advance(1f);
        Assert.Equal(1d, renderer.GetSnapshot().Toasts[0].Opacity, 3);

        // 4,8 s ecoulees sur 5 : le fondu de sortie est entame.
        renderer.Advance(3.8f);
        double fading = renderer.GetSnapshot().Toasts[0].Opacity;
        Assert.True(fading is > 0d and < 1d, $"Opacite attendue en fondu, obtenue {fading}.");
    }
}

public class ToastViewModelTests
{
    [Fact]
    public void Sans_partie_en_cours_la_pile_est_vide()
    {
        using var host = new GameRuntimeHost(new SkiaLayer.SkiaGameRuntime());
        var vm = new ToastViewModel(host);

        vm.Refresh();

        Assert.Empty(vm.Toasts);
    }
}

public class ToastStackViewTests
{
    /// Les toasts sont ancres en bas a droite : le reste de l'ecran doit rester cliquable.
    [AvaloniaFact]
    public void La_pile_de_toasts_ne_recouvre_pas_la_carte()
    {
        var (window, map, _) = BuildProbeWindow(toastCount: 0);

        window.MouseDown(new Point(300, 200), MouseButton.Left);
        window.MouseUp(new Point(300, 200), MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(1, map.PointerPressedCount);
    }

    /// <summary>
    /// Un controle dont le theme ne s'applique pas reste sans template : aucun element n'est
    /// instancie et la vue disparait sans lever d'erreur. Ce test verifie que la pile materialise
    /// bien ses cartes, avec une geometrie non nulle.
    /// </summary>
    [AvaloniaFact]
    public void Chaque_toast_est_materialise_avec_une_geometrie_visible()
    {
        var (_, _, stack) = BuildProbeWindow(toastCount: 2);

        var cards = stack.GetVisualDescendants().OfType<Border>()
            .Where(b => b.Width > 0 && b.Height > 0)
            .ToList();

        Assert.Equal(2, cards.Count);
        Assert.All(cards, c => Assert.True(c.Bounds.Width > 0 && c.Bounds.Height > 0,
            "Le toast occupe zero pixel : son template ne s'est pas applique."));
    }

    /// Un clic sur un toast le ferme, et n'atteint pas la carte derriere.
    [AvaloniaFact]
    public void Un_clic_sur_un_toast_le_ferme_sans_atteindre_la_carte()
    {
        var (window, map, stack) = BuildProbeWindow(toastCount: 1);

        var card = stack.GetVisualDescendants().OfType<Border>().First(b => b.Width > 0);
        var center = card.TranslatePoint(new Point(card.Bounds.Width / 2, card.Bounds.Height / 2), window);
        Assert.NotNull(center);

        window.MouseDown(center!.Value, MouseButton.Left);
        window.MouseUp(center!.Value, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(0, map.PointerPressedCount);
    }

    private static (Window Window, ProbeMapControl Map, ToastStackView Stack) BuildProbeWindow(int toastCount)
    {
        var host = new GameRuntimeHost(new SkiaLayer.SkiaGameRuntime());
        var map = new ProbeMapControl();
        var vm = new ToastViewModel(host);

        // Pas de partie en cours : la pile est alimentee a la main pour eprouver la vue.
        for (int i = 0; i < toastCount; i++)
            vm.Toasts.Add(new ToastItemViewModel(new SkiaLayer.ToastSnapshot(
                i, $"Titre {i}", "Message", NotificationIcon.Info, 1d)));

        var stack = new ToastStackView(vm);
        var window = new Window
        {
            Width = 800,
            Height = 600,
            Content = new Panel { Children = { map, stack } },
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return (window, map, stack);
    }
}
