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

public class AutomationRowViewModelTests
{
    private static SkiaLayer.AutomationRowSnapshot Row(
        bool? isOn = true, bool isLocked = false, bool isPinned = false, string[]? summary = null) =>
        new("Road", "Routes", isLocked ? "Necessite une Guilde des Batisseurs" : "Construit les routes",
            isLocked ? null : "Note", isOn, isLocked, CanPin: !isLocked, isPinned, summary ?? []);

    [Fact]
    public void Une_ligne_verrouillee_n_a_pas_de_bascule()
    {
        var row = new AutomationRowViewModel(Row(isOn: null, isLocked: true));

        Assert.False(row.HasToggle);
        Assert.False(row.CanPin);
    }

    [Fact]
    public void L_etat_mixte_est_conserve_tel_quel()
    {
        // Certains batiments du type actifs, d'autres non : la case doit rester indeterminee
        // plutot que de trancher arbitrairement.
        Assert.Null(new AutomationRowViewModel(Row(isOn: null)).IsOn);
    }

    [Fact]
    public void Deverrouiller_une_ligne_notifie_l_apparition_de_sa_bascule()
    {
        var row = new AutomationRowViewModel(Row(isOn: null, isLocked: true));
        var changed = new List<string?>();
        row.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        row.Apply(Row(isOn: false, isLocked: false));

        Assert.Contains(nameof(AutomationRowViewModel.HasToggle), changed);
        Assert.Contains(nameof(AutomationRowViewModel.IsLocked), changed);
        Assert.True(row.HasToggle);
    }

    /// <summary>
    /// Une trentaine de cartes sont reappliquees dix fois par seconde : sans la garde d'egalite,
    /// chaque tick leverait des notifications et ferait relayouter toute la page.
    /// </summary>
    [Fact]
    public void Reappliquer_le_meme_instantane_ne_notifie_rien()
    {
        var row = new AutomationRowViewModel(Row(summary: ["Scierie: 2xNiv1"]));
        int notifications = 0;
        row.PropertyChanged += (_, _) => notifications++;

        row.Apply(Row(summary: ["Scierie: 2xNiv1"]));
        row.Apply(Row(summary: ["Scierie: 2xNiv1"]));

        Assert.Equal(0, notifications);
    }
}

public class AutomationViewModelTests
{
    [Fact]
    public void Hors_de_l_onglet_Automatisation_la_vue_est_masquee()
    {
        using var host = new GameRuntimeHost(new SkiaLayer.SkiaGameRuntime());
        var vm = new AutomationViewModel(host);

        vm.Refresh();

        Assert.False(vm.IsVisible);
        Assert.Empty(vm.LeftColumn);
        Assert.Empty(vm.RightColumn);
    }

    [Fact]
    public void Une_page_inchangee_ne_reconstruit_pas_les_colonnes()
    {
        using var host = new GameRuntimeHost(new SkiaLayer.SkiaGameRuntime());
        var vm = new AutomationViewModel(host);
        vm.Refresh();

        int rebuilds = 0;
        vm.LeftColumn.CollectionChanged += (_, _) => rebuilds++;
        vm.RightColumn.CollectionChanged += (_, _) => rebuilds++;
        vm.Refresh();
        vm.Refresh();

        Assert.Equal(0, rebuilds);
    }
}

public class AutomationViewTests
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
    /// Une ligne verrouillee ne doit montrer ni bascule ni case d'epinglage : rien a regler tant
    /// que l'automatisme n'est pas debloque.
    /// </summary>
    [AvaloniaFact]
    public void Une_ligne_verrouillee_n_affiche_ni_bascule_ni_epinglage()
    {
        var (_, _, view) = BuildProbeWindow(visible: true, section: new(
            "Constructions",
            [
                new("Road", "Routes", "Necessite une Guilde des Batisseurs", null, null,
                    IsLocked: true, CanPin: false, IsPinned: false, []),
            ]));

        var visibleBoxes = view.GetVisualDescendants().OfType<CheckBox>()
            .Where(c => c.DataContext is AutomationRowViewModel && c.IsVisible)
            .ToList();

        Assert.Empty(visibleBoxes);
    }

    [AvaloniaFact]
    public void Une_ligne_deverrouillee_affiche_sa_bascule_son_epinglage_et_son_resume()
    {
        var (_, _, view) = BuildProbeWindow(visible: true, section: new(
            "Constructions",
            [
                new("Production", "Production", "Construit les batiments de production", "Note", true,
                    IsLocked: false, CanPin: true, IsPinned: true, ["Scierie: 2xNiv1"]),
            ]));

        var boxes = view.GetVisualDescendants().OfType<CheckBox>()
            .Where(c => c.DataContext is AutomationRowViewModel && c.IsVisible)
            .ToList();
        Assert.Equal(2, boxes.Count);

        var texts = view.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
        Assert.Contains("Scierie: 2xNiv1", texts);
    }

    private static (Window Window, ProbeMapControl Map, AutomationView View) BuildProbeWindow(
        bool visible, SkiaLayer.AutomationSectionSnapshot? section = null)
    {
        var host = new GameRuntimeHost(new SkiaLayer.SkiaGameRuntime());
        var map = new ProbeMapControl();
        var vm = new AutomationViewModel(host);

        // Pas de partie en cours : la colonne est alimentee a la main pour eprouver la vue.
        if (section != null) vm.LeftColumn.Add(new AutomationSectionViewModel(section));

        var view = new AutomationView(vm) { IsVisible = visible };

        var window = new Window
        {
            Width = 900,
            Height = 600,
            Content = new Panel { Children = { map, view } },
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return (window, map, view);
    }
}
