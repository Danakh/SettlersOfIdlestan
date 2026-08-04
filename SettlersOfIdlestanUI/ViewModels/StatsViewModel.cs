using System.Collections.ObjectModel;
using SkiaLayer = SettlersOfIdlestanSkia.Services;

namespace SettlersOfIdlestanUI.ViewModels;

/// <summary>Un sous-onglet de la page Stats.</summary>
public sealed class StatsSubTabViewModel : ViewModelBase
{
    private bool _isActive;

    public StatsSubTabViewModel(SkiaLayer.StatsSubTabSnapshot snapshot)
    {
        Key = snapshot.Key;
        Label = snapshot.Label;
        _isActive = snapshot.IsActive;
    }

    public string Key { get; }
    public string Label { get; }
    public bool IsActive { get => _isActive; internal set => SetProperty(ref _isActive, value); }
}

/// <summary>
/// Onglet plein ecran des statistiques. Le sous-onglet actif, le choix des statistiques
/// affichees et leur formatage restent cote renderer : ce ViewModel reflete l'instantane.
/// </summary>
public sealed class StatsViewModel : ViewModelBase
{
    private readonly GameRuntimeHost _host;

    private bool _isVisible;
    private IReadOnlyList<SkiaLayer.StatSectionSnapshot> _lastSections = [];

    public StatsViewModel(GameRuntimeHost host) => _host = host;

    public ObservableCollection<StatsSubTabViewModel> SubTabs { get; } = [];

    /// <summary>
    /// Sections exposees telles quelles : ce sont des records immuables, la vue les lie
    /// directement plutot que de recopier trois niveaux d'enveloppes sans comportement.
    /// </summary>
    public ObservableCollection<SkiaLayer.StatSectionSnapshot> Sections { get; } = [];

    public bool IsVisible { get => _isVisible; private set => SetProperty(ref _isVisible, value); }

    public void Refresh()
    {
        var snapshot = _host.GetStatsSnapshot();
        IsVisible = snapshot.IsVisible;

        SyncSubTabs(snapshot.SubTabs);

        // Les chiffres changent en continu, mais l'egalite structurelle des records rend la
        // comparaison exacte : on ne reconstruit que lorsqu'une valeur a reellement bouge.
        if (_lastSections.SequenceEqual(snapshot.Sections)) return;
        _lastSections = snapshot.Sections;

        Sections.Clear();
        foreach (var section in snapshot.Sections) Sections.Add(section);
    }

    /// La composition ne change qu'au deblocage de l'ascension ; l'etat actif change a chaque
    /// clic. Mise a jour en place pour ne pas recreer les boutons sous le curseur.
    private void SyncSubTabs(IReadOnlyList<SkiaLayer.StatsSubTabSnapshot> incoming)
    {
        bool sameKeys = incoming.Count == SubTabs.Count;
        for (int i = 0; i < incoming.Count && sameKeys; i++)
            sameKeys = incoming[i].Key == SubTabs[i].Key;

        if (sameKeys)
        {
            for (int i = 0; i < incoming.Count; i++) SubTabs[i].IsActive = incoming[i].IsActive;
            return;
        }

        SubTabs.Clear();
        foreach (var tab in incoming) SubTabs.Add(new StatsSubTabViewModel(tab));
    }

    public void SelectSubTab(StatsSubTabViewModel subTab)
    {
        _host.SetStatsSubTab(subTab.Key);
        Refresh();
    }
}
