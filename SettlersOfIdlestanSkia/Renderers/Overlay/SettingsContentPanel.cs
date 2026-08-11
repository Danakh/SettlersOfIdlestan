using System.Text.RegularExpressions;
using SettlersOfIdlestan.Controller.Store;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.Localization;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Renderers.Debug;
using SettlersOfIdlestanSkia.Services;
using SettlersOfIdlestanSkia.Services.Localization;
using SkiaSharp;

namespace SettlersOfIdlestanSkia.Renderers.Overlay;

/// <summary>
/// Panneau de contenu des paramètres — utilisé par SettingsPopupRenderer et TitleScreen.
/// Ajouter une option ici la fait apparaître automatiquement dans les deux endroits.
/// </summary>
public sealed class SettingsContentPanel : IDisposable
{
    public const float UiScaleMin = 0.5f;
    public const float UiScaleMax = 2f;

    private const int MinDebugResolution = 128;
    private static readonly Regex DebugResolutionRegex = new(@"^(\d{1,5})[xX](\d{1,5})$", RegexOptions.Compiled);

    private readonly UILayoutService _uiLayout;

    private string _debugResolutionText     = "";
    private bool   _debugResolutionFocused;

    /// Valeur du curseur d'échelle en cours de glissement, pas encore appliquée aux réglages.
    private float? _pendingUiScaleValue;
    private bool _disposed;

    public event Action<bool>? FullscreenToggleRequested;
    public event Action<float>? UiScaleChanged;
    public event Action<int, int>? DebugWindowResizeRequested;

    public SettingsContentPanel(UILayoutService uiLayout)
    {
        _uiLayout = uiLayout;
    }

    private static SettingRowSnapshot Toggle(string key, string label, bool value, bool enabled = true) =>
        new(key, label, SettingRowKind.Toggle, enabled, value, [], 0, 0, 0, "", "");

    private static SettingRowSnapshot Choice(string key, string label, IReadOnlyList<SettingChoiceSnapshot> choices) =>
        new(key, label, SettingRowKind.Choice, true, false, choices, 0, 0, 0, "", "");

    /// <summary>
    /// Instantane du panneau pour une vue portee par l'hote. Reprend l'ordre des lignes, leurs
    /// libelles et leurs conditions d'affichage de Render : les lignes de debogage n'existent
    /// qu'en mode debogage, et la sauvegarde cloud reste grisee sans store connecte.
    /// </summary>
    public SettingsPanelSnapshot GetSnapshot(
        GameSettings settings, LocalizationService localization,
        bool allowDebugMode = false, SKSize currentResolution = default,
        StoreController? storeController = null)
    {
        string? connectedStore = storeController?.ConnectedStoreName;
        bool cloudAvailable = connectedStore != null;
        string cloudLabel = localization.Get("settings_cloud_save") + " " + (cloudAvailable
            ? localization.GetFormated("settings_cloud_save_connected", connectedStore!)
            : localization.Get("settings_cloud_save_not_connected"));

        double uiScale = _pendingUiScaleValue ?? settings.UiScale;

        var rows = new List<SettingRowSnapshot>
        {
            Choice(SettingsPanelSnapshot.KeyLanguage, localization.Get("settings_language"),
            [
                new("english", localization.Get("menu_language_english"), settings.Language == Language.English),
                new("french",  localization.Get("menu_language_french"),  settings.Language == Language.French),
            ]),
            Toggle(SettingsPanelSnapshot.KeyFullscreen, localization.Get("settings_fullscreen"), settings.Fullscreen),
            Toggle(SettingsPanelSnapshot.KeyMenuPosition, localization.Get("settings_force_menu_position"), _uiLayout.MenuAtBottomSetting),
            Toggle(SettingsPanelSnapshot.KeyPauseAfterPrestige, localization.Get("settings_pause_after_prestige"), settings.PauseAfterPrestige),
            Toggle(SettingsPanelSnapshot.KeyHarvestParticles, localization.Get("settings_harvest_particles"), settings.ShowHarvestParticles),
            Toggle(SettingsPanelSnapshot.KeyMilitaryStats, localization.Get("settings_show_military_stats"), settings.ShowCityMilitaryStats),
            new(SettingsPanelSnapshot.KeyUiScale, localization.Get("settings_ui_scale"), SettingRowKind.Slider,
                IsEnabled: true, ToggleValue: false, Choices: [],
                SliderValue: uiScale, SliderMin: UiScaleMin, SliderMax: UiScaleMax,
                SliderText: $"x{uiScale:0.0}", TextValue: ""),
            Toggle(SettingsPanelSnapshot.KeyCloudSave, cloudLabel, settings.CloudSaveEnabled, enabled: cloudAvailable),
            Choice(SettingsPanelSnapshot.KeyNumberFormat, localization.Get("settings_number_format"),
            [
                new("classic",     localization.Get("settings_number_format_classic"),     settings.NumberFormat == NumberFormatMode.Classic),
                new("scientific",  localization.Get("settings_number_format_scientific"),  settings.NumberFormat == NumberFormatMode.Scientific),
                new("engineering", localization.Get("settings_number_format_engineering"), settings.NumberFormat == NumberFormatMode.Engineering),
            ]),
        };

        if (allowDebugMode)
        {
            // Tant que le champ n'a pas le focus, il reflete la resolution courante de la fenetre.
            if (!_debugResolutionFocused && currentResolution.Width > 0f && currentResolution.Height > 0f)
                _debugResolutionText = $"{(int)MathF.Round(currentResolution.Width)}x{(int)MathF.Round(currentResolution.Height)}";

            rows.Add(new SettingRowSnapshot(
                SettingsPanelSnapshot.KeyDebugResolution, localization.Get("settings_debug_window_resolution"),
                SettingRowKind.TextInput, true, false, [], 0, 0, 0, "", _debugResolutionText));
            rows.Add(Toggle(SettingsPanelSnapshot.KeyExportTransparentBg,
                localization.Get("settings_debug_export_transparent_bg"), DebugSettings.ExportTransparentBackground));
        }

        return new SettingsPanelSnapshot(rows);
    }

    /// <summary>
    /// Bascule un reglage depuis une vue portee par l'hote. Meme effets de bord que le
    /// hit-testing Skia : le plein ecran previent l'hote, la position du menu met a jour le
    /// service de disposition.
    /// </summary>
    public void ToggleFromHost(string key, GameSettings settings, StoreController? storeController)
    {
        switch (key)
        {
            case SettingsPanelSnapshot.KeyFullscreen:
                settings.Fullscreen = !settings.Fullscreen;
                FullscreenToggleRequested?.Invoke(settings.Fullscreen);
                break;
            case SettingsPanelSnapshot.KeyMenuPosition:
                settings.ForceMenuPosition = _uiLayout.MenuAtBottomSetting ? MenuPosition.Top : MenuPosition.Bottom;
                _uiLayout.SetMenuPosition(settings.ForceMenuPosition);
                break;
            case SettingsPanelSnapshot.KeyPauseAfterPrestige:
                settings.PauseAfterPrestige = !settings.PauseAfterPrestige;
                break;
            case SettingsPanelSnapshot.KeyHarvestParticles:
                settings.ShowHarvestParticles = !settings.ShowHarvestParticles;
                break;
            case SettingsPanelSnapshot.KeyMilitaryStats:
                settings.ShowCityMilitaryStats = !settings.ShowCityMilitaryStats;
                break;
            // Sans store connecte, la sauvegarde cloud n'a pas d'objet : la ligne est grisee et
            // le clic reste sans effet, comme dans le rendu Skia.
            case SettingsPanelSnapshot.KeyCloudSave:
                if (storeController?.ConnectedStoreName != null)
                    settings.CloudSaveEnabled = !settings.CloudSaveEnabled;
                break;
            case SettingsPanelSnapshot.KeyExportTransparentBg:
                DebugSettings.ExportTransparentBackground = !DebugSettings.ExportTransparentBackground;
                break;
        }
    }

    /// <summary>Choisit une option exclusive (langue, format des nombres) depuis la vue de l'hote.</summary>
    public void SetChoiceFromHost(string key, string choiceKey, GameSettings settings, LocalizationService localization)
    {
        switch (key)
        {
            case SettingsPanelSnapshot.KeyLanguage:
                var language = choiceKey == "french" ? Language.French : Language.English;
                localization.SetLanguage(language);
                settings.Language = language;
                break;
            case SettingsPanelSnapshot.KeyNumberFormat:
                settings.NumberFormat = choiceKey switch
                {
                    "scientific"  => NumberFormatMode.Scientific,
                    "engineering" => NumberFormatMode.Engineering,
                    _             => NumberFormatMode.Classic,
                };
                SkiaTextUtils.NumberFormat = settings.NumberFormat;
                break;
        }
    }

    /// <summary>Applique la valeur d'un curseur depuis la vue de l'hote.</summary>
    public void SetSliderFromHost(string key, double value, GameSettings settings)
    {
        if (key != SettingsPanelSnapshot.KeyUiScale) return;
        float clamped = Math.Clamp((float)value, UiScaleMin, UiScaleMax);
        settings.UiScale = clamped;
        _pendingUiScaleValue = null;
        UiScaleChanged?.Invoke(clamped);
    }

    /// <summary>Applique le texte d'un champ depuis la vue de l'hote (resolution de debogage).</summary>
    public void SetTextFromHost(string key, string value)
    {
        if (key != SettingsPanelSnapshot.KeyDebugResolution) return;
        _debugResolutionText = value;
        TryApplyDebugResolution();
    }

    /// <summary>Applique la résolution saisie — uniquement si elle correspond au format "LARGEURxHAUTEUR"
    /// et que chaque dimension est d'au moins <see cref="MinDebugResolution"/> pixels.</summary>
    private void TryApplyDebugResolution()
    {
        var match = DebugResolutionRegex.Match(_debugResolutionText);
        if (!match.Success) return;
        if (!int.TryParse(match.Groups[1].Value, out int width)  || width  < MinDebugResolution) return;
        if (!int.TryParse(match.Groups[2].Value, out int height) || height < MinDebugResolution) return;

        DebugWindowResizeRequested?.Invoke(width, height);
    }

    /// <summary>Abandonne la saisie en cours du champ de résolution debug — à appeler quand
    /// l'écran ou le popup qui héberge ce panneau se ferme.</summary>
    public void ClearFocus() => _debugResolutionFocused = false;

    public void Dispose() => _disposed = true;
}
