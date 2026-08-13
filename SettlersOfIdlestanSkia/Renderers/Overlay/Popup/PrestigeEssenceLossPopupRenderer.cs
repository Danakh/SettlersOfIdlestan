using SettlersOfIdlestanSkia.Services.Localization;
using SettlersOfIdlestanSkia.Services;

namespace SettlersOfIdlestanSkia.Renderers.Overlay.Popup;

public sealed class PrestigeEssenceLossPopupRenderer : PopupRendererBase
{
    private readonly LocalizationService _localization;
    private readonly Action<bool>        _onConfirm;

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
}
