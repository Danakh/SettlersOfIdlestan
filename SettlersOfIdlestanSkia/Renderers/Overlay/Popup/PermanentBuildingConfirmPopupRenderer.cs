using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestanSkia.Services;
using SettlersOfIdlestanSkia.Services.Localization;

namespace SettlersOfIdlestanSkia.Renderers.Overlay.Popup;

/// <summary>
/// Confirmation avant de choisir un bâtiment unique permanent d'Ascension (voir
/// AscensionController.SelectPermanentUniqueBuilding) : ce choix est définitif, aucune interface ne
/// permet plus de revenir dessus une fois confirmé — d'où cet avertissement, même forme que
/// AscensionConfirmPopupRenderer.
///
/// Modale purement déclarative : elle n'a pas de rendu Skia, seulement un instantané et la
/// machine à états de ses boutons, la vue étant celle de l'hôte Avalonia.
/// </summary>
public sealed class PermanentBuildingConfirmPopupRenderer : PopupRendererBase
{
    private const string KeyCancel = "cancel";
    private const string KeyConfirm = "confirm";

    private readonly LocalizationService _localization;
    private readonly Action<BuildingType> _onConfirm;
    private BuildingType _pendingType;

    public PermanentBuildingConfirmPopupRenderer(LocalizationService localization, Action<BuildingType> onConfirm)
    {
        _localization = localization;
        _onConfirm = onConfirm;
    }

    /// <summary>Ouvre la confirmation pour le bâtiment sur lequel le joueur vient de cliquer.</summary>
    public void Open(BuildingType type)
    {
        _pendingType = type;
        Open();
    }

    /// <summary>Instantané pour la vue portée par l'hôte.</summary>
    public ModalPopupSnapshot GetSnapshot()
    {
        if (!IsOpen || Disposed) return ModalPopupSnapshot.None;

        string buildingName = _localization.Get($"building_{_pendingType.ToString().ToLowerInvariant()}_name");
        return new ModalPopupSnapshot(
            IsOpen: true,
            Id: ModalPopupSnapshot.IdPermanentBuildingConfirm,
            Title: _localization.Get("ascension_permanent_building_confirm_title"),
            Tone: ModalPopupTone.Highlight,
            Lines: [_localization.GetFormated("ascension_permanent_building_confirm_desc", buildingName)],
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
                _onConfirm(_pendingType);
                break;
        }
    }
}
