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

/// <summary>Une ligne de l'onglet Auto : le seuil de declenchement de la vente automatique du
/// surplus d'une ressource, en % du stock max.</summary>
public sealed class TradeAutoRowViewModel : ViewModelBase
{
    private SkiaLayer.TradeAutoResourceRowSnapshot _snapshot;

    public TradeAutoRowViewModel(SkiaLayer.TradeAutoResourceRowSnapshot snapshot) => _snapshot = snapshot;

    /// Nom d'enum de la ressource : identifiant stable, sert au routage.
    public string Key => _snapshot.Key;

    public string IconName => _snapshot.IconName;
    public string Name => _snapshot.Name;
    public int ThresholdPercent => _snapshot.ThresholdPercent;

    internal void Apply(SkiaLayer.TradeAutoResourceRowSnapshot snapshot)
    {
        if (_snapshot == snapshot) return;
        var previous = _snapshot;
        _snapshot = snapshot;

        if (previous.ThresholdPercent != snapshot.ThresholdPercent) RaisePropertyChanged(nameof(ThresholdPercent));
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
    private string _autoTabLabel = "";
    private bool _showingHistory;
    private bool _showingAuto;
    private bool _autoTabUnlocked;
    private string _sellHeader = "";
    private string _buyHeader = "";
    private string _autoSellHeader = "";
    private string _autoGoldHeader = "";
    private string _autoNote = "";
    private string _goldLabel = "";
    private int _autoGoldKeepPercent = -1;
    private bool _hasAutoSellRows;
    private string? _historyEmptyMessage;
    private IReadOnlyList<SkiaLayer.TradeHistoryEntrySnapshot> _lastHistory = [];

    public TradePopupViewModel(GameRuntimeHost host) => _host = host;

    public ObservableCollection<TradeRowViewModel> SellRows { get; } = [];
    public ObservableCollection<TradeRowViewModel> BuyRows { get; } = [];
    public ObservableCollection<TradeMultiplierViewModel> Multipliers { get; } = [];
    public ObservableCollection<TradeHistoryEntryViewModel> HistoryEntries { get; } = [];
    public ObservableCollection<TradeAutoRowViewModel> AutoSellRows { get; } = [];

    public bool IsOpen { get => _isOpen; private set => SetProperty(ref _isOpen, value); }
    public string Title { get => _title; private set => SetProperty(ref _title, value); }
    public string TradeTabLabel { get => _tradeTabLabel; private set => SetProperty(ref _tradeTabLabel, value); }
    public string HistoryTabLabel { get => _historyTabLabel; private set => SetProperty(ref _historyTabLabel, value); }
    public string AutoTabLabel { get => _autoTabLabel; private set => SetProperty(ref _autoTabLabel, value); }
    public bool AutoTabUnlocked { get => _autoTabUnlocked; private set => SetProperty(ref _autoTabUnlocked, value); }
    public string SellHeader { get => _sellHeader; private set => SetProperty(ref _sellHeader, value); }
    public string BuyHeader { get => _buyHeader; private set => SetProperty(ref _buyHeader, value); }
    public string AutoSellHeader { get => _autoSellHeader; private set => SetProperty(ref _autoSellHeader, value); }
    public string AutoGoldHeader { get => _autoGoldHeader; private set => SetProperty(ref _autoGoldHeader, value); }
    public string AutoNote { get => _autoNote; private set => SetProperty(ref _autoNote, value); }
    public string GoldLabel { get => _goldLabel; private set => SetProperty(ref _goldLabel, value); }

    /// -1 tant que l'achat automatique n'est pas debloque : le slider correspondant reste masque.
    public int AutoGoldKeepPercent
    {
        get => _autoGoldKeepPercent;
        private set
        {
            if (SetProperty(ref _autoGoldKeepPercent, value)) RaisePropertyChanged(nameof(AutoGoldKeepUnlocked));
        }
    }

    public bool AutoGoldKeepUnlocked => _autoGoldKeepPercent >= 0;

    public bool HasAutoSellRows { get => _hasAutoSellRows; private set => SetProperty(ref _hasAutoSellRows, value); }

    public bool ShowingHistory
    {
        get => _showingHistory;
        private set
        {
            if (SetProperty(ref _showingHistory, value)) RaisePropertyChanged(nameof(ShowingTrade));
        }
    }

    public bool ShowingAuto
    {
        get => _showingAuto;
        private set
        {
            if (SetProperty(ref _showingAuto, value)) RaisePropertyChanged(nameof(ShowingTrade));
        }
    }

    /// Les trois onglets s'excluent : un seul est affiche a la fois.
    public bool ShowingTrade => !_showingHistory && !_showingAuto;

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
        AutoTabLabel = snapshot.AutoTabLabel;
        AutoTabUnlocked = snapshot.AutoTabUnlocked;
        ShowingHistory = snapshot.ShowingHistory;
        ShowingAuto = snapshot.ShowingAuto;
        SellHeader = snapshot.SellHeader;
        BuyHeader = snapshot.BuyHeader;
        AutoSellHeader = snapshot.AutoSellHeader;
        AutoGoldHeader = snapshot.AutoGoldHeader;
        AutoNote = snapshot.AutoNote;
        GoldLabel = snapshot.GoldLabel;
        AutoGoldKeepPercent = snapshot.AutoGoldKeepPercent;
        HasAutoSellRows = snapshot.AutoSellRows.Count > 0;
        HistoryEmptyMessage = snapshot.HistoryEmptyMessage;

        SyncRows(SellRows, snapshot.SellRows);
        SyncRows(BuyRows, snapshot.BuyRows);
        SyncMultipliers(snapshot.Multipliers);
        SyncHistory(snapshot.HistoryEntries);
        SyncAutoRows(snapshot.AutoSellRows);
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

    /// Meme raisonnement que SyncRows : la composition ne change qu'au deblocage d'une ressource.
    private void SyncAutoRows(IReadOnlyList<SkiaLayer.TradeAutoResourceRowSnapshot> incoming)
    {
        bool sameKeys = incoming.Count == AutoSellRows.Count;
        for (int i = 0; i < incoming.Count && sameKeys; i++)
            sameKeys = incoming[i].Key == AutoSellRows[i].Key;

        if (sameKeys)
        {
            for (int i = 0; i < incoming.Count; i++) AutoSellRows[i].Apply(incoming[i]);
            return;
        }

        AutoSellRows.Clear();
        foreach (var row in incoming) AutoSellRows.Add(new TradeAutoRowViewModel(row));
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

    public void ShowAuto()
    {
        _host.TradeSetAutoTab();
        Refresh();
    }

    public void SetAutoSellThreshold(TradeAutoRowViewModel row, double percent)
    {
        _host.TradeSetAutoSellThreshold(row.Key, (int)Math.Round(percent));
        Refresh();
    }

    public void SetAutoGoldKeepPercent(double percent)
    {
        _host.TradeSetAutoGoldKeepPercent((int)Math.Round(percent));
        Refresh();
    }

    public void Close()
    {
        _host.CloseTradePopup();
        Refresh();
    }
}
