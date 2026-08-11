using System.Runtime.InteropServices.JavaScript;

namespace SettlersOfIdlestanAvalonia.Browser.Services;

/// <summary>
/// Pont vers les APIs du navigateur qu'Avalonia n'expose pas : stockage local, telechargement
/// de fichier, plein ecran, visibilite de l'onglet.
///
/// Tout le reste (rendu, pointeur, clavier, molette, DPI) est deja pris en charge par
/// Avalonia.Browser — c'est toute la difference avec le head Blazor, qui devait aussi
/// router l'input a la main.
/// </summary>
internal static partial class BrowserInterop
{
    private const string Module = "soiInterop";

    /// <summary>
    /// Charge le module d'interop. Doit etre attendu <b>avant</b> de demarrer Avalonia :
    /// le runtime lit la sauvegarde des sa construction, donc le stockage doit deja repondre.
    /// </summary>
    /// <remarks>
    /// Le chemin est relatif a dotnet.js, qui vit dans <c>_framework/</c> — d'ou le <c>../</c>.
    /// </remarks>
    public static Task InitializeAsync() => JSHost.ImportAsync(Module, "../soiInterop.js");

    // ── Stockage local ────────────────────────────────────────────────────────
    // Volontairement synchrone : SkiaGameRuntime.Initialize deroule ses Task avec
    // GetAwaiter().GetResult(), ce qui bloquerait le thread unique du WASM sur une
    // promesse non encore resolue.

    [JSImport("getStorageItem", Module)]
    public static partial string? GetStorageItem(string key);

    [JSImport("setStorageItem", Module)]
    public static partial void SetStorageItem(string key, string value);

    [JSImport("removeStorageItem", Module)]
    public static partial void RemoveStorageItem(string key);

    // ── Fichiers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Enregistrement explicite : le joueur choisit le fichier a ecrire quand le navigateur le
    /// permet, sinon la sauvegarde part en telechargement. Doit etre appele depuis la pile du
    /// clic — le selecteur exige une activation utilisateur recente.
    /// </summary>
    [JSImport("saveFilePicker", Module)]
    public static partial Task SaveFilePicker(string fileName, string content);

    [JSImport("openFilePicker", Module)]
    public static partial Task<string?> OpenFilePicker();

    // ── Fenetre ───────────────────────────────────────────────────────────────

    [JSImport("setFullscreen", Module)]
    public static partial void SetFullscreen(bool fullscreen);

    [JSImport("openUrl", Module)]
    public static partial void OpenUrl(string url);

    [JSImport("closeWindow", Module)]
    public static partial void CloseWindow();

    [JSImport("logError", Module)]
    public static partial void LogError(string message);

    /// <summary>
    /// Equivalent navigateur des arguments de ligne de commande des heads natifs :
    /// <c>?demo</c> vaut <c>--demo</c>.
    /// </summary>
    [JSImport("hasQueryFlag", Module)]
    public static partial bool HasQueryFlag(string name);

    /// <summary>Retire l'ecran de chargement de la page une fois le jeu affiche.</summary>
    [JSImport("appReady", Module)]
    public static partial void AppReady();

    // ── Evenements du navigateur ──────────────────────────────────────────────

    /// <summary>
    /// Notifie le retour de l'onglet au premier plan, avec la duree passee masque en secondes.
    /// C'est ce qui alimente la progression hors ligne : sans lui, un onglet en arriere-plan
    /// voit son timer bride par le navigateur et le temps de jeu derive.
    /// </summary>
    [JSImport("registerVisibilityHandler", Module)]
    public static partial void RegisterVisibilityHandler(
        [JSMarshalAs<JSType.Function<JSType.Number>>] Action<double> onVisible);

    /// <summary>
    /// Notifie les changements de plein ecran, y compris ceux declenches hors du jeu
    /// (Echap, F11) — sinon le reglage affiche divergerait de l'etat reel.
    /// </summary>
    [JSImport("registerFullscreenHandler", Module)]
    public static partial void RegisterFullscreenHandler(
        [JSMarshalAs<JSType.Function<JSType.Boolean>>] Action<bool> onChanged);
}
