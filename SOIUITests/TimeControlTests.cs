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

public class TimeControlViewModelTests
{
    [Theory]
    [InlineData(0, "0s")]
    [InlineData(45, "45s")]
    [InlineData(90, "1,5m")]
    [InlineData(3600, "1h")]
    [InlineData(5400, "1,5h")]
    public void Le_formatage_de_la_banque_reprend_celui_du_renderer_d_origine(double seconds, string expected)
    {
        // Culture-dependant (virgule decimale en fr-FR) : on force la culture du test pour
        // que l'assertion ne depende pas de la machine.
        var previous = System.Globalization.CultureInfo.CurrentCulture;
        System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("fr-FR");
        try
        {
            Assert.Equal(expected, TimeControlViewModel.FormatBankTime(seconds));
        }
        finally { System.Globalization.CultureInfo.CurrentCulture = previous; }
    }

    [Fact]
    public void Sans_partie_en_cours_le_bloc_temps_est_indisponible()
    {
        using var host = new GameRuntimeHost(new SkiaLayer.SkiaGameRuntime());
        var vm = new TimeControlViewModel(host);

        vm.Refresh();

        Assert.False(vm.IsAvailable);
    }

    [Fact]
    public void Une_valeur_inchangee_ne_declenche_pas_de_notification()
    {
        using var host = new GameRuntimeHost(new SkiaLayer.SkiaGameRuntime());
        var vm = new TimeControlViewModel(host);
        vm.Refresh();

        int notifications = 0;
        vm.PropertyChanged += (_, _) => notifications++;
        vm.Refresh();
        vm.Refresh();

        // L'etat est repousse par sondage : sans cette garde, chaque tick de synchronisation
        // ferait relayouter Avalonia pour des valeurs identiques.
        Assert.Equal(0, notifications);
    }
}

public class TimeControlViewTests
{
    [AvaloniaFact]
    public void Un_clic_sur_le_bouton_pause_n_atteint_pas_la_carte()
    {
        using var host = new GameRuntimeHost(new SkiaLayer.SkiaGameRuntime());
        var vm = new TimeControlViewModel(host);
        var map = new ProbeMapControl();
        var view = new TimeControlView(vm, host.Localize, (k, a) => host.LocalizeFormat(k, a))
        {
            IsVisible = true,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
        };

        var window = new Window
        {
            Width = 800,
            Height = 600,
            Content = new Panel { Children = { map, view } },
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var toggle = view.GetVisualDescendants().OfType<Button>().First();
        var center = toggle.TranslatePoint(
            new Point(toggle.Bounds.Width / 2, toggle.Bounds.Height / 2), window);
        Assert.NotNull(center);

        window.MouseDown(center!.Value, MouseButton.Left);
        window.MouseUp(center!.Value, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(0, map.PointerPressedCount);
    }

    [AvaloniaFact]
    public void Le_bouton_de_temps_montre_l_etat_courant_et_non_l_action()
    {
        using var host = new GameRuntimeHost(new SkiaLayer.SkiaGameRuntime());
        var toggle = ShowTimeControl(host);

        // Sans partie en cours l'instantane rend IsPaused = false : le jeu est cense tourner,
        // donc le triangle. C'est l'etat COURANT — l'ancien libelle affichait « || », l'action.
        var play = toggle.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Path>().Single();
        var bars = toggle.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Rectangle>().ToList();

        Assert.True(play.IsVisible);
        Assert.Equal(2, bars.Count);
        Assert.All(bars, bar => Assert.False(bar.GetVisualParent<Control>()!.IsVisible));
    }

    [AvaloniaFact]
    public void En_pause_le_fond_du_bouton_de_temps_pulse()
    {
        using var host = new GameRuntimeHost(new SkiaLayer.SkiaGameRuntime());
        var toggle = ShowTimeControl(host);

        var atRest = toggle.Background;

        // L'etat de pause n'est pas atteignable sans partie : on pose la classe que la vue
        // poserait, ce qui verifie le seul maillon fragile — l'animation attachee au style.
        toggle.Classes.Add("paused");
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();

        Assert.NotEqual(atRest, toggle.Background);

        toggle.Classes.Remove("paused");
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();

        // Le jeu repart : le bouton doit retrouver sa couleur locale, pas rester dore.
        Assert.Equal(atRest, toggle.Background);
    }

    /// <summary>Affiche le bloc de temps dans une fenetre et rend son bouton pause/lecture.</summary>
    private static Button ShowTimeControl(GameRuntimeHost host)
    {
        var vm = new TimeControlViewModel(host);
        var view = new TimeControlView(vm, host.Localize, (k, a) => host.LocalizeFormat(k, a))
        {
            IsVisible = true,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
        };

        var window = new Window { Width = 800, Height = 600, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        // Ordre de construction : banque (Border), bascule, vitesse.
        return view.GetVisualDescendants().OfType<Button>().First();
    }
}
