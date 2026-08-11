using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using SettlersOfIdlestanUI;

namespace SettlersOfIdlestanAvalonia.Browser;

public sealed class App : Application
{
    public override void Initialize() => GameTheme.Apply(this);

    public override void OnFrameworkInitializationCompleted()
    {
        // Le navigateur n'a pas de fenetre a gerer : le jeu est la vue unique de la page.
        if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
            singleView.MainView = new BrowserGameView();

        base.OnFrameworkInitializationCompleted();
    }
}
