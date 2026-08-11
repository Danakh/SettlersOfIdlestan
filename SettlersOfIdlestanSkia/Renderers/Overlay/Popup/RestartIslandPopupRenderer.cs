using SettlersOfIdlestanSkia.Services;
using SettlersOfIdlestanSkia.Services.Localization;

namespace SettlersOfIdlestanSkia.Renderers.Overlay.Popup;

/// <summary>
/// Confirmation avant de recommencer l'ile depuis le menu des reglages. Recommencer detruit la
/// partie en cours sans rien accorder en echange — contrairement a un Prestige, aucun point n'est
/// gagne — d'ou la confirmation explicite.
///
/// Modale purement declarative : elle n'a pas de rendu Skia, seulement un instantane et la
/// machine a etats de ses boutons, la vue etant celle de l'hote Avalonia.
/// </summary>
public sealed class RestartIslandPopupRenderer : PopupRendererBase
{
    private const string KeyCancel = "cancel";
    private const string KeyConfirm = "confirm";

    private readonly LocalizationService _localization;
    private readonly Action _onConfirm;

    public RestartIslandPopupRenderer(LocalizationService localization, Action onConfirm)
    {
        _localization = localization;
        _onConfirm = onConfirm;
    }

    /// <summary>Instantane pour la vue portee par l'hote.</summary>
    public ModalPopupSnapshot GetSnapshot()
    {
        if (!IsOpen || Disposed) return ModalPopupSnapshot.None;

        return new ModalPopupSnapshot(
            IsOpen: true,
            Id: ModalPopupSnapshot.IdRestartIsland,
            Title: _localization.Get("restart_island_title"),
            Tone: ModalPopupTone.Danger,
            Lines:
            [
                _localization.Get("restart_island_line1"),
                _localization.Get("restart_island_line2"),
                _localization.Get("restart_island_line3"),
            ],
            Buttons:
            [
                new(KeyCancel, _localization.Get("restart_island_btn_cancel"), ModalPopupButtonTone.Neutral),
                new(KeyConfirm, _localization.Get("restart_island_btn_confirm"), ModalPopupButtonTone.Danger),
            ],
            // Annuler tient deja lieu de renoncement : une croix ferait doublon.
            HasCloseButton: false,
            ButtonsSideBySide: true);
    }

    /// <summary>Declenche un bouton depuis la vue de l'hote.</summary>
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
