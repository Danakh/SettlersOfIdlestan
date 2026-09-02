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

public class RitualRowViewModelTests
{
    private static SkiaLayer.RitualRowSnapshot Row(bool isActive = false, int power = 0, bool canIncrease = false) =>
        new("Fertility", "Fertilite", "Accelere les recoltes", "Entretien : 3 cristaux/s",
            isActive ? "Bonus : +30%" : null, isActive,
            isActive ? "Arreter" : "Lancer", true, power, canIncrease);

    [Fact]
    public void Un_rituel_inactif_n_annonce_pas_de_bonus()
    {
        var row = new RitualRowViewModel(Row(isActive: false));

        Assert.False(row.HasBonus);
        Assert.Null(row.BonusText);
    }

    [Fact]
    public void Lancer_un_rituel_notifie_les_proprietes_qui_changent()
    {
        var row = new RitualRowViewModel(Row(isActive: false));
        var changed = new List<string?>();
        row.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        row.Apply(Row(isActive: true, power: 2, canIncrease: true));

        Assert.Contains(nameof(RitualRowViewModel.IsActive), changed);
        Assert.Contains(nameof(RitualRowViewModel.HasBonus), changed);
        Assert.Contains(nameof(RitualRowViewModel.PowerText), changed);
        Assert.Equal("2", row.PowerText);
    }

    /// <summary>
    /// Les lignes sont reappliquees dix fois par seconde. Sans la garde d'egalite, chaque tick
    /// leverait des notifications et ferait relayouter la page en continu.
    /// </summary>
    [Fact]
    public void Reappliquer_le_meme_instantane_ne_notifie_rien()
    {
        var row = new RitualRowViewModel(Row(isActive: true, power: 3));
        int notifications = 0;
        row.PropertyChanged += (_, _) => notifications++;

        row.Apply(Row(isActive: true, power: 3));
        row.Apply(Row(isActive: true, power: 3));

        Assert.Equal(0, notifications);
    }
}

public class RitualsViewModelTests
{
    [Fact]
    public void Hors_de_l_onglet_Rituels_la_vue_est_masquee()
    {
        using var host = new GameRuntimeHost(new SkiaLayer.SkiaGameRuntime());
        var vm = new RitualsViewModel(host);

        vm.Refresh();

        Assert.False(vm.IsVisible);
        Assert.Empty(vm.Rituals);
        Assert.Empty(vm.Spells);
        Assert.False(vm.HasSpells);
    }
}

public class RitualsViewTests
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
    /// Le reglage de puissance n'existe que sur un rituel en cours : sur un rituel inactif il ne
    /// doit pas apparaitre, sous peine de laisser croire qu'on peut le regler avant de le lancer.
    /// </summary>
    [AvaloniaFact]
    public void Le_reglage_de_puissance_n_apparait_que_sur_un_rituel_actif()
    {
        var (_, _, inactive) = BuildProbeWindow(visible: true, rituals:
        [
            new("A", "Rituel A", "desc", "cout", null, false, "Lancer", true, 0, false),
        ]);
        Assert.DoesNotContain(VisibleButtonLabels(inactive), l => l is "+" or "-");

        var (_, _, active) = BuildProbeWindow(visible: true, rituals:
        [
            new("B", "Rituel B", "desc", "cout", "bonus", true, "Arreter", true, 2, true),
        ]);
        var labels = VisibleButtonLabels(active);
        Assert.Contains("+", labels);
        Assert.Contains("-", labels);
    }

    [AvaloniaFact]
    public void Les_sorts_sont_materialises_avec_leur_avertissement()
    {
        var (_, _, view) = BuildProbeWindow(visible: true, spells:
        [
            new("Gold", "Filon", "Convertit des cristaux", "Cout : 10", "Pas assez de cristaux", "Lancer", false, 0, 0.0, "", 0, 0, ""),
        ]);

        var texts = view.GetVisualDescendants().OfType<TextBlock>()
            .Where(t => t.IsVisible)
            .Select(t => t.Text)
            .ToList();

        Assert.Contains("Filon", texts);
        Assert.Contains("Pas assez de cristaux", texts);
    }

    private static List<string?> VisibleButtonLabels(RitualsView view) =>
        view.GetVisualDescendants().OfType<Button>()
            .Where(b => b.IsVisible && b.IsEffectivelyVisible)
            .Select(b => b.Content as string)
            .ToList();

    private static (Window Window, ProbeMapControl Map, RitualsView View) BuildProbeWindow(
        bool visible,
        SkiaLayer.RitualRowSnapshot[]? rituals = null,
        SkiaLayer.SpellRowSnapshot[]? spells = null)
    {
        var host = new GameRuntimeHost(new SkiaLayer.SkiaGameRuntime());
        var map = new ProbeMapControl();
        var vm = new RitualsViewModel(host);

        // Pas de partie en cours : les listes sont alimentees a la main pour eprouver la vue.
        foreach (var ritual in rituals ?? []) vm.Rituals.Add(new RitualRowViewModel(ritual));
        foreach (var spell in spells ?? []) vm.Spells.Add(new SpellRowViewModel(spell));

        var view = new RitualsView(vm) { IsVisible = visible };

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
