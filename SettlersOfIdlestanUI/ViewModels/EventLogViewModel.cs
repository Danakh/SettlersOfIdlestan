using System.Collections.ObjectModel;
using SkiaLayer = SettlersOfIdlestanSkia.Services;

namespace SettlersOfIdlestanUI.ViewModels;

/// <summary>Une entree du journal. Son contenu est fige : un evenement passe ne change plus.</summary>
public sealed class EventLogEntryViewModel
{
    public EventLogEntryViewModel(SkiaLayer.EventLogEntrySnapshot snapshot)
    {
        Title = snapshot.Title;
        Body = snapshot.Body;
        Tone = snapshot.Tone;
    }

    public string Title { get; }
    public string Body { get; }
    public SkiaLayer.EventLogTone Tone { get; }
}

/// <summary>
/// Onglet plein ecran du journal des evenements. Le classement des types d'evenement en tons et
/// la selection de l'onglet actif restent cote renderer.
/// </summary>
public sealed class EventLogViewModel : ViewModelBase
{
    private readonly GameRuntimeHost _host;

    private bool _isVisible;
    private string _title = "";
    private string _emptyMessage = "";
    private IReadOnlyList<SkiaLayer.EventLogEntrySnapshot> _last = [];

    public EventLogViewModel(GameRuntimeHost host) => _host = host;

    public ObservableCollection<EventLogEntryViewModel> Entries { get; } = [];

    public bool IsVisible { get => _isVisible; private set => SetProperty(ref _isVisible, value); }
    public string Title { get => _title; private set => SetProperty(ref _title, value); }
    public string EmptyMessage { get => _emptyMessage; private set => SetProperty(ref _emptyMessage, value); }

    public bool IsEmpty => Entries.Count == 0;

    public void Refresh()
    {
        var snapshot = _host.GetEventLogSnapshot();

        // Onglet inactif : on masque la vue sans toucher aux entrees, sous peine de detruire
        // l'arbre de controles de la page et de devoir le rebatir au retour sur l'onglet (voir
        // AutomationViewModel.Refresh pour les mesures).
        if (!snapshot.IsVisible)
        {
            IsVisible = false;
            return;
        }

        IsVisible = snapshot.IsVisible;
        Title = snapshot.Title;
        EmptyMessage = snapshot.EmptyMessage;

        Sync(snapshot.Entries);
    }

    /// <summary>
    /// Une entree du journal est immuable : seule la composition de la liste peut changer, par
    /// ajout en tete. On compare donc l'instantane precedent au nouveau — l'egalite structurelle
    /// des records rend la comparaison exacte, la ou un raccourci (nombre d'entrees, titre de la
    /// premiere) manquerait deux evenements identiques consecutifs a liste pleine.
    ///
    /// Reconstruire sans cette garde recreerait 50 controles dix fois par seconde.
    /// </summary>
    private void Sync(IReadOnlyList<SkiaLayer.EventLogEntrySnapshot> incoming)
    {
        if (_last.SequenceEqual(incoming)) return;

        bool wasEmpty = IsEmpty;
        _last = incoming;

        Entries.Clear();
        foreach (var entry in incoming) Entries.Add(new EventLogEntryViewModel(entry));

        if (wasEmpty != IsEmpty) RaisePropertyChanged(nameof(IsEmpty));
    }
}
