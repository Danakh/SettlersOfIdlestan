using SkiaSharp;

namespace SettlersOfIdlestanSkia.Services;

public class UILayoutService
{
    private bool _forceMobile;
    private SKSize _canvasSize;

    // Layout dimensions — authoritative source for the entire overlay system
    public const float TopBarHeight    = 50f;
    public const float SecondRowHeight = 36f;
    public const float GearIconSize    = 32f;
    public const float BarPadding      = 12f;
    public const float MobileTabBarHeight = 44f;

    public float UiScale { get; set; } = 1f;

    public void UpdateCanvasSize(SKSize size) => _canvasSize = size;

    // Auto-détection : petit écran ou orientation portrait
    private bool IsAutoMobile =>
        _canvasSize.Width > 0 &&
        (_canvasSize.Width < 600 || _canvasSize.Width < _canvasSize.Height);

    public bool IsMobile => _forceMobile || IsAutoMobile;

    public void ToggleForceMode() => _forceMobile = !_forceMobile;
    public bool IsForcedMobile => _forceMobile;

    // Computed layout values — valid after UpdateCanvasSize() and UiScale are set

    /// Bottom Y of the main resource bar (always TopBarHeight * UiScale).
    public float ResourceBarBottom => TopBarHeight * UiScale;

    /// Bottom Y of the second control row: includes SecondRowHeight on mobile, equals ResourceBarBottom on desktop.
    public float SecondRowBottom => IsMobile ? (TopBarHeight + SecondRowHeight) * UiScale : ResourceBarBottom;

    /// Top Y where side panels should start (accounts for second row on mobile, adds 10px gap on desktop).
    public float PanelTopY => IsMobile ? SecondRowBottom : ResourceBarBottom + 10f * UiScale;

    /// Top Y of the row that hosts the time controls (second row on mobile, inside top bar on desktop).
    public float TimeControlRowTop => IsMobile ? ResourceBarBottom : 0f;

    /// X position of the gear icon, flush against the right edge of the canvas.
    public float GearX => _canvasSize.Width - BarPadding * UiScale - GearIconSize * UiScale;
}
