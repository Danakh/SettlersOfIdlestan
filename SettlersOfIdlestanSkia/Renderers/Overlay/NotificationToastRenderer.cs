using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Services;
using SkiaSharp;

namespace SettlersOfIdlestanSkia.Renderers.Overlay;

public enum NotificationIcon { Info, Achievement, StoreOk, StoreFail }

public sealed class NotificationToastRenderer : IGameRenderer, IDisposable
{
    private sealed record ActiveToast(
        string Title,
        string Message,
        NotificationIcon Icon,
        float TotalDuration)
    {
        public float TimeLeft { get; set; } = TotalDuration;
    }

    private readonly UILayoutService _layout;
    private readonly List<ActiveToast> _toasts = [];
    private SKSize _canvasSize;

    private const int   MaxToasts      = 3;
    private const float ToastDuration  = 5f;
    private const float FadeOutTime    = 0.4f;
    private const float SlideInTime    = 0.25f;
    private const float ToastWidth     = 274f;
    private const float ToastHeight    = 64f;
    private const float ToastGap       = 6f;
    private const float ToastMargin    = 12f;
    private const float IconAreaWidth  = 44f;
    private const float CornerRadius   = 8f;

    private readonly SKPaint _bgPaint       = new() { Color = new SKColor(20, 20, 28, 245), Style = SKPaintStyle.Fill,   IsAntialias = true };
    private readonly SKPaint _borderPaint   = new() { Color = new SKColor(180, 150, 60),   StrokeWidth = 1.5f, Style = SKPaintStyle.Stroke, IsAntialias = true };
    private readonly SKPaint _titlePaint    = new() { Color = SKColors.White,              IsAntialias = true };
    private readonly SKPaint _messagePaint  = new() { Color = new SKColor(180, 180, 195),  IsAntialias = true };
    private readonly SKPaint _iconPaint     = new() { IsAntialias = true };

    private readonly SKFont _titleFont   = new() { Size = 12f, Typeface = SkiaFonts.Bold };
    private readonly SKFont _messageFont = new() { Size = 11f, Typeface = SkiaFonts.Regular };
    private readonly SKFont _iconFont    = new() { Size = 18f, Typeface = SkiaFonts.Bold };

    private bool _disposed;

    // Rects des toasts actuellement rendus, pour détecter les clics (index 0 = le plus bas)
    private readonly List<SKRect> _toastRects = [];

    public NotificationToastRenderer(UILayoutService layout)
    {
        _layout = layout;
    }

    public void ShowNotification(string title, string message, NotificationIcon icon = NotificationIcon.Info)
    {
        // Dépile le plus ancien si on est au max
        if (_toasts.Count >= MaxToasts)
            _toasts.RemoveAt(0);

        _toasts.Add(new ActiveToast(title, message, icon, ToastDuration));
    }

    public void Initialize(SKSize canvasSize) => _canvasSize = canvasSize;

    public void Render(SKCanvas canvas, GameRenderContext context) =>
        Render(canvas, context.CanvasSize, context.DeltaTime, context.UiScale);

    /// <summary>
    /// Variante de rendu sans GameRenderContext, pour les écrans sans MainGameState (ex: TitleScreen).
    /// </summary>
    public void Render(SKCanvas canvas, SKSize canvasSize, float deltaTime, float uiScale)
    {
        if (_disposed || _toasts.Count == 0) return;

        _canvasSize = canvasSize;
        float dt = deltaTime;
        float s  = uiScale;

        // Avancer les timers et supprimer les toasts expirés
        for (int i = _toasts.Count - 1; i >= 0; i--)
        {
            _toasts[i].TimeLeft -= dt;
            if (_toasts[i].TimeLeft <= 0f)
                _toasts.RemoveAt(i);
        }

        if (_toasts.Count == 0) return;

        _toastRects.Clear();

        float w  = ToastWidth  * s;
        float h  = ToastHeight * s;
        float gap = ToastGap   * s;
        float margin = ToastMargin * s;

        // Position Y de base (bas de la zone de toasts)
        float baseBottom = _layout.IsMobile
            ? _canvasSize.Height - (UILayoutService.MobileTabBarHeight * s + margin)
            : _canvasSize.Height - margin;

        float rightX = _canvasSize.Width - margin;

        for (int i = 0; i < _toasts.Count; i++)
        {
            var toast = _toasts[i];
            float elapsed = toast.TotalDuration - toast.TimeLeft;

            // Alpha : fondu en entrée puis en sortie
            float alpha;
            if (elapsed < SlideInTime)
                alpha = elapsed / SlideInTime;
            else if (toast.TimeLeft < FadeOutTime)
                alpha = toast.TimeLeft / FadeOutTime;
            else
                alpha = 1f;

            // Décalage horizontal pour animation d'entrée (glisse depuis la droite)
            float slideX = elapsed < SlideInTime
                ? (1f - elapsed / SlideInTime) * w
                : 0f;

            float toastBottom = baseBottom - i * (h + gap);
            float toastTop    = toastBottom - h;
            float toastLeft   = rightX - w + slideX;

            var rect = new SKRect(toastLeft, toastTop, rightX + slideX, toastBottom);
            _toastRects.Add(rect);

            DrawToast(canvas, rect, toast, alpha, s);
        }
    }

    private void DrawToast(SKCanvas canvas, SKRect rect, ActiveToast toast, float alpha, float s)
    {
        byte a = (byte)(alpha * 255);
        float cr = CornerRadius * s;

        // Fond
        _bgPaint.Color = new SKColor(20, 20, 28, (byte)(alpha * 245));
        canvas.DrawRoundRect(rect, cr, cr, _bgPaint);

        // Bordure
        _borderPaint.Color = new SKColor(180, 150, 60, a);
        canvas.DrawRoundRect(rect, cr, cr, _borderPaint);

        // Séparateur vertical
        var (iconColor, iconGlyph) = GetIconStyle(toast.Icon);
        float sepX = rect.Left + IconAreaWidth * s;
        using var sepPaint = new SKPaint
        {
            Color       = new SKColor(iconColor.Red, iconColor.Green, iconColor.Blue, (byte)(alpha * 80)),
            StrokeWidth = 1f,
            Style       = SKPaintStyle.Stroke,
        };
        canvas.DrawLine(sepX, rect.Top + cr, sepX, rect.Bottom - cr, sepPaint);

        // Icône centrée dans la zone gauche
        _iconPaint.Color = new SKColor(iconColor.Red, iconColor.Green, iconColor.Blue, a);
        float iconX = rect.Left + IconAreaWidth * s / 2f;
        float iconY = rect.MidY + _iconFont.Size * 0.35f;
        SkiaTextUtils.DrawText(canvas, iconGlyph, iconX, iconY, SKTextAlign.Center, _iconFont, _iconPaint);

        // Zone texte
        float textLeft  = sepX + 8 * s;
        float textRight = rect.Right - 8 * s;
        float maxTextW  = textRight - textLeft;

        // Titre
        _titlePaint.Color = new SKColor(255, 255, 255, a);
        string title = ClipText(toast.Title, _titleFont, maxTextW);
        SkiaTextUtils.DrawText(canvas, title, textLeft, rect.Top + 22 * s, _titleFont, _titlePaint);

        // Message
        _messagePaint.Color = new SKColor(180, 180, 195, a);
        string msg = ClipText(toast.Message, _messageFont, maxTextW);
        SkiaTextUtils.DrawText(canvas, msg, textLeft, rect.Top + 40 * s, _messageFont, _messagePaint);
    }

    private static (SKColor Color, string Glyph) GetIconStyle(NotificationIcon icon) => icon switch
    {
        NotificationIcon.Achievement => (new SKColor(255, 215, 0),   "★"),
        NotificationIcon.StoreOk     => (new SKColor(80,  200, 100), "✓"),
        NotificationIcon.StoreFail   => (new SKColor(220, 70,  70),  "✗"),
        _                            => (new SKColor(80,  150, 220), "ℹ"),
    };

    private static string ClipText(string text, SKFont font, float maxWidth)
    {
        if (font.MeasureText(text) <= maxWidth) return text;
        while (text.Length > 1 && font.MeasureText(text + "…") > maxWidth)
            text = text[..^1];
        return text + "…";
    }

    /// <summary>
    /// Gère un clic : retourne true si un toast a été fermé (le clic est consommé).
    /// </summary>
    public bool HandlePointerPressed(SKPoint pos)
    {
        // Les rects sont dans l'ordre croissant d'index (bas en premier)
        // On teste du haut vers le bas (dernier rendu = le plus récent = index le plus élevé)
        for (int i = _toastRects.Count - 1; i >= 0; i--)
        {
            if (_toastRects[i].Contains(pos.X, pos.Y))
            {
                _toasts.RemoveAt(i);
                _toastRects.RemoveAt(i);
                return true;
            }
        }
        return false;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _bgPaint.Dispose();
        _borderPaint.Dispose();
        _titlePaint.Dispose();
        _messagePaint.Dispose();
        _iconPaint.Dispose();
        _titleFont.Dispose();
        _messageFont.Dispose();
        _iconFont.Dispose();
        _disposed = true;
    }
}
