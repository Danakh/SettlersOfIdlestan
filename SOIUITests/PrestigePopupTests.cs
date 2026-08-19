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

public class PrestigeActionViewModelTests
{
    private static SkiaLayer.PrestigeActionSnapshot Action(bool enabled = true, bool corrupted = false) =>
        new(corrupted ? "corruptedPrestige" : "prestige", corrupted ? "Prestige corrompu" : "Prestige",
            corrupted ? "2 -> 3" : null, enabled, corrupted, ["Infobulle"]);

    [Fact]
    public void Seule_l_action_corrompue_a_une_seconde_ligne()
    {
        Assert.False(new PrestigeActionViewModel(Action()).HasSubLabel);
        Assert.True(new PrestigeActionViewModel(Action(corrupted: true)).HasSubLabel);
    }

    [Fact]
    public void Reappliquer_le_meme_instantane_ne_notifie_rien()
    {
        var action = new PrestigeActionViewModel(Action());
        int notifications = 0;
        action.PropertyChanged += (_, _) => notifications++;

        action.Apply(Action());
        action.Apply(Action());

        Assert.Equal(0, notifications);
    }
}

public class PrestigePopupViewModelTests
{
    [Fact]
    public void Sans_partie_en_cours_le_popup_est_ferme()
    {
        using var host = new GameRuntimeHost(new SkiaLayer.SkiaGameRuntime());
        var vm = new PrestigePopupViewModel(host);

        vm.Refresh();

        Assert.False(vm.IsOpen);
        Assert.Empty(vm.Rows);
        Assert.Empty(vm.Actions);
    }

    /// <summary>
    /// Le choix de palier et le rappel du Port Imperial sont conditionnels : ils ne doivent pas
    /// apparaitre tant que l'instantane ne les fournit pas.
    /// </summary>
    [Fact]
    public void Les_blocs_conditionnels_sont_absents_par_defaut()
    {
        using var host = new GameRuntimeHost(new SkiaLayer.SkiaGameRuntime());
        var vm = new PrestigePopupViewModel(host);
        vm.Refresh();

        Assert.False(vm.HasTierPicker);
        Assert.False(vm.HasWarning);
        Assert.False(vm.HasWonderRow);
    }

    [Fact]
    public void Un_decompte_inchange_ne_reconstruit_pas_les_lignes()
    {
        using var host = new GameRuntimeHost(new SkiaLayer.SkiaGameRuntime());
        var vm = new PrestigePopupViewModel(host);
        vm.Refresh();

        int rebuilds = 0;
        vm.Rows.CollectionChanged += (_, _) => rebuilds++;
        vm.Refresh();
        vm.Refresh();

        Assert.Equal(0, rebuilds);
    }
}

public class PrestigePopupViewTests
{
    /// Le popup est bloquant : son voile plein ecran doit intercepter les clics.
    [AvaloniaFact]
    public void Un_clic_n_importe_ou_n_atteint_pas_la_carte_quand_le_popup_est_ouvert()
    {
        var (window, map, _) = BuildProbeWindow(open: true);

        foreach (var point in new[] { new Point(80, 60), new Point(400, 300), new Point(700, 560) })
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
    /// Une valeur defavorable (malus de race, monstres restants) doit se distinguer : c'est la
    /// seule information de la colonne de droite qui merite l'attention du joueur.
    /// </summary>
    [AvaloniaFact]
    public void Une_valeur_defavorable_est_signalee()
    {
        var (_, _, view) = BuildProbeWindow(open: true, rows:
        [
            new("Bonus monstres", "+0%", true, ["Des monstres subsistent"]),
            new("Bonus race", "+10%", false, []),
        ]);

        var values = view.GetVisualDescendants().OfType<TextBlock>()
            .Where(t => t.Text is "+0%" or "+10%")
            .ToList();

        Assert.Equal(2, values.Count);
        var warning = values.First(t => t.Text == "+0%");
        var normal = values.First(t => t.Text == "+10%");
        Assert.NotEqual(normal.Foreground, warning.Foreground);
    }

    /// L'action corrompue affiche sa seconde ligne, l'action normale non.
    [AvaloniaFact]
    public void L_action_corrompue_montre_le_niveau_vise()
    {
        var (_, _, view) = BuildProbeWindow(open: true, actions:
        [
            new("prestige", "Prestige", null, true, false, ["a"]),
            new("corruptedPrestige", "Prestige corrompu", "2 -> 3", true, true, ["b"]),
        ]);

        var texts = view.GetVisualDescendants().OfType<TextBlock>()
            .Where(t => t.IsVisible)
            .Select(t => t.Text)
            .ToList();

        Assert.Contains("Prestige corrompu", texts);
        Assert.Contains("2 -> 3", texts);
    }

    private static (Window Window, ProbeMapControl Map, PrestigePopupView View) BuildProbeWindow(
        bool open,
        SkiaLayer.PrestigeRowSnapshot[]? rows = null,
        SkiaLayer.PrestigeActionSnapshot[]? actions = null)
    {
        var host = new GameRuntimeHost(new SkiaLayer.SkiaGameRuntime());
        var map = new ProbeMapControl();
        var vm = new PrestigePopupViewModel(host);

        // Pas de partie en cours : les listes sont alimentees a la main pour eprouver la vue.
        foreach (var row in rows ?? []) vm.Rows.Add(row);
        foreach (var action in actions ?? []) vm.Actions.Add(new PrestigeActionViewModel(action));

        var view = new PrestigePopupView(vm) { IsVisible = open };

        var window = new Window
        {
            Width = 800,
            Height = 700,
            Content = new Panel { Children = { map, view } },
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return (window, map, view);
    }
}
