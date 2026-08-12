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

/// <summary>
/// Regression : au doigt, une pastille de ressource restait muette. ToolTipService n'ouvre
/// l'infobulle qu'au survol, or un doigt ne survole rien — il touche puis se retire, et le
/// pointeur ressort du controle avant le delai d'apparition. Un tapotement doit donc ouvrir
/// l'infobulle explicitement.
/// </summary>
public class TapTooltipTests
{
    [AvaloniaFact]
    public void Un_tapotement_ouvre_l_infobulle_du_controle()
    {
        var target = ShowInWindow(WithTip("Bois : +12/s"));

        TapTooltip.Show(target);
        Dispatcher.UIThread.RunJobs();

        Assert.True(ToolTip.GetIsOpen(target));
    }

    [AvaloniaFact]
    public void L_infobulle_ouverte_au_tapotement_se_referme()
    {
        var target = ShowInWindow(WithTip("Bois : +12/s"));

        TapTooltip.Show(target);
        Dispatcher.UIThread.RunJobs();
        TapTooltip.Hide();

        Assert.False(ToolTip.GetIsOpen(target));
    }

    /// <summary>
    /// Un tapotement sur une pastille sans infobulle ne doit pas laisser a l'ecran celle de la
    /// pastille precedente : sans survol, rien d'autre ne viendrait la fermer.
    /// </summary>
    [AvaloniaFact]
    public void Un_tapotement_sur_un_controle_sans_infobulle_ferme_la_precedente()
    {
        var window = new Window { Width = 400, Height = 200 };
        var withTip = WithTip("Bois : +12/s");
        var without = new Border { Width = 50, Height = 20 };
        window.Content = new StackPanel { Children = { withTip, without } };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        TapTooltip.Show(withTip);
        Dispatcher.UIThread.RunJobs();

        TapTooltip.Show(without);
        Dispatcher.UIThread.RunJobs();

        Assert.False(ToolTip.GetIsOpen(withTip));
    }

    /// <summary>
    /// La barre retrouve la pastille tapotee par test de collision, et non via la source de
    /// l'evenement : quand le glisser a la souris a saisi le pointeur, le relachement est route
    /// vers la barre elle-meme. Ce test verifie que les pastilles restent atteignables ainsi —
    /// l'indicateur de defilement, pose par-dessus, ne doit pas les masquer.
    /// </summary>
    [AvaloniaFact]
    public void Une_pastille_est_atteignable_par_test_de_collision()
    {
        var view = BuildResourceBar(pillCount: 3);

        var pill = view.GetVisualDescendants().OfType<ResourcePill>().First();
        var center = pill.TranslatePoint(new Point(pill.Bounds.Width / 2, pill.Bounds.Height / 2), view);
        Assert.NotNull(center);

        // Le test de collision interroge la scene du compositeur, pas l'arbre visuel : tant
        // qu'aucune image n'a ete rendue, il ne trouve rien et renvoie null. RunJobs ne suffit
        // pas — la minuterie de rendu bat sur l'horloge reelle, d'ou un resultat aleatoire.
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();

        var hit = view.InputHitTest(center.Value) as Visual;

        Assert.Same(pill, hit?.FindAncestorOfType<ResourcePill>(includeSelf: true));
    }

    private static Border WithTip(string tip)
    {
        var target = new Border { Width = 50, Height = 20 };
        ToolTip.SetTip(target, tip);
        return target;
    }

    private static T ShowInWindow<T>(T content) where T : Control
    {
        var window = new Window { Width = 400, Height = 200, Content = content };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return content;
    }

    private static ResourceBarView BuildResourceBar(int pillCount)
    {
        var host = new GameRuntimeHost(new SkiaLayer.SkiaGameRuntime());
        var viewModel = new ResourceBarViewModel(host);
        for (int i = 0; i < pillCount; i++)
            viewModel.Resources.Add(new ResourceItemViewModel(
                new SkiaLayer.ResourceSnapshot("Wood", "120", "500", false, false)));

        var view = new ResourceBarView(viewModel, new SvgIconCache());
        var window = new Window { Width = 800, Height = 300, Content = view };
        window.Show();
        view.IsVisible = true;
        Dispatcher.UIThread.RunJobs();

        return view;
    }
}
