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

public class StatsViewModelTests
{
    [Fact]
    public void Hors_de_l_onglet_Stats_la_vue_est_masquee()
    {
        using var host = new GameRuntimeHost(new SkiaLayer.SkiaGameRuntime());
        var vm = new StatsViewModel(host);

        vm.Refresh();

        Assert.False(vm.IsVisible);
        Assert.Empty(vm.Sections);
        Assert.Empty(vm.SubTabs);
    }

    /// <summary>
    /// Les chiffres sont resynchronises dix fois par seconde. Sans garde, chaque tick
    /// reconstruirait toutes les cartes — et ferait perdre la position de defilement.
    /// </summary>
    [Fact]
    public void Des_statistiques_inchangees_ne_reconstruisent_pas_les_sections()
    {
        using var host = new GameRuntimeHost(new SkiaLayer.SkiaGameRuntime());
        var vm = new StatsViewModel(host);
        vm.Refresh();

        int rebuilds = 0;
        vm.Sections.CollectionChanged += (_, _) => rebuilds++;
        vm.Refresh();
        vm.Refresh();

        Assert.Equal(0, rebuilds);
    }
}

public class StatsViewTests
{
    /// L'onglet plein ecran remplace la carte : son fond opaque doit intercepter les clics.
    [AvaloniaFact]
    public void Un_clic_sur_l_onglet_plein_ecran_n_atteint_pas_la_carte()
    {
        var (window, map, _) = BuildProbeWindow(visible: true);

        window.MouseDown(new Point(400, 300), MouseButton.Left);
        window.MouseUp(new Point(400, 300), MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(0, map.PointerPressedCount);
    }

    /// Controle negatif : hors de l'onglet, la carte redevient cliquable.
    [AvaloniaFact]
    public void Onglet_ferme_les_clics_atteignent_la_carte()
    {
        var (window, map, _) = BuildProbeWindow(visible: false);

        window.MouseDown(new Point(400, 300), MouseButton.Left);
        window.MouseUp(new Point(400, 300), MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(1, map.PointerPressedCount);
    }

    /// <summary>
    /// Une carte se remplit depuis son DataContext, pas depuis un template declaratif : ce test
    /// verifie que les cellules sont bien instanciees et occupent des pixels.
    /// </summary>
    [AvaloniaFact]
    public void Les_cellules_d_une_carte_sont_materialisees()
    {
        var (_, _, view) = BuildProbeWindow(visible: true, sections:
        [
            new("Partie en cours", true, null,
            [
                new([new("Ile", "#3"), new("Villes", "7"), new("Recherches", "42")], 4, true, []),
            ]),
        ]);

        var values = view.GetVisualDescendants().OfType<TextBlock>()
            .Select(t => t.Text)
            .ToList();

        Assert.Contains("Partie en cours", values);
        Assert.Contains("#3", values);
        Assert.Contains("Villes", values);
        Assert.Contains("42", values);
    }

    /// Une carte de liste (les races jouees) n'a pas de cellules mais des lignes de texte.
    [AvaloniaFact]
    public void Une_carte_de_liste_affiche_ses_lignes()
    {
        var (_, _, view) = BuildProbeWindow(visible: true, sections:
        [
            new("Races jouees", false, null, [new([], 1, false, ["Nains", "Geants"])]),
        ]);

        var values = view.GetVisualDescendants().OfType<TextBlock>()
            .Select(t => t.Text)
            .ToList();

        Assert.Contains("Nains", values);
        Assert.Contains("Geants", values);
    }

    private static (Window Window, ProbeMapControl Map, StatsView View) BuildProbeWindow(
        bool visible, SkiaLayer.StatSectionSnapshot[]? sections = null)
    {
        var host = new GameRuntimeHost(new SkiaLayer.SkiaGameRuntime());
        var map = new ProbeMapControl();
        var vm = new StatsViewModel(host);

        // Pas de partie en cours : les sections sont alimentees a la main pour eprouver la vue.
        foreach (var section in sections ?? []) vm.Sections.Add(section);

        var view = new StatsView(vm) { IsVisible = visible };

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
