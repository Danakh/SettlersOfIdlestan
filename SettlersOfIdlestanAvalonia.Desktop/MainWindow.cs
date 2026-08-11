using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Media;
using SettlersOfIdlestan.Controller.Store;
using SettlersOfIdlestanAvalonia.Desktop.Services;
using SettlersOfIdlestanAvalonia.Desktop.Services.Store;
using SettlersOfIdlestanSkia.Services;
using SettlersOfIdlestanUI;
using SettlersOfIdlestanUI.Views;

namespace SettlersOfIdlestanAvalonia.Desktop;

/// <summary>
/// Head Avalonia du jeu, en remplacement de la fenetre OpenTK.
/// Reprend le meme contrat d'hote : creation du runtime, branchement du store, plein ecran.
/// </summary>
public sealed class MainWindow : Window
{
    private readonly SkiaGameRuntime _runtime = new();
    private readonly GameRuntimeHost _host;
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
        // La fenetre se passe elle-meme : c'est d'elle que le service tire le selecteur de
        // fichier natif de l'export/import. Elle n'est pas encore ouverte ici, mais le
        // StorageProvider n'est resolu qu'au clic.
        _runtime.Initialize(new DesktopFileSystemService(this), allowDebug, demoMode, _storeController);

        _host = new GameRuntimeHost(_runtime);
        Content = new GameView(_host);

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
        // Passe par l'hote : la fermeture peut survenir alors qu'un frame est en cours
        // de rendu sur le thread de rendu.
        _host.Dispose();
        _storeController?.Dispose();
    }
}
