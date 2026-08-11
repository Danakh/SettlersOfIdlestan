using System.Collections.ObjectModel;
using SkiaLayer = SettlersOfIdlestanSkia.Services;

namespace SettlersOfIdlestanUI.ViewModels;

/// <summary>Une ligne d'echange : une ressource a vendre ou a acheter.</summary>
public sealed class TradeRowViewModel : ViewModelBase
{
    private SkiaLayer.TradeRowSnapshot _snapshot;

    public TradeRowViewModel(SkiaLayer.TradeRowSnapshot snapshot) => _snapshot = snapshot;

    /// Nom d'enum de la ressource : identifiant stable, sert au routage.
    public string Key => _snapshot.Key;

    public string IconName => _snapshot.IconName;
    public string Name => _snapshot.Name;
    public string StockLabel => _snapshot.StockLabel;
    public bool IsAtMax => _snapshot.IsAtMax;
    public string ButtonLabel => _snapshot.ButtonLabel;
    public bool IsEnabled => _snapshot.IsEnabled;

    /// Raison du blocage, ou null si l'echange est possible.
    public string? DisabledTooltip => _snapshot.DisabledTooltip;

    internal void Apply(SkiaLayer.TradeRowSnapshot snapshot)
    {
        if (_snapshot == snapshot) return;
        var previous = _snapshot;
        _snapshot = snapshot;

        if (previous.StockLabel != snapshot.StockLabel) RaisePropertyChanged(nameof(StockLabel));
        if (previous.IsAtMax != snapshot.IsAtMax) RaisePropertyChanged(nameof(IsAtMax));
        if (previous.ButtonLabel != snapshot.ButtonLabel) RaisePropertyChanged(nameof(ButtonLabel));
        if (previous.IsEnabled != snapshot.IsEnabled) RaisePropertyChanged(nameof(IsEnabled));
        if (previous.DisabledTooltip != snapshot.DisabledTooltip) RaisePropertyChanged(nameof(DisabledTooltip));
    }
}

/// <summary>Un multiplicateur de paquet.</summary>
public sealed class TradeMultiplierViewModel : ViewModelBase
{
    private bool _isActive;
    private bool _isTemporary;

    public TradeMultiplierViewModel(SkiaLayer.TradeMultiplierSnapshot snapshot)
    {
        Value = snapshot.Value;
        Label = snapshot.Label;
        _isActive = snapshot.IsActive;
        _isTemporary = snapshot.IsTemporary;
    }

    public int Value { get; }
    public string Label { get; }

    public bool IsActive { get => _isActive; private set => SetProperty(ref _isActive, value); }

    /// Impose par Ctrl/Maj : il retombe des que la touche est relachee.
    public bool IsTemporary { get => _isTemporary; private set => SetProperty(ref _isTemporary, value); }

    internal void Apply(SkiaLayer.TradeMultiplierSnapshot snapshot)
    {
        IsActive = snapshot.IsActive;
        IsTemporary = snapshot.IsTemporary;
    }
}

/// <summary>Une entree de l'historique des echanges. Son contenu est fige.</summary>
public sealed class TradeHistoryEntryViewModel
{
    public TradeHistoryEntryViewModel(SkiaLayer.TradeHistoryEntrySnapshot snapshot)
    {
        IconName = snapshot.IconName;
        Label = snapshot.Label;
        GoldText = snapshot.GoldText;
        IsGain = snapshot.IsGain;
        TimeText = snapshot.TimeText;
    }

    public string IconName { get; }
    public string Label { get; }
    public string GoldText { get; }
    public bool IsGain { get; }
    public string TimeText { get; }
}

/// <summary>
/// Popup de commerce. Les taux, le deblocage des ressources et la solvabilite restent dans
/// TradeController : ce ViewModel reflete l'instantane et relaie les commandes.
/// </summary>
public sealed class TradePopupViewModel : ViewModelBase
{
    private readonly GameRuntimeHost _host;

    private bool _isOpen;
    private string _title = "";
    private string _tradeTabLabel = "";
    private string _historyTabLabel = "";
    private bool _showingHistory;
    private string _sellHeader = "";
    private string _buyHeader = "";
    private string _goldLabel = "";
    private string? _historyEmptyMessage;
    private IReadOnlyList<SkiaLayer.TradeHistoryEntrySnapshot> _lastHistory = [];

    public TradePopupViewModel(GameRuntimeHost host) => _host = host;

    public ObservableCollection<TradeRowViewModel> SellRows { get; } = [];
    public ObservableCollection<TradeRowViewModel> BuyRows { get; } = [];
    public ObservableCollection<TradeMultiplierViewModel> Multipliers { get; } = [];
    public ObservableCollection<TradeHistoryEntryViewModel> HistoryEntries { get; } = [];

    public bool IsOpen { get => _isOpen; private set => SetProperty(ref _isOpen, value); }
    public string Title { get => _title; private set => SetProperty(ref _title, value); }
    public string TradeTabLabel { get => _tradeTabLabel; private set => SetProperty(ref _tradeTabLabel, value); }
    public string HistoryTabLabel { get => _historyTabLabel; private set => SetProperty(ref _historyTabLabel, value); }
    public string SellHeader { get => _sellHeader; private set => SetProperty(ref _sellHeader, value); }
    public string BuyHeader { get => _buyHeader; private set => SetProperty(ref _buyHeader, value); }
    public string GoldLabel { get => _goldLabel; private set => SetProperty(ref _goldLabel, value); }

    public bool ShowingHistory
    {
        get => _showingHistory;
        private set
        {
            if (SetProperty(ref _showingHistory, value)) RaisePropertyChanged(nameof(ShowingTrade));
        }
    }

    /// Les deux onglets s'excluent : l'un des deux est toujours affiche.
    public bool ShowingTrade => !_showingHistory;

    public string? HistoryEmptyMessage
    {
        get => _historyEmptyMessage;
        private set
        {
            if (SetProperty(ref _historyEmptyMessage, value)) RaisePropertyChanged(nameof(IsHistoryEmpty));
        }
    }

    public bool IsHistoryEmpty => _historyEmptyMessage != null;

    public void Refresh()
    {
        var snapshot = _host.GetTradePopupSnapshot();

        IsOpen = snapshot.IsOpen;
        Title = snapshot.Title;
        TradeTabLabel = snapshot.TradeTabLabel;
        HistoryTabLabel = snapshot.HistoryTabLabel;
        ShowingHistory = snapshot.ShowingHistory;
        SellHeader = snapshot.SellHeader;
        BuyHeader = snapshot.BuyHeader;
        GoldLabel = snapshot.GoldLabel;
        HistoryEmptyMessage = snapshot.HistoryEmptyMessage;

        SyncRows(SellRows, snapshot.SellRows);
        SyncRows(BuyRows, snapshot.BuyRows);
        SyncMultipliers(snapshot.Multipliers);
        SyncHistory(snapshot.HistoryEntries);
    }

    /// La composition ne change qu'au deblocage d'une ressource ; les prix et la solvabilite
    /// changent en continu, et le multiplicateur les fait tous bouger d'un coup. Mise a jour en
    /// place pour ne pas recreer les lignes a chaque tick.
    private static void SyncRows(
        ObservableCollection<TradeRowViewModel> target,
        IReadOnlyList<SkiaLayer.TradeRowSnapshot> incoming)
    {
        bool sameKeys = incoming.Count == target.Count;
        for (int i = 0; i < incoming.Count && sameKeys; i++)
            sameKeys = incoming[i].Key == target[i].Key;

        if (sameKeys)
        {
            for (int i = 0; i < incoming.Count; i++) target[i].Apply(incoming[i]);
            return;
        }

        target.Clear();
        foreach (var row in incoming) target.Add(new TradeRowViewModel(row));
    }

    private void SyncMultipliers(IReadOnlyList<SkiaLayer.TradeMultiplierSnapshot> incoming)
    {
        if (Multipliers.Count == incoming.Count)
        {
            for (int i = 0; i < incoming.Count; i++) Multipliers[i].Apply(incoming[i]);
            return;
        }

        Multipliers.Clear();
        foreach (var m in incoming) Multipliers.Add(new TradeMultiplierViewModel(m));
    }

    /// L'historique ne change que par ajout, et une entree passee est immuable.
    private void SyncHistory(IReadOnlyList<SkiaLayer.TradeHistoryEntrySnapshot> incoming)
    {
        if (_lastHistory.SequenceEqual(incoming)) return;
        _lastHistory = incoming;

        HistoryEntries.Clear();
        foreach (var entry in incoming) HistoryEntries.Add(new TradeHistoryEntryViewModel(entry));
    }

    public void Sell(TradeRowViewModel row)
    {
        _host.TradeSell(row.Key);
        Refresh();
    }

    public void Buy(TradeRowViewModel row)
    {
        _host.TradeBuy(row.Key);
        Refresh();
    }

    public void SetMultiplier(TradeMultiplierViewModel multiplier)
    {
        _host.TradeSetMultiplier(multiplier.Value);
        Refresh();
    }

    public void ShowTrade()
    {
        _host.TradeSetHistoryTab(false);
        Refresh();
    }

    public void ShowHistory()
    {
        _host.TradeSetHistoryTab(true);
        Refresh();
    }

    public void Close()
    {
        _host.CloseTradePopup();
        Refresh();
    }
}
