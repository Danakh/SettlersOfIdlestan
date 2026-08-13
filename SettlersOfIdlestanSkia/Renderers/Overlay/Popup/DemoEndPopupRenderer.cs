using SettlersOfIdlestanSkia.Services.Localization;
using SettlersOfIdlestanSkia.Services;

namespace SettlersOfIdlestanSkia.Renderers.Overlay.Popup;

public sealed class DemoEndPopupRenderer : PopupRendererBase
{
    private readonly LocalizationService _localization;
    private readonly Action              _onReplay;

    public DemoEndPopupRenderer(LocalizationService localization, Action onReplay)
    {
        _localization = localization;
        _onReplay     = onReplay;
    }

    private const string KeyReplay = "replay";

    /// <summary>Instantané pour une vue portée par l'hôte.</summary>
    public ModalPopupSnapshot GetSnapshot()
    {
        if (!IsOpen || Disposed) return ModalPopupSnapshot.None;

        return new ModalPopupSnapshot(
            IsOpen: true,
            Id: ModalPopupSnapshot.IdDemoEnd,
            Title: _localization.Get("demo_end_title"),
            Tone: ModalPopupTone.Highlight,
            Lines: [_localization.Get("demo_end_line1"), _localization.Get("demo_end_line2")],
            Buttons: [new(KeyReplay, _localization.Get("demo_end_replay"), ModalPopupButtonTone.Confirm)],
            // Rien n'est perdu : le joueur peut refermer et continuer a regarder sa partie.
            HasCloseButton: true,
            ButtonsSideBySide: false);
    }

    /// <summary>Déclenche un bouton, depuis le hit-testing Skia comme depuis la vue de l'hôte.</summary>
    public void InvokeButton(string key)
    {
        if (!IsOpen || Disposed) return;

        switch (key)
        {
            case ModalPopupSnapshot.KeyClose:
                Close();
                break;
            case KeyReplay:
                Close();
                _onReplay();
                break;
        }
    }
}
