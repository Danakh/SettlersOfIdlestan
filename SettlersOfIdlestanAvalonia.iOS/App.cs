using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;

namespace SettlersOfIdlestanAvalonia.iOS;

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
        // Comme le head navigateur : pas de fenetre a gerer, le jeu est la vue unique.
        if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
            singleView.MainView = new IosGameView();

        base.OnFrameworkInitializationCompleted();
    }
}
