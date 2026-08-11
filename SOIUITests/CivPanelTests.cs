using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SettlersOfIdlestanUI;
using SettlersOfIdlestanUI.Controls;
using SettlersOfIdlestanUI.ViewModels;
using Xunit;
using SkiaLayer = SettlersOfIdlestanSkia.Services;

namespace SOIUITests;

public class CivActionViewModelTests
{
    private static SkiaLayer.CivActionSnapshot Action(bool enabled = true, bool highlighted = false) =>
        new(SkiaLayer.CivPanelSnapshot.KeyPrestige, "Prestige (+12)", enabled, highlighted,
            IconName: null, Glyph: null, TooltipLines: ["Passer a l'ile suivante"]);

    [Theory]
    [InlineData(true, false, CivActionVisual.Normal)]
    [InlineData(false, false, CivActionVisual.Disabled)]
    [InlineData(true, true, CivActionVisual.Highlighted)]
    // Une action indisponible reste indisponible meme si elle est en cours : le refus prime.
    [InlineData(false, true, CivActionVisual.Disabled)]
    public void Le_visuel_se_deduit_de_l_etat(bool enabled, bool highlighted, CivActionVisual expected)
    {
        Assert.Equal(expected, new CivActionViewModel(Action(enabled, highlighted)).Visual);
    }

    [Fact]
    public void Changer_de_disponibilite_notifie_le_visuel()
    {
        var action = new CivActionViewModel(Action(enabled: true));
        var changed = new List<string?>();
        action.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        action.Apply(Action(enabled: false));

        Assert.Contains(nameof(CivActionViewModel.Visual), changed);
        Assert.Equal(CivActionVisual.Disabled, action.Visual);
    }

    [Fact]
    public void Les_lignes_d_infobulle_sont_jointes_par_des_retours_a_la_ligne()
    {
        var action = new CivActionViewModel(new SkiaLayer.CivActionSnapshot(
            SkiaLayer.CivPanelSnapshot.KeyWonder, "Merveille", false, false, null, null,
            ["Merveille", "Surface uniquement"]));

        Assert.Equal("Merveille\nSurface uniquement", action.Tooltip);
    }
}

public class CivPanelViewModelTests
{
    [Fact]
    public void Sans_partie_en_cours_le_panneau_est_masque()
    {
        using var host = new GameRuntimeHost(new SkiaLayer.SkiaGameRuntime());
        var vm = new CivPanelViewModel(host);

        vm.Refresh();

        Assert.False(vm.IsVisible);
        Assert.Empty(vm.Actions);
        Assert.Empty(vm.IconActions);
        Assert.Empty(vm.Toggles);
        Assert.False(vm.HasSeparator);
    }
}

public class CivPanelViewTests
{
    /// Le panneau civilisation est le premier ancre a gauche : un UserControl sans alignement
    /// explicite s'etirerait sur toute la fenetre et avalerait les clics destines a la carte.
    [AvaloniaFact]
    public void Un_clic_sur_le_panneau_n_atteint_pas_la_carte()
    {
        var (window, map, panel, _) = BuildProbeWindow();

        var bounds = panel.Bounds;
        Assert.True(bounds.Width > 0, "Le panneau occupe zero pixel : le test ne prouverait rien.");
        var inside = new Point(bounds.X + 5, bounds.Y + bounds.Height / 2);

        window.MouseDown(inside, MouseButton.Left);
        window.MouseUp(inside, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(0, map.PointerPressedCount);
    }

    /// Controle negatif : sans lui, un panneau qui avalerait tout passerait le test precedent.
    [AvaloniaFact]
    public void Un_clic_a_droite_du_panneau_atteint_la_carte()
    {
        var (window, map, panel, _) = BuildProbeWindow();

        var outside = new Point(panel.Bounds.Right + 200, 400);

        window.MouseDown(outside, MouseButton.Left);
        window.MouseUp(outside, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(1, map.PointerPressedCount);
    }

    /// <summary>
    /// Le clic doit atteindre le ViewModel, pas seulement etre absorbe par le panneau.
    /// Execute() relit l'instantane : sans partie en cours, la liste peuplee a la main est donc
    /// videe — un effet observable qui prouve que le bouton a bien route vers le ViewModel.
    /// </summary>
    [AvaloniaFact]
    public void Un_clic_sur_un_bouton_d_action_declenche_l_action_et_pas_la_carte()
    {
        var (window, map, panel, vm) = BuildProbeWindow();

        var button = panel.GetVisualDescendants().OfType<Button>()
            .First(b => b.DataContext is CivActionViewModel);
        var center = button.TranslatePoint(
            new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), window);
        Assert.NotNull(center);

        window.MouseDown(center!.Value, MouseButton.Left);
        window.MouseUp(center!.Value, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.Empty(vm.Actions);
        Assert.Equal(0, map.PointerPressedCount);
    }

    /// <summary>
    /// Une action indisponible garde son infobulle — c'est elle qui dit pourquoi l'action est
    /// bloquee. Un bouton Avalonia desactive ne recevrait plus le pointeur et ne l'afficherait
    /// donc jamais : le bouton reste actif, seule son apparence change, et le renderer refuse.
    /// </summary>
    [AvaloniaFact]
    public void Un_bouton_indisponible_reste_actif_et_conserve_son_infobulle()
    {
        var (_, _, panel, _) = BuildProbeWindow(enabled: false);

        var button = panel.GetVisualDescendants().OfType<Button>()
            .First(b => b.DataContext is CivActionViewModel);

        Assert.True(button.IsEnabled);
        Assert.Equal(Color.FromRgb(70, 70, 78), Assert.IsType<SolidColorBrush>(button.Background).Color);
        Assert.Equal("Merveille\nSurface uniquement", ToolTip.GetTip(button));
    }

    private static (Window Window, ProbeMapControl Map, CivPanelView Panel, CivPanelViewModel ViewModel)
        BuildProbeWindow(bool enabled = true)
    {
        var host = new GameRuntimeHost(new SkiaLayer.SkiaGameRuntime());
        var icons = new SvgIconCache();
        var map = new ProbeMapControl();

        var vm = new CivPanelViewModel(host);
        var panel = new CivPanelView(vm, icons)
        {
            // Le VM le masquerait (pas de partie en cours) : on force l'affichage pour eprouver
            // le hit-testing, qui est l'objet de ces tests.
            IsVisible = true,
        };

        // Meme raison : la grille d'actions est alimentee a la main, l'instantane etant vide.
        vm.Actions.Add(new CivActionViewModel(new SkiaLayer.CivActionSnapshot(
            SkiaLayer.CivPanelSnapshot.KeyWonder, "Merveille", enabled, false, null, null,
            ["Merveille", "Surface uniquement"])));

        var window = new Window
        {
            Width = 800,
            Height = 600,
            Content = new Panel { Children = { map, panel } },
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return (window, map, panel, vm);
    }
}
