using System.Collections.ObjectModel;
using SkiaLayer = SettlersOfIdlestanSkia.Services;

namespace SettlersOfIdlestanUI.ViewModels;

/// <summary>Une option d'un reglage a choix exclusif.</summary>
public sealed class SettingChoiceViewModel : ViewModelBase
{
    private bool _isSelected;
    private string _label;

    public SettingChoiceViewModel(SkiaLayer.SettingChoiceSnapshot snapshot)
    {
        Key = snapshot.Key;
        _label = snapshot.Label;
        _isSelected = snapshot.IsSelected;
    }

    public string Key { get; }

    /// <summary>
    /// Doit rester modifiable : changer la langue depuis ce panneau relocalise ses propres
    /// libelles. Fige a la construction, les boutons de format des nombres restaient en francais
    /// apres un passage en anglais.
    /// </summary>
    public string Label { get => _label; internal set => SetProperty(ref _label, value); }

    public bool IsSelected { get => _isSelected; internal set => SetProperty(ref _isSelected, value); }
}

/// <summary>Un reglage. Sa nature dicte lequel de ses controles est affiche.</summary>
public sealed class SettingRowViewModel : ViewModelBase
{
    private SkiaLayer.SettingRowSnapshot _snapshot;

    public SettingRowViewModel(SkiaLayer.SettingRowSnapshot snapshot)
    {
        _snapshot = snapshot;
        foreach (var choice in snapshot.Choices) Choices.Add(new SettingChoiceViewModel(choice));
    }

    /// Identifiant stable du reglage : sert au routage de la commande.
    public string Key => _snapshot.Key;

    public string Label => _snapshot.Label;
    public SkiaLayer.SettingRowKind Kind => _snapshot.Kind;
    public bool IsEnabled => _snapshot.IsEnabled;
    public bool ToggleValue => _snapshot.ToggleValue;
    public double SliderValue => _snapshot.SliderValue;
    public double SliderMin => _snapshot.SliderMin;
    public double SliderMax => _snapshot.SliderMax;
    public string SliderText => _snapshot.SliderText;
    public string TextValue => _snapshot.TextValue;

    public ObservableCollection<SettingChoiceViewModel> Choices { get; } = [];

    // Une ligne n'affiche qu'un type de controle : ces drapeaux pilotent leur visibilite.
    public bool IsToggle => Kind == SkiaLayer.SettingRowKind.Toggle;
    public bool IsChoice => Kind == SkiaLayer.SettingRowKind.Choice;
    public bool IsSlider => Kind == SkiaLayer.SettingRowKind.Slider;
    public bool IsTextInput => Kind == SkiaLayer.SettingRowKind.TextInput;

    internal void Apply(SkiaLayer.SettingRowSnapshot snapshot)
    {
        if (_snapshot == snapshot) return;
        var previous = _snapshot;
        _snapshot = snapshot;

        if (previous.Label != snapshot.Label) RaisePropertyChanged(nameof(Label));
        if (previous.IsEnabled != snapshot.IsEnabled) RaisePropertyChanged(nameof(IsEnabled));
        if (previous.ToggleValue != snapshot.ToggleValue) RaisePropertyChanged(nameof(ToggleValue));
        if (previous.TextValue != snapshot.TextValue) RaisePropertyChanged(nameof(TextValue));
        if (previous.SliderValue != snapshot.SliderValue)
        {
            RaisePropertyChanged(nameof(SliderValue));
            RaisePropertyChanged(nameof(SliderText));
        }

        for (int i = 0; i < Choices.Count && i < snapshot.Choices.Count; i++)
        {
            Choices[i].Label = snapshot.Choices[i].Label;
            Choices[i].IsSelected = snapshot.Choices[i].IsSelected;
        }
    }
}

/// <summary>
/// Panneau de reglages, partage par le popup en jeu et l'ecran-titre. Les reglages disponibles
/// et l'effet de chacun restent dans SettingsContentPanel : ce ViewModel reflete l'instantane et
/// relaie les commandes vers celui que l'hote lui a designe.
/// </summary>
public sealed class SettingsPanelViewModel : ViewModelBase
{
    private readonly Action<string> _toggle;
    private readonly Action<string, string> _setChoice;
    private readonly Action<string, double> _setSlider;
    private readonly Action<string, string> _setText;

    /// <param name="toggle">Commandes injectees plutot que le runtime entier : le meme panneau
    /// sert au popup en jeu et a l'ecran-titre, qui ne passent pas par le meme chemin.</param>
    public SettingsPanelViewModel(
        Action<string> toggle,
        Action<string, string> setChoice,
        Action<string, double> setSlider,
        Action<string, string> setText)
    {
        _toggle = toggle;
        _setChoice = setChoice;
        _setSlider = setSlider;
        _setText = setText;
    }

    public ObservableCollection<SettingRowViewModel> Rows { get; } = [];

    /// <summary>Reflete un instantane. Appelee par le proprietaire du panneau a chaque tick.</summary>
    public void Apply(SkiaLayer.SettingsPanelSnapshot snapshot)
    {
        bool sameKeys = snapshot.Rows.Count == Rows.Count;
        for (int i = 0; i < snapshot.Rows.Count && sameKeys; i++)
            sameKeys = snapshot.Rows[i].Key == Rows[i].Key;

        if (sameKeys)
        {
            for (int i = 0; i < snapshot.Rows.Count; i++) Rows[i].Apply(snapshot.Rows[i]);
            return;
        }

        Rows.Clear();
        foreach (var row in snapshot.Rows) Rows.Add(new SettingRowViewModel(row));
    }

    public void Toggle(SettingRowViewModel row)
    {
        // Une ligne sans objet (sauvegarde cloud sans store) reste inerte, comme en Skia.
        if (!row.IsEnabled) return;
        _toggle(row.Key);
    }

    public void SelectChoice(SettingRowViewModel row, SettingChoiceViewModel choice) =>
        _setChoice(row.Key, choice.Key);

    public void SetSlider(SettingRowViewModel row, double value) => _setSlider(row.Key, value);

    public void SetText(SettingRowViewModel row, string value) => _setText(row.Key, value);
}

/// <summary>Popup de reglages en jeu : un chrome autour du panneau partage.</summary>
public sealed class SettingsPopupViewModel : ViewModelBase
{
    private readonly GameRuntimeHost _host;

    private bool _isOpen;
    private string _title = "";

    public SettingsPopupViewModel(GameRuntimeHost host)
    {
        _host = host;
        Panel = new SettingsPanelViewModel(
            host.ToggleSetting, host.SetSettingChoice, host.SetSettingSlider, host.SetSettingText);
    }

    public SettingsPanelViewModel Panel { get; }

    public bool IsOpen { get => _isOpen; private set => SetProperty(ref _isOpen, value); }
    public string Title { get => _title; private set => SetProperty(ref _title, value); }

    public void Refresh()
    {
        var snapshot = _host.GetSettingsPopupSnapshot();
        IsOpen = snapshot.IsOpen;
        Title = snapshot.Title;
        Panel.Apply(snapshot.Panel);
    }

    public void Close()
    {
        _host.CloseSettingsPopup();
        Refresh();
    }
}
