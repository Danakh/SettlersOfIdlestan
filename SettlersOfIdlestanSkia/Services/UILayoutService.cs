using SkiaSharp;

namespace SettlersOfIdlestanSkia.Services;

public class UILayoutService
{
    private bool _forceMobile;
    private SKSize _canvasSize;

    public const float MobileTabBarHeight = 44f;

    public void UpdateCanvasSize(SKSize size) => _canvasSize = size;

    // Auto-détection : petit écran ou orientation portrait
    private bool IsAutoMobile =>
        _canvasSize.Width > 0 &&
        (_canvasSize.Width < 600 || _canvasSize.Width < _canvasSize.Height);

    public bool IsMobile => _forceMobile || IsAutoMobile;

    public void ToggleForceMode() => _forceMobile = !_forceMobile;
    public bool IsForcedMobile => _forceMobile;
}
