using System.Collections.ObjectModel;
using SkiaLayer = SettlersOfIdlestanSkia.Services;

namespace SettlersOfIdlestanUI.ViewModels;

/// <summary>Un bouton de modale.</summary>
public sealed class ModalPopupButtonViewModel
{
    public ModalPopupButtonViewModel(SkiaLayer.ModalPopupButtonSnapshot snapshot)
    {
        Key = snapshot.Key;
        Label = snapshot.Label;
        Tone = snapshot.Tone;
    }

    /// Identifiant stable du bouton dans sa modale : sert au routage du clic.
    public string Key { get; }

    public string Label { get; }

    public SkiaLayer.ModalPopupButtonTone Tone { get; }
}

/// <summary>
/// La modale bloquante ouverte, s'il y en a une. Une seule vue sert les quatre (partie terminee,
/// sauvegarde corrompue, remise a zero, fin de demo) : elles ont la meme forme, et les regles
/// qui decident de leur ouverture et de l'effet de leurs boutons restent cote renderer.
/// </summary>
public sealed class ModalPopupViewModel : ViewModelBase
{
    private readonly GameRuntimeHost _host;

    private bool _isOpen;
    private string _id = "";
    private string _title = "";
    private SkiaLayer.ModalPopupTone _tone = SkiaLayer.ModalPopupTone.Danger;
    private bool _hasCloseButton;
    private bool _buttonsSideBySide;

    public ModalPopupViewModel(GameRuntimeHost host) => _host = host;

    public ObservableCollection<string> Lines { get; } = [];
    public ObservableCollection<ModalPopupButtonViewModel> Buttons { get; } = [];

    public bool IsOpen { get => _isOpen; private set => SetProperty(ref _isOpen, value); }
    public string Title { get => _title; private set => SetProperty(ref _title, value); }
    public SkiaLayer.ModalPopupTone Tone { get => _tone; private set => SetProperty(ref _tone, value); }
    public bool HasCloseButton { get => _hasCloseButton; private set => SetProperty(ref _hasCloseButton, value); }
    public bool ButtonsSideBySide { get => _buttonsSideBySide; private set => SetProperty(ref _buttonsSideBySide, value); }

    public void Refresh()
    {
        var snapshot = _host.GetModalPopupSnapshot();

        IsOpen = snapshot.IsOpen;
        Title = snapshot.Title;
        Tone = snapshot.Tone;
        HasCloseButton = snapshot.HasCloseButton;
        ButtonsSideBySide = snapshot.ButtonsSideBySide;

        // Contrairement aux panneaux, le contenu d'une modale est fige tant qu'elle est ouverte :
        // on ne reconstruit que lorsque la modale change, repere par son identifiant.
        if (_id == snapshot.Id) return;
        _id = snapshot.Id;

        Lines.Clear();
        foreach (var line in snapshot.Lines) Lines.Add(line);

        Buttons.Clear();
        foreach (var button in snapshot.Buttons) Buttons.Add(new ModalPopupButtonViewModel(button));
    }

    public void Invoke(ModalPopupButtonViewModel button) => Invoke(button.Key);

    public void Close() => Invoke(SkiaLayer.ModalPopupSnapshot.KeyClose);

    private void Invoke(string buttonKey)
    {
        // L'identifiant est capture avant l'appel : le bouton peut fermer la modale, et le
        // rafraichissement qui suit repose alors _id sur la modale suivante — ou sur aucune.
        _host.InvokeModalPopupButton(_id, buttonKey);
        Refresh();
    }
}
