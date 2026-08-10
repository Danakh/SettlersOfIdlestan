using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
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

/// <summary>
/// Regression : la barre de ressources doit se replier sur sa propre ligne quand les trois
/// blocs de la barre du haut ne tiennent pas ensemble, comme le faisait UILayoutService avant
/// la migration. Un DockPanel, lui, retrecissait les ressources jusqu'a une fenetre de deux
/// pastilles — techniquement defilante, illisible en pratique.
/// </summary>
public class TopBarLayoutTests
{
    private static (TopBarLayout Layout, Control Resources) BuildLayout()
    {
        var tabs = new Border { Width = 300, Height = 34 };
        var resources = new Border { Width = 400, Height = 32 };
        var right = new Border { Width = 250, Height = 32 };
        return (new TopBarLayout(tabs, resources, right), resources);
    }

    private static void Show(Control content, double width)
    {
        var window = new Window { Width = width, Height = 300, Content = content };
        window.Show();
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void Les_ressources_passent_sur_leur_propre_ligne_quand_la_largeur_manque()
    {
        var (layout, resources) = BuildLayout();

        // 300 + 400 + 250 ne tiennent pas dans 500.
        Show(layout, 500);

        Assert.True(layout.ResourcesOnOwnRow);
        Assert.Equal(TopBarView.BarHeight + TopBarLayout.SecondRowHeight, layout.TotalHeight);

        // Pose sous la ligne principale, sans l'assiette exacte : un bloc plus court que sa
        // ligne y est centre.
        Assert.True(resources.Bounds.Y >= TopBarView.BarHeight, $"Y = {resources.Bounds.Y}");
    }

    [AvaloniaFact]
    public void Les_ressources_restent_en_ligne_quand_la_place_suffit()
    {
        var (layout, resources) = BuildLayout();

        Show(layout, 1400);

        Assert.False(layout.ResourcesOnOwnRow);
        Assert.Equal(TopBarView.BarHeight, layout.TotalHeight);

        // Sur la ligne principale, entre les onglets et le bloc de droite.
        Assert.True(resources.Bounds.Bottom <= TopBarView.BarHeight, $"Bas = {resources.Bounds.Bottom}");
        Assert.True(resources.Bounds.X >= 300, $"X = {resources.Bounds.X}");
    }

    /// <summary>
    /// La regle de repli compare la largeur naturelle du contenu des ressources a la place
    /// restante. Si le ScrollViewer annoncait la largeur qu'il accepte de prendre — il tient
    /// toujours, par construction — plus rien ne se replierait jamais.
    /// </summary>
    [AvaloniaFact]
    public void La_barre_de_ressources_annonce_la_largeur_de_tout_son_contenu()
    {
        var view = BuildResourceBar(pillCount: 10);

        view.Measure(new Avalonia.Size(double.PositiveInfinity, TopBarView.BarHeight));

        Assert.True(
            view.DesiredSize.Width >= 10 * ResourcePill.PillWidth,
            $"Largeur mesuree {view.DesiredSize.Width}, attendue au moins {10 * ResourcePill.PillWidth}");
    }

    /// <summary>
    /// Regression : l'ascenseur du theme Fluent se pose par-dessus le contenu et masque le bas
    /// des pastilles d'une barre de 50 px. La barre garde le ScrollViewer pour sa logique de
    /// defilement, mais dessine son propre indicateur fin.
    /// </summary>
    [AvaloniaFact]
    public void L_ascenseur_du_theme_ne_s_affiche_pas_sur_un_contenu_qui_deborde()
    {
        var view = BuildResourceBar(pillCount: 10, windowWidth: 200);

        Assert.DoesNotContain(view.GetVisualDescendants().OfType<ScrollBar>(), b => b.IsVisible);
        Assert.Equal(3, Assert.Single(view.GetVisualDescendants().OfType<ScrollIndicator>()).Height);
    }

    /// <summary>
    /// Le glisser a la souris de l'ancien rendu : la barre suit le pointeur, sans seuil ni
    /// inertie. Le tactile, lui, est laisse au geste natif du ScrollViewer.
    /// </summary>
    [AvaloniaFact]
    public void La_barre_se_deroule_au_glisser()
    {
        var (view, window) = BuildResourceBarInWindow(pillCount: 10, windowWidth: 200);
        var scroller = view.GetVisualDescendants().OfType<ScrollViewer>().First();

        window.MouseDown(new Point(150, 20), MouseButton.Left);
        window.MouseMove(new Point(90, 20));
        window.MouseUp(new Point(90, 20), MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        // Tire vers la gauche de 60 px : le contenu avance d'autant sous la fenetre.
        Assert.Equal(60, scroller.Offset.X, precision: 0);
    }

    /// <summary>Sans debordement il n'y a rien a derouler : le glisser ne doit rien decaler.</summary>
    [AvaloniaFact]
    public void Le_glisser_ne_fait_rien_quand_tout_le_contenu_tient()
    {
        var (view, window) = BuildResourceBarInWindow(pillCount: 2, windowWidth: 800);
        var scroller = view.GetVisualDescendants().OfType<ScrollViewer>().First();

        window.MouseDown(new Point(400, 20), MouseButton.Left);
        window.MouseMove(new Point(100, 20));
        window.MouseUp(new Point(100, 20), MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(0, scroller.Offset.X);
    }

    private static ResourceBarView BuildResourceBar(int pillCount, double windowWidth = 800) =>
        BuildResourceBarInWindow(pillCount, windowWidth).View;

    private static (ResourceBarView View, Window Window) BuildResourceBarInWindow(
        int pillCount, double windowWidth = 800)
    {
        var host = new GameRuntimeHost(new SkiaLayer.SkiaGameRuntime());
        var viewModel = new ResourceBarViewModel(host);
        for (int i = 0; i < pillCount; i++)
            viewModel.Resources.Add(new ResourceItemViewModel(
                new SkiaLayer.ResourceSnapshot("Wood", "120", "500", false, false)));

        var view = new ResourceBarView(viewModel, new SvgIconCache());

        // Sans partie en cours l'instantane est indisponible, donc la vue reste masquee — et
        // un controle masque mesure zero. On force l'affichage : c'est la mesure qu'on teste.
        var window = new Window { Width = windowWidth, Height = 300, Content = view };
        window.Show();
        view.IsVisible = true;
        Dispatcher.UIThread.RunJobs();

        return (view, window);
    }
}
