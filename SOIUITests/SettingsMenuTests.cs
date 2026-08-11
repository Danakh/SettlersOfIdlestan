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

public class SettingsMenuItemViewModelTests
{
    [Fact]
    public void Un_separateur_n_est_pas_cliquable()
    {
        var separator = new SettingsMenuItemViewModel(new SkiaLayer.SettingsMenuItemSnapshot("", "", true));
        var item = new SettingsMenuItemViewModel(new SkiaLayer.SettingsMenuItemSnapshot("menu_settings", "Paramètres", false));

        Assert.False(separator.IsClickable);
        Assert.True(item.IsClickable);
    }
}

public class SettingsMenuViewModelTests
{
    [Fact]
    public void Sans_partie_en_cours_le_menu_est_ferme()
    {
        using var host = new GameRuntimeHost(new SkiaLayer.SkiaGameRuntime());
        var vm = new SettingsMenuViewModel(host);

        vm.Refresh();

        Assert.False(vm.IsOpen);
        Assert.Empty(vm.Items);
    }

    [Fact]
    public void Une_composition_inchangee_ne_reconstruit_pas_les_items()
    {
        using var host = new GameRuntimeHost(new SkiaLayer.SkiaGameRuntime());
        var vm = new SettingsMenuViewModel(host);
        vm.Refresh();

        int rebuilds = 0;
        vm.Items.CollectionChanged += (_, _) => rebuilds++;
        vm.Refresh();
        vm.Refresh();

        Assert.Equal(0, rebuilds);
    }
}

public class SettingsMenuViewTests
{
    /// <summary>
    /// Le menu doit se refermer sur un clic ailleurs, et ce clic ne doit pas atteindre la carte :
    /// c'est le role du capteur transparent, la ou le rendu Skia comparait le point au rectangle.
    /// </summary>
    [AvaloniaFact]
    public void Un_clic_hors_du_menu_n_atteint_pas_la_carte()
    {
        var (window, map, _) = BuildProbeWindow(open: true);

        window.MouseDown(new Point(400, 400), MouseButton.Left);
        window.MouseUp(new Point(400, 400), MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(0, map.PointerPressedCount);
    }

    /// Controle negatif : menu ferme, la carte redevient cliquable partout.
    [AvaloniaFact]
    public void Menu_ferme_les_clics_atteignent_la_carte()
    {
        var (window, map, _) = BuildProbeWindow(open: false);

        window.MouseDown(new Point(400, 400), MouseButton.Left);
        window.MouseUp(new Point(400, 400), MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(1, map.PointerPressedCount);
    }

    [AvaloniaFact]
    public void Les_items_et_separateurs_sont_materialises_a_leur_hauteur()
    {
        var (_, _, view) = BuildProbeWindow(open: true, items:
        [
            new("menu_settings", "Paramètres", false),
            new("", "", true),
            new("menu_save_game", "Enregistrer", false),
        ]);

        var rows = view.GetVisualDescendants().OfType<Border>()
            .Where(b => b.DataContext is SettingsMenuItemViewModel)
            .ToList();

        Assert.Equal(3, rows.Count);
        // Un separateur est nettement plus bas qu'un item : c'est ce qui les distingue a l'oeil.
        Assert.Equal(new[] { 35d, 10d, 35d }, rows.Select(r => r.Height));
    }

    private static (Window Window, ProbeMapControl Map, SettingsMenuView View) BuildProbeWindow(
        bool open, SkiaLayer.SettingsMenuItemSnapshot[]? items = null)
    {
        var host = new GameRuntimeHost(new SkiaLayer.SkiaGameRuntime());
        var map = new ProbeMapControl();
        var vm = new SettingsMenuViewModel(host);

        // Pas de partie en cours : les items sont alimentes a la main pour eprouver la vue.
        foreach (var item in items ?? []) vm.Items.Add(new SettingsMenuItemViewModel(item));

        var view = new SettingsMenuView(vm, topOffset: 40) { IsVisible = open };

        // Le capteur se pose a part dans l'arbre, comme le fait GameView : sous la barre du haut
        // pour l'engrenage, alors que la boite passe au-dessus des panneaux lateraux.
        view.Catcher.IsVisible = open;

        var window = new Window
        {
            Width = 800,
            Height = 600,
            Content = new Panel { Children = { map, view.Catcher, view } },
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return (window, map, view);
    }
}
