using Avalonia;
using Avalonia.Browser;
using SettlersOfIdlestanAvalonia.Browser.Services;

namespace SettlersOfIdlestanAvalonia.Browser;

internal static class Program
{
    public static async Task Main(string[] args)
    {
        // L'interop d'abord : le runtime lit la sauvegarde des la construction de la vue.
        await BrowserInterop.InitializeAsync();

        await BuildAvaloniaApp().StartBrowserAppAsync("out");
    }

    public static AppBuilder BuildAvaloniaApp() => AppBuilder
        .Configure<App>()
        .LogToTrace();
}
