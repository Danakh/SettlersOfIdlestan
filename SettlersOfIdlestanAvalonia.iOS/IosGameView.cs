using Avalonia.Controls;
using Avalonia.Media;
using Foundation;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestanAvalonia.iOS.Services;
using SettlersOfIdlestanSkia.Services;
using SettlersOfIdlestanUI;
using SettlersOfIdlestanUI.Views;
using UIKit;

namespace SettlersOfIdlestanAvalonia.iOS;

/// <summary>
/// Racine du head iOS, pendant de <c>MainWindow</c> (Desktop) et de <c>BrowserGameView</c> :
/// cree le runtime, le branche sur les APIs du systeme, et affiche la vue de jeu partagee.
///
/// Comme sous navigateur, rien ici ne route l'input ni le rendu — Avalonia.iOS s'en charge,
/// tactile et facteur d'echelle Retina compris.
/// </summary>
public sealed class IosGameView : Border
{
    private readonly SkiaGameRuntime _runtime = new();
    private readonly GameRuntimeHost _host;

    // Conserves : NSNotificationCenter ne retient pas ses observateurs, un token collecte
    // arreterait silencieusement de recevoir les notifications.
    private readonly NSObject _didEnterBackground;
    private readonly NSObject _willEnterForeground;

    private DateTime? _backgroundedAt;

    public IosGameView()
    {
        Background = Brushes.Black;

        // iOS interdit de terminer son propre processus (motif de rejet a la revue) : le bouton
        // Quitter du jeu n'a donc pas d'effet ici.
        _runtime.DiscordLinkClicked += OpenUrl;

        try
        {
            _runtime.Initialize(new IosFileSystemService());
        }
        catch (Exception ex)
        {
            _runtime.NotifyError(ex);
            Console.WriteLine(ex);
        }

        _host = new GameRuntimeHost(_runtime);
        Child = new GameView(_host);

        // Une app iOS occupe toujours tout l'ecran : on aligne le reglage sur cette realite,
        // sans quoi il afficherait "desactive" alors que le jeu est bel et bien plein ecran.
        if (!_runtime.IsFullscreenEnabled) _ = _runtime.SyncFullscreenSetting(true);

        // Meme probleme qu'un onglet de navigateur en arriere-plan : iOS gele l'app, la boucle
        // de jeu ne tourne plus, et le temps de jeu derive du temps reel. On rattrape l'ecart
        // au retour au premier plan.
        _didEnterBackground = NSNotificationCenter.DefaultCenter.AddObserver(
            UIApplication.DidEnterBackgroundNotification, _ => _backgroundedAt = DateTime.UtcNow);

        _willEnterForeground = NSNotificationCenter.DefaultCenter.AddObserver(
            UIApplication.WillEnterForegroundNotification, _ =>
            {
                if (_backgroundedAt is not { } since) return;
                _backgroundedAt = null;
                _host.NotifyPageVisible((DateTime.UtcNow - since).TotalSeconds);
            });
    }

    private static void OpenUrl(string url)
    {
        try
        {
            UIApplication.SharedApplication.OpenUrl(new NSUrl(url), new NSDictionary(), null);
        }
        catch (Exception ex)
        {
            // Console.WriteLine n'atteint personne sur un appareil de joueur : le lien Discord
            // semble simplement inerte. Le journal, lui, part avec le rapport de bug.
            GameLog.Error(nameof(IosGameView), nameof(OpenUrl), ex);
        }
    }
}
