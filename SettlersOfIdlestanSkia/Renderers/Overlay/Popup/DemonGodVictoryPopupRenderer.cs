using SettlersOfIdlestan.Model.Monsters;
using SettlersOfIdlestanSkia.Services.Localization;
using SettlersOfIdlestanSkia.Services;

namespace SettlersOfIdlestanSkia.Renderers.Overlay.Popup;

/// <summary>
/// Modale de victoire : le tout premier Dieu demon abattu de la partie. Le joueur peut considerer
/// qu'il a gagne, et le texte le lui dit — mais rien n'est perdu ni verrouille, il referme et
/// continue de jouer. C'est la raison pour laquelle elle ne s'ouvre qu'une fois
/// (GodState.HighestDemonGodLevelDefeated, voir PandemoniumGateController.RegisterDemonGodDefeat) :
/// les victoires suivantes ne se signalent plus que par leur toast et leur entree de journal.
/// </summary>
public sealed class DemonGodVictoryPopupRenderer : PopupRendererBase
{
    private readonly LocalizationService _localization;

    /// <summary>Niveau du boss abattu, cite dans le titre et la premiere ligne.</summary>
    private int _level;

    public DemonGodVictoryPopupRenderer(LocalizationService localization)
    {
        _localization = localization;
    }

    private const string KeyContinue = "continue";

    /// <summary>Ouvre la modale sur le bilan de la victoire (voir <see cref="DemonGodDefeat"/>).</summary>
    public void Open(DemonGodDefeat defeat)
    {
        _level = defeat.Level;
        Open();
    }

    /// <summary>Instantane pour une vue portee par l'hote.</summary>
    public ModalPopupSnapshot GetSnapshot()
    {
        if (!IsOpen || Disposed) return ModalPopupSnapshot.None;

        return new ModalPopupSnapshot(
            IsOpen: true,
            Id: ModalPopupSnapshot.IdDemonGodVictory,
            Title: _localization.Get("demon_god_victory_title"),
            Tone: ModalPopupTone.Highlight,
            Lines:
            [
                _localization.GetFormated("demon_god_victory_line1", _level),
                _localization.Get("demon_god_victory_line2"),
                _localization.Get("demon_god_victory_line3"),
            ],
            Buttons: [new(KeyContinue, _localization.Get("demon_god_victory_continue"), ModalPopupButtonTone.Confirm)],
            // Rien n'est perdu : la partie continue exactement comme avant.
            HasCloseButton: true,
            ButtonsSideBySide: false);
    }

    /// <summary>Declenche un bouton, depuis le hit-testing Skia comme depuis la vue de l'hote.</summary>
    public void InvokeButton(string key)
    {
        if (!IsOpen || Disposed) return;

        switch (key)
        {
            case ModalPopupSnapshot.KeyClose:
            case KeyContinue:
                Close();
                break;
        }
    }
}
