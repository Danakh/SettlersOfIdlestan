using SkiaSharp;

namespace SettlersOfIdlestanSkia.Renderers.Overlay.Popup;

/// <summary>
/// Couleurs et cotes de référence du chrome des popups. Le dessin lui-même vit dans
/// <see cref="PopupRendererBase"/>, qui lit ces valeurs.
/// </summary>
public sealed class PopupChrome
{
    // ── Couleurs de référence ────────────────────────────────────────────────────
    public static readonly SKColor BackgroundColor = new(24, 24, 30, 245);
    public static readonly SKColor BorderColor     = SKColors.Gold;
    public static readonly SKColor OverlayColor    = new(0, 0, 0, 120);
    public static readonly SKColor CloseBtnColor   = new(90, 50, 50, 230);

    // ── Constantes de layout (valeurs de base à scale=1) ────────────────────────
    public const float CornerRadius  = 10f;
    public const float CloseSize     = 28f;
    public const float CloseMargin   = 10f;
}
