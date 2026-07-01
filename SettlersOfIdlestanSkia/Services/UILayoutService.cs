using SkiaSharp;
using SettlersOfIdlestanSkia.Renderers.Overlay;

namespace SettlersOfIdlestanSkia.Services;

public class UILayoutService
{
    private bool _forceMobile;
    private SKSize _canvasSize;
    private float _tabsInlineWidth;
    private float _resourcesContentWidth;

    // Layout dimensions — authoritative source for the entire overlay system
    public const float TopBarHeight    = 50f;
    public const float SecondRowHeight = 36f;
    public const float GearIconSize    = 32f;
    public const float BarPadding      = 12f;
    public const float MobileTabBarHeight = 44f;

    /// Échelle détectée automatiquement par la plateforme hôte (densité d'écran, grande résolution…).
    public float AutoUiScale { get; set; } = 1f;

    /// Multiplicateur manuel choisi par le joueur dans les paramètres (x0.5 à x4), en plus de l'échelle automatique.
    public float ManualUiScaleMultiplier { get; set; } = 1f;

    /// Échelle effective appliquée à l'ensemble de l'UI.
    public float UiScale => AutoUiScale * ManualUiScaleMultiplier;

    public void UpdateCanvasSize(SKSize size) => _canvasSize = size;

    /// Largeur occupée par les tabs si affichés en ligne, poussée chaque frame par TabBarRenderer.
    public void SetTabsInlineWidth(float width) => _tabsInlineWidth = width;

    /// Largeur totale du contenu des ressources (avant clip), poussée chaque frame par OverlayRenderer.
    public void SetResourcesContentWidth(float width) => _resourcesContentWidth = width;

    // Auto-détection : petit écran ou orientation portrait
    private bool IsAutoMobile =>
        _canvasSize.Width > 0 &&
        (_canvasSize.Width < 600 || _canvasSize.Width < _canvasSize.Height);

    /// Force toutes les ruptures ci-dessous en mode compact (debug + génération de trailer).
    private bool IsCompactOverride => _forceMobile || IsAutoMobile;

    public void ToggleForceMode() => _forceMobile = !_forceMobile;
    public void SetForceMobile(bool forceMobile) => _forceMobile = forceMobile;
    public bool IsForcedMobile => _forceMobile;

    /// Largeur déjà mise à l'échelle du bloc temps+paramètres (banque, boutons, engrenage) tel qu'affiché inline.
    public float TimeSettingsBlockWidth => (BarPadding + GearIconSize + TimeControlRenderer.RequiredWidth) * UiScale;

    /// Règle 4 : le bloc temps+paramètres passe sur sa propre ligne dès que l'écran est trop étroit pour 3x sa largeur.
    public bool TimeSettingsOnSecondRow =>
        IsCompactOverride || (_canvasSize.Width > 0 && _canvasSize.Width < 3f * TimeSettingsBlockWidth);

    /// Règle 1 : les tabs basculent en bas dès que la somme des 3 largeurs déborde l'écran.
    public bool TabsAtBottom
    {
        get
        {
            if (IsCompactOverride) return true;
            if (_canvasSize.Width <= 0) return false;
            float timeSettingsW = TimeSettingsOnSecondRow ? 0f : TimeSettingsBlockWidth;
            return _tabsInlineWidth + _resourcesContentWidth + timeSettingsW > _canvasSize.Width;
        }
    }

    /// Règle 2 : le drag + les flèches de pagination des ressources s'activent dès qu'elles ne tiennent plus dans l'espace restant.
    public bool ResourcesOverflow
    {
        get
        {
            if (IsCompactOverride) return true;
            if (_canvasSize.Width <= 0) return false;
            float tabsW = TabsAtBottom ? 0f : _tabsInlineWidth;
            float timeSettingsW = TimeSettingsOnSecondRow ? 0f : TimeSettingsBlockWidth;
            float available = _canvasSize.Width - tabsW - timeSettingsW - BarPadding * UiScale;
            return _resourcesContentWidth > available;
        }
    }

    // Computed layout values — valid after UpdateCanvasSize() and UiScale are set

    /// Bottom Y of the main resource bar (always TopBarHeight * UiScale).
    public float ResourceBarBottom => TopBarHeight * UiScale;

    /// Bottom Y of the second control row: includes SecondRowHeight when the time+settings block wraps, equals ResourceBarBottom otherwise.
    public float SecondRowBottom => TimeSettingsOnSecondRow ? (TopBarHeight + SecondRowHeight) * UiScale : ResourceBarBottom;

    /// Top Y where side panels should start (accounts for the second row when present, adds 10px gap otherwise).
    public float PanelTopY => TimeSettingsOnSecondRow ? SecondRowBottom : ResourceBarBottom + 10f * UiScale;

    /// Top Y of the row that hosts the time controls (second row when it wraps, inside top bar otherwise).
    public float TimeControlRowTop => TimeSettingsOnSecondRow ? ResourceBarBottom : 0f;

    /// X position of the gear icon, flush against the right edge of the canvas.
    public float GearX => _canvasSize.Width - BarPadding * UiScale - GearIconSize * UiScale;
}
