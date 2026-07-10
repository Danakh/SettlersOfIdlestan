using SkiaSharp;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestanSkia.Renderers.Overlay;

namespace SettlersOfIdlestanSkia.Services;

public class UILayoutService
{
    private bool _forceMobile;
    private MenuPosition _menuPosition = MenuPosition.Auto;
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

    /// Force toutes les ruptures ci-dessous en mode compact (debug).
    private bool IsCompactOverride => _forceMobile || IsAutoMobile;

    public void ToggleForceMode() => _forceMobile = !_forceMobile;
    public void SetForceMobile(bool forceMobile) => _forceMobile = forceMobile;
    public bool IsForcedMobile => _forceMobile;

    /// Réglage utilisateur (Auto/Top/Bottom) piloté par GameSettings.ForceMenuPosition, poussé chaque frame.
    public void SetMenuPosition(MenuPosition position) => _menuPosition = position;

    /// Largeur déjà mise à l'échelle du bloc temps+paramètres (banque, boutons, engrenage) tel qu'affiché inline.
    public float TimeSettingsBlockWidth => (BarPadding + GearIconSize + TimeControlRenderer.RequiredWidth) * UiScale;

    /// Position effective des tabs : pilotée par le réglage utilisateur (Auto se rabat sur la détection mobile),
    /// le mode debug forçant toujours le bas quel que soit le réglage choisi.
    public bool TabsAtBottom => _forceMobile || _menuPosition switch
    {
        MenuPosition.Bottom => true,
        MenuPosition.Top    => false,
        _                   => IsAutoMobile,
    };

    /// Valeur à afficher/éditer dans l'écran de réglages : le réglage utilisateur résolu (Auto se rabat sur la
    /// détection mobile), sans tenir compte du mode debug qui ne doit pas polluer l'affichage.
    public bool MenuAtBottomSetting => _menuPosition switch
    {
        MenuPosition.Bottom => true,
        MenuPosition.Top    => false,
        _                   => IsAutoMobile,
    };

    /// Menu en haut uniquement : les ressources débordent de la ligne du haut (tabs + ressources + temps) et
    /// basculent sur leur propre ligne, sous les tabs.
    public bool ResourcesOnOwnRow =>
        !TabsAtBottom && _canvasSize.Width > 0 &&
        _tabsInlineWidth + _resourcesContentWidth + TimeSettingsBlockWidth > _canvasSize.Width;

    /// Le bloc temps+paramètres a besoin de sa propre ligne dédiée :
    ///  - Menu en bas : heuristique de largeur inchangée (règle historique, tabs déjà hors de cette ligne).
    ///  - Menu en haut : seulement une fois les ressources déjà reléguées sur leur propre ligne, si tabs+temps
    ///    ne tiennent toujours pas ensemble (3e ligne).
    public bool TimeSettingsOnSecondRow => TabsAtBottom
        ? (IsCompactOverride || (_canvasSize.Width > 0 && _canvasSize.Width < 3f * TimeSettingsBlockWidth))
        : (ResourcesOnOwnRow && _canvasSize.Width > 0 && _tabsInlineWidth + TimeSettingsBlockWidth > _canvasSize.Width);

    /// Drag + flèches de pagination des ressources (menu en bas : dans la barre principale ; menu en haut :
    /// sur leur ligne dédiée si elle ne suffit toujours pas). Le menu en haut ne les fait jamais défiler ailleurs :
    /// il les relègue sur leur propre ligne à la place (voir ResourcesOnOwnRow).
    public bool ResourcesOverflow
    {
        get
        {
            if (_canvasSize.Width <= 0) return false;

            if (TabsAtBottom)
            {
                if (IsCompactOverride) return true;
                float timeSettingsW = TimeSettingsOnSecondRow ? 0f : TimeSettingsBlockWidth;
                float available = _canvasSize.Width - timeSettingsW - BarPadding * UiScale;
                return _resourcesContentWidth > available;
            }

            if (ResourcesOnOwnRow)
            {
                float available = _canvasSize.Width - 2f * BarPadding * UiScale;
                return _resourcesContentWidth > available;
            }

            return false;
        }
    }

    // Computed layout values — valid after UpdateCanvasSize() and UiScale are set

    /// Bottom Y of the main resource bar (always TopBarHeight * UiScale).
    public float ResourceBarBottom => TopBarHeight * UiScale;

    /// Nombre de lignes supplémentaires sous la barre principale (ressources et/ou temps+paramètres relégués).
    private int ExtraRowCount => (ResourcesOnOwnRow ? 1 : 0) + (TimeSettingsOnSecondRow ? 1 : 0);

    /// Bottom Y of the last wrapped row, or ResourceBarBottom when nothing wraps.
    public float SecondRowBottom => ResourceBarBottom + ExtraRowCount * SecondRowHeight * UiScale;

    /// Top Y where side panels should start (accounts for any wrapped row when present, adds 10px gap otherwise).
    public float PanelTopY => ExtraRowCount > 0 ? SecondRowBottom : ResourceBarBottom + 10f * UiScale;

    /// Top Y of the row that hosts the resource bar : 0 when inline in the top bar, ResourceBarBottom when it has
    /// wrapped to its own row (Top menu only, see ResourcesOnOwnRow).
    public float ResourcesRowTop => ResourcesOnOwnRow ? ResourceBarBottom : 0f;

    /// Top Y of the row that hosts the time controls : inline (0) unless it needs its own row, in which case it
    /// sits right after any row already claimed by the resources bar.
    public float TimeControlRowTop => TimeSettingsOnSecondRow
        ? ResourceBarBottom + (ResourcesOnOwnRow ? SecondRowHeight * UiScale : 0f)
        : 0f;

    /// X position of the gear icon, flush against the right edge of the canvas.
    public float GearX => _canvasSize.Width - BarPadding * UiScale - GearIconSize * UiScale;
}
