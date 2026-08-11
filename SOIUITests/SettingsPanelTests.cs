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

public class SettingsPanelViewModelTests
{
    private static SkiaLayer.SettingRowSnapshot Toggle(string key, bool value, bool enabled = true) =>
        new(key, key, SkiaLayer.SettingRowKind.Toggle, enabled, value, [], 0, 0, 0, "", "");

    private static SettingsPanelViewModel Panel(
        List<string>? toggled = null, List<(string, string)>? choices = null) =>
        new(k => toggled?.Add(k),
            (k, c) => choices?.Add((k, c)),
            (_, _) => { },
            (_, _) => { });

    /// <summary>
    /// Un reglage sans objet (sauvegarde cloud sans store connecte) doit rester inerte, comme
    /// dans le rendu Skia ou son rectangle n'etait meme pas teste.
    /// </summary>
    [Fact]
    public void Un_reglage_desactive_ne_declenche_rien()
    {
        var toggled = new List<string>();
        var panel = Panel(toggled);
        panel.Apply(new SkiaLayer.SettingsPanelSnapshot([Toggle("cloudSave", false, enabled: false)]));

        panel.Toggle(panel.Rows[0]);

        Assert.Empty(toggled);
    }

    [Fact]
    public void Un_reglage_actif_relaie_sa_cle()
    {
        var toggled = new List<string>();
        var panel = Panel(toggled);
        panel.Apply(new SkiaLayer.SettingsPanelSnapshot([Toggle("fullscreen", false)]));

        panel.Toggle(panel.Rows[0]);

        Assert.Equal(["fullscreen"], toggled);
    }

    /// <summary>
    /// La composition change entre l'ecran-titre et le jeu, et selon le mode debogage : la liste
    /// ne doit se reconstruire que dans ce cas, pas a chaque tick.
    /// </summary>
    [Fact]
    public void Une_composition_inchangee_ne_reconstruit_pas_les_lignes()
    {
        var panel = Panel();
        var snapshot = new SkiaLayer.SettingsPanelSnapshot([Toggle("fullscreen", false)]);
        panel.Apply(snapshot);

        int rebuilds = 0;
        panel.Rows.CollectionChanged += (_, _) => rebuilds++;
        panel.Apply(snapshot);
        panel.Apply(snapshot);

        Assert.Equal(0, rebuilds);
    }

    /// <summary>
    /// Changer la langue depuis ce panneau relocalise ses propres libelles, y compris ceux des
    /// options de choix. Figes a la construction, les boutons de format des nombres restaient en
    /// francais apres un passage en anglais — defaut vu a l'ecran.
    /// </summary>
    [Fact]
    public void Changer_de_langue_relocalise_les_libelles_des_options()
    {
        var panel = Panel();
        panel.Apply(new SkiaLayer.SettingsPanelSnapshot(
        [
            new("numberFormat", "Affichage des grands nombres", SkiaLayer.SettingRowKind.Choice, true, false,
                [new("classic", "Classique", true), new("scientific", "Scientifique", false)], 0, 0, 0, "", ""),
        ]));

        panel.Apply(new SkiaLayer.SettingsPanelSnapshot(
        [
            new("numberFormat", "Large number display", SkiaLayer.SettingRowKind.Choice, true, false,
                [new("classic", "Classic", true), new("scientific", "Scientific", false)], 0, 0, 0, "", ""),
        ]));

        Assert.Equal("Large number display", panel.Rows[0].Label);
        Assert.Equal(["Classic", "Scientific"], panel.Rows[0].Choices.Select(c => c.Label));
    }

    [Fact]
    public void Chaque_nature_de_reglage_n_expose_que_son_controle()
    {
        var panel = Panel();
        panel.Apply(new SkiaLayer.SettingsPanelSnapshot(
        [
            Toggle("fullscreen", true),
            new("language", "Langue", SkiaLayer.SettingRowKind.Choice, true, false,
                [new("english", "English", true), new("french", "Français", false)], 0, 0, 0, "", ""),
            new("uiScale", "Échelle", SkiaLayer.SettingRowKind.Slider, true, false, [], 1.0, 0.5, 2.0, "x1,0", ""),
        ]));

        Assert.True(panel.Rows[0].IsToggle);
        Assert.False(panel.Rows[0].IsChoice);
        Assert.True(panel.Rows[1].IsChoice);
        Assert.Equal(2, panel.Rows[1].Choices.Count);
        Assert.True(panel.Rows[2].IsSlider);
    }
}

public class SettingsPopupViewTests
{
    /// Le popup est bloquant : son voile plein ecran doit intercepter les clics.
    [AvaloniaFact]
    public void Un_clic_n_importe_ou_n_atteint_pas_la_carte_quand_le_popup_est_ouvert()
    {
        var (window, map, _) = BuildProbeWindow(open: true);

        window.MouseDown(new Point(100, 80), MouseButton.Left);
        window.MouseUp(new Point(100, 80), MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(0, map.PointerPressedCount);
    }

    /// Controle negatif : popup ferme, la carte redevient cliquable.
    [AvaloniaFact]
    public void Popup_ferme_les_clics_atteignent_la_carte()
    {
        var (window, map, _) = BuildProbeWindow(open: false);

        window.MouseDown(new Point(100, 80), MouseButton.Left);
        window.MouseUp(new Point(100, 80), MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(1, map.PointerPressedCount);
    }

    /// <summary>
    /// La bascule d'un reglage desactive doit aussi etre inerte au sens Avalonia : la ligne est
    /// grisee et la case ne repond pas.
    /// </summary>
    [AvaloniaFact]
    public void Un_reglage_desactive_a_sa_case_inactive()
    {
        var (_, _, view) = BuildProbeWindow(open: true, rows:
        [
            new("cloudSave", "Sauvegarde cloud", SkiaLayer.SettingRowKind.Toggle, false, false, [], 0, 0, 0, "", ""),
        ]);

        var box = view.GetVisualDescendants().OfType<CheckBox>()
            .First(c => c.DataContext is SettingRowViewModel);

        Assert.False(box.IsEnabled);
    }

    private static (Window Window, ProbeMapControl Map, SettingsPopupView View) BuildProbeWindow(
        bool open, SkiaLayer.SettingRowSnapshot[]? rows = null)
    {
        var host = new GameRuntimeHost(new SkiaLayer.SkiaGameRuntime());
        var map = new ProbeMapControl();
        var vm = new SettingsPopupViewModel(host);

        // Pas de partie en cours : les lignes sont alimentees a la main pour eprouver la vue.
        if (rows != null) vm.Panel.Apply(new SkiaLayer.SettingsPanelSnapshot(rows));

        var view = new SettingsPopupView(vm) { IsVisible = open };

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
