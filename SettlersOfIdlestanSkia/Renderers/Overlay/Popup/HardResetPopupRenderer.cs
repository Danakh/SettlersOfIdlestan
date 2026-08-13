using SettlersOfIdlestanSkia.Services.Localization;
using SettlersOfIdlestanSkia.Services;

namespace SettlersOfIdlestanSkia.Renderers.Overlay.Popup;

public sealed class HardResetPopupRenderer : PopupRendererBase
{
    private readonly LocalizationService _localization;
    private readonly IFileSystemService  _fileSystemService;
    private readonly Action              _onConfirm;

    public HardResetPopupRenderer(
        LocalizationService localization,
        IFileSystemService  fileSystemService,
        Action              onConfirm)
    {
        _localization      = localization;
        _fileSystemService = fileSystemService;
        _onConfirm         = onConfirm;
    }

    private const string KeyCancel  = "cancel";
    private const string KeyConfirm = "confirm";

    /// <summary>Instantané pour une vue portée par l'hôte.</summary>
    public ModalPopupSnapshot GetSnapshot()
    {
        if (!IsOpen || Disposed) return ModalPopupSnapshot.None;

        return new ModalPopupSnapshot(
            IsOpen: true,
            Id: ModalPopupSnapshot.IdHardReset,
            Title: _localization.Get("hard_reset_title"),
            Tone: ModalPopupTone.Danger,
            Lines: [_localization.Get("hard_reset_desc")],
            Buttons:
            [
                new(KeyCancel,  _localization.Get("hard_reset_btn_cancel"),  ModalPopupButtonTone.Neutral),
                new(KeyConfirm, _localization.Get("hard_reset_btn_confirm"), ModalPopupButtonTone.Danger),
            ],
            // Annuler tient deja lieu de renoncement : une croix ferait doublon.
            HasCloseButton: false,
            ButtonsSideBySide: true);
    }

    /// <summary>Déclenche un bouton, depuis le hit-testing Skia comme depuis la vue de l'hôte.</summary>
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
                _ = _fileSystemService.DeleteAuto();
                _onConfirm();
                break;
        }
    }
}
