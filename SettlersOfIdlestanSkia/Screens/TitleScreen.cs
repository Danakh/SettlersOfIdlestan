using System.Reflection;
using SkiaSharp;
using Svg.Skia;
using SettlersOfIdlestan.Controller.Store;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Services;
using SettlersOfIdlestanSkia.Services.Localization;
using SettlersOfIdlestanSkia.Renderers.Overlay;
using SettlersOfIdlestanSkia.Renderers.Overlay.Popup;

namespace SettlersOfIdlestanSkia.Screens;

public sealed class TitleScreen : IDisposable
{
    private const string CloudSaveFileName = "autosave.json";

    private readonly IFileSystemService _fileSystemService;
    private readonly LocalizationService _localization;
    private readonly UILayoutService _uiLayoutService;
    private readonly StoreController? _storeController;

    private readonly NotificationToastRenderer _notificationToastRenderer;
    private readonly System.Diagnostics.Stopwatch _frameStopwatch = new();

    private HardResetPopupRenderer? _hardResetPopup;
    private bool _hasSave;
    private bool _disposed;

    /// Alimente le champ de résolution du panneau de réglages en mode debug. Poussée par le
    /// runtime : l'écran ne se dessine plus lui-même et n'a donc plus d'autre source.
    private SKSize _canvasSize;

    /// 0 = Changelog, 1 = Crédits, 2 = Paramètres.
    private int _activeTab;

    private readonly GameSettings        _settings;
    private readonly SettingsContentPanel _settingsPanel;
    private readonly bool _allowDebugMode;

    private string? _cachedChangelogContent;
    private SettlersOfIdlestan.Model.Localization.Language _cachedChangelogLanguage = (SettlersOfIdlestan.Model.Localization.Language)(-1);

    private const string DiscordUrl = "https://discord.gg/DBCvwt9vZf";

    public event Action? NewGameRequested;
    public event Action? ContinueRequested;
    public event Action<string>? DiscordLinkClicked;
    public event Action<bool>? FullscreenToggleRequested;
    public event Action<int, int>? DebugWindowResizeRequested;

    public TitleScreen(IFileSystemService fileSystemService, LocalizationService localization,
        UILayoutService uiLayoutService, ResourceManager resourceManager, bool hasSave, GameSettings? settings = null, bool allowDebugMode = false,
        StoreController? storeController = null)
    {
        _fileSystemService = fileSystemService;
        _localization      = localization;
        _uiLayoutService   = uiLayoutService;
        _hasSave           = hasSave;
        _settings          = settings ?? new GameSettings();
        _allowDebugMode    = allowDebugMode;
        _storeController   = storeController;
        _settingsPanel     = new SettingsContentPanel(_uiLayoutService);
        _settingsPanel.FullscreenToggleRequested += v => FullscreenToggleRequested?.Invoke(v);
        _settingsPanel.DebugWindowResizeRequested += (w, h) => DebugWindowResizeRequested?.Invoke(w, h);
        _settingsPanel.UiScaleChanged += v => _uiLayoutService.ManualUiScaleMultiplier =
            Math.Clamp(v, SettingsContentPanel.UiScaleMin, SettingsContentPanel.UiScaleMax);
        _uiLayoutService.ManualUiScaleMultiplier =
            Math.Clamp(_settings.UiScale, SettingsContentPanel.UiScaleMin, SettingsContentPanel.UiScaleMax);
        _uiLayoutService.SetMenuPosition(_settings.ForceMenuPosition);

        _hardResetPopup = new HardResetPopupRenderer(
            localization, fileSystemService,
            onConfirm: () => { _hasSave = false; NewGameRequested?.Invoke(); });

        _notificationToastRenderer = new NotificationToastRenderer(_uiLayoutService);
        StoreConnectionToastHelper.ShowConnectionToasts(_storeController, _notificationToastRenderer, _localization);
        _frameStopwatch.Start();
    }

    private bool CanLoadFromCloud => _storeController?.HasCloudSave(CloudSaveFileName) == true;

    // ── Pont vers l'hote Avalonia ─────────────────────────────────────────────

    /// <summary>Taille du canevas, pour le champ de résolution du mode debug.</summary>
    public void SetCanvasSize(SKSize canvasSize) => _canvasSize = canvasSize;

    /// <summary>Fait vieillir les toasts : plus aucun rendu Skia ne s'en charge.</summary>
    public void AdvanceToasts()
    {
        float dt = (float)_frameStopwatch.Elapsed.TotalSeconds;
        _frameStopwatch.Restart();
        _notificationToastRenderer.Advance(dt);
    }

    public ToastListSnapshot GetToastSnapshot() => _notificationToastRenderer.GetSnapshot();
    public void DismissToastFromHost(long id) => _notificationToastRenderer.Dismiss(id);

    /// <summary>
    /// Popup de remise a zero de l'ecran-titre. Meme forme que les modales bloquantes du jeu :
    /// il emprunte leur instantane et leur vue.
    /// </summary>
    public ModalPopupSnapshot GetModalSnapshot() => _hardResetPopup?.GetSnapshot() ?? ModalPopupSnapshot.None;

    public void InvokeModalButtonFromHost(string key) => _hardResetPopup?.InvokeButton(key);

    /// <summary>
    /// Instantane de l'ecran pour une vue portee par l'hote. Reprend les memes conditions que
    /// Render : le bouton principal dit « Continuer » ou « Nouvelle partie » selon la presence
    /// d'une sauvegarde, le chargement cloud n'apparait qu'avec une sauvegarde cloud, et la
    /// remise a zero qu'avec une sauvegarde locale.
    /// </summary>
    public TitleScreenSnapshot GetSnapshot()
    {
        if (_disposed) return TitleScreenSnapshot.Hidden;

        var tabs = new List<TitleTabSnapshot>
        {
            new(TitleScreenSnapshot.TabChangelog, _localization.Get("title_tab_changelog"), _activeTab == 0),
            new(TitleScreenSnapshot.TabCredits,   _localization.Get("title_tab_credits"),   _activeTab == 1),
            new(TitleScreenSnapshot.TabSettings,  _localization.Get("title_tab_settings"),  _activeTab == 2),
        };

        var actions = new List<TitleActionSnapshot>
        {
            new(TitleScreenSnapshot.ActionPrimary,
                _localization.Get(_hasSave ? "title_btn_continue" : "title_btn_new_game"),
                TitleActionTone.Primary),
        };
        if (CanLoadFromCloud)
            actions.Add(new(TitleScreenSnapshot.ActionLoadCloud, _localization.Get("title_btn_load_cloud"), TitleActionTone.Cloud));
        if (_hasSave)
            actions.Add(new(TitleScreenSnapshot.ActionHardReset, _localization.Get("title_btn_hard_reset"), TitleActionTone.Danger));

        return new TitleScreenSnapshot(
            IsVisible: true,
            Title: "Settlers of Idlestan",
            Tabs: tabs,
            ChangelogText: GetChangelogContent(),
            CreditsStudio: _localization.Get("credits_studio"),
            CreditsDev: _localization.Get("credits_dev"),
            Settings: _settingsPanel.GetSnapshot(_settings, _localization, _allowDebugMode, _canvasSize, _storeController),
            Actions: actions,
            DiscordUrl: DiscordUrl);
    }

    /// <summary>Selectionne un onglet depuis une vue portee par l'hote.</summary>
    public void SetTabFromHost(string key)
    {
        int target = key switch
        {
            TitleScreenSnapshot.TabCredits  => 1,
            TitleScreenSnapshot.TabSettings => 2,
            _                               => 0,
        };
        if (_activeTab == target) return;
        // Quitter l'onglet des reglages abandonne le focus d'un champ en cours de saisie.
        if (target != 2) _settingsPanel.ClearFocus();
        _activeTab = target;
    }

    /// <summary>
    /// Declenche un bouton depuis une vue portee par l'hote. Les gardes vivent ici : l'action est
    /// declenchee par deux chemins et une garde dupliquee finirait par diverger.
    /// </summary>
    public void InvokeActionFromHost(string key)
    {
        switch (key)
        {
            case TitleScreenSnapshot.ActionPrimary:
                if (_hasSave) ContinueRequested?.Invoke();
                else NewGameRequested?.Invoke();
                break;
            case TitleScreenSnapshot.ActionLoadCloud:
                if (CanLoadFromCloud) _ = LoadFromCloud();
                break;
            // La remise a zero passe par une confirmation : c'est elle qui efface, pas ce clic.
            case TitleScreenSnapshot.ActionHardReset:
                if (_hasSave) _hardResetPopup?.Open();
                break;
            case TitleScreenSnapshot.ActionDiscord:
                DiscordLinkClicked?.Invoke(DiscordUrl);
                break;
        }
    }

    // Commandes de reglages : l'ecran-titre a ses propres settings, distincts de ceux d'une partie.
    public void ToggleSettingFromHost(string key) => _settingsPanel.ToggleFromHost(key, _settings, _storeController);
    public void SetSettingChoiceFromHost(string key, string choiceKey) => _settingsPanel.SetChoiceFromHost(key, choiceKey, _settings, _localization);
    public void SetSettingSliderFromHost(string key, double value) => _settingsPanel.SetSliderFromHost(key, value, _settings);
    public void SetSettingTextFromHost(string key, string value) => _settingsPanel.SetTextFromHost(key, value);

    private string GetChangelogContent()
    {
        var lang = _localization.CurrentLanguage;
        if (_cachedChangelogContent != null && _cachedChangelogLanguage == lang)
            return _cachedChangelogContent;

        string langCode    = lang == SettlersOfIdlestan.Model.Localization.Language.English ? "en" : "fr";
        string suffix      = _settings.DemoMode ? "demo_" : "";
        string resourceName = $"SettlersOfIdlestanSkia.Resources.changelog.changelog_{suffix}{langCode}.txt";

        var asm = Assembly.GetExecutingAssembly();
        using var stream = asm.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            _cachedChangelogContent  = string.Empty;
            _cachedChangelogLanguage = lang;
            return string.Empty;
        }

        using var reader = new StreamReader(stream, System.Text.Encoding.UTF8);
        _cachedChangelogContent  = reader.ReadToEnd();
        _cachedChangelogLanguage = lang;
        return _cachedChangelogContent;
    }

    private async Task LoadFromCloud()
    {
        var json = _storeController?.LoadCloudFile(CloudSaveFileName);
        if (string.IsNullOrEmpty(json))
        {
            _notificationToastRenderer.ShowNotification(
                _localization.Get("notification_cloud_load_empty"), string.Empty, NotificationIcon.StoreFail);
            return;
        }

        await _fileSystemService.SaveAuto(json);
        _hasSave = true;
        _notificationToastRenderer.ShowNotification(
            _localization.Get("notification_cloud_load_success"), string.Empty, NotificationIcon.StoreOk);
        ContinueRequested?.Invoke();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _settingsPanel.Dispose();
        _hardResetPopup?.Dispose();
        _notificationToastRenderer.Dispose();
        _disposed = true;
    }
}
