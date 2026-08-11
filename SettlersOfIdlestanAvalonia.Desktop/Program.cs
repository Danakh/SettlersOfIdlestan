using Avalonia;
using SettlersOfIdlestanUI;

namespace SettlersOfIdlestanAvalonia.Desktop;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args) =>
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    // Referencee par le designer Avalonia et par Main.
    public static AppBuilder BuildAvaloniaApp() => AppBuilder
        .Configure<App>()
        .UsePlatformDetect()
        .WithGameFonts()
        .LogToTrace();
}
