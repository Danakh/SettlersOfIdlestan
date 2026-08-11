using System.Collections.Specialized;
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
        vm.SyncSections(Sections(playtime: "1m00s"));

        int rebuilds = 0;
        vm.Sections.CollectionChanged += (_, _) => rebuilds++;
        vm.SyncSections(Sections(playtime: "1m00s"));
        vm.SyncSections(Sections(playtime: "1m00s"));

        Assert.Equal(0, rebuilds);
    }

    /// <summary>
    /// Le temps de jeu de la section « partie en cours » bouge a chaque synchronisation, alors que
    /// l'historique ne change qu'a un prestige. Vider puis remplir la collection rebatissait
    /// l'arbre de controles de tout l'historique dix fois par seconde : sur une sauvegarde a
    /// nombreuses parties passees, l'onglet figeait l'application.
    /// </summary>
    [Fact]
    public void Seule_la_section_qui_change_est_remplacee()
    {
        using var host = new GameRuntimeHost(new SkiaLayer.SkiaGameRuntime());
        var vm = new StatsViewModel(host);
        vm.SyncSections(Sections(playtime: "1m00s"));
        var historyBefore = vm.Sections[1];

        var changes = new List<NotifyCollectionChangedEventArgs>();
        vm.Sections.CollectionChanged += (_, e) => changes.Add(e);
        vm.SyncSections(Sections(playtime: "1m01s"));

        var change = Assert.Single(changes);
        Assert.Equal(NotifyCollectionChangedAction.Replace, change.Action);
        Assert.Equal(0, change.NewStartingIndex);
        Assert.Same(historyBefore, vm.Sections[1]);
    }

    /// <summary>
    /// Une section disparait quand le sous-onglet change de composition (les records d'ascension
    /// n'apparaissent qu'apres une premiere ascension) : la collection doit se raccourcir.
    /// </summary>
    [Fact]
    public void Une_section_en_moins_est_retiree()
    {
        using var host = new GameRuntimeHost(new SkiaLayer.SkiaGameRuntime());
        var vm = new StatsViewModel(host);
        vm.SyncSections(Sections(playtime: "1m00s"));

        vm.SyncSections([Sections(playtime: "1m00s")[0]]);

        Assert.Single(vm.Sections);
    }

    /// Deux sections construites a l'identique : la garde ne tient que si elles sont egales.
    [Fact]
    public void Deux_instantanes_identiques_sont_structurellement_egaux() =>
        Assert.Equal(Sections(playtime: "1m00s")[1], Sections(playtime: "1m00s")[1]);

    /// <summary>
    /// Deux sections comme le renderer les produit : la partie en cours, dont le temps de jeu
    /// bouge en continu, puis un historique fige.
    /// </summary>
    private static SkiaLayer.StatSectionSnapshot[] Sections(string playtime) =>
    [
        new("Partie en cours", true, null,
        [
            new([new("Ile", "#3"), new("Duree", playtime)], 4, true, []),
        ]),
        new("Parties precedentes", false, null,
        [
            new([new("Ile", "#1"), new("Duree", "12m00s")], 4, false, []),
            new([new("Ile", "#2"), new("Duree", "9m00s")], 4, false, []),
        ]),
    ];
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
