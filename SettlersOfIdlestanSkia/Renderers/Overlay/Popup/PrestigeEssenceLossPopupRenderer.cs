using SettlersOfIdlestanSkia.Services.Localization;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Services;
using SkiaSharp;

namespace SettlersOfIdlestanSkia.Renderers.Overlay.Popup;

public sealed class PrestigeEssenceLossPopupRenderer : PopupRendererBase
{
    protected override float PopupWidth  => 440;
    protected override float PopupHeight => 220;

    private const float BtnWidth  = 180;
    private const float BtnHeight = 42;
    private const float BtnGap    = 16;

    private readonly LocalizationService _localization;
    private readonly Action<bool>        _onConfirm;

    private readonly SKPaint _titlePaint   = new() { Color = new SKColor(255, 180, 60), IsAntialias = true };
    private readonly SKPaint _cancelPaint  = new() { Color = new SKColor(55,  55, 65),  Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _confirmPaint = new() { Color = new SKColor(140, 90, 20),  Style = SKPaintStyle.Fill, IsAntialias = true };

    private SKRect _cancelRect  = SKRect.Empty;
    private SKRect _confirmRect = SKRect.Empty;
    private int    _essenceLoss;
    private bool   _corrupted;

    public PrestigeEssenceLossPopupRenderer(LocalizationService localization, Action<bool> onConfirm)
    {
        _localization = localization;
        _onConfirm    = onConfirm;
    }

    public void Open(int essenceLoss, bool corrupted)
    {
        _essenceLoss = essenceLoss;
        _corrupted   = corrupted;
        Open();
    }

    public void Render(SKCanvas canvas, SKSize canvasSize, float scale = 1f)
    {
        if (!IsOpen || Disposed) return;
        CanvasSize = canvasSize;
        float s    = ComputeScale(scale);
        UpdateFonts(s);

        float popupW     = PopupWidth  * s;
        float btnW       = BtnWidth    * s;
        float btnH       = BtnHeight   * s;
        float btnGap     = BtnGap      * s;
        var   popup      = GetCenteredRect(s);
        float totalBtns  = btnW * 2 + btnGap;
        float btnStartX  = popup.Left + (popupW - totalBtns) / 2f;

        DrawBackground(canvas, popup, s);

        string title = _localization.Get("prestige_essence_loss_title");
        SkiaTextUtils.DrawText(canvas, title, popup.Left + popupW / 2f, popup.Top + 44 * s, SKTextAlign.Center, TitleFont, _titlePaint);

        string desc = _localization.GetFormated("prestige_essence_loss_desc", _essenceLoss);
        float  descW = BodyFont!.MeasureText(desc);
        SkiaTextUtils.DrawText(canvas, desc, popup.Left + (popupW - descW) / 2f, popup.Top + 90 * s, BodyFont, SubtlePaint);

        float btnY = popup.Top + 150 * s;
        _cancelRect  = new SKRect(btnStartX,                 btnY, btnStartX + btnW,          btnY + btnH);
        _confirmRect = new SKRect(btnStartX + btnW + btnGap, btnY, btnStartX + totalBtns,      btnY + btnH);

        DrawButton(canvas, _cancelRect,  _cancelPaint,  _localization.Get("prestige_essence_loss_btn_cancel"),  s);
        DrawButton(canvas, _confirmRect, _confirmPaint, _localization.Get("prestige_essence_loss_btn_confirm"), s);
    }

    public void HandlePointerPressed(SKPoint pos, PointerButton button)
    {
        if (!IsOpen || Disposed) return;

        if (_cancelRect.Contains(pos.X, pos.Y))  { InvokeButton(KeyCancel);  return; }
        if (_confirmRect.Contains(pos.X, pos.Y)) { InvokeButton(KeyConfirm); }
    }

    private const string KeyCancel  = "cancel";
    private const string KeyConfirm = "confirm";

    /// <summary>
    /// Instantane pour une vue portee par l'hote. Cette confirmation a la meme forme que les
    /// modales bloquantes de GameScreen — titre, une ligne, deux boutons — et emprunte donc leur
    /// instantane et leur vue plutot que d'en faire ecrire une seconde.
    /// </summary>
    public ModalPopupSnapshot GetSnapshot()
    {
        if (!IsOpen || Disposed) return ModalPopupSnapshot.None;

        return new ModalPopupSnapshot(
            IsOpen: true,
            Id: ModalPopupSnapshot.IdPrestigeEssenceLoss,
            Title: _localization.Get("prestige_essence_loss_title"),
            // Ni perte definitive ni simple information : un avertissement avant un choix couteux.
            Tone: ModalPopupTone.Highlight,
            Lines: [_localization.GetFormated("prestige_essence_loss_desc", _essenceLoss)],
            Buttons:
            [
                new(KeyCancel, _localization.Get("prestige_essence_loss_btn_cancel"), ModalPopupButtonTone.Neutral),
                new(KeyConfirm, _localization.Get("prestige_essence_loss_btn_confirm"), ModalPopupButtonTone.Danger),
            ],
            // Annuler tient lieu de renoncement.
            HasCloseButton: false,
            ButtonsSideBySide: true);
    }

    /// <summary>Declenche un bouton, depuis le hit-testing Skia comme depuis la vue de l'hote.</summary>
    public void InvokeButton(string key)
    {
        if (!IsOpen || Disposed) return;

        switch (key)
        {
            case KeyCancel:
                IsOpen = false;
                break;
            case KeyConfirm:
                IsOpen = false;
                _onConfirm(_corrupted);
                break;
        }
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
