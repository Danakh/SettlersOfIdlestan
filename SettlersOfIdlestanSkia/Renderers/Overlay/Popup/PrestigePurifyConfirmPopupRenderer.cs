using SettlersOfIdlestanSkia.Services.Localization;
using SettlersOfIdlestanSkia.Services;

namespace SettlersOfIdlestanSkia.Renderers.Overlay.Popup;

/// <summary>
/// Confirmation avant un Prestige Purifie, qui fait redescendre la corruption du monde d'un niveau
/// (voir PrestigeController.IsPurifiedPrestigeAvailable). Toujours demandee : la corruption ne
/// remonte qu'un niveau par Prestige Corrompu, la redescendre coute donc un run complet.
///
/// Deux textes selon le niveau vise : sous le seuil de progression
/// (PrestigeController.PurifiedPrestigeDropsBelowProgressionThreshold, soit
/// AbyssGate.RequiredCorruptionLevel) l'avertissement explique que la Faille des Abysses redevient
/// hors de portee ; au-dela, un simple rappel du niveau vise suffit. Meme forme que les autres
/// confirmations du popup de prestige : elle emprunte le meme instantane et la meme vue.
/// </summary>
public sealed class PrestigePurifyConfirmPopupRenderer : PopupRendererBase
{
    private readonly LocalizationService _localization;
    private readonly Action              _onConfirm;

    private int  _nextCorruptionLevel;
    private int  _progressionThreshold;
    private bool _dropsBelowProgressionThreshold;

    public PrestigePurifyConfirmPopupRenderer(LocalizationService localization, Action onConfirm)
    {
        _localization = localization;
        _onConfirm    = onConfirm;
    }

    public void Open(int nextCorruptionLevel, bool dropsBelowProgressionThreshold, int progressionThreshold)
    {
        _nextCorruptionLevel            = nextCorruptionLevel;
        _dropsBelowProgressionThreshold = dropsBelowProgressionThreshold;
        _progressionThreshold           = progressionThreshold;
        Open();
    }

    private const string KeyCancel  = "cancel";
    private const string KeyConfirm = "confirm";

    public ModalPopupSnapshot GetSnapshot()
    {
        if (!IsOpen || Disposed) return ModalPopupSnapshot.None;

        var lines = new List<string>
        {
            _localization.GetFormated("prestige_purify_confirm_desc", _nextCorruptionLevel),
        };
        lines.Add(_dropsBelowProgressionThreshold
            ? _localization.GetFormated("prestige_purify_confirm_progression", _progressionThreshold)
            : _localization.Get("prestige_purify_confirm_plain"));

        return new ModalPopupSnapshot(
            IsOpen: true,
            Id: ModalPopupSnapshot.IdPrestigePurifyConfirm,
            Title: _localization.Get("prestige_purify_confirm_title"),
            // Choix couteux mais pas destructeur : meme ton que les deux autres confirmations.
            Tone: ModalPopupTone.Highlight,
            Lines: lines,
            Buttons:
            [
                new(KeyCancel, _localization.Get("prestige_purify_confirm_btn_cancel"), ModalPopupButtonTone.Neutral),
                new(KeyConfirm, _localization.Get("prestige_purify_confirm_btn_confirm"), ModalPopupButtonTone.Danger),
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
