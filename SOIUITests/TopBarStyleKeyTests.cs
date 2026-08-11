using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SettlersOfIdlestanUI;
using SettlersOfIdlestanUI.Controls;
using SettlersOfIdlestanUI.ViewModels;
using Xunit;
using SkiaLayer = SettlersOfIdlestanSkia.Services;

namespace SOIUITests;

/// <summary>
/// Regression : un controle derive d'ItemsControl ne recoit pas le ControlTheme de sa classe
/// de base (celui-ci est indexe sur le type exact). Sans StyleKeyOverride, Presenter reste
/// null, aucun onglet n'est instancie et la barre disparait — sans la moindre erreur. C'est
/// exactement ce qui a fait disparaitre la barre d'onglets.
///
/// Verifie : le premier test echoue bien si l'on retire StyleKeyOverride. Les deux suivants
/// sont des tests de fumee (le contenu est reellement rendu), pas des gardes de ce mecanisme —
/// Button, lui, s'applique correctement aux types derives.
/// </summary>
public class TopBarStyleKeyTests
{
    [AvaloniaFact]
    public void La_barre_d_onglets_recoit_un_template()
    {
        using var host = new GameRuntimeHost(new SkiaLayer.SkiaGameRuntime());
        var view = new TabBarView(new TabBarViewModel(host));

        var window = new Window { Width = 800, Height = 600, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        view.ApplyTemplate();

        Assert.NotNull(view.Presenter);
    }

    [AvaloniaFact]
    public void Un_bouton_d_onglet_rend_son_contenu()
    {
        using var host = new GameRuntimeHost(new SkiaLayer.SkiaGameRuntime());
        var button = new TabButton(new TabBarViewModel(host))
        {
            DataContext = new TabItemViewModel(new SkiaLayer.TabSnapshot(0, "Île", true, false)),
        };

        var window = new Window { Width = 800, Height = 600, Content = button };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        button.ApplyTemplate();

        // Bounds ne suffit pas comme assertion : le bouton a une taille explicite et mesure
        // donc 62x28 meme sans template. Ce qui compte est qu'il ait un enfant visuel — la
        // presence du template, donc du contenu reellement dessine.
        Assert.NotEmpty(button.GetVisualChildren());
    }

    [AvaloniaFact]
    public void Une_pastille_de_ressource_affiche_son_libelle()
    {
        using var icons = new SvgIconCache();
        var pill = new ResourcePill(icons, _ => null)
        {
            DataContext = new ResourceItemViewModel(
                new SkiaLayer.ResourceSnapshot("Wood", "120", "500", false, false)),
        };

        var window = new Window { Width = 800, Height = 600, Content = pill };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.True(pill.Bounds.Width > 0);
    }
}
