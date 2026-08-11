using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using SettlersOfIdlestanUI;

namespace SettlersOfIdlestanAvalonia.Desktop;

public sealed class App : Application
{
    public override void Initialize() => GameTheme.Apply(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
            desktop.ShutdownMode = Avalonia.Controls.ShutdownMode.OnMainWindowClose;
        }

        base.OnFrameworkInitializationCompleted();
    }
}
