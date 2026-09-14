using SettlersOfIdlestanSkia.Services.Localization;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Services;
using SkiaSharp;

namespace SettlersOfIdlestanSkia.Renderers.Overlay.Popup;

public sealed class ResearchCancelPopupRenderer : PopupRendererBase
{
    protected override float PopupWidth  => 440;
    protected override float PopupHeight => _popupHeight;

    private const float BtnWidth  = 180;
    private const float BtnHeight = 42;
    private const float BtnGap    = 16;

    private const float BaseHeight       = 220;  // hauteur pour une description d'une seule ligne
    private const float HorizPadding     = 28;   // marge gauche/droite réservée au texte
    private const float DescTop          = 90;   // ligne de base de la première ligne de description
    private const float BtnBottomMargin  = 28;   // espace sous les boutons

    private readonly LocalizationService _localization;
    private readonly Action              _onConfirm;

    private readonly SKPaint _titlePaint   = new() { Color = new SKColor(255, 180, 60), IsAntialias = true };
    private readonly SKPaint _cancelPaint  = new() { Color = new SKColor(55,  55, 65),  Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _confirmPaint = new() { Color = new SKColor(140, 90, 20),  Style = SKPaintStyle.Fill, IsAntialias = true };

    private SKRect   _cancelRect  = SKRect.Empty;
    private SKRect   _confirmRect = SKRect.Empty;
    private long     _refundAmount;
    private string[] _descLines   = [];
    private float    _popupHeight = BaseHeight;

    public ResearchCancelPopupRenderer(LocalizationService localization, Action onConfirm)
    {
        _localization = localization;
        _onConfirm    = onConfirm;
    }

    public void Open(long refundAmount)
    {
        _refundAmount = refundAmount;
        LayoutDescription();
        Open();
    }

    /// <summary>
    /// Découpe la description en lignes et adapte la hauteur du popup.
    /// La mesure se fait à l'échelle 1 : largeur disponible et taille de police suivent
    /// toutes deux le facteur d'échelle, le découpage est donc identique à toute échelle.
    /// </summary>
    private void LayoutDescription()
    {
        string desc = _localization.GetFormated("research_cancel_desc", _refundAmount);
        using var font = new SKFont { Size = BodyFontSize, Typeface = SkiaFonts.Regular };

        var layout = SkiaTextUtils.MeasureWrappedText(desc, PopupWidth - 2 * HorizPadding, font);
        _descLines = [.. layout.Lines];

        int extraLines = Math.Max(0, _descLines.Length - 1);
        _popupHeight = BaseHeight + extraLines * font.Spacing;
    }

    public void Render(SKCanvas canvas, SKSize canvasSize, float scale = 1f)
    {
        if (!IsOpen || Disposed) return;
        CanvasSize = canvasSize;
        float s    = ComputeScale(scale);
        UpdateFonts(s);

        float popupW     = PopupWidth  * s;
        float popupH     = PopupHeight * s;
        float btnW       = BtnWidth    * s;
        float btnH       = BtnHeight   * s;
        float btnGap     = BtnGap      * s;
        var   popup      = GetCenteredRect(s);
        float totalBtns  = btnW * 2 + btnGap;
        float btnStartX  = popup.Left + (popupW - totalBtns) / 2f;

        DrawBackground(canvas, popup, s);

        string title = _localization.Get("research_cancel_title");
        SkiaTextUtils.DrawText(canvas, title, popup.Left + popupW / 2f, popup.Top + 44 * s, SKTextAlign.Center, TitleFont, _titlePaint);

        float lineHeight = BodyFont!.Spacing;
        float descY      = popup.Top + DescTop * s;
        for (int i = 0; i < _descLines.Length; i++)
            SkiaTextUtils.DrawText(canvas, _descLines[i], popup.Left + popupW / 2f, descY + i * lineHeight,
                                   SKTextAlign.Center, BodyFont, SubtlePaint);

        float btnY = popup.Bottom - (BtnBottomMargin + BtnHeight) * s;
        _cancelRect  = new SKRect(btnStartX,              btnY, btnStartX + btnW,          btnY + btnH);
        _confirmRect = new SKRect(btnStartX + btnW + btnGap, btnY, btnStartX + totalBtns, btnY + btnH);

        DrawButton(canvas, _cancelRect,  _cancelPaint,  _localization.Get("research_cancel_btn_cancel"),  s);
        DrawButton(canvas, _confirmRect, _confirmPaint, _localization.Get("research_cancel_btn_confirm"), s);
    }

    public void HandlePointerPressed(SKPoint pos, PointerButton button)
    {
        if (!IsOpen || Disposed) return;

        if (_cancelRect.Contains(pos.X, pos.Y))  { IsOpen = false; return; }
        if (_confirmRect.Contains(pos.X, pos.Y)) { IsOpen = false; _onConfirm(); }
    }

    public override void Dispose()
    {
        if (Disposed) return;
        _titlePaint.Dispose();
        _cancelPaint.Dispose();
        _confirmPaint.Dispose();
        base.Dispose();
    }
}
