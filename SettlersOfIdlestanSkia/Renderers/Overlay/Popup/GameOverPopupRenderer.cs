using SettlersOfIdlestanSkia.Services.Localization;
using SettlersOfIdlestanSkia.Services;

namespace SettlersOfIdlestanSkia.Renderers.Overlay.Popup;

public sealed class GameOverPopupRenderer : PopupRendererBase
{
    private readonly LocalizationService _localization;
    private readonly Action              _onRestart;

    public GameOverPopupRenderer(LocalizationService localization, Action onRestart)
    {
        _localization = localization;
        _onRestart    = onRestart;
    }

    private const string KeyRestart = "restart";

    /// <summary>Instantané pour une vue portée par l'hôte.</summary>
    public ModalPopupSnapshot GetSnapshot()
    {
        if (!IsOpen || Disposed) return ModalPopupSnapshot.None;

        return new ModalPopupSnapshot(
            IsOpen: true,
            Id: ModalPopupSnapshot.IdGameOver,
            Title: _localization.Get("game_over_title"),
            Tone: ModalPopupTone.Danger,
            Lines: [_localization.Get("game_over_line1"), _localization.Get("game_over_line2")],
            Buttons: [new(KeyRestart, _localization.Get("game_over_btn_restart"), ModalPopupButtonTone.Primary)],
            // La partie est finie : il n'y a pas d'etat auquel revenir, donc pas de croix.
            HasCloseButton: false,
            ButtonsSideBySide: false);
    }

    /// <summary>Déclenche un bouton, depuis le hit-testing Skia comme depuis la vue de l'hôte.</summary>
    public void InvokeButton(string key)
    {
        if (!IsOpen || Disposed) return;
        if (key != KeyRestart) return;
        IsOpen = false;
        _onRestart();
    }
}
