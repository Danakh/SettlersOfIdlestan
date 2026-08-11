using System.Collections.ObjectModel;
using SkiaLayer = SettlersOfIdlestanSkia.Services;

namespace SettlersOfIdlestanUI.ViewModels;

/// <summary>Un onglet affichable.</summary>
public sealed class TabItemViewModel : ViewModelBase
{
    private bool _isActive;
    private bool _isGlowing;
    private string _label;

    public TabItemViewModel(SkiaLayer.TabSnapshot snapshot)
    {
        TabId = snapshot.TabId;
        _label = snapshot.Label;
        _isActive = snapshot.IsActive;
        _isGlowing = snapshot.IsGlowing;
    }

    public int TabId { get; }

    /// Peut contenir un saut de ligne (ex. "Infra\nmonde").
    public string Label { get => _label; private set => SetProperty(ref _label, value); }

    public bool IsActive { get => _isActive; private set => SetProperty(ref _isActive, value); }

    /// Pulsation d'attention : recherche disponible, nouvel evenement, inframonde jamais visite.
    public bool IsGlowing { get => _isGlowing; private set => SetProperty(ref _isGlowing, value); }

    internal void Apply(SkiaLayer.TabSnapshot snapshot)
    {
        Label = snapshot.Label;
        IsActive = snapshot.IsActive;
        IsGlowing = snapshot.IsGlowing;
    }
}

/// <summary>
/// Barre d'onglets. La logique de deblocage et de notification reste dans TabBarRenderer :
/// ce ViewModel ne fait que refleter l'instantane qu'il en recoit.
/// </summary>
public sealed class TabBarViewModel : ViewModelBase
{
    private readonly GameRuntimeHost _host;
    private bool _isVisible;
    private bool _tabsAtBottom;

    public TabBarViewModel(GameRuntimeHost host) => _host = host;

    public ObservableCollection<TabItemViewModel> Tabs { get; } = [];

    public bool IsVisible
    {
        get => _isVisible;
        private set { if (SetProperty(ref _isVisible, value)) RaisePlacementChanged(); }
    }

    /// <summary>
    /// Vrai en disposition compacte : les onglets passent en bas de l'ecran. Pilote par
    /// UILayoutService.TabsAtBottom, soit le reglage Auto/Haut/Bas, Auto se rabattant sur la
    /// detection d'un ecran etroit ou en orientation portrait — le cas du navigateur mobile.
    /// </summary>
    public bool TabsAtBottom
    {
        get => _tabsAtBottom;
        private set { if (SetProperty(ref _tabsAtBottom, value)) RaisePlacementChanged(); }
    }

    /// <summary>
    /// La barre existe en deux exemplaires — un dans la barre du haut, un ancre en bas — et
    /// c'est la visibilite qui designe celui qui s'affiche. Deux vues distinctes plutot qu'un
    /// reparentage : leur disposition differe (onglets a largeur naturelle en haut, colonnes
    /// egales sur toute la largeur en bas), et Avalonia recreerait de toute facon leurs
    /// conteneurs a chaque changement de parent.
    /// </summary>
    public bool ShowAtTop => _isVisible && !_tabsAtBottom;

    public bool ShowAtBottom => _isVisible && _tabsAtBottom;

    private void RaisePlacementChanged()
    {
        RaisePropertyChanged(nameof(ShowAtTop));
        RaisePropertyChanged(nameof(ShowAtBottom));
    }

    public void Refresh() => Apply(_host.GetTabBarSnapshot());

    /// Separe de <see cref="Refresh"/> pour que les tests puissent poser un etat sans avoir a
    /// demarrer une partie complete derriere le runtime.
    internal void Apply(SkiaLayer.TabBarSnapshot snapshot)
    {
        IsVisible = snapshot.IsVisible;
        TabsAtBottom = snapshot.TabsAtBottom;

        // La liste change rarement (deblocage d'un onglet) alors que le refresh est frequent :
        // on met a jour en place plutot que de reconstruire, sinon Avalonia recreerait tous les
        // boutons a chaque tick et l'onglet survole perdrait son etat.
        if (SameTabIds(snapshot.Tabs))
        {
            for (int i = 0; i < Tabs.Count; i++) Tabs[i].Apply(snapshot.Tabs[i]);
            return;
        }

        Tabs.Clear();
        foreach (var tab in snapshot.Tabs) Tabs.Add(new TabItemViewModel(tab));
    }

    private bool SameTabIds(IReadOnlyList<SkiaLayer.TabSnapshot> incoming)
    {
        if (incoming.Count != Tabs.Count) return false;
        for (int i = 0; i < incoming.Count; i++)
            if (incoming[i].TabId != Tabs[i].TabId) return false;
        return true;
    }

    /// <summary>
    /// Leve apres un changement d'onglet. La synchronisation de l'etat vers l'UI est cadencee a
    /// 100 ms : sans ce signal, la page qui vient d'etre demandee resterait vide jusqu'au tick
    /// suivant, soit un dixieme de seconde de page blanche a chaque changement d'onglet.
    /// </summary>
    public event Action? TabChanged;

    public void Select(int tabId)
    {
        _host.SetActiveTab(tabId);
        Refresh();
        TabChanged?.Invoke();
    }
}
