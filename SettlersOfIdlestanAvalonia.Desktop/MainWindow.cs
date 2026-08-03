using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Media;
using SettlersOfIdlestan.Controller.Store;
// Les services desktop sont partages par lien avec le head OpenTK et conservent
// donc son namespace tant que ce projet existe (cf. csproj).
using SettlersOfIdlestanOpenTK.Services;
using SettlersOfIdlestanOpenTK.Services.Store;
using SettlersOfIdlestanSkia.Services;
using SettlersOfIdlestanUI.Controls;

namespace SettlersOfIdlestanAvalonia.Desktop;

/// <summary>
/// Head Avalonia du jeu, en remplacement de la fenetre OpenTK.
/// Reprend le meme contrat d'hote : creation du runtime, branchement du store, plein ecran.
/// </summary>
public sealed class MainWindow : Window
{
    private readonly SkiaGameRuntime _runtime = new();
    private readonly StoreController? _storeController;

    public MainWindow()
    {
        Title = "Settlers of Idlestan";
        Width = 1280;
        Height = 720;
        Background = Brushes.Black;

        _runtime.QuitRequested += Close;
        _runtime.DiscordLinkClicked += OpenUrl;
        _runtime.FullscreenStateChanged += ApplyFullscreen;

        var args = Environment.GetCommandLineArgs();
        bool allowDebug = false;
#if DEBUG
        allowDebug = args.Contains("--debug");
#endif
        bool demoMode = args.Contains("--demo");

        _storeController = new StoreController([new StoreServiceSteam()]);
        _runtime.Initialize(new DesktopFileSystemService(), allowDebug, demoMode, _storeController);

        Content = new GameRuntimeControl(_runtime);

        if (_runtime.IsFullscreenEnabled) ApplyFullscreen(true);
    }

    private void ApplyFullscreen(bool fullscreen) =>
        WindowState = fullscreen ? WindowState.FullScreen : WindowState.Normal;

    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Impossible d'ouvrir {url} : {ex.Message}");
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _runtime.Dispose();
        _storeController?.Dispose();
    }
}
