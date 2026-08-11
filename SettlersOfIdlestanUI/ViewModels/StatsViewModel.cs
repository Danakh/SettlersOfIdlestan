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

        // Onglet inactif : on masque la vue sans toucher aux collections. Repercuter
        // l'instantane masque, qui est vide, detruisait tout l'arbre de controles de la page —
        // a rebatir au retour sur l'onglet, ce qui rendait le changement d'onglet lent (voir
        // AutomationViewModel.Refresh pour les mesures).
        if (!snapshot.IsVisible)
        {
            IsVisible = false;
            return;
        }

        IsVisible = snapshot.IsVisible;

        SyncSubTabs(snapshot.SubTabs);
        SyncSections(snapshot.Sections);
    }

    /// <summary>
    /// Mise a jour section par section, jamais par un vidage suivi d'un remplissage.
    ///
    /// La section « partie en cours » contient un temps de jeu, donc change a chaque
    /// synchronisation ; l'historique, lui, ne bouge qu'a un prestige. Tout reconstruire des que
    /// l'une bouge rebatissait l'arbre de controles de l'historique entier dix fois par seconde —
    /// avec quelques dizaines de parties passees, l'onglet figeait l'application. Ne remplacer
    /// que les sections reellement differentes laisse l'historique intact.
    /// </summary>
    internal void SyncSections(IReadOnlyList<SkiaLayer.StatSectionSnapshot> incoming)
    {
        for (int i = 0; i < incoming.Count; i++)
        {
            if (i >= Sections.Count) Sections.Add(incoming[i]);
            else if (!Sections[i].Equals(incoming[i])) Sections[i] = incoming[i];
        }

        while (Sections.Count > incoming.Count) Sections.RemoveAt(Sections.Count - 1);
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
