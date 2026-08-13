using SettlersOfIdlestanSkia.Services.Localization;
using SettlersOfIdlestanSkia.Services;

namespace SettlersOfIdlestanSkia.Renderers.Overlay.Popup;

public sealed class CorruptSavePopupRenderer : PopupRendererBase
{
    private readonly LocalizationService _localization;
    private readonly IFileSystemService   _fileSystemService;
    private readonly string               _corruptJson;
    private readonly Action               _onStartFresh;
    private readonly Action               _onQuit;

    public CorruptSavePopupRenderer(
        LocalizationService localization,
        IFileSystemService   fileSystemService,
        string               corruptJson,
        Action               onStartFresh,
        Action               onQuit)
    {
        _localization      = localization;
        _fileSystemService = fileSystemService;
        _corruptJson       = corruptJson;
        _onStartFresh      = onStartFresh;
        _onQuit            = onQuit;
    }

    private const string KeyExport  = "export";
    private const string KeyNewGame = "newGame";
    private const string KeyQuit    = "quit";

    /// <summary>Instantané pour une vue portée par l'hôte.</summary>
    public ModalPopupSnapshot GetSnapshot()
    {
        if (!IsOpen || Disposed) return ModalPopupSnapshot.None;

        return new ModalPopupSnapshot(
            IsOpen: true,
            Id: ModalPopupSnapshot.IdCorruptSave,
            Title: _localization.Get("corrupt_save_title"),
            Tone: ModalPopupTone.Danger,
            Lines: [_localization.Get("corrupt_save_line1"), _localization.Get("corrupt_save_line2")],
            Buttons:
            [
                new(KeyExport,  _localization.Get("corrupt_save_btn_export"),   ModalPopupButtonTone.Primary),
                new(KeyNewGame, _localization.Get("corrupt_save_btn_new_game"), ModalPopupButtonTone.Danger),
                new(KeyQuit,    _localization.Get("corrupt_save_btn_quit"),     ModalPopupButtonTone.Neutral),
            ],
            // Aucune partie chargeable derriere : renoncer sans choisir laisserait le jeu vide.
            HasCloseButton: false,
            ButtonsSideBySide: false);
    }

    /// <summary>Déclenche un bouton, depuis le hit-testing Skia comme depuis la vue de l'hôte.</summary>
    public void InvokeButton(string key)
    {
        if (!IsOpen || Disposed) return;

        switch (key)
        {
            // Exporter ne ferme pas : le joueur doit encore choisir entre repartir et quitter.
            case KeyExport:
                _ = _fileSystemService.SaveText("sauvegarde_corrompue.json", _corruptJson);
                break;
            case KeyNewGame:
                IsOpen = false;
                _onStartFresh();
                break;
            case KeyQuit:
                _onQuit();
                break;
        }
    }
}
