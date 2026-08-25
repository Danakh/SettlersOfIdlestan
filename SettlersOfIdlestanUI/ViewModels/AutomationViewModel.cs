using System.Collections.ObjectModel;
using SkiaLayer = SettlersOfIdlestanSkia.Services;

namespace SettlersOfIdlestanUI.ViewModels;

/// <summary>Une ligne de la page Automatisation.</summary>
public sealed class AutomationRowViewModel : ViewModelBase
{
    private SkiaLayer.AutomationRowSnapshot _snapshot;

    public AutomationRowViewModel(SkiaLayer.AutomationRowSnapshot snapshot)
    {
        _snapshot = snapshot;
        foreach (var line in snapshot.SummaryLines) SummaryLines.Add(line);
    }

    /// Cle d'epinglage, qui sert aussi d'identifiant et de routage.
    public string Key => _snapshot.Key;

    /// Famille de l'automatisme (construction, comportement, activation) : fixe pour la duree de
    /// vie de la ligne, comme la section a laquelle elle appartient — pas repercutee dans Apply.
    public SkiaLayer.AutomationCategory Category => _snapshot.Category;

    public string Name => _snapshot.Name;
    public string Description => _snapshot.Description;
    public string? Note => _snapshot.Note;
    public bool? IsOn => _snapshot.IsOn;
    public bool IsLocked => _snapshot.IsLocked;
    public bool CanPin => _snapshot.CanPin;
    public bool IsPinned => _snapshot.IsPinned;

    /// Une ligne verrouillee n'a pas de bascule : sa description porte la condition de deblocage.
    public bool HasToggle => !_snapshot.IsLocked;

    public ObservableCollection<string> SummaryLines { get; } = [];

    internal void Apply(SkiaLayer.AutomationRowSnapshot snapshot)
    {
        if (_snapshot == snapshot) return;
        var previous = _snapshot;
        _snapshot = snapshot;

        if (previous.Name != snapshot.Name) RaisePropertyChanged(nameof(Name));
        if (previous.Description != snapshot.Description) RaisePropertyChanged(nameof(Description));
        if (previous.Note != snapshot.Note) RaisePropertyChanged(nameof(Note));
        if (previous.IsOn != snapshot.IsOn) RaisePropertyChanged(nameof(IsOn));
        if (previous.IsPinned != snapshot.IsPinned) RaisePropertyChanged(nameof(IsPinned));
        if (previous.CanPin != snapshot.CanPin) RaisePropertyChanged(nameof(CanPin));
        if (previous.IsLocked != snapshot.IsLocked)
        {
            RaisePropertyChanged(nameof(IsLocked));
            RaisePropertyChanged(nameof(HasToggle));
        }

        if (!previous.SummaryLines.SequenceEqual(snapshot.SummaryLines))
        {
            SummaryLines.Clear();
            foreach (var line in snapshot.SummaryLines) SummaryLines.Add(line);
        }
    }
}

/// <summary>Une section de la page : un en-tete et ses lignes.</summary>
public sealed class AutomationSectionViewModel
{
    public AutomationSectionViewModel(SkiaLayer.AutomationSectionSnapshot snapshot)
    {
        Header = snapshot.Header;
        foreach (var row in snapshot.Rows) Rows.Add(new AutomationRowViewModel(row));
    }

    public string Header { get; }
    public ObservableCollection<AutomationRowViewModel> Rows { get; } = [];

    internal bool HasSameShape(SkiaLayer.AutomationSectionSnapshot snapshot)
    {
        if (Header != snapshot.Header || Rows.Count != snapshot.Rows.Count) return false;
        for (int i = 0; i < Rows.Count; i++)
            if (Rows[i].Key != snapshot.Rows[i].Key) return false;
        return true;
    }

    internal void Apply(SkiaLayer.AutomationSectionSnapshot snapshot)
    {
        for (int i = 0; i < Rows.Count; i++) Rows[i].Apply(snapshot.Rows[i]);
    }
}

/// <summary>
/// Onglet plein ecran de l'automatisation. Les conditions de deblocage, l'ordre des lignes et
/// l'effet de chaque bascule restent dans AutomationRenderer : ce ViewModel reflete l'instantane
/// et relaie les commandes.
/// </summary>
public sealed class AutomationViewModel : ViewModelBase
{
    private readonly GameRuntimeHost _host;

    private bool _isVisible;
    private string _title = "";
    private string _globalToggleLabel = "";
    private bool _globalToggleOn;
    private string _pinTooltip = "";
    private bool _showPresetBar;
    private int _activePreset = 1;
    private string _presetChangeButtonLabel = "";

    public AutomationViewModel(GameRuntimeHost host) => _host = host;

    public ObservableCollection<AutomationSectionViewModel> LeftColumn { get; } = [];
    public ObservableCollection<AutomationSectionViewModel> RightColumn { get; } = [];

    public bool IsVisible { get => _isVisible; private set => SetProperty(ref _isVisible, value); }
    public string Title { get => _title; private set => SetProperty(ref _title, value); }
    public string GlobalToggleLabel { get => _globalToggleLabel; private set => SetProperty(ref _globalToggleLabel, value); }
    public bool GlobalToggleOn { get => _globalToggleOn; private set => SetProperty(ref _globalToggleOn, value); }
    public string PinTooltip { get => _pinTooltip; private set => SetProperty(ref _pinTooltip, value); }

    /// Visible une fois TechnologyId.AutomationPreset debloquee.
    public bool ShowPresetBar { get => _showPresetBar; private set => SetProperty(ref _showPresetBar, value); }
    public int ActivePreset { get => _activePreset; private set => SetProperty(ref _activePreset, value); }
    public string PresetChangeButtonLabel { get => _presetChangeButtonLabel; private set => SetProperty(ref _presetChangeButtonLabel, value); }

    public void Refresh()
    {
        var snapshot = _host.GetAutomationSnapshot();

        // Onglet inactif : on masque la vue sans toucher aux collections. L'instantane masque
        // est vide ; le repercuter les vidait, donc detruisait l'arbre de controles de la page
        // — qu'il fallait rebatir integralement au retour sur l'onglet. Mesure sur une trentaine
        // de cartes : ~160 ms de reconstruction contre ~1 ms pour la seule rebascule de
        // visibilite. C'est ce qui rendait le changement d'onglet lent, et davantage encore sous
        // WebAssembly. Les valeurs affichees sont resynchronisees avant le retour a l'ecran,
        // GameView appelant SyncFromGameState des le clic sur l'onglet.
        if (!snapshot.IsVisible)
        {
            IsVisible = false;
            return;
        }

        IsVisible = snapshot.IsVisible;
        Title = snapshot.Title;
        GlobalToggleLabel = snapshot.GlobalToggleLabel;
        GlobalToggleOn = snapshot.GlobalToggleOn;
        PinTooltip = snapshot.PinTooltip;
        ShowPresetBar = snapshot.PresetBarVisible;
        ActivePreset = snapshot.ActivePreset;
        PresetChangeButtonLabel = snapshot.PresetChangeButtonLabel;

        Sync(LeftColumn, snapshot.LeftColumn);
        Sync(RightColumn, snapshot.RightColumn);
    }

    /// La composition ne change qu'au deblocage d'un automatisme ou a la construction d'un
    /// premier batiment d'un type ; les etats changent en continu. Mise a jour en place pour ne
    /// pas recreer une trentaine de cartes a chaque tick de synchronisation.
    private static void Sync(
        ObservableCollection<AutomationSectionViewModel> target,
        IReadOnlyList<SkiaLayer.AutomationSectionSnapshot> incoming)
    {
        bool sameShape = incoming.Count == target.Count;
        for (int i = 0; i < incoming.Count && sameShape; i++)
            sameShape = target[i].HasSameShape(incoming[i]);

        if (sameShape)
        {
            for (int i = 0; i < incoming.Count; i++) target[i].Apply(incoming[i]);
            return;
        }

        target.Clear();
        foreach (var section in incoming) target.Add(new AutomationSectionViewModel(section));
    }

    public void Toggle(AutomationRowViewModel row)
    {
        _host.ToggleAutomation(row.Key);
        Refresh();
    }

    public void TogglePin(AutomationRowViewModel row)
    {
        _host.ToggleAutomationPin(row.Key);
        Refresh();
    }

    public void ToggleGlobal()
    {
        _host.ToggleAutomationsGlobally();
        Refresh();
    }

    public void SelectPreset(int preset)
    {
        _host.SelectAutomationPreset(preset);
        Refresh();
    }

    public void OpenPresetPopup() => _host.OpenAutomationPresetPopup();
}
