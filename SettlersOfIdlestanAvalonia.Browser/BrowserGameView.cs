using Avalonia.Controls;
using Avalonia.Media;
using SettlersOfIdlestanAvalonia.Browser.Services;
using SettlersOfIdlestanSkia.Services;
using SettlersOfIdlestanUI;
using SettlersOfIdlestanUI.Views;

namespace SettlersOfIdlestanAvalonia.Browser;

/// <summary>
/// Racine du head navigateur, pendant de <c>MainWindow</c> cote Desktop : cree le runtime,
/// le branche sur les APIs du navigateur, et affiche la vue de jeu partagee.
///
/// Contrairement au head Blazor, rien ici ne route l'input ni le rendu : Avalonia.Browser
/// s'en charge, y compris la molette, le tactile et le facteur DPI.
/// </summary>
public sealed class BrowserGameView : Border
{
    private readonly SkiaGameRuntime _runtime = new();
    private readonly GameRuntimeHost _host;

    public BrowserGameView()
    {
        Background = Brushes.Black;

        _runtime.QuitRequested += BrowserInterop.CloseWindow;
        _runtime.DiscordLinkClicked += BrowserInterop.OpenUrl;
        _runtime.FullscreenStateChanged += BrowserInterop.SetFullscreen;

        try
        {
            _runtime.Initialize(new BrowserFileSystemService(), demoMode: BrowserInterop.HasQueryFlag("demo"));
        }
        catch (Exception ex)
        {
            _runtime.NotifyError(ex);
            BrowserInterop.LogError(ex.ToString());
        }

        _host = new GameRuntimeHost(_runtime);
        Child = new GameView(_host);

        // Un navigateur refuse le plein ecran hors geste utilisateur : le reglage sauvegarde ne
        // peut pas etre applique au demarrage, on le remet donc a false pour que l'affichage du
        // reglage corresponde a l'etat reel de la page.
        if (_runtime.IsFullscreenEnabled) _ = _runtime.SyncFullscreenSetting(false);

        BrowserInterop.RegisterVisibilityHandler(hiddenSeconds => _host.NotifyPageVisible(hiddenSeconds));
        BrowserInterop.RegisterFullscreenHandler(isFullscreen => _host.SyncFullscreenSetting(isFullscreen));

        BrowserInterop.AppReady();
    }
}
