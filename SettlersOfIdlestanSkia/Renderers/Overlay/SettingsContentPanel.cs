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
public sealed class SettingsContentPanel
{
    public const float UiScaleMin = 0.5f;
    public const float UiScaleMax = 2f;

    private const int MinDebugResolution = 128;
    private static readonly Regex DebugResolutionRegex = new(@"^(\d{1,5})[xX](\d{1,5})$", RegexOptions.Compiled);

    private readonly UILayoutService _uiLayout;

    /// <summary>
    /// Service audio, ou null pour un head muet. Sert à deux choses : appliquer immédiatement le
    /// réglage que le joueur vient de changer (sans quoi il faudrait attendre la frame suivante)
    /// et lui faire entendre le résultat.
    /// </summary>
    private readonly Services.Audio.GameAudioService? _audio;

    private string _debugResolutionText     = "";
    private bool   _debugResolutionFocused;

    /// Valeur du curseur d'échelle en cours de glissement, pas encore appliquée aux réglages.
    private float? _pendingUiScaleValue;

    public event Action<bool>? FullscreenToggleRequested;
    public event Action<float>? UiScaleChanged;
    public event Action<int, int>? DebugWindowResizeRequested;

    public SettingsContentPanel(UILayoutService uiLayout, Services.Audio.GameAudioService? audio = null)
    {
        _uiLayout = uiLayout;
        _audio = audio;
    }

    /// <summary>
    /// Applique le réglage sonore qui vient de changer et le fait entendre. L'aperçu est le son
    /// d'information, le plus neutre : un volume se règle à l'oreille, et attendre le prochain
    /// toast pour savoir où l'on a mis le curseur ne marche pas.
    ///
    /// <para>Passe par <c>Play</c>, donc par l'intervalle minimum entre deux passages du même
    /// son : glisser le curseur émet un instantané régulier plutôt qu'un son par pixel parcouru.</para>
    /// </summary>
    private void PreviewSound(GameSettings settings)
    {
        if (_audio == null) return;
        _audio.ApplySettings(settings);
        // PlayPreview plutôt que Play : l'aperçu est un toast, et le joueur qui vient de couper
        // la famille des toasts doit quand même entendre où il met le curseur de volume.
        if (settings.SoundEnabled) _audio.PlayPreview(Services.Audio.SoundId.ToastInfo);
    }

    private static SettingRowSnapshot Toggle(string key, string label, bool value, SettingsTab tab, bool enabled = true) =>
        new(key, label, SettingRowKind.Toggle, enabled, value, [], 0, 0, 0, "", "", tab);

    private static SettingRowSnapshot Choice(string key, string label, IReadOnlyList<SettingChoiceSnapshot> choices, SettingsTab tab) =>
        new(key, label, SettingRowKind.Choice, true, false, choices, 0, 0, 0, "", "", tab);

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

        const SettingsTab general = SettingsTab.General;
        const SettingsTab display = SettingsTab.Display;
        const SettingsTab sound   = SettingsTab.Sound;

        var rows = new List<SettingRowSnapshot>
        {
            // ── Général ──
            Choice(SettingsPanelSnapshot.KeyLanguage, localization.Get("settings_language"),
            [
                new("english", localization.Get("menu_language_english"), settings.Language == Language.English),
                new("french",  localization.Get("menu_language_french"),  settings.Language == Language.French),
            ], general),
            Toggle(SettingsPanelSnapshot.KeyPauseAfterPrestige, localization.Get("settings_pause_after_prestige"), settings.PauseAfterPrestige, general),
            Toggle(SettingsPanelSnapshot.KeyShowTutorial, localization.Get("settings_show_tutorial"), settings.ShowTutorial, general),
            Toggle(SettingsPanelSnapshot.KeyCloudSave, cloudLabel, settings.CloudSaveEnabled, general, enabled: cloudAvailable),

            // ── Affichage ──
            Toggle(SettingsPanelSnapshot.KeyFullscreen, localization.Get("settings_fullscreen"), settings.Fullscreen, display),
            Toggle(SettingsPanelSnapshot.KeyMenuPosition, localization.Get("settings_force_menu_position"), _uiLayout.MenuAtBottomSetting, display),
            new(SettingsPanelSnapshot.KeyUiScale, localization.Get("settings_ui_scale"), SettingRowKind.Slider,
                IsEnabled: true, ToggleValue: false, Choices: [],
                SliderValue: uiScale, SliderMin: UiScaleMin, SliderMax: UiScaleMax,
                SliderText: $"x{uiScale:0.0}", TextValue: "", Tab: display),
            Toggle(SettingsPanelSnapshot.KeyHarvestParticles, localization.Get("settings_harvest_particles"), settings.ShowHarvestParticles, display),
            Toggle(SettingsPanelSnapshot.KeyMilitaryStats, localization.Get("settings_show_military_stats"), settings.ShowCityMilitaryStats, display),
            Toggle(SettingsPanelSnapshot.KeyHarvestCooldown, localization.Get("settings_harvest_cooldown"), settings.ShowHarvestCooldown, display),
            Toggle(SettingsPanelSnapshot.KeyCorruptionDominion, localization.Get("settings_corruption_dominion"), settings.ShowCorruptionDominion, display),
            Choice(SettingsPanelSnapshot.KeyNumberFormat, localization.Get("settings_number_format"),
            [
                new("classic",     localization.Get("settings_number_format_classic"),     settings.NumberFormat == NumberFormatMode.Classic),
                new("scientific",  localization.Get("settings_number_format_scientific"),  settings.NumberFormat == NumberFormatMode.Scientific),
                new("engineering", localization.Get("settings_number_format_engineering"), settings.NumberFormat == NumberFormatMode.Engineering),
            ], display),

            // ── Son ──
            Toggle(SettingsPanelSnapshot.KeySoundEnabled, localization.Get("settings_sound_enabled"), settings.SoundEnabled, sound),
            // Le curseur de volume reste visible, mais grisé, quand le son est coupé : l'effacer
            // ferait sauter toutes les lignes suivantes d'un cran à chaque bascule.
            new(SettingsPanelSnapshot.KeySoundVolume, localization.Get("settings_sound_volume"), SettingRowKind.Slider,
                IsEnabled: settings.SoundEnabled, ToggleValue: false, Choices: [],
                SliderValue: settings.SoundVolume, SliderMin: 0, SliderMax: 1,
                SliderText: $"{settings.SoundVolume * 100:0} %", TextValue: "", Tab: sound),
            // Les familles se grisent avec l'interrupteur général, pour la même raison que le
            // volume : le joueur voit ce qu'il retrouvera en rétablissant le son.
            Toggle(SettingsPanelSnapshot.KeySoundCombat, localization.Get("settings_sound_combat"), settings.SoundCombatEnabled, sound, enabled: settings.SoundEnabled),
            Toggle(SettingsPanelSnapshot.KeySoundToast, localization.Get("settings_sound_toasts"), settings.SoundToastEnabled, sound, enabled: settings.SoundEnabled),
            // La fanfare est une sous-famille des notifications : elle se grise aussi quand
            // celles-ci sont coupées, puisqu'elles la coupent déjà.
            Toggle(SettingsPanelSnapshot.KeySoundAchievement, localization.Get("settings_sound_achievement"), settings.SoundAchievementEnabled, sound,
                enabled: settings.SoundEnabled && settings.SoundToastEnabled),
            Toggle(SettingsPanelSnapshot.KeySoundCity, localization.Get("settings_sound_city"), settings.SoundCityEnabled, sound, enabled: settings.SoundEnabled),
            Toggle(SettingsPanelSnapshot.KeySoundCityLost, localization.Get("settings_sound_city_lost"), settings.SoundCityLostEnabled, sound, enabled: settings.SoundEnabled),
            Toggle(SettingsPanelSnapshot.KeySoundHarvest, localization.Get("settings_sound_harvest"), settings.SoundHarvestEnabled, sound, enabled: settings.SoundEnabled),
        };

        if (allowDebugMode)
        {
            // Tant que le champ n'a pas le focus, il reflete la resolution courante de la fenetre.
            if (!_debugResolutionFocused && currentResolution.Width > 0f && currentResolution.Height > 0f)
                _debugResolutionText = $"{(int)MathF.Round(currentResolution.Width)}x{(int)MathF.Round(currentResolution.Height)}";

            rows.Add(new SettingRowSnapshot(
                SettingsPanelSnapshot.KeyDebugResolution, localization.Get("settings_debug_window_resolution"),
                SettingRowKind.TextInput, true, false, [], 0, 0, 0, "", _debugResolutionText, display));
            rows.Add(Toggle(SettingsPanelSnapshot.KeyExportTransparentBg,
                localization.Get("settings_debug_export_transparent_bg"), DebugSettings.ExportTransparentBackground, display));
        }

        return new SettingsPanelSnapshot(rows,
        [
            new(general, localization.Get("settings_tab_general")),
            new(display, localization.Get("settings_tab_display")),
            new(sound,   localization.Get("settings_tab_sound")),
        ]);
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
            case SettingsPanelSnapshot.KeyHarvestCooldown:
                settings.ShowHarvestCooldown = !settings.ShowHarvestCooldown;
                break;
            case SettingsPanelSnapshot.KeyCorruptionDominion:
                settings.ShowCorruptionDominion = !settings.ShowCorruptionDominion;
                break;
            case SettingsPanelSnapshot.KeyShowTutorial:
                settings.ShowTutorial = !settings.ShowTutorial;
                break;
            case SettingsPanelSnapshot.KeySoundEnabled:
                settings.SoundEnabled = !settings.SoundEnabled;
                PreviewSound(settings);
                break;
            // Les deux familles sont grisées quand le son est coupé : même prudence que pour le
            // volume, on refuse ici plutôt que de compter sur la vue.
            case SettingsPanelSnapshot.KeySoundCombat:
                if (!settings.SoundEnabled) break;
                settings.SoundCombatEnabled = !settings.SoundCombatEnabled;
                // L'aperçu du combat est un coup porté : la famille qu'on vient de rallumer doit
                // s'entendre, et non le toast d'information des autres réglages sonores.
                if (_audio != null)
                {
                    _audio.ApplySettings(settings);
                    if (settings.SoundCombatEnabled) _audio.PlayPreview(Services.Audio.SoundId.AttackDealt);
                }
                break;
            case SettingsPanelSnapshot.KeySoundToast:
                if (!settings.SoundEnabled) break;
                settings.SoundToastEnabled = !settings.SoundToastEnabled;
                if (settings.SoundToastEnabled) PreviewSound(settings);
                else _audio?.ApplySettings(settings);
                break;
            // La fanfare suit les notifications : la ligne est grisée quand elles sont coupées,
            // et le clic reste sans effet, comme pour les lignes grisées par l'interrupteur général.
            case SettingsPanelSnapshot.KeySoundAchievement:
                if (!settings.SoundEnabled || !settings.SoundToastEnabled) break;
                settings.SoundAchievementEnabled = !settings.SoundAchievementEnabled;
                if (_audio != null)
                {
                    _audio.ApplySettings(settings);
                    if (settings.SoundAchievementEnabled) _audio.PlayPreview(Services.Audio.SoundId.Achievement);
                }
                break;
            case SettingsPanelSnapshot.KeySoundCity:
                if (!settings.SoundEnabled) break;
                settings.SoundCityEnabled = !settings.SoundCityEnabled;
                // Même raison que pour le combat : l'aperçu est le son de la famille qu'on vient
                // de rallumer, pas le toast d'information des autres réglages sonores.
                if (_audio != null)
                {
                    _audio.ApplySettings(settings);
                    if (settings.SoundCityEnabled) _audio.PlayPreview(Services.Audio.SoundId.CityFounded);
                }
                break;
            case SettingsPanelSnapshot.KeySoundCityLost:
                if (!settings.SoundEnabled) break;
                settings.SoundCityLostEnabled = !settings.SoundCityLostEnabled;
                // Même raison que pour le combat et la fondation : l'aperçu est le son de la
                // famille qu'on vient de rallumer, pas le toast d'information.
                if (_audio != null)
                {
                    _audio.ApplySettings(settings);
                    if (settings.SoundCityLostEnabled) _audio.PlayPreview(Services.Audio.SoundId.CityLost);
                }
                break;
            case SettingsPanelSnapshot.KeySoundHarvest:
                if (!settings.SoundEnabled) break;
                settings.SoundHarvestEnabled = !settings.SoundHarvestEnabled;
                if (_audio != null)
                {
                    _audio.ApplySettings(settings);
                    if (settings.SoundHarvestEnabled) _audio.PlayPreview(Services.Audio.SoundId.HarvestManual);
                }
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
        switch (key)
        {
            case SettingsPanelSnapshot.KeyUiScale:
                float clamped = Math.Clamp((float)value, UiScaleMin, UiScaleMax);
                settings.UiScale = clamped;
                _pendingUiScaleValue = null;
                UiScaleChanged?.Invoke(clamped);
                break;

            case SettingsPanelSnapshot.KeySoundVolume:
                // La ligne est grisée quand le son est coupé, mais rien n'empêche une vue de
                // pousser quand même une valeur : on refuse ici plutôt que de compter sur elle.
                if (!settings.SoundEnabled) break;
                settings.SoundVolume = Math.Clamp((float)value, 0f, 1f);
                PreviewSound(settings);
                break;
        }
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
}
