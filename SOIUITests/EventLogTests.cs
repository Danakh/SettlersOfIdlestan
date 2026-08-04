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

public class EventLogViewModelTests
{
    [Fact]
    public void Hors_de_l_onglet_Journal_la_vue_est_masquee()
    {
        using var host = new GameRuntimeHost(new SkiaLayer.SkiaGameRuntime());
        var vm = new EventLogViewModel(host);

        vm.Refresh();

        Assert.False(vm.IsVisible);
        Assert.Empty(vm.Entries);
        Assert.True(vm.IsEmpty);
    }

    /// <summary>
    /// Le journal est resynchronise dix fois par seconde. Sans garde, chaque tick recreerait les
    /// 50 controles de la liste — et ferait perdre la position de defilement en cours de lecture.
    /// </summary>
    [Fact]
    public void Un_journal_inchange_ne_reconstruit_pas_la_liste()
    {
        using var host = new GameRuntimeHost(new SkiaLayer.SkiaGameRuntime());
        var vm = new EventLogViewModel(host);
        vm.Refresh();

        int rebuilds = 0;
        vm.Entries.CollectionChanged += (_, _) => rebuilds++;
        vm.Refresh();
        vm.Refresh();

        Assert.Equal(0, rebuilds);
    }
}

public class EventLogViewTests
{
    /// <summary>
    /// L'onglet plein ecran remplace la carte : son fond opaque doit intercepter les clics.
    /// Le rendu Skia devait pour cela declarer l'onglet actif dans IsPointBlockedByUI.
    /// </summary>
    [AvaloniaFact]
    public void Un_clic_sur_l_onglet_plein_ecran_n_atteint_pas_la_carte()
    {
        var (window, map, _) = BuildProbeWindow(visible: true);

        foreach (var point in new[] { new Point(120, 120), new Point(400, 300), new Point(700, 520) })
        {
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
        }
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(0, map.PointerPressedCount);
    }

    /// Controle negatif : hors de l'onglet, la carte doit redevenir cliquable.
    [AvaloniaFact]
    public void Onglet_ferme_les_clics_atteignent_la_carte()
    {
        var (window, map, _) = BuildProbeWindow(visible: false);

        window.MouseDown(new Point(400, 300), MouseButton.Left);
        window.MouseUp(new Point(400, 300), MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(1, map.PointerPressedCount);
    }

    [AvaloniaFact]
    public void Chaque_entree_est_materialisee_avec_une_geometrie_visible()
    {
        var (_, _, view) = BuildProbeWindow(visible: true, entries:
        [
            new("Bandits", "Des bandits rodent", SkiaLayer.EventLogTone.Danger),
            new("Tresor", "Un tresor decouvert", SkiaLayer.EventLogTone.Reward),
            new("Victoire", "Repaire detruit", SkiaLayer.EventLogTone.Success),
        ]);

        var cards = view.GetVisualDescendants().OfType<Border>()
            .Where(b => b.DataContext is EventLogEntryViewModel)
            .ToList();

        Assert.Equal(3, cards.Count);
        Assert.All(cards, c => Assert.True(c.Bounds.Height > 0,
            "L'entree occupe zero pixel : son template ne s'est pas applique."));
    }

    private static (Window Window, ProbeMapControl Map, EventLogView View) BuildProbeWindow(
        bool visible, SkiaLayer.EventLogEntrySnapshot[]? entries = null)
    {
        var host = new GameRuntimeHost(new SkiaLayer.SkiaGameRuntime());
        var map = new ProbeMapControl();
        var vm = new EventLogViewModel(host);

        // Pas de partie en cours : la liste est alimentee a la main pour eprouver la vue.
        foreach (var entry in entries ?? []) vm.Entries.Add(new EventLogEntryViewModel(entry));

        var view = new EventLogView(vm) { IsVisible = visible };

        var window = new Window
        {
            Width = 800,
            Height = 600,
            Content = new Panel { Children = { map, view } },
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return (window, map, view);
    }
}
