using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System.Runtime.InteropServices;
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

/// <summary>
/// Composition reelle du panneau de reglages, telle que SettingsContentPanel la produit en jeu.
/// Sert aux tests de taille : c'est elle qui doit tenir dans la hauteur fixe du panneau.
/// </summary>
internal static class RealSettingsSnapshot
{
    private static SkiaLayer.SettingRowSnapshot Toggle(string key, string label, SkiaLayer.SettingsTab tab) =>
        new(key, label, SkiaLayer.SettingRowKind.Toggle, true, false, [], 0, 0, 0, "", "", tab);

    private static SkiaLayer.SettingRowSnapshot Slider(string key, string label, SkiaLayer.SettingsTab tab) =>
        new(key, label, SkiaLayer.SettingRowKind.Slider, true, false, [], 1, 0, 2, "x1,0", "", tab);

    private static SkiaLayer.SettingRowSnapshot Choice(string key, string label, string[] options, SkiaLayer.SettingsTab tab) =>
        new(key, label, SkiaLayer.SettingRowKind.Choice, true, false,
            [.. options.Select((o, i) => new SkiaLayer.SettingChoiceSnapshot($"c{i}", o, i == 0))],
            0, 0, 0, "", "", tab);

    /// <summary>
    /// Les lignes du jeu livre, avec les libelles francais (les plus longs des deux langues) et
    /// une sauvegarde cloud connectee, dont le libelle est le seul a passer a la ligne. Les deux
    /// lignes du mode debogage sont volontairement absentes : elles debordent et defilent.
    /// </summary>
    public static SkiaLayer.SettingsPanelSnapshot Create() => new(
    [
        Choice("language", "Langue", ["English", "Français"], SkiaLayer.SettingsTab.General),
        Toggle("pauseAfterPrestige", "Pause après prestige", SkiaLayer.SettingsTab.General),
        Toggle("showTutorial", "Afficher le tutoriel", SkiaLayer.SettingsTab.General),
        Toggle("cloudSave", "Sauvegarde Cloud [connecté à : Steam]", SkiaLayer.SettingsTab.General),

        Toggle("fullscreen", "Plein écran", SkiaLayer.SettingsTab.Display),
        Toggle("menuPosition", "Menu en bas", SkiaLayer.SettingsTab.Display),
        Slider("uiScale", "Échelle de l'interface", SkiaLayer.SettingsTab.Display),
        Toggle("harvestParticles", "Particules de récolte", SkiaLayer.SettingsTab.Display),
        Toggle("militaryStats", "Stats militaires des villes", SkiaLayer.SettingsTab.Display),
        Toggle("harvestCooldown", "Indicateurs de temps de récolte", SkiaLayer.SettingsTab.Display),
        Toggle("corruptionDominion", "Corruption et dominion", SkiaLayer.SettingsTab.Display),
        Choice("numberFormat", "Affichage des grands nombres",
            ["Classique", "Scientifique", "Ingénieur"], SkiaLayer.SettingsTab.Display),

        Toggle("soundEnabled", "Bruitages", SkiaLayer.SettingsTab.Sound),
        Slider("soundVolume", "Volume des bruitages", SkiaLayer.SettingsTab.Sound),
        Toggle("soundCombat", "Bruitages de combat", SkiaLayer.SettingsTab.Sound),
        Toggle("soundToast", "Bruitages des notifications", SkiaLayer.SettingsTab.Sound),
        Toggle("soundAchievement", "Fanfare des succès", SkiaLayer.SettingsTab.Sound),
        Toggle("soundCity", "Bruitages de fondation de villes", SkiaLayer.SettingsTab.Sound),
        Toggle("soundCityLost", "Bruitage de perte de ville", SkiaLayer.SettingsTab.Sound),
        Toggle("soundHarvest", "Bruitages de récolte manuelle", SkiaLayer.SettingsTab.Sound),
    ],
    [
        new(SkiaLayer.SettingsTab.General, "Général"),
        new(SkiaLayer.SettingsTab.Display, "Affichage"),
        new(SkiaLayer.SettingsTab.Sound, "Son"),
    ]);

    /// <summary>
    /// La meme composition, plus les deux lignes du mode debogage : l'onglet Affichage deborde
    /// alors de la hauteur fixe, et l'ascenseur apparait.
    /// </summary>
    public static SkiaLayer.SettingsPanelSnapshot CreateOverflowing()
    {
        var full = Create();
        return new SkiaLayer.SettingsPanelSnapshot(
        [
            .. full.Rows,
            new("debugResolution", "Résolution fenêtre (debug)", SkiaLayer.SettingRowKind.TextInput,
                true, false, [], 0, 0, 0, "", "1920x1080", SkiaLayer.SettingsTab.Display),
            Toggle("exportTransparentBg", "Export PNG fond transparent (debug)", SkiaLayer.SettingsTab.Display),
        ], full.Tabs);
    }
}

/// <summary>
/// Taille du panneau de reglages. Deux exigences, toutes deux visibles a l'oeil : le cadre ne
/// doit pas changer de taille quand on change d'onglet, et la barre d'onglets ne doit pas bouger
/// sous le curseur. Les deux tenaient a la hauteur du panneau, qui suivait son contenu.
/// </summary>
public class SettingsPanelSizeTests
{
    /// <summary>
    /// Chaque onglet doit tenir dans la hauteur fixe de la zone des lignes, sans ascenseur : une
    /// ligne ajoutee a l'onglet le plus charge doit faire echouer ce test, pas apparaitre a
    /// moitie coupee en jeu.
    /// </summary>
    [AvaloniaFact]
    public void Chaque_onglet_tient_dans_la_hauteur_fixe()
    {
        var (panel, view, window) = BuildPanel();

        foreach (var tab in panel.Tabs.ToList())
        {
            panel.SelectTab(tab);
            Dispatcher.UIThread.RunJobs();
            window.Measure(new Size(900, 900));
            window.Arrange(new Rect(0, 0, 900, 900));
            Dispatcher.UIThread.RunJobs();

            double needed = view.GetVisualDescendants().OfType<ItemsControl>()
                .First(c => c.DataContext is SettingsPanelViewModel p && ReferenceEquals(p.Rows, c.ItemsSource))
                .DesiredSize.Height;

            Assert.True(needed <= SettingsPanelView.RowsHeight,
                $"L'onglet {tab.Tab} demande {needed:0} px pour {SettingsPanelView.RowsHeight:0} disponibles.");
        }
    }

    /// <summary>
    /// Quand un onglet deborde — le mode debogage ajoute ses lignes a « Affichage » — l'ascenseur
    /// apparait. Pose par-dessus le contenu au bord droit de sa zone, il recouvrait les controles
    /// des reglages, tous alignes a droite : bascules et boutons de choix devenaient
    /// partiellement inatteignables. Il doit desormais passer entierement a leur droite.
    /// </summary>
    [AvaloniaFact]
    public void L_ascenseur_ne_recouvre_pas_les_controles()
    {
        var (window, panel, box) = BuildPopup(RealSettingsSnapshot.CreateOverflowing());
        panel.SelectTab(panel.Tabs.Single(t => t.Tab == SkiaLayer.SettingsTab.Display));
        Relayout(window);

        var bar = window.GetVisualDescendants().OfType<ScrollBar>()
            .Single(s => s.Orientation == Orientation.Vertical && s.IsVisible);
        double barLeft = bar.TranslatePoint(default, window)!.Value.X;

        // Le controle le plus a droite de l'onglet : c'est lui que l'ascenseur mordait.
        double controlsRight = window.GetVisualDescendants().OfType<CheckBox>()
            .Where(c => c.DataContext is SettingRowViewModel && c.IsVisible)
            .Max(c => c.TranslatePoint(new Point(c.Bounds.Width, 0), window)!.Value.X);

        Assert.True(barLeft >= controlsRight,
            $"L'ascenseur commence a {barLeft:0} alors que les controles vont jusqu'a {controlsRight:0}.");

        // Et il reste dans le cadre : le panneau en etait a 30 px, l'ascenseur doit tenir dans
        // la moitie de cette bande, sans jamais la depasser.
        double boxRight = box.TranslatePoint(new Point(box.Bounds.Width, 0), window)!.Value.X;
        double barRight = bar.TranslatePoint(new Point(bar.Bounds.Width, 0), window)!.Value.X;
        Assert.InRange(boxRight - barRight, 0, 15);
    }

    /// <summary>
    /// L'ascenseur doit etre <b>dessine</b>, et pas seulement place. Sorti des limites du panneau
    /// pour degager les controles, il n'etait plus rendu du tout — la molette repondait, mais
    /// rien n'apparaissait a l'ecran. Les tests de position ne voyaient rien : eux mesurent la
    /// mise en page, qui restait juste. Celui-ci relit les pixels.
    /// </summary>
    [AvaloniaFact]
    public void L_ascenseur_est_reellement_dessine()
    {
        var (window, panel, _) = BuildPopup(RealSettingsSnapshot.CreateOverflowing());
        panel.SelectTab(panel.Tabs.Single(t => t.Tab == SkiaLayer.SettingsTab.Display));

        // L'ascenseur de Fluent s'efface tant que le pointeur n'est pas sur la zone : sans cela
        // la capture ne montrerait rien, quoi qu'on fasse.
        var scroll = window.GetVisualDescendants().OfType<ScrollViewer>().First();
        ScrollViewer.SetAllowAutoHide(scroll, false);
        Relayout(window);

        var bar = window.GetVisualDescendants().OfType<ScrollBar>()
            .Single(s => s.Orientation == Orientation.Vertical && s.IsVisible);
        int barLeft = (int)Math.Ceiling(bar.TranslatePoint(default, window)!.Value.X);
        int barRight = (int)(bar.TranslatePoint(new Point(bar.Bounds.Width, 0), window)!.Value.X);
        int y = (int)scroll.TranslatePoint(new Point(0, scroll.Bounds.Height / 2), window)!.Value.Y;

        using var frame = window.CaptureRenderedFrame()!;
        Assert.NotNull(frame);
        using var buffer = frame.Lock();
        var row = new byte[buffer.RowBytes];
        Marshal.Copy(buffer.Address + y * buffer.RowBytes, row, 0, buffer.RowBytes);

        // Le fond du panneau, lu juste a gauche de l'ascenseur : tout pixel qui s'en ecarte dans
        // sa bande est l'ascenseur lui-meme.
        (byte B, byte G, byte R) background = (row[(barLeft - 4) * 4], row[(barLeft - 4) * 4 + 1], row[(barLeft - 4) * 4 + 2]);

        int painted = 0;
        for (int x = barLeft; x < barRight; x++)
            if ((row[x * 4], row[x * 4 + 1], row[x * 4 + 2]) != background) painted++;

        Assert.True(painted > 0,
            $"Aucun pixel d'ascenseur entre x={barLeft} et x={barRight} (ligne y={y}, fond {background}).");
    }

    /// <summary>
    /// Le cadre du popup garde la meme taille d'un onglet a l'autre. Mesure sur le popup reel et
    /// non sur le panneau seul : c'est le cadre, centre a l'ecran, que le joueur voit grandir et
    /// retrecir, et le panneau seul s'etire au gre de son hote.
    /// </summary>
    [AvaloniaFact]
    public void Le_cadre_du_popup_garde_la_meme_taille_d_un_onglet_a_l_autre()
    {
        var (window, panel, box) = BuildPopup();

        var sizes = new List<Size>();
        foreach (var tab in panel.Tabs.ToList())
        {
            panel.SelectTab(tab);
            Relayout(window);
            sizes.Add(box.Bounds.Size);
        }

        Assert.Equal(3, sizes.Count);
        Assert.All(sizes, s => Assert.Equal(sizes[0], s));
        // Un cadre de hauteur nulle rendrait la comparaison vide de sens.
        Assert.True(sizes[0].Height > SettingsPanelView.RowsHeight, $"Cadre suspect : {sizes[0]}");
    }

    /// <summary>
    /// La barre d'onglets est ancree au-dessus de la zone qui defile, et le cadre ne bouge pas :
    /// un bouton d'onglet doit donc rester exactement au meme endroit de l'ecran. C'est le
    /// symptome que le joueur ressent — cliquer « Son » puis vouloir revenir sur « Affichage »
    /// alors que le bouton s'est deplace sous le curseur.
    /// </summary>
    [AvaloniaFact]
    public void La_barre_d_onglets_ne_bouge_pas()
    {
        var (window, panel, _) = BuildPopup();

        var positions = new List<Point>();
        foreach (var tab in panel.Tabs.ToList())
        {
            panel.SelectTab(tab);
            Relayout(window);

            var first = window.GetVisualDescendants().OfType<Button>()
                .First(b => b.DataContext is SettingsTabViewModel t && t.Tab == panel.Tabs[0].Tab);
            positions.Add(first.TranslatePoint(default, window) ?? default);
        }

        Assert.All(positions, p => Assert.Equal(positions[0], p));
        Assert.True(positions[0].Y > 0, $"Position suspecte : {positions[0]}");
    }

    private static void Relayout(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.Measure(new Size(window.Width, window.Height));
        window.Arrange(new Rect(0, 0, window.Width, window.Height));
        Dispatcher.UIThread.RunJobs();
    }

    private static (SettingsPanelViewModel Panel, SettingsPanelView View, Window Window) BuildPanel()
    {
        var panel = new SettingsPanelViewModel(_ => { }, (_, _) => { }, (_, _) => { }, (_, _) => { });
        panel.Apply(RealSettingsSnapshot.Create());

        var view = new SettingsPanelView(panel);
        var window = new Window { Width = 900, Height = 900, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return (panel, view, window);
    }

    /// <summary>Le popup de reglages reel, avec la composition du jeu livre par defaut.</summary>
    private static (Window Window, SettingsPanelViewModel Panel, Border Box) BuildPopup(
        SkiaLayer.SettingsPanelSnapshot? snapshot = null)
    {
        var host = new GameRuntimeHost(new SkiaLayer.SkiaGameRuntime());
        var vm = new SettingsPopupViewModel(host);
        vm.Panel.Apply(snapshot ?? RealSettingsSnapshot.Create());

        var view = new SettingsPopupView(vm) { IsVisible = true };
        var window = new Window { Width = 900, Height = 900, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        // Le cadre borde d'or : le seul Border du popup qui porte la largeur du panneau.
        var box = window.GetVisualDescendants().OfType<Border>()
            .First(b => b.Width == SettingsPopupView.BoxWidth);

        return (window, vm.Panel, box);
    }
}

/// <summary>
/// Onglets du panneau de reglages. La liste unique depassait la hauteur du popup : les reglages
/// sont ranges en General / Affichage / Son, et l'onglet affiche est un etat de vue, jamais
/// remonte au runtime.
/// </summary>
public class SettingsPanelTabTests
{
    private static SkiaLayer.SettingRowSnapshot Toggle(string key, SkiaLayer.SettingsTab tab) =>
        new(key, key, SkiaLayer.SettingRowKind.Toggle, true, false, [], 0, 0, 0, "", "", tab);

    private static readonly SkiaLayer.SettingsTabSnapshot[] ThreeTabs =
    [
        new(SkiaLayer.SettingsTab.General, "Général"),
        new(SkiaLayer.SettingsTab.Display, "Affichage"),
        new(SkiaLayer.SettingsTab.Sound,   "Son"),
    ];

    private static SkiaLayer.SettingsPanelSnapshot Snapshot() => new(
    [
        Toggle("showTutorial", SkiaLayer.SettingsTab.General),
        Toggle("fullscreen",   SkiaLayer.SettingsTab.Display),
        Toggle("soundEnabled", SkiaLayer.SettingsTab.Sound),
        Toggle("soundCombat",  SkiaLayer.SettingsTab.Sound),
    ], ThreeTabs);

    private static SettingsPanelViewModel Panel(List<string>? toggled = null) =>
        new(k => toggled?.Add(k), (_, _) => { }, (_, _) => { }, (_, _) => { });

    [Fact]
    public void Le_panneau_s_ouvre_sur_l_onglet_general()
    {
        var panel = Panel();
        panel.Apply(Snapshot());

        Assert.True(panel.HasTabs);
        Assert.Equal(["showTutorial"], panel.Rows.Select(r => r.Key));
        Assert.Equal(SkiaLayer.SettingsTab.General, panel.Tabs.Single(t => t.IsActive).Tab);
    }

    [Fact]
    public void Changer_d_onglet_ne_montre_que_ses_lignes()
    {
        var panel = Panel();
        panel.Apply(Snapshot());

        panel.SelectTab(panel.Tabs.Single(t => t.Tab == SkiaLayer.SettingsTab.Sound));

        Assert.Equal(["soundEnabled", "soundCombat"], panel.Rows.Select(r => r.Key));
        Assert.Equal(SkiaLayer.SettingsTab.Sound, panel.Tabs.Single(t => t.IsActive).Tab);
    }

    /// <summary>
    /// L'onglet choisi doit survivre aux instantanes suivants : ils tombent a chaque tick, et
    /// le panneau reviendrait sinon a « General » sous les doigts du joueur.
    /// </summary>
    [Fact]
    public void L_onglet_choisi_survit_aux_instantanes_suivants()
    {
        var panel = Panel();
        panel.Apply(Snapshot());
        panel.SelectTab(panel.Tabs.Single(t => t.Tab == SkiaLayer.SettingsTab.Display));

        panel.Apply(Snapshot());
        panel.Apply(Snapshot());

        Assert.Equal(["fullscreen"], panel.Rows.Select(r => r.Key));
    }

    /// <summary>Meme garantie qu'avant les onglets : pas de reconstruction a chaque tick.</summary>
    [Fact]
    public void Une_composition_inchangee_ne_reconstruit_ni_lignes_ni_onglets()
    {
        var panel = Panel();
        panel.Apply(Snapshot());
        panel.SelectTab(panel.Tabs.Single(t => t.Tab == SkiaLayer.SettingsTab.Sound));

        int rebuilds = 0;
        panel.Rows.CollectionChanged += (_, _) => rebuilds++;
        panel.Tabs.CollectionChanged += (_, _) => rebuilds++;
        panel.Apply(Snapshot());
        panel.Apply(Snapshot());

        Assert.Equal(0, rebuilds);
    }

    /// <summary>
    /// Le mode debogage ajoute ses lignes a l'onglet Affichage : la liste s'y reconstruit, et
    /// nulle part ailleurs.
    /// </summary>
    [Fact]
    public void Une_ligne_ajoutee_a_un_autre_onglet_ne_touche_pas_l_onglet_affiche()
    {
        var panel = Panel();
        panel.Apply(Snapshot());
        panel.SelectTab(panel.Tabs.Single(t => t.Tab == SkiaLayer.SettingsTab.Sound));

        int rebuilds = 0;
        panel.Rows.CollectionChanged += (_, _) => rebuilds++;

        var withDebug = new SkiaLayer.SettingsPanelSnapshot(
            [.. Snapshot().Rows, Toggle("exportTransparentBg", SkiaLayer.SettingsTab.Display)], ThreeTabs);
        panel.Apply(withDebug);

        Assert.Equal(0, rebuilds);
        Assert.Equal(["soundEnabled", "soundCombat"], panel.Rows.Select(r => r.Key));
    }

    /// <summary>
    /// La commande part avec la cle de la ligne, onglet ou pas : le routage ne connait que les
    /// cles, les onglets ne servent qu'a l'affichage.
    /// </summary>
    [Fact]
    public void Une_bascule_d_un_autre_onglet_relaie_sa_cle()
    {
        var toggled = new List<string>();
        var panel = Panel(toggled);
        panel.Apply(Snapshot());
        panel.SelectTab(panel.Tabs.Single(t => t.Tab == SkiaLayer.SettingsTab.Sound));

        panel.Toggle(panel.Rows[1]);

        Assert.Equal(["soundCombat"], toggled);
    }

    /// <summary>
    /// Un instantane sans onglets (compose a la main par les tests de vue, ou un hote qui n'en
    /// veut pas) montre toutes ses lignes et n'affiche pas de barre.
    /// </summary>
    [Fact]
    public void Sans_onglets_toutes_les_lignes_s_affichent()
    {
        var panel = Panel();
        panel.Apply(new SkiaLayer.SettingsPanelSnapshot(
        [
            Toggle("showTutorial", SkiaLayer.SettingsTab.General),
            Toggle("fullscreen",   SkiaLayer.SettingsTab.Display),
            Toggle("soundEnabled", SkiaLayer.SettingsTab.Sound),
        ]));

        Assert.False(panel.HasTabs);
        Assert.Equal(3, panel.Rows.Count);
    }

    /// <summary>Changer la langue relocalise aussi la barre d'onglets.</summary>
    [Fact]
    public void Changer_de_langue_relocalise_les_onglets()
    {
        var panel = Panel();
        panel.Apply(Snapshot());

        panel.Apply(new SkiaLayer.SettingsPanelSnapshot(Snapshot().Rows,
        [
            new(SkiaLayer.SettingsTab.General, "General"),
            new(SkiaLayer.SettingsTab.Display, "Display"),
            new(SkiaLayer.SettingsTab.Sound,   "Sound"),
        ]));

        Assert.Equal(["General", "Display", "Sound"], panel.Tabs.Select(t => t.Label));
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

    /// <summary>
    /// Les onglets doivent etre cliquables pour de vrai : un bouton par onglet, et le clic
    /// remplace les lignes affichees. Le ViewModel seul ne dit rien du cablage des liaisons.
    /// </summary>
    [AvaloniaFact]
    public void Cliquer_un_onglet_remplace_les_lignes_affichees()
    {
        var (_, _, view) = BuildProbeWindow(open: true, panel: new SkiaLayer.SettingsPanelSnapshot(
        [
            new("showTutorial", "Tutoriel", SkiaLayer.SettingRowKind.Toggle, true, false, [], 0, 0, 0, "", "",
                SkiaLayer.SettingsTab.General),
            new("soundEnabled", "Bruitages", SkiaLayer.SettingRowKind.Toggle, true, false, [], 0, 0, 0, "", "",
                SkiaLayer.SettingsTab.Sound),
        ],
        [
            new(SkiaLayer.SettingsTab.General, "Général"),
            new(SkiaLayer.SettingsTab.Sound, "Son"),
        ]));

        var tabButtons = view.GetVisualDescendants().OfType<Button>()
            .Where(b => b.DataContext is SettingsTabViewModel).ToList();
        Assert.Equal(2, tabButtons.Count);

        Assert.Equal(["Tutoriel"], VisibleRowLabels(view));

        tabButtons.First(b => ((SettingsTabViewModel)b.DataContext!).Tab == SkiaLayer.SettingsTab.Sound)
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["Bruitages"], VisibleRowLabels(view));
    }

    private static List<string> VisibleRowLabels(SettingsPopupView view) =>
        [.. view.GetVisualDescendants().OfType<CheckBox>()
            .Where(c => c.DataContext is SettingRowViewModel)
            .Select(c => ((SettingRowViewModel)c.DataContext!).Label)];

    private static (Window Window, ProbeMapControl Map, SettingsPopupView View) BuildProbeWindow(
        bool open, SkiaLayer.SettingRowSnapshot[]? rows = null, SkiaLayer.SettingsPanelSnapshot? panel = null)
    {
        var host = new GameRuntimeHost(new SkiaLayer.SkiaGameRuntime());
        var map = new ProbeMapControl();
        var vm = new SettingsPopupViewModel(host);

        // Pas de partie en cours : les lignes sont alimentees a la main pour eprouver la vue.
        if (rows != null) vm.Panel.Apply(new SkiaLayer.SettingsPanelSnapshot(rows));
        if (panel != null) vm.Panel.Apply(panel);

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
