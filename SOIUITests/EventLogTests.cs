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

    /// <summary>
    /// Page Reglages : deux cases par famille, l'affichage et le son, reglables separement. Une
    /// seule case par ligne — le cas d'avant — rendrait le son indissociable de l'affichage.
    /// </summary>
    [AvaloniaFact]
    public void Chaque_famille_porte_une_case_d_affichage_et_une_case_de_son()
    {
        var (_, _, view) = BuildProbeWindow(visible: true, filters:
        [
            new("Bandit", "Bandits", IsChecked: true, IsSoundChecked: false),
            new("Dragon", "Dragons", IsChecked: false, IsSoundChecked: true),
        ]);

        var rows = FilterRows(view);
        Assert.Equal(2, rows.Count);

        Assert.Collection(Boxes(rows[0]),
            display => Assert.True(display.IsChecked),
            sound => Assert.False(sound.IsChecked));

        Assert.Collection(Boxes(rows[1]),
            display => Assert.False(display.IsChecked),
            sound => Assert.True(sound.IsChecked));
    }

    /// <summary>
    /// Les cases se rangent sous leur entete de colonne. L'entete et les lignes sont deux grilles
    /// distinctes : elles ne s'alignent que parce que leurs colonnes ont une largeur fixe — une
    /// largeur auto se calerait sur le contenu de chacune, donc differemment.
    /// </summary>
    [AvaloniaFact]
    public void Les_cases_s_alignent_sous_leur_entete_de_colonne()
    {
        var (_, _, view) = BuildProbeWindow(visible: true, filters:
        [
            new("Bandit", "Bandits", IsChecked: true, IsSoundChecked: true),
        ]);

        var headerGrid = view.GetVisualDescendants().OfType<Grid>()
            .Single(g => g.Name == EventLogView.ColumnHeaderName);
        var headers = headerGrid.Children.OfType<TextBlock>().OrderBy(t => CenterX(t, view)).ToList();

        var boxes = Boxes(FilterRows(view)[0]);

        Assert.Equal(2, headers.Count);
        Assert.Equal(CenterX(headers[0], view), CenterX(boxes[0], view), precision: 0);
        Assert.Equal(CenterX(headers[1], view), CenterX(boxes[1], view), precision: 0);
    }

    private static List<Grid> FilterRows(EventLogView view) =>
        view.GetVisualDescendants().OfType<Grid>()
            .Where(g => g.Name == EventLogView.FilterRowName)
            .ToList();

    /// Les deux cases d'une ligne, dans l'ordre des colonnes : affichage puis son.
    private static List<CheckBox> Boxes(Grid row) =>
        row.GetVisualDescendants().OfType<CheckBox>().OrderBy(b => CenterX(b, row)).ToList();

    /// Abscisse du centre d'un controle, exprimee dans le repere de l'onglet : l'entete et les
    /// lignes sont dans deux sous-arbres differents, leurs Bounds locaux ne se comparent pas.
    private static double CenterX(Visual visual, Visual relativeTo) =>
        visual.TranslatePoint(new Point(visual.Bounds.Width / 2, 0), relativeTo)!.Value.X;

    private static (Window Window, ProbeMapControl Map, EventLogView View) BuildProbeWindow(
        bool visible,
        SkiaLayer.EventLogEntrySnapshot[]? entries = null,
        SkiaLayer.EventLogFilterSnapshot[]? filters = null)
    {
        var host = new GameRuntimeHost(new SkiaLayer.SkiaGameRuntime());
        var map = new ProbeMapControl();
        var vm = new EventLogViewModel(host);

        // Pas de partie en cours : la liste est alimentee a la main pour eprouver la vue.
        foreach (var entry in entries ?? []) vm.Entries.Add(new EventLogEntryViewModel(entry));

        // Les reglages sont la seconde page de l'onglet : sans ce basculement, ses controles ne
        // sont pas materialises.
        foreach (var filter in filters ?? []) vm.Filters.Add(new EventLogFilterViewModel(filter));
        if (filters is { Length: > 0 }) vm.ShowSettings = true;

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
