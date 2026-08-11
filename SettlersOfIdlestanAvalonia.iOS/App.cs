using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using SettlersOfIdlestanUI;

namespace SettlersOfIdlestanAvalonia.iOS;

public sealed class App : Application
{
    public override void Initialize() => GameTheme.Apply(this);

    public override void OnFrameworkInitializationCompleted()
    {
        // Comme le head navigateur : pas de fenetre a gerer, le jeu est la vue unique.
        if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
            singleView.MainView = new IosGameView();

        base.OnFrameworkInitializationCompleted();
    }
}
