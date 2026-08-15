using SettlersOfIdlestanSkia.Services.Localization;
using SettlersOfIdlestanSkia.Services;

namespace SettlersOfIdlestanSkia.Renderers.Overlay.Popup;

/// <summary>
/// Confirmation avant un prestige corrompu qui porterait la corruption au-delà de
/// PrestigeController.CorruptionWarningLevelWithoutAscension sans qu'aucune Ascension n'ait été
/// faite (voir PrestigeController.CorruptedPrestigeNeedsAscensionWarning). Même forme que la
/// confirmation de perte d'essences : elle emprunte le même instantané et la même vue.
/// </summary>
public sealed class PrestigeCorruptionWarningPopupRenderer : PopupRendererBase
{
    private readonly LocalizationService _localization;
    private readonly Action              _onConfirm;

    private int _nextCorruptionLevel;

    public PrestigeCorruptionWarningPopupRenderer(LocalizationService localization, Action onConfirm)
    {
        _localization = localization;
        _onConfirm    = onConfirm;
    }

    public void Open(int nextCorruptionLevel)
    {
        _nextCorruptionLevel = nextCorruptionLevel;
        Open();
    }

    private const string KeyCancel  = "cancel";
    private const string KeyConfirm = "confirm";

    public ModalPopupSnapshot GetSnapshot()
    {
        if (!IsOpen || Disposed) return ModalPopupSnapshot.None;

        return new ModalPopupSnapshot(
            IsOpen: true,
            Id: ModalPopupSnapshot.IdPrestigeCorruptionWarning,
            Title: _localization.Get("prestige_corruption_warning_title"),
            // Choix couteux mais pas destructeur : meme ton que la perte d'essences.
            Tone: ModalPopupTone.Highlight,
            Lines:
            [
                _localization.GetFormated("prestige_corruption_warning_desc", _nextCorruptionLevel),
                _localization.Get("prestige_corruption_warning_ascension"),
            ],
            Buttons:
            [
                new(KeyCancel, _localization.Get("prestige_corruption_warning_btn_cancel"), ModalPopupButtonTone.Neutral),
                new(KeyConfirm, _localization.Get("prestige_corruption_warning_btn_confirm"), ModalPopupButtonTone.Danger),
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
                _onConfirm();
                break;
        }
    }
}
