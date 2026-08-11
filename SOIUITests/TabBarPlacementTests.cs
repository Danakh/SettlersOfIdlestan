using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using SettlersOfIdlestanUI;
using SettlersOfIdlestanUI.Controls;
using SettlersOfIdlestanUI.ViewModels;
using Xunit;
using SkiaLayer = SettlersOfIdlestanSkia.Services;

namespace SOIUITests;

/// <summary>
/// Regression : la disposition compacte — onglets ancres en bas de l'ecran, pleine largeur —
/// existait dans le rendu Skia (UILayoutService.TabsAtBottom) mais n'a pas ete portee sur
/// l'overlay Avalonia. Sur navigateur mobile, ou l'ecran est etroit et en portrait, la barre
/// restait donc coincee en haut de l'ecran.
/// </summary>
public class TabBarPlacementTests
{
    private static SkiaLayer.TabBarSnapshot Snapshot(bool visible, bool atBottom) =>
        new(visible, atBottom, [new SkiaLayer.TabSnapshot(0, "Ile", true, false)]);

    /// <summary>
    /// Les deux exemplaires de la barre partagent un ViewModel : c'est la visibilite qui designe
    /// celui qui s'affiche, et jamais les deux a la fois.
    /// </summary>
    [Fact]
    public void La_disposition_designe_une_seule_des_deux_barres()
    {
        using var host = new GameRuntimeHost(new SkiaLayer.SkiaGameRuntime());
        var vm = new TabBarViewModel(host);

        vm.Apply(Snapshot(visible: true, atBottom: false));
        Assert.True(vm.ShowAtTop);
        Assert.False(vm.ShowAtBottom);

        vm.Apply(Snapshot(visible: true, atBottom: true));
        Assert.False(vm.ShowAtTop);
        Assert.True(vm.ShowAtBottom);

        // Ecran titre : la barre n'existe nulle part.
        vm.Apply(Snapshot(visible: false, atBottom: true));
        Assert.False(vm.ShowAtTop);
        Assert.False(vm.ShowAtBottom);
    }

    /// <summary>
    /// La bascule se produit en cours de partie — rotation de l'ecran, redimensionnement de la
    /// fenetre, ou reglage « menus en bas » : les deux vues doivent la suivre par notification.
    /// </summary>
    [AvaloniaFact]
    public void La_barre_du_bas_apparait_quand_la_disposition_bascule()
    {
        using var host = new GameRuntimeHost(new SkiaLayer.SkiaGameRuntime());
        var vm = new TabBarViewModel(host);

        var top = new TabBarView(vm, TabBarPlacement.Top);
        var bottom = new TabBarView(vm, TabBarPlacement.Bottom);

        var window = new Window
        {
            Width = 400,
            Height = 600,
            Content = new StackPanel { Children = { top, bottom } },
        };
        window.Show();
        try
        {
            vm.Apply(Snapshot(visible: true, atBottom: false));
            Dispatcher.UIThread.RunJobs();
            Assert.True(top.IsVisible);
            Assert.False(bottom.IsVisible);

            vm.Apply(Snapshot(visible: true, atBottom: true));
            Dispatcher.UIThread.RunJobs();
            Assert.False(top.IsVisible);
            Assert.True(bottom.IsVisible);
        }
        finally
        {
            // Les tests headless partagent un dispatcher : une fenetre laissee ouverte perturbe
            // le test de collision d'autres tests.
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Les_onglets_du_bas_se_partagent_la_largeur_de_l_ecran()
    {
        var panel = new EvenColumnsPanel();
        for (int i = 0; i < 4; i++) panel.Children.Add(new Border());

        // Pas de fenetre : la disposition d'un Panel se mesure et s'arrange hors de tout arbre
        // visuel, et une fenetre headless laissee ouverte deteint sur les autres tests.
        panel.Measure(new Size(400, TabBarView.BottomBarHeight));
        panel.Arrange(new Rect(0, 0, 400, TabBarView.BottomBarHeight));

        for (int i = 0; i < 4; i++)
        {
            var bounds = panel.Children[i].Bounds;
            Assert.Equal(100, bounds.Width, 3);
            Assert.Equal(i * 100, bounds.X, 3);
            // La colonne occupe toute la hauteur de la barre : c'est la surface tactile.
            Assert.Equal(TabBarView.BottomBarHeight, bounds.Height, 3);
        }
    }

    /// <summary>
    /// Onze onglets sur 400 px font 36 px chacun : la largeur minimale de 62 px des onglets du
    /// haut ferait deborder la barre hors de l'ecran.
    /// </summary>
    [AvaloniaFact]
    public void Un_onglet_du_bas_n_impose_pas_de_largeur_minimale()
    {
        using var host = new GameRuntimeHost(new SkiaLayer.SkiaGameRuntime());
        var vm = new TabBarViewModel(host);

        Assert.Equal(0, new TabButton(vm, TabBarPlacement.Bottom).MinWidth);
        Assert.Equal(62, new TabButton(vm, TabBarPlacement.Top).MinWidth);
    }

    /// <summary>
    /// Bout en bout, sur une vraie partie : un canevas etroit et en portrait — le navigateur
    /// mobile — doit renvoyer une barre ancree en bas, et un canevas de bureau une barre en
    /// haut. C'est ce qui relie la detection de UILayoutService a la vue Avalonia.
    /// </summary>
    [Fact]
    public void Un_canevas_de_telephone_demande_les_onglets_en_bas()
    {
        var runtime = new SkiaLayer.SkiaGameRuntime();
        using var host = new GameRuntimeHost(runtime);
        runtime.Initialize(new SansFichier());

        // Sans sauvegarde, le bouton principal de l'ecran titre demarre une nouvelle partie.
        host.InvokeTitleAction(SkiaLayer.TitleScreenSnapshot.ActionPrimary);

        var vm = new TabBarViewModel(host);

        RenderOneFrame(runtime, 420, 860);
        vm.Refresh();
        Assert.True(vm.ShowAtBottom, "Un canevas 420x860 doit poser les onglets en bas.");
        Assert.False(vm.ShowAtTop);

        // Rotation vers un canevas large : les onglets quittent le bas. Ils ne remontent pas
        // pour autant dans la barre du haut — une partie neuve n'a que l'onglet Ile, et la
        // disposition large n'affiche pas de barre pour un onglet unique. C'est justement
        // pourquoi la disposition compacte force la barre : elle y est la seule navigation.
        RenderOneFrame(runtime, 1280, 720);
        vm.Refresh();
        Assert.False(vm.ShowAtBottom);
        Assert.False(vm.TabsAtBottom);
    }

    /// <summary>
    /// L'instantane de la barre est produit par le renderer : il faut un frame pour qu'il
    /// prenne en compte la nouvelle taille de canevas, comme dans la boucle de jeu.
    /// </summary>
    private static void RenderOneFrame(SkiaLayer.SkiaGameRuntime runtime, int width, int height)
    {
        runtime.EnsureCanvasInitialized(new SkiaSharp.SKSize(width, height));
        using var surface = SkiaSharp.SKSurface.Create(
            new SkiaSharp.SKImageInfo(width, height, SkiaSharp.SKColorType.Rgba8888, SkiaSharp.SKAlphaType.Premul));
        runtime.Render(surface.Canvas);
    }

    /// <summary>Aucune sauvegarde, aucun reglage : le jeu demarre neuf et n'ecrit rien.</summary>
    private sealed class SansFichier : SkiaLayer.IFileSystemService
    {
        public Task SaveText(string fileName, string content) => Task.CompletedTask;
        public Task<string?> LoadText(string fileName) => Task.FromResult<string?>(null);
        public Task SaveAuto(string content) => Task.CompletedTask;
        public Task<string?> LoadAuto() => Task.FromResult<string?>(null);
        public Task DeleteAuto() => Task.CompletedTask;
        public Task SaveSettings(string content) => Task.CompletedTask;
        public Task<string?> LoadSettings() => Task.FromResult<string?>(null);
        public Task SaveStats(string content) => Task.CompletedTask;
        public Task<string?> LoadStats() => Task.FromResult<string?>(null);
    }
}
