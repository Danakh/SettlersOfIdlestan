using Avalonia;
using Avalonia.Headless;
using Avalonia.Themes.Fluent;

[assembly: AvaloniaTestApplication(typeof(SOIUITests.TestAppBuilder))]

namespace SOIUITests;

public sealed class HeadlessTestApp : Application
{
    public override void Initialize() => Styles.Add(new FluentTheme());
}

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder
        .Configure<HeadlessTestApp>()
        .UseSkia()
        // UseHeadlessDrawing = false : on veut le vrai backend Skia, sinon le lease
        // SkiaSharp n'existe pas et les tests de rendu ne prouveraient rien.
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}
