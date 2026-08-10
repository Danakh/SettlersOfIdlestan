using SkiaSharp;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestanSkia.Renderers.Overlay;

namespace SettlersOfIdlestanSkia.Services;

public class UILayoutService
{
    private bool _forceMobile;
    private MenuPosition _menuPosition = MenuPosition.Auto;
    private SKSize _canvasSize;

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

    // Auto-détection : petit écran ou orientation portrait
    private bool IsAutoMobile =>
        _canvasSize.Width > 0 &&
        (_canvasSize.Width < 600 || _canvasSize.Width < _canvasSize.Height);

    public void ToggleForceMode() => _forceMobile = !_forceMobile;
    public bool IsForcedMobile => _forceMobile;

    /// Réglage utilisateur (Auto/Top/Bottom) piloté par GameSettings.ForceMenuPosition, poussé chaque frame.
    public void SetMenuPosition(MenuPosition position) => _menuPosition = position;

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

    // Valeurs de disposition calculées — valides après UpdateCanvasSize() et le réglage de UiScale.
    //
    // La barre du haut est rendue par l'hôte : c'est lui qui décide de son repli (ressources
    // reléguées sur leur propre ligne quand les trois blocs ne tiennent pas ensemble) et qui
    // pousse ici la hauteur obtenue. Les règles de repli qui vivaient dans ce service auraient
    // dû mesurer un contenu qu'il ne dessine plus.

    /// Hauteur totale de la barre du haut telle que mesurée par l'hôte, en unités logiques
    /// (la barre Avalonia ne suit pas UiScale). Vaut TopBarHeight tant que rien ne se replie.
    public float HostTopBarHeight { get; set; } = TopBarHeight;

    /// Hauteur des lignes gagnées par le repli de la barre de l'hôte, hors ligne principale.
    private float HostExtraRowsHeight => Math.Max(0f, HostTopBarHeight - TopBarHeight);

    /// Bas de la ligne principale de la barre du haut.
    public float ResourceBarBottom => TopBarHeight * UiScale;

    /// Y où commencent les vues plein écran encore dessinées en Skia (recherche, carte de
    /// prestige, ascension) : sous la barre du haut, seconde ligne de ressources comprise.
    public float SecondRowBottom => ResourceBarBottom + HostExtraRowsHeight;

    /// Y où commencent les panneaux latéraux, sous la barre du haut.
    public float PanelTopY => SecondRowBottom + 10f * UiScale;

    /// X de l'icône d'engrenage, collée au bord droit du canevas.
    public float GearX => _canvasSize.Width - BarPadding * UiScale - GearIconSize * UiScale;
}
