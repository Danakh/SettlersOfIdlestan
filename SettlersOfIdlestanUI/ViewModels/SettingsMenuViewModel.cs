using System.Collections.ObjectModel;
using SkiaLayer = SettlersOfIdlestanSkia.Services;

namespace SettlersOfIdlestanUI.ViewModels;

/// <summary>Un item du menu de l'engrenage.</summary>
public sealed class SettingsMenuItemViewModel
{
    public SettingsMenuItemViewModel(SkiaLayer.SettingsMenuItemSnapshot snapshot)
    {
        Key = snapshot.Key;
        Label = snapshot.Label;
        IsSeparator = snapshot.IsSeparator;
    }

    /// Cle de localisation, qui sert aussi d'identifiant stable et de routage.
    public string Key { get; }

    public string Label { get; }
    public bool IsSeparator { get; }

    /// Un separateur n'est pas cliquable : il ne fait que separer deux groupes.
    public bool IsClickable => !IsSeparator;
}

/// <summary>
/// Menu deroulant de l'engrenage. La composition (dont la presence de la section de debogage) et
/// l'effet de chaque item restent dans SettingsMenu cote Skia : ce ViewModel reflete
/// l'instantane et relaie les commandes.
/// </summary>
public sealed class SettingsMenuViewModel : ViewModelBase
{
    private readonly GameRuntimeHost _host;

    private bool _isOpen;
    private IReadOnlyList<SkiaLayer.SettingsMenuItemSnapshot> _last = [];

    public SettingsMenuViewModel(GameRuntimeHost host) => _host = host;

    public ObservableCollection<SettingsMenuItemViewModel> Items { get; } = [];

    public bool IsOpen { get => _isOpen; private set => SetProperty(ref _isOpen, value); }

    public void Refresh()
    {
        var snapshot = _host.GetSettingsMenuSnapshot();
        IsOpen = snapshot.IsOpen;

        // La composition ne change qu'entre parties (section de debogage) : l'egalite
        // structurelle des records suffit a n'y toucher que lorsqu'elle bouge vraiment.
        if (_last.SequenceEqual(snapshot.Items)) return;
        _last = snapshot.Items;

        Items.Clear();
        foreach (var item in snapshot.Items) Items.Add(new SettingsMenuItemViewModel(item));
    }

    public void Invoke(SettingsMenuItemViewModel item)
    {
        _host.InvokeSettingsMenuItem(item.Key);
        Refresh();
    }

    /// <summary>Referme le menu, sur un clic hors de sa surface.</summary>
    public void Close()
    {
        _host.CloseSettingsMenu();
        Refresh();
    }
}
