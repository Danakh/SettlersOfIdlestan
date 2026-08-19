using System.Collections.ObjectModel;
using SkiaLayer = SettlersOfIdlestanSkia.Services;

namespace SettlersOfIdlestanUI.ViewModels;

/// <summary>Une action de prestige : normale, ou corrompue.</summary>
public sealed class PrestigeActionViewModel : ViewModelBase
{
    private SkiaLayer.PrestigeActionSnapshot _snapshot;

    public PrestigeActionViewModel(SkiaLayer.PrestigeActionSnapshot snapshot)
    {
        _snapshot = snapshot;
        Tooltip = string.Join('\n', snapshot.Tooltip);
    }

    public string Key => _snapshot.Key;
    public string Label => _snapshot.Label;

    /// Seconde ligne du bouton (niveau de corruption vise) ; null pour l'action normale.
    public string? SubLabel => _snapshot.SubLabel;

    public bool HasSubLabel => _snapshot.SubLabel != null;
    public bool IsEnabled => _snapshot.IsEnabled;
    public bool IsCorrupted => _snapshot.IsCorrupted;
    public string Tooltip { get; }

    internal void Apply(SkiaLayer.PrestigeActionSnapshot snapshot)
    {
        if (_snapshot == snapshot) return;
        var previous = _snapshot;
        _snapshot = snapshot;

        if (previous.Label != snapshot.Label) RaisePropertyChanged(nameof(Label));
        if (previous.SubLabel != snapshot.SubLabel) RaisePropertyChanged(nameof(SubLabel));
        if (previous.IsEnabled != snapshot.IsEnabled) RaisePropertyChanged(nameof(IsEnabled));
    }
}

/// <summary>
/// Popup de prestige. Le calcul des points, les conditions de disponibilite et le declenchement
/// de la confirmation de perte d'essences restent dans PrestigeController et PrestigeRenderer.
/// </summary>
public sealed class PrestigePopupViewModel : ViewModelBase
{
    private readonly GameRuntimeHost _host;

    private bool _isOpen;
    private string _title = "";
    private string _totalLabel = "";
    private string _totalValue = "";
    private string? _tierPickerLabel;
    private string _tierPickerTooltip = "";
    private bool _canDecreaseTier;
    private bool _canIncreaseTier;
    private string? _warning;
    private bool _hasWonderRow;
    private string _wonderLabel = "";
    private string _wonderValue = "";
    private string _wonderTooltip = "";
    private string _wonderSkipTooltip = "";
    private bool _canSkipWonderTime;
    private IReadOnlyList<SkiaLayer.PrestigeRowSnapshot> _lastRows = [];

    public PrestigePopupViewModel(GameRuntimeHost host) => _host = host;

    /// Exposees telles quelles : ce sont des records immuables, sans comportement a envelopper.
    public ObservableCollection<SkiaLayer.PrestigeRowSnapshot> Rows { get; } = [];

    public ObservableCollection<PrestigeActionViewModel> Actions { get; } = [];

    public bool IsOpen { get => _isOpen; private set => SetProperty(ref _isOpen, value); }
    public string Title { get => _title; private set => SetProperty(ref _title, value); }
    public string TotalLabel { get => _totalLabel; private set => SetProperty(ref _totalLabel, value); }
    public string TotalValue { get => _totalValue; private set => SetProperty(ref _totalValue, value); }
    public bool CanDecreaseTier { get => _canDecreaseTier; private set => SetProperty(ref _canDecreaseTier, value); }
    public bool CanIncreaseTier { get => _canIncreaseTier; private set => SetProperty(ref _canIncreaseTier, value); }
    public string TierPickerTooltip { get => _tierPickerTooltip; private set => SetProperty(ref _tierPickerTooltip, value); }
    public bool HasWonderRow { get => _hasWonderRow; private set => SetProperty(ref _hasWonderRow, value); }
    public string WonderLabel { get => _wonderLabel; private set => SetProperty(ref _wonderLabel, value); }
    public string WonderValue { get => _wonderValue; private set => SetProperty(ref _wonderValue, value); }
    public string WonderTooltip { get => _wonderTooltip; private set => SetProperty(ref _wonderTooltip, value); }
    public string WonderSkipTooltip { get => _wonderSkipTooltip; private set => SetProperty(ref _wonderSkipTooltip, value); }
    public bool CanSkipWonderTime { get => _canSkipWonderTime; private set => SetProperty(ref _canSkipWonderTime, value); }

    /// Null tant que le choix de palier n'est pas debloque (Grand Phare niveau 3).
    public string? TierPickerLabel
    {
        get => _tierPickerLabel;
        private set
        {
            if (SetProperty(ref _tierPickerLabel, value)) RaisePropertyChanged(nameof(HasTierPicker));
        }
    }

    public bool HasTierPicker => _tierPickerLabel != null;

    /// Rappel affiche sous les actions : Port Imperial manquant, plafond de prestige de la demo atteint.
    public string? Warning
    {
        get => _warning;
        private set
        {
            if (SetProperty(ref _warning, value)) RaisePropertyChanged(nameof(HasWarning));
        }
    }

    public bool HasWarning => _warning != null;

    public void Refresh()
    {
        var snapshot = _host.GetPrestigePopupSnapshot();

        IsOpen = snapshot.IsOpen;
        Title = snapshot.Title;
        TotalLabel = snapshot.TotalLabel;
        TotalValue = snapshot.TotalValue;
        TierPickerLabel = snapshot.TierPickerLabel;
        CanDecreaseTier = snapshot.CanDecreaseTier;
        CanIncreaseTier = snapshot.CanIncreaseTier;
        TierPickerTooltip = string.Join('\n', snapshot.TierPickerTooltip);
        Warning = snapshot.Warning;

        HasWonderRow = snapshot.WonderRow != null;
        WonderLabel = snapshot.WonderRow?.Label ?? "";
        WonderValue = snapshot.WonderRow?.Value ?? "";
        WonderTooltip = snapshot.WonderRow != null ? string.Join('\n', snapshot.WonderRow.Tooltip) : "";
        CanSkipWonderTime = snapshot.CanSkipWonderTime;
        WonderSkipTooltip = string.Join('\n', snapshot.WonderSkipTooltip);

        // Les valeurs bougent en continu mais la composition rarement : l'egalite structurelle
        // des records evite de reconstruire la liste a chaque tick.
        if (!_lastRows.SequenceEqual(snapshot.Rows))
        {
            _lastRows = snapshot.Rows;
            Rows.Clear();
            foreach (var row in snapshot.Rows) Rows.Add(row);
        }

        SyncActions(snapshot.Actions);
    }

    private void SyncActions(IReadOnlyList<SkiaLayer.PrestigeActionSnapshot> incoming)
    {
        bool sameKeys = incoming.Count == Actions.Count;
        for (int i = 0; i < incoming.Count && sameKeys; i++)
            sameKeys = incoming[i].Key == Actions[i].Key;

        if (sameKeys)
        {
            for (int i = 0; i < incoming.Count; i++) Actions[i].Apply(incoming[i]);
            return;
        }

        Actions.Clear();
        foreach (var action in incoming) Actions.Add(new PrestigeActionViewModel(action));
    }

    public void Invoke(PrestigeActionViewModel action)
    {
        _host.InvokePrestigeAction(action.Key);
        Refresh();
    }

    public void SkipWonderTime()
    {
        _host.PrestigeSkipWonderTime();
        Refresh();
    }

    public void ChangeTier(bool increase)
    {
        _host.PrestigeChangeTier(increase);
        Refresh();
    }

    public void Close()
    {
        _host.ClosePrestigePopup();
        Refresh();
    }
}
