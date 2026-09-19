using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Media;
using SettlersOfIdlestan.Controller.Store;
using SettlersOfIdlestan.Model.Game;
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
#if DEMO
        // Build produite par install\build_desktop_demo_*.bat (-p:DemoBuild=true) : le binaire
        // EST la demo, le drapeau ne se negocie pas en ligne de commande.
        bool demoMode = true;
#else
        bool demoMode = args.Contains("--demo");
#endif

        _storeController = new StoreController([new StoreServiceSteam()]);
        // La fenetre se passe elle-meme : c'est d'elle que le service tire le selecteur de
        // fichier natif de l'export/import. Elle n'est pas encore ouverte ici, mais le
        // StorageProvider n'est resolu qu'au clic. Les deux delegues donnent au selecteur le
        // dernier dossier utilise, garde dans les reglages : eux aussi ne sont lus qu'au clic,
        // donc bien apres l'initialisation du runtime qui recoit ce service.
        var fileSystem = new DesktopFileSystemService(
            this,
            () => _runtime.LastSaveDirectory,
            _runtime.SetLastSaveDirectory);
        // canQuit : ce head a une fenetre a fermer, le menu de l'engrenage peut donc proposer
        // « Quitter le jeu ». QuitRequested est deja branche sur Close plus haut.
        // Le service audio se construit avant l'initialisation : c'est elle qui charge les
        // bruitages et applique le volume des reglages relus sur le disque.
        _runtime.Initialize(fileSystem, allowDebug, demoMode, _storeController, canQuit: true,
            audioService: new DesktopAudioService());

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
            // Aucun navigateur associe, ou shell verrouille : le clic sur le lien Discord ne fait
            // visiblement rien. Debug.WriteLine etant supprime en Release, l'echec etait muet.
            GameLog.Error(nameof(MainWindow), nameof(OpenUrl), ex);
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
