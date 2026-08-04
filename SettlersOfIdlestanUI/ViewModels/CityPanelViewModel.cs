using System.Collections.ObjectModel;
using SkiaLayer = SettlersOfIdlestanSkia.Services;

namespace SettlersOfIdlestanUI.ViewModels;

/// <summary>Un element de cout affiche sous le nom du batiment.</summary>
public sealed class CostItemViewModel : ViewModelBase
{
    private string _amount;
    private bool _canAfford;

    public CostItemViewModel(SkiaLayer.CostItemSnapshot snapshot)
    {
        IconName = snapshot.IconName;
        _amount = snapshot.Amount;
        _canAfford = snapshot.CanAfford;
    }

    public string IconName { get; }
    public string Amount { get => _amount; private set => SetProperty(ref _amount, value); }

    /// Le joueur possede la quantite requise : le montant s'affiche en vert.
    public bool CanAfford { get => _canAfford; private set => SetProperty(ref _canAfford, value); }

    internal void Apply(SkiaLayer.CostItemSnapshot snapshot)
    {
        Amount = snapshot.Amount;
        CanAfford = snapshot.CanAfford;
    }
}

/// <summary>Une ligne de batiment du panneau ville.</summary>
public sealed class CityBuildingRowViewModel : ViewModelBase
{
    private string _label;
    private SkiaLayer.BuildingActivationState _activation;
    private SkiaLayer.CityBuildingAction _action;
    private string _actionLabel;
    private bool _isActionEnabled;

    public CityBuildingRowViewModel(SkiaLayer.CityBuildingRowSnapshot snapshot)
    {
        Key = snapshot.Key;
        _label = snapshot.Label;
        _activation = snapshot.Activation;
        _action = snapshot.Action;
        _actionLabel = snapshot.ActionLabel;
        _isActionEnabled = snapshot.IsActionEnabled;
        foreach (var item in snapshot.Cost) Cost.Add(new CostItemViewModel(item));
    }

    /// Nom d'enum du BuildingType : identifiant stable, sert au routage des commandes.
    public string Key { get; }

    public ObservableCollection<CostItemViewModel> Cost { get; } = [];

    public string Label { get => _label; private set => SetProperty(ref _label, value); }

    public SkiaLayer.BuildingActivationState Activation
    {
        get => _activation;
        private set
        {
            if (!SetProperty(ref _activation, value)) return;
            RaisePropertyChanged(nameof(HasActivation));
            RaisePropertyChanged(nameof(IsActivated));
        }
    }

    /// Le batiment est activable : la case a cocher est affichee.
    public bool HasActivation => Activation != SkiaLayer.BuildingActivationState.None;

    public bool IsActivated => Activation == SkiaLayer.BuildingActivationState.Active;

    public SkiaLayer.CityBuildingAction Action
    {
        get => _action;
        private set
        {
            if (!SetProperty(ref _action, value)) return;
            RaisePropertyChanged(nameof(HasAction));
            RaisePropertyChanged(nameof(IsGoToOtherCity));
        }
    }

    public bool HasAction => Action != SkiaLayer.CityBuildingAction.None;

    /// Le bouton renvoie vers la ville qui possede deja ce batiment unique.
    public bool IsGoToOtherCity => Action == SkiaLayer.CityBuildingAction.GoToOtherCity;

    public string ActionLabel { get => _actionLabel; private set => SetProperty(ref _actionLabel, value); }

    public bool IsActionEnabled { get => _isActionEnabled; private set => SetProperty(ref _isActionEnabled, value); }

    internal void Apply(SkiaLayer.CityBuildingRowSnapshot snapshot)
    {
        Label = snapshot.Label;
        Activation = snapshot.Activation;
        Action = snapshot.Action;
        ActionLabel = snapshot.ActionLabel;
        IsActionEnabled = snapshot.IsActionEnabled;

        // Le nombre de ressources d'un cout change au passage de palier : on ne met a jour en
        // place que si la composition est identique.
        if (snapshot.Cost.Count == Cost.Count)
        {
            for (int i = 0; i < Cost.Count; i++) Cost[i].Apply(snapshot.Cost[i]);
            return;
        }

        Cost.Clear();
        foreach (var item in snapshot.Cost) Cost.Add(new CostItemViewModel(item));
    }
}

/// <summary>
/// Panneau de la ville selectionnee. Les regles de construction restent cote Skia : ce
/// ViewModel reflete l'instantane et relaie les commandes.
/// </summary>
public sealed class CityPanelViewModel : ViewModelBase
{
    private readonly GameRuntimeHost _host;

    private bool _isVisible;
    private bool _hasUniqueTab;
    private bool _showingUnique;
    private string _regularTabLabel = "";
    private string _uniqueTabLabel = "";
    private string _militaryFooter = "";

    public CityPanelViewModel(GameRuntimeHost host) => _host = host;

    public ObservableCollection<CityBuildingRowViewModel> Rows { get; } = [];

    public bool IsVisible { get => _isVisible; private set => SetProperty(ref _isVisible, value); }
    public bool HasUniqueTab { get => _hasUniqueTab; private set => SetProperty(ref _hasUniqueTab, value); }
    public bool ShowingUnique { get => _showingUnique; private set => SetProperty(ref _showingUnique, value); }
    public string RegularTabLabel { get => _regularTabLabel; private set => SetProperty(ref _regularTabLabel, value); }
    public string UniqueTabLabel { get => _uniqueTabLabel; private set => SetProperty(ref _uniqueTabLabel, value); }
    public string MilitaryFooter { get => _militaryFooter; private set => SetProperty(ref _militaryFooter, value); }

    public void Refresh()
    {
        var snapshot = _host.GetCityPanelSnapshot();

        IsVisible = snapshot.IsVisible;
        HasUniqueTab = snapshot.HasUniqueTab;
        ShowingUnique = snapshot.ShowingUnique;
        RegularTabLabel = snapshot.RegularTabLabel;
        UniqueTabLabel = snapshot.UniqueTabLabel;
        MilitaryFooter = snapshot.MilitaryFooter;

        SyncRows(snapshot.Rows);
    }

    /// La liste ne change qu'au changement de ville, d'onglet ou de deblocage ; les couts et
    /// niveaux changent en continu. Mise a jour en place pour ne pas recreer les lignes a
    /// chaque tick de synchronisation.
    private void SyncRows(IReadOnlyList<SkiaLayer.CityBuildingRowSnapshot> incoming)
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
        foreach (var row in incoming) Rows.Add(new CityBuildingRowViewModel(row));
    }

    public void Close()
    {
        _host.CloseCityPanel();
        Refresh();
    }

    public void ShowRegular()
    {
        _host.SetCityShowUnique(false);
        Refresh();
    }

    public void ShowUnique()
    {
        _host.SetCityShowUnique(true);
        Refresh();
    }

    public void ToggleActivation(string key)
    {
        _host.ToggleCityBuildingActivation(key);
        Refresh();
    }

    /// <summary>Construit, ameliore, ou recentre sur l'autre ville selon l'action de la ligne.</summary>
    public void ExecuteAction(CityBuildingRowViewModel row)
    {
        if (row.IsGoToOtherCity) _host.GoToOtherCity(row.Key);
        else _host.ExecuteCityBuildingAction(row.Key);
        Refresh();
    }

    /// <summary>
    /// Signale le batiment survole et la position du pointeur : le tooltip reste dessine en
    /// Skia par-dessus la carte, et a besoin de la position car Avalonia consomme desormais
    /// le survol au-dessus du panneau.
    /// </summary>
    public void SetHovered(string? key, float pointerX, float pointerY) =>
        _host.SetHoveredCityBuilding(key, pointerX, pointerY);
}
