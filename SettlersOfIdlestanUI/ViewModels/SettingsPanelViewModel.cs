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

/// <summary>Un onglet du panneau de reglages.</summary>
public sealed class SettingsTabViewModel : ViewModelBase
{
    private bool _isActive;
    private string _label;

    public SettingsTabViewModel(SkiaLayer.SettingsTabSnapshot snapshot, bool isActive)
    {
        Tab = snapshot.Tab;
        _label = snapshot.Label;
        _isActive = isActive;
    }

    public SkiaLayer.SettingsTab Tab { get; }

    /// Comme les libelles des options : changer la langue depuis ce panneau relocalise sa
    /// propre barre d'onglets.
    public string Label { get => _label; internal set => SetProperty(ref _label, value); }

    public bool IsActive { get => _isActive; internal set => SetProperty(ref _isActive, value); }
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
    public SkiaLayer.SettingsTab Tab => _snapshot.Tab;
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

    /// <summary>
    /// Onglet affiche. C'est un etat de vue, pas un reglage : il ne remonte pas au runtime et ne
    /// se sauvegarde pas — rouvrir les reglages repart de l'onglet general.
    /// </summary>
    private SkiaLayer.SettingsTab _activeTab = SkiaLayer.SettingsTab.General;

    /// Dernier instantane recu : relu au changement d'onglet, qui ne passe pas par Apply.
    private SkiaLayer.SettingsPanelSnapshot _snapshot = SkiaLayer.SettingsPanelSnapshot.Empty;

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

    /// <summary>
    /// Les lignes de l'onglet affiche — pas toutes celles du panneau. Un instantane sans onglets
    /// les porte donc toutes.
    /// </summary>
    public ObservableCollection<SettingRowViewModel> Rows { get; } = [];

    public ObservableCollection<SettingsTabViewModel> Tabs { get; } = [];

    /// <summary>
    /// Faux pour un panneau d'un seul tenant (instantane sans onglets) : la barre d'onglets
    /// disparait et toutes les lignes s'affichent.
    /// </summary>
    public bool HasTabs => Tabs.Count > 0;

    /// <summary>Reflete un instantane. Appelee par le proprietaire du panneau a chaque tick.</summary>
    public void Apply(SkiaLayer.SettingsPanelSnapshot snapshot)
    {
        _snapshot = snapshot;
        ApplyTabs(snapshot.Tabs);
        ApplyRows();
    }

    private void ApplyTabs(IReadOnlyList<SkiaLayer.SettingsTabSnapshot> tabs)
    {
        bool sameTabs = tabs.Count == Tabs.Count;
        for (int i = 0; i < tabs.Count && sameTabs; i++) sameTabs = tabs[i].Tab == Tabs[i].Tab;

        if (!sameTabs)
        {
            Tabs.Clear();
            foreach (var tab in tabs) Tabs.Add(new SettingsTabViewModel(tab, tab.Tab == _activeTab));
            RaisePropertyChanged(nameof(HasTabs));
        }

        // Un onglet disparu (composition changee) laisserait le panneau vide : on retombe sur
        // le premier propose.
        if (Tabs.Count > 0 && Tabs.All(t => t.Tab != _activeTab)) _activeTab = Tabs[0].Tab;

        for (int i = 0; i < Tabs.Count; i++)
        {
            Tabs[i].Label = tabs[i].Label;
            Tabs[i].IsActive = Tabs[i].Tab == _activeTab;
        }
    }

    /// <summary>
    /// Rapproche <see cref="Rows"/> des lignes de l'onglet affiche. Le parcours saute les lignes
    /// des autres onglets sur place plutot que de construire une liste filtree : cette methode
    /// tourne a chaque tick tant que le panneau est ouvert, et n'alloue rien quand rien ne change.
    /// </summary>
    private void ApplyRows()
    {
        int index = 0;
        bool same = true;

        for (int i = 0; i < _snapshot.Rows.Count && same; i++)
        {
            var row = _snapshot.Rows[i];
            if (!IsInActiveTab(row)) continue;

            same = index < Rows.Count && Rows[index].Key == row.Key;
            // Sur un rapprochement qui echoue plus loin, ces lignes-la auront ete mises a jour
            // pour rien : la liste est reconstruite juste apres.
            if (same) Rows[index].Apply(row);
            index++;
        }

        if (same && index == Rows.Count) return;

        Rows.Clear();
        for (int i = 0; i < _snapshot.Rows.Count; i++)
            if (IsInActiveTab(_snapshot.Rows[i])) Rows.Add(new SettingRowViewModel(_snapshot.Rows[i]));
    }

    private bool IsInActiveTab(SkiaLayer.SettingRowSnapshot row) => !HasTabs || row.Tab == _activeTab;

    public void SelectTab(SettingsTabViewModel tab)
    {
        if (_activeTab == tab.Tab) return;
        _activeTab = tab.Tab;

        foreach (var candidate in Tabs) candidate.IsActive = candidate.Tab == _activeTab;
        ApplyRows();
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
