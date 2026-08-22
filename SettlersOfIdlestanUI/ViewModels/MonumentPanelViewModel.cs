using System.Collections.ObjectModel;
using SkiaLayer = SettlersOfIdlestanSkia.Services;

namespace SettlersOfIdlestanUI.ViewModels;

/// <summary>Une ligne d'investissement (ressource ou points de recherche).</summary>
public sealed class InvestmentRowViewModel : ViewModelBase
{
    private long _invested;
    private long _required;
    private bool _isEnabled;
    private bool _isDone;
    private string _label;
    private string _investedLabel;
    private string _requiredLabel;

    public InvestmentRowViewModel(SkiaLayer.InvestmentRowSnapshot snapshot)
    {
        Key = snapshot.Key;
        IconName = snapshot.IconName;
        _label = snapshot.Label;
        _invested = snapshot.Invested;
        _required = snapshot.Required;
        _isEnabled = snapshot.IsEnabled;
        _isDone = snapshot.IsDone;
        _investedLabel = snapshot.InvestedLabel;
        _requiredLabel = snapshot.RequiredLabel;
    }

    public string Key { get; }

    /// Null pour la ligne de recherche, qui n'a pas d'icone de ressource.
    public string? IconName { get; }

    public string Label { get => _label; private set => SetProperty(ref _label, value); }

    public long Invested
    {
        get => _invested;
        private set { if (SetProperty(ref _invested, value)) RaiseDerived(); }
    }

    public long Required
    {
        get => _required;
        private set { if (SetProperty(ref _required, value)) RaiseDerived(); }
    }

    public bool IsEnabled { get => _isEnabled; private set => SetProperty(ref _isEnabled, value); }

    /// Investissement complet : la case n'est plus decochable.
    public bool IsDone { get => _isDone; private set => SetProperty(ref _isDone, value); }

    /// Libelle deja formate cote jeu (k/M... ou notation scientifique/ingenieur selon le
    /// reglage joueur) : ne pas reformater <see cref="Invested"/>/<see cref="Required"/> ici.
    private string InvestedLabel { get => _investedLabel; set { if (SetProperty(ref _investedLabel, value)) RaiseDerived(); } }
    private string RequiredLabel { get => _requiredLabel; set { if (SetProperty(ref _requiredLabel, value)) RaiseDerived(); } }

    public string AmountLabel => $"{InvestedLabel}/{RequiredLabel}";

    /// Progression dans [0,1], saturee a 1 une fois la ligne complete.
    public double Progress => IsDone ? 1d : Required > 0 ? Math.Min(1d, (double)Invested / Required) : 0d;

    internal void Apply(SkiaLayer.InvestmentRowSnapshot snapshot)
    {
        Label = snapshot.Label;
        Invested = snapshot.Invested;
        Required = snapshot.Required;
        IsEnabled = snapshot.IsEnabled;
        IsDone = snapshot.IsDone;
        InvestedLabel = snapshot.InvestedLabel;
        RequiredLabel = snapshot.RequiredLabel;
    }

    private void RaiseDerived()
    {
        RaisePropertyChanged(nameof(AmountLabel));
        RaisePropertyChanged(nameof(Progress));
    }
}

/// <summary>
/// Panneau du monument selectionne. Les regles polymorphes restent cote Skia : ce ViewModel
/// ne fait que refleter l'instantane et relayer les commandes.
/// </summary>
public sealed class MonumentPanelViewModel : ViewModelBase
{
    private readonly GameRuntimeHost _host;

    private bool _isVisible;
    private string _title = "";
    private string? _wonderMaxedMessage;
    private string? _purifiedMessage;
    private bool _purifiedGrantedEssence;
    private string? _noCityWarning;
    private string? _corruptedPrestigeMessage;
    private string? _evolveButtonLabel;
    private string? _wonderSkipButtonLabel;
    private bool _canSkipWonder;
    private string? _destroyButtonLabel;

    public MonumentPanelViewModel(GameRuntimeHost host) => _host = host;

    public ObservableCollection<InvestmentRowViewModel> Rows { get; } = [];
    public ObservableCollection<BonusLineViewModel> BonusLines { get; } = [];

    public bool IsVisible { get => _isVisible; private set => SetProperty(ref _isVisible, value); }
    public string Title { get => _title; private set => SetProperty(ref _title, value); }

    public string? WonderMaxedMessage { get => _wonderMaxedMessage; private set => SetProperty(ref _wonderMaxedMessage, value); }
    public string? PurifiedMessage { get => _purifiedMessage; private set => SetProperty(ref _purifiedMessage, value); }
    public bool PurifiedGrantedEssence { get => _purifiedGrantedEssence; private set => SetProperty(ref _purifiedGrantedEssence, value); }
    public string? NoCityWarning { get => _noCityWarning; private set => SetProperty(ref _noCityWarning, value); }
    public string? CorruptedPrestigeMessage { get => _corruptedPrestigeMessage; private set => SetProperty(ref _corruptedPrestigeMessage, value); }
    public string? EvolveButtonLabel { get => _evolveButtonLabel; private set => SetProperty(ref _evolveButtonLabel, value); }
    public string? WonderSkipButtonLabel { get => _wonderSkipButtonLabel; private set => SetProperty(ref _wonderSkipButtonLabel, value); }
    public bool CanSkipWonder { get => _canSkipWonder; private set => SetProperty(ref _canSkipWonder, value); }

    /// Libelle du bouton de destruction de la Spire : il bascule sur un texte de confirmation une
    /// fois le bouton arme, la logique en deux temps restant cote Skia.
    public string? DestroyButtonLabel { get => _destroyButtonLabel; private set => SetProperty(ref _destroyButtonLabel, value); }

    public void Refresh()
    {
        var snapshot = _host.GetMonumentPanelSnapshot();

        IsVisible = snapshot.IsVisible;
        Title = snapshot.Title;
        WonderMaxedMessage = snapshot.WonderMaxedMessage;
        PurifiedMessage = snapshot.PurifiedMessage;
        PurifiedGrantedEssence = snapshot.PurifiedGrantedEssence;
        NoCityWarning = snapshot.NoCityWarning;
        CorruptedPrestigeMessage = snapshot.CorruptedPrestigeMessage;
        EvolveButtonLabel = snapshot.EvolveButtonLabel;
        WonderSkipButtonLabel = snapshot.WonderSkipButtonLabel;
        CanSkipWonder = snapshot.CanSkipWonder;
        DestroyButtonLabel = snapshot.DestroyButtonLabel;

        SyncRows(snapshot.Rows);
        SyncBonusLines(snapshot.BonusLines);
    }

    /// La composition des lignes ne change qu'au changement de monument ou de palier ; les
    /// montants changent en continu. On met a jour en place pour ne pas recreer les lignes a
    /// chaque tick de synchronisation.
    private void SyncRows(IReadOnlyList<SkiaLayer.InvestmentRowSnapshot> incoming)
    {
        bool sameKeys = incoming.Count == Rows.Count;
        if (sameKeys)
            for (int i = 0; i < incoming.Count && sameKeys; i++)
                sameKeys = incoming[i].Key == Rows[i].Key;

        if (sameKeys)
        {
            for (int i = 0; i < incoming.Count; i++) Rows[i].Apply(incoming[i]);
            return;
        }

        Rows.Clear();
        foreach (var row in incoming) Rows.Add(new InvestmentRowViewModel(row));
    }

    private void SyncBonusLines(IReadOnlyList<SkiaLayer.BonusLineSnapshot> incoming)
    {
        if (incoming.Count == BonusLines.Count)
        {
            for (int i = 0; i < incoming.Count; i++) BonusLines[i].Apply(incoming[i]);
            return;
        }

        BonusLines.Clear();
        foreach (var line in incoming) BonusLines.Add(new BonusLineViewModel(line));
    }

    public void Close()
    {
        _host.CloseMonumentPanel();
        Refresh();
    }

    public void ToggleInvestment(string rowKey)
    {
        _host.ToggleMonumentInvestment(rowKey);
        Refresh();
    }

    public void Evolve()
    {
        _host.EvolveMonument();
        Refresh();
    }

    public void SkipWonder()
    {
        _host.SkipWonder();
        Refresh();
    }

    /// Premier clic : arme la confirmation (le libelle change). Second clic : detruit la Spire.
    public void Destroy()
    {
        _host.DestroyMonument();
        Refresh();
    }
}

/// <summary>Une ligne de bonus du monument.</summary>
public sealed class BonusLineViewModel : ViewModelBase
{
    private string _text;
    private bool _isActive;

    public BonusLineViewModel(SkiaLayer.BonusLineSnapshot snapshot)
    {
        _text = snapshot.Text;
        _isActive = snapshot.IsActive;
    }

    public string Text { get => _text; private set => SetProperty(ref _text, value); }
    public bool IsActive { get => _isActive; private set => SetProperty(ref _isActive, value); }

    internal void Apply(SkiaLayer.BonusLineSnapshot snapshot)
    {
        Text = snapshot.Text;
        IsActive = snapshot.IsActive;
    }
}
