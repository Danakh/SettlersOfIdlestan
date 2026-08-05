using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;

namespace SettlersOfIdlestanAvalonia.Browser;

public sealed class App : Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());

        // Apres le theme : ces styles corrigent ce que Fluent impose aux boutons du jeu.
        Styles.Add(SettlersOfIdlestanUI.GameControlStyles.Create());
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Le navigateur n'a pas de fenetre a gerer : le jeu est la vue unique de la page.
        if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
            singleView.MainView = new BrowserGameView();

        base.OnFrameworkInitializationCompleted();
    }
}
