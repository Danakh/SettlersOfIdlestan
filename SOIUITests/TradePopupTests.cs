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

public class TradeRowViewModelTests
{
    private static SkiaLayer.TradeRowSnapshot Row(bool enabled = true, string stock = "12/100", bool atMax = false) =>
        new("Wood", "Wood", "Bois", stock, atMax, "Vendre 10 → 5", enabled,
            enabled ? null : "Pas assez de ressources");

    [Fact]
    public void Une_ligne_disponible_n_a_pas_d_infobulle_de_blocage()
    {
        Assert.Null(new TradeRowViewModel(Row(enabled: true)).DisabledTooltip);
        Assert.NotNull(new TradeRowViewModel(Row(enabled: false)).DisabledTooltip);
    }

    [Fact]
    public void Changer_de_multiplicateur_notifie_le_libelle_et_la_disponibilite()
    {
        var row = new TradeRowViewModel(Row(enabled: true));
        var changed = new List<string?>();
        row.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        row.Apply(new SkiaLayer.TradeRowSnapshot("Wood", "Wood", "Bois", "12/100", false,
            "Vendre 100 → 50", false, "Pas assez de ressources"));

        Assert.Contains(nameof(TradeRowViewModel.ButtonLabel), changed);
        Assert.Contains(nameof(TradeRowViewModel.IsEnabled), changed);
        Assert.Contains(nameof(TradeRowViewModel.DisabledTooltip), changed);
    }

    /// <summary>
    /// Les lignes sont reappliquees dix fois par seconde : sans la garde d'egalite, chaque tick
    /// leverait des notifications et ferait relayouter le popup.
    /// </summary>
    [Fact]
    public void Reappliquer_le_meme_instantane_ne_notifie_rien()
    {
        var row = new TradeRowViewModel(Row());
        int notifications = 0;
        row.PropertyChanged += (_, _) => notifications++;

        row.Apply(Row());
        row.Apply(Row());

        Assert.Equal(0, notifications);
    }
}

public class TradePopupViewModelTests
{
    [Fact]
    public void Sans_partie_en_cours_le_popup_est_ferme()
    {
        using var host = new GameRuntimeHost(new SkiaLayer.SkiaGameRuntime());
        var vm = new TradePopupViewModel(host);

        vm.Refresh();

        Assert.False(vm.IsOpen);
        Assert.Empty(vm.SellRows);
        Assert.Empty(vm.BuyRows);
    }

    /// Les deux onglets s'excluent : l'un des deux est toujours affiche.
    [Fact]
    public void Les_deux_onglets_s_excluent()
    {
        using var host = new GameRuntimeHost(new SkiaLayer.SkiaGameRuntime());
        var vm = new TradePopupViewModel(host);
        vm.Refresh();

        Assert.NotEqual(vm.ShowingTrade, vm.ShowingHistory);
    }
}

public class TradePopupViewTests
{
    /// <summary>
    /// Le popup est bloquant : son voile plein ecran doit intercepter les clics, la ou le rendu
    /// Skia comparait la position au rectangle du popup.
    /// </summary>
    [AvaloniaFact]
    public void Un_clic_n_importe_ou_n_atteint_pas_la_carte_quand_le_popup_est_ouvert()
    {
        var (window, map, _) = BuildProbeWindow(open: true);

        foreach (var point in new[] { new Point(80, 60), new Point(400, 300), new Point(760, 560) })
        {
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
        }
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(0, map.PointerPressedCount);
    }

    /// Controle negatif : popup ferme, la carte redevient cliquable.
    [AvaloniaFact]
    public void Popup_ferme_les_clics_atteignent_la_carte()
    {
        var (window, map, _) = BuildProbeWindow(open: false);

        window.MouseDown(new Point(400, 300), MouseButton.Left);
        window.MouseUp(new Point(400, 300), MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(1, map.PointerPressedCount);
    }

    /// <summary>
    /// Une ligne bloquee garde son bouton actif au sens Avalonia : desactive, il ne recevrait
    /// plus le pointeur et n'afficherait jamais l'infobulle qui explique le blocage.
    /// </summary>
    [AvaloniaFact]
    public void Une_ligne_bloquee_garde_un_bouton_actif_et_son_infobulle()
    {
        var (_, _, view) = BuildProbeWindow(open: true, sellRows:
        [
            new("Wood", "Wood", "Bois", "2/100", false, "Vendre 10 → 5", false, "Pas assez de ressources"),
        ]);

        var button = view.GetVisualDescendants().OfType<Button>()
            .First(b => b.DataContext is TradeRowViewModel);

        Assert.True(button.IsEnabled);
        Assert.Equal("Pas assez de ressources", ToolTip.GetTip(button));
    }

    /// <summary>
    /// La section historique est masquee tant que son onglet n'est pas actif, et cet onglet vient
    /// de l'instantane — impossible a forcer sans partie en cours. On la rend donc visible a la
    /// main pour eprouver son gabarit, ce qui est bien l'objet du test.
    /// </summary>
    [AvaloniaFact]
    public void L_historique_affiche_ses_entrees()
    {
        var (_, _, view) = BuildProbeWindow(open: true, history:
        [
            new("Wood", "Vendu 10 Bois", "+5 or", true, "1h02m03s"),
        ]);

        var section = view.GetVisualDescendants().OfType<StackPanel>()
            .First(p => p.Children.OfType<ItemsControl>()
                .Any(c => c.ItemsSource is IEnumerable<TradeHistoryEntryViewModel>));
        section.IsVisible = true;
        Dispatcher.UIThread.RunJobs();

        var texts = view.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();

        Assert.Contains("Vendu 10 Bois", texts);
        Assert.Contains("+5 or", texts);
        Assert.Contains("1h02m03s", texts);
    }

    private static (Window Window, ProbeMapControl Map, TradePopupView View) BuildProbeWindow(
        bool open,
        SkiaLayer.TradeRowSnapshot[]? sellRows = null,
        SkiaLayer.TradeHistoryEntrySnapshot[]? history = null)
    {
        var host = new GameRuntimeHost(new SkiaLayer.SkiaGameRuntime());
        var icons = new SvgIconCache();
        var map = new ProbeMapControl();
        var vm = new TradePopupViewModel(host);

        // Pas de partie en cours : les listes sont alimentees a la main pour eprouver la vue.
        foreach (var row in sellRows ?? []) vm.SellRows.Add(new TradeRowViewModel(row));
        foreach (var entry in history ?? []) vm.HistoryEntries.Add(new TradeHistoryEntryViewModel(entry));

        var view = new TradePopupView(vm, icons) { IsVisible = open };

        var window = new Window
        {
            Width = 900,
            Height = 700,
            Content = new Panel { Children = { map, view } },
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return (window, map, view);
    }
}
