using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestanUI;
using SettlersOfIdlestanUI.Controls;
using SettlersOfIdlestanUI.ViewModels;
using Xunit;
using SkiaLayer = SettlersOfIdlestanSkia.Services;

namespace SOIUITests;

public class TimeJumpServiceTests
{
    private static GameClock ClockWithBank(long bankTicks) => new() { OfflineBankTicks = bankTicks };

    [Fact]
    public void Un_saut_avance_l_horloge_et_vide_la_banque_d_autant()
    {
        var clock = ClockWithBank(50_000);
        var service = new SkiaLayer.TimeJumpService();

        Assert.True(service.Request(clock, 30_000, "reason"));
        service.Advance(clock);

        Assert.Equal(30_000, clock.CurrentTick);
        Assert.Equal(20_000, clock.OfflineBankTicks);
        Assert.False(service.IsActive);
    }

    [Fact]
    public void Un_saut_plus_grand_que_la_banque_est_refuse()
    {
        var clock = ClockWithBank(5_000);
        var service = new SkiaLayer.TimeJumpService();

        Assert.False(service.Request(clock, 30_000, "reason"));
        Assert.False(service.IsActive);
        Assert.Equal(0, clock.CurrentTick);
    }

    [Fact]
    public void Un_second_saut_ne_se_superpose_pas_a_celui_en_cours()
    {
        var clock = ClockWithBank(100_000);
        var service = new SkiaLayer.TimeJumpService();

        // Chaque tranche depasse a elle seule le budget de temps du tick : l'appel rend la main
        // avec le saut encore actif, ce qui est exactement l'etat pendant lequel un second clic
        // peut arriver.
        clock.Advanced += (_, _) => Spin(40);

        Assert.True(service.Request(clock, 50_000, "reason"));
        service.Advance(clock);

        Assert.True(service.IsActive);
        Assert.False(service.Request(clock, 10_000, "autre"));
        Assert.Equal("reason", service.ReasonKey);
    }

    [Fact]
    public void Une_tranche_par_tick_fait_progresser_la_barre_sans_tout_simuler()
    {
        var clock = ClockWithBank(100_000);
        var service = new SkiaLayer.TimeJumpService();
        clock.Advanced += (_, _) => Spin(40);

        service.Request(clock, 30_000, "reason");
        service.Advance(clock);

        // Une seule tranche de 10 000 ticks a ete simulee : le reste attend les ticks suivants.
        Assert.Equal(10_000, clock.CurrentTick);
        Assert.Equal(1d / 3d, service.Progress, 3);

        service.Advance(clock);
        service.Advance(clock);

        Assert.Equal(30_000, clock.CurrentTick);
        Assert.False(service.IsActive);
    }

    [Fact]
    public void Une_annulation_laisse_en_banque_le_temps_non_simule()
    {
        var clock = ClockWithBank(100_000);
        var service = new SkiaLayer.TimeJumpService();
        clock.Advanced += (_, _) => Spin(40);

        service.Request(clock, 30_000, "reason");
        service.Advance(clock);
        service.Cancel();

        // La banque n'est prelevee que tranche par tranche : fermer la partie au milieu d'un saut
        // ne doit pas engloutir le temps pas encore simule.
        Assert.False(service.IsActive);
        Assert.Equal(90_000, clock.OfflineBankTicks);
    }

    /// <summary>Occupe le processeur <paramref name="ms"/> millisecondes, pour epuiser le budget d'une tranche.</summary>
    private static void Spin(double ms)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        while (watch.Elapsed.TotalMilliseconds < ms) { }
    }
}

public class TimeJumpViewModelTests
{
    [Fact]
    public void Sans_saut_en_cours_la_popup_reste_masquee()
    {
        using var host = new GameRuntimeHost(new SkiaLayer.SkiaGameRuntime());
        var vm = new TimeJumpViewModel(host);

        vm.Refresh();

        Assert.False(vm.IsActive);
    }

    [Fact]
    public void Une_valeur_inchangee_ne_declenche_pas_de_notification()
    {
        using var host = new GameRuntimeHost(new SkiaLayer.SkiaGameRuntime());
        var vm = new TimeJumpViewModel(host);
        vm.Refresh();

        int notifications = 0;
        vm.PropertyChanged += (_, _) => notifications++;
        vm.Refresh();
        vm.Refresh();

        Assert.Equal(0, notifications);
    }
}

public class TimeJumpViewTests
{
    [AvaloniaFact]
    public void Le_voile_du_saut_de_temps_intercepte_les_clics_sur_la_carte()
    {
        using var host = new GameRuntimeHost(new SkiaLayer.SkiaGameRuntime());
        var map = new ProbeMapControl();
        // La visibilite est liee a l'instantane, qui reste inactif sans partie : la poser en
        // valeur locale detache la liaison et ne laisse en jeu que le blocage — le seul role du voile.
        var view = new TimeJumpView(new TimeJumpViewModel(host)) { IsVisible = true };

        var window = new Window
        {
            Width = 800,
            Height = 600,
            Content = new Panel { Children = { map, view } },
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        window.MouseDown(new Point(60, 400), MouseButton.Left);
        window.MouseUp(new Point(60, 400), MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(0, map.PointerPressedCount);
    }
}
