using Avalonia;
using Avalonia.Headless;
using SettlersOfIdlestanUI;

[assembly: AvaloniaTestApplication(typeof(SOIUITests.TestAppBuilder))]

namespace SOIUITests;

public sealed class HeadlessTestApp : Application
{
    // Le theme complet du jeu, pas seulement Fluent : les tests doivent voir les memes
    // couleurs d'etat que les heads, variante sombre imposee comprise.
    public override void Initialize() => GameTheme.Apply(this);
}

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder
        .Configure<HeadlessTestApp>()
        .UseSkia()
        // Memes polices que les heads : sans cela les tests mesureraient du texte rendu avec
        // la police du systeme, absente sous WebAssembly.
        .WithGameFonts()
        // UseHeadlessDrawing = false : on veut le vrai backend Skia, sinon le lease
        // SkiaSharp n'existe pas et les tests de rendu ne prouveraient rien.
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}
