using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using SettlersOfIdlestanUI;
using SettlersOfIdlestanUI.Controls;
using SettlersOfIdlestanUI.ViewModels;
using Xunit;
using SkiaLayer = SettlersOfIdlestanSkia.Services;

namespace SOIUITests;

public class InvestmentRowViewModelTests
{
    [Theory]
    [InlineData(0, 100, false, 0d)]
    [InlineData(50, 100, false, 0.5d)]
    [InlineData(100, 100, true, 1d)]
    // Depassement possible quand le cout baisse apres investissement : la barre sature.
    [InlineData(150, 100, true, 1d)]
    // Cout nul : ne doit pas diviser par zero.
    [InlineData(0, 0, false, 0d)]
    public void La_progression_est_bornee(long invested, long required, bool done, double expected)
    {
        var row = new InvestmentRowViewModel(
            new SkiaLayer.InvestmentRowSnapshot("Wood", "Wood", "Bois", invested, required, true, done));

        Assert.Equal(expected, row.Progress, 5);
        Assert.Equal($"{invested}/{required}", row.AmountLabel);
    }

    [Fact]
    public void Mettre_a_jour_une_ligne_notifie_les_proprietes_derivees()
    {
        var row = new InvestmentRowViewModel(
            new SkiaLayer.InvestmentRowSnapshot("Wood", "Wood", "Bois", 0, 100, true, false));

        var changed = new List<string?>();
        row.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        row.Apply(new SkiaLayer.InvestmentRowSnapshot("Wood", "Wood", "Bois", 50, 100, true, false));

        Assert.Contains(nameof(InvestmentRowViewModel.AmountLabel), changed);
        Assert.Contains(nameof(InvestmentRowViewModel.Progress), changed);
    }
}

public class MonumentPanelViewTests
{
    [Fact]
    public void Sans_monument_selectionne_le_panneau_est_masque()
    {
        using var host = new GameRuntimeHost(new SkiaLayer.SkiaGameRuntime());
        var vm = new MonumentPanelViewModel(host);

        vm.Refresh();

        Assert.False(vm.IsVisible);
        Assert.Empty(vm.Rows);
    }

    /// Le panneau lateral etait l'une des sources du bug d'origine : il devait se declarer
    /// dans IsPointBlockedByUI pour ne pas laisser le clic atteindre la carte derriere.
    [AvaloniaFact]
    public void Un_clic_sur_le_panneau_n_atteint_pas_la_carte()
    {
        using var host = new GameRuntimeHost(new SkiaLayer.SkiaGameRuntime());
        using var icons = new SvgIconCache();
        var map = new ProbeMapControl();

        var vm = new MonumentPanelViewModel(host);
        var panel = new MonumentPanelView(vm, icons)
        {
            // Le VM le masquerait (pas de partie en cours) : on force l'affichage pour
            // eprouver le hit-testing, qui est l'objet du test.
            IsVisible = true,
        };

        var window = new Window
        {
            Width = 800,
            Height = 600,
            Content = new Panel { Children = { map, panel } },
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var bounds = panel.Bounds;
        Assert.True(bounds.Width > 0, "Le panneau occupe zero pixel : le test ne prouverait rien.");
        var inside = new Point(bounds.X + bounds.Width / 2, bounds.Y + 20);

        window.MouseDown(inside, MouseButton.Left);
        window.MouseUp(inside, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(0, map.PointerPressedCount);
    }
}
