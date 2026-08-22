using SettlersOfIdlestanSkia.Services;
using SettlersOfIdlestanSkia.Services.Localization;

namespace SettlersOfIdlestanSkia.Renderers.Overlay.Popup;

/// <summary>
/// Confirmation avant une Ascension (hors choix de race, qui reste un panneau à part — voir
/// AscensionRenderer.DrawRaceSelectionOverlayIfNeeded). Une Ascension efface l'île et la
/// progression de Prestige en cours, d'où la confirmation explicite.
///
/// Modale purement déclarative : elle n'a pas de rendu Skia, seulement un instantané et la
/// machine à états de ses boutons, la vue étant celle de l'hôte Avalonia.
/// </summary>
public sealed class AscensionConfirmPopupRenderer : PopupRendererBase
{
    private const string KeyCancel = "cancel";
    private const string KeyConfirm = "confirm";

    private readonly LocalizationService _localization;
    private readonly Action _onConfirm;

    public AscensionConfirmPopupRenderer(LocalizationService localization, Action onConfirm)
    {
        _localization = localization;
        _onConfirm = onConfirm;
    }

    /// <summary>Instantané pour la vue portée par l'hôte.</summary>
    public ModalPopupSnapshot GetSnapshot()
    {
        if (!IsOpen || Disposed) return ModalPopupSnapshot.None;

        return new ModalPopupSnapshot(
            IsOpen: true,
            Id: ModalPopupSnapshot.IdAscensionConfirm,
            Title: _localization.Get("ascension_confirm_title"),
            Tone: ModalPopupTone.Danger,
            Lines: [_localization.Get("ascension_confirm_warning")],
            Buttons:
            [
                new(KeyCancel, _localization.Get("ascension_cancel_button"), ModalPopupButtonTone.Neutral),
                new(KeyConfirm, _localization.Get("ascension_confirm_button"), ModalPopupButtonTone.Danger),
            ],
            // Annuler tient déjà lieu de renoncement : une croix ferait doublon.
            HasCloseButton: false,
            ButtonsSideBySide: true);
    }

    /// <summary>Déclenche un bouton depuis la vue de l'hôte.</summary>
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
