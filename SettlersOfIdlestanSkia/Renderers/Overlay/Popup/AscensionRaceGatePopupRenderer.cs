using SettlersOfIdlestan.Model.Races;
using SettlersOfIdlestanSkia.Services;
using SettlersOfIdlestanSkia.Services.Localization;

namespace SettlersOfIdlestanSkia.Renderers.Overlay.Popup;

/// <summary>
/// Clic sur l'onglet Surface pendant qu'une Ascension attend son choix de race (voir
/// AscensionController.IsAscensionPending, OverlayRenderer.HandleTabClickFromHost). Il n'y a alors
/// plus d'île à afficher — le clic vaut donc « repartir sur une nouvelle île », soit exactement le
/// bouton Confirmer de l'onglet Races. Deux issues selon l'état du choix, portées par la même
/// modale car déclenchées par le même geste :
///  - aucune race valide sélectionnée : avertissement, un seul bouton, rien ne se passe ;
///  - race sélectionnée : confirmation avant de générer la nouvelle partie, le geste étant
///    irréversible et venu d'un onglet où rien ne l'annonçait.
///
/// Modale purement déclarative : elle n'a pas de rendu Skia, seulement un instantané et la
/// machine à états de ses boutons, la vue étant celle de l'hôte Avalonia.
/// </summary>
public sealed class AscensionRaceGatePopupRenderer : PopupRendererBase
{
    private const string KeyCancel  = "cancel";
    private const string KeyConfirm = "confirm";
    private const string KeyOk      = "ok";

    private readonly LocalizationService _localization;
    private readonly Action<RaceId> _onConfirm;

    /// <summary>Race à confirmer, ou null quand la modale est ouverte en avertissement.</summary>
    private RaceId? _chosenRace;

    public AscensionRaceGatePopupRenderer(LocalizationService localization, Action<RaceId> onConfirm)
    {
        _localization = localization;
        _onConfirm = onConfirm;
    }

    /// <summary>Ouvre l'avertissement : aucune race valide n'est sélectionnée.</summary>
    public void OpenWarning()
    {
        _chosenRace = null;
        Open();
    }

    /// <summary>Ouvre la confirmation de départ sur une nouvelle île avec la race choisie.</summary>
    public void OpenConfirm(RaceId race)
    {
        _chosenRace = race;
        Open();
    }

    /// <summary>Instantané pour la vue portée par l'hôte.</summary>
    public ModalPopupSnapshot GetSnapshot()
    {
        if (!IsOpen || Disposed) return ModalPopupSnapshot.None;

        if (_chosenRace is not { } race)
        {
            return new ModalPopupSnapshot(
                IsOpen: true,
                Id: ModalPopupSnapshot.IdAscensionRaceRequired,
                Title: _localization.Get("ascension_race_required_title"),
                Tone: ModalPopupTone.Highlight,
                Lines: [_localization.Get("ascension_race_required_desc")],
                Buttons: [new(KeyOk, _localization.Get("ui_ok"), ModalPopupButtonTone.Primary)],
                HasCloseButton: true,
                ButtonsSideBySide: false);
        }

        string raceName = _localization.Get(RaceDefinitions.Get(race).NameKey);
        return new ModalPopupSnapshot(
            IsOpen: true,
            Id: ModalPopupSnapshot.IdAscensionRaceConfirm,
            Title: _localization.Get("ascension_race_start_title"),
            Tone: ModalPopupTone.Highlight,
            Lines: [_localization.GetFormated("ascension_race_start_desc", raceName)],
            Buttons:
            [
                new(KeyCancel, _localization.Get("ascension_cancel_button"), ModalPopupButtonTone.Neutral),
                new(KeyConfirm, _localization.Get("ascension_confirm_button"), ModalPopupButtonTone.Confirm),
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
            case KeyOk:
            case KeyCancel:
            case ModalPopupSnapshot.KeyClose:
                Close();
                break;
            case KeyConfirm:
                // La race est relue avant fermeture : Close() ne la remet pas à null, mais l'appel
                // régénère toute la partie et le renderer peut être réinitialisé au passage.
                if (_chosenRace is { } race)
                {
                    Close();
                    _onConfirm(race);
                }
                break;
        }
    }
}
