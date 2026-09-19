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
/// Une ligne de l'onglet Reglages : une famille d'evenements, affichable et audible
/// independamment. Deux cases a cocher, une par colonne.
/// </summary>
public sealed class EventLogFilterViewModel : ViewModelBase
{
    private bool _isChecked;
    private bool _isSoundChecked;

    public EventLogFilterViewModel(SkiaLayer.EventLogFilterSnapshot snapshot)
    {
        Key = snapshot.Key;
        Label = snapshot.Label;
        _isChecked = snapshot.IsChecked;
        _isSoundChecked = snapshot.IsSoundChecked;
    }

    public string Key { get; }
    public string Label { get; }

    /// Cochee = les evenements de cette famille sont journalises. Le modele, lui, stocke
    /// l'inverse (les familles masquees) : voir EventLogFilter.
    public bool IsChecked { get => _isChecked; internal set => SetProperty(ref _isChecked, value); }

    /// Cochee = le bruitage de notification de cette famille se fait entendre. Independant de
    /// <see cref="IsChecked"/> : une famille masquee peut rester audible.
    public bool IsSoundChecked
    {
        get => _isSoundChecked;
        internal set => SetProperty(ref _isSoundChecked, value);
    }
}

/// <summary>
/// Onglet plein ecran du journal des evenements. Le classement des types d'evenement en tons, la
/// selection de l'onglet actif et l'ouverture du sous-onglet Reglages restent cote renderer.
/// </summary>
public sealed class EventLogViewModel : ViewModelBase
{
    private readonly GameRuntimeHost _host;

    private bool _isVisible;
    private string _title = "";
    private string _emptyMessage = "";
    private bool _showSettings;
    private string _settingsTitle = "";
    private string _settingsHint = "";
    private string _settingsEmptyMessage = "";
    private string _settingsDisplayHeader = "";
    private string _settingsSoundHeader = "";
    private IReadOnlyList<SkiaLayer.EventLogEntrySnapshot> _last = [];

    public EventLogViewModel(GameRuntimeHost host) => _host = host;

    public ObservableCollection<EventLogEntryViewModel> Entries { get; } = [];
    public ObservableCollection<EventLogFilterViewModel> Filters { get; } = [];

    public bool IsVisible { get => _isVisible; private set => SetProperty(ref _isVisible, value); }
    public string Title { get => _title; private set => SetProperty(ref _title, value); }
    public string EmptyMessage { get => _emptyMessage; private set => SetProperty(ref _emptyMessage, value); }

    /// Sous-onglet Reglages ouvert : la liste des entrees cede la place aux cases a cocher.
    /// Le setter est interne et non prive pour que les tests d'interface puissent ouvrir la page
    /// sans partie en cours — hors tests, il ne se pilote que par l'instantane.
    public bool ShowSettings
    {
        get => _showSettings;
        internal set
        {
            if (SetProperty(ref _showSettings, value)) RaisePropertyChanged(nameof(ShowEntries));
        }
    }

    public bool ShowEntries => !_showSettings;
    public string SettingsTitle { get => _settingsTitle; private set => SetProperty(ref _settingsTitle, value); }
    public string SettingsHint { get => _settingsHint; private set => SetProperty(ref _settingsHint, value); }
    public string SettingsEmptyMessage
    {
        get => _settingsEmptyMessage;
        private set => SetProperty(ref _settingsEmptyMessage, value);
    }

    /// En-tetes des deux colonnes de cases a cocher.
    public string SettingsDisplayHeader
    {
        get => _settingsDisplayHeader;
        private set => SetProperty(ref _settingsDisplayHeader, value);
    }

    public string SettingsSoundHeader
    {
        get => _settingsSoundHeader;
        private set => SetProperty(ref _settingsSoundHeader, value);
    }

    public bool IsEmpty => Entries.Count == 0;

    /// Aucune famille encore croisee : rien a regler, on l'explique plutot que d'afficher un vide.
    public bool HasNoFilter => Filters.Count == 0;

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
        ShowSettings = snapshot.ShowSettings;
        SettingsTitle = snapshot.SettingsTitle;
        SettingsHint = snapshot.SettingsHint;
        SettingsEmptyMessage = snapshot.SettingsEmptyMessage;
        SettingsDisplayHeader = snapshot.SettingsDisplayHeader;
        SettingsSoundHeader = snapshot.SettingsSoundHeader;

        Sync(snapshot.Entries);
        SyncFilters(snapshot.Filters);
    }

    /// <summary>Ouvre ou ferme le sous-onglet Reglages (bouton engrenage en haut a droite).</summary>
    public void ToggleSettings()
    {
        _host.ToggleEventLogSettings();
        Refresh();
    }

    public void ToggleFilter(EventLogFilterViewModel filter)
    {
        _host.ToggleEventLogFilter(filter.Key);
        Refresh();
    }

    /// <summary>Colonne Son : coupe ou rend le bruitage de cette famille, sans toucher a l'affichage.</summary>
    public void ToggleFilterSound(EventLogFilterViewModel filter)
    {
        _host.ToggleEventLogFilterSound(filter.Key);
        Refresh();
    }

    /// <summary>
    /// La liste des familles est fixe : seul l'etat des cases change. On ne la reconstruit donc
    /// que si les cles different — sinon chaque tick recreerait les controles sous le curseur.
    /// </summary>
    private void SyncFilters(IReadOnlyList<SkiaLayer.EventLogFilterSnapshot> incoming)
    {
        bool sameKeys = incoming.Count == Filters.Count;
        for (int i = 0; i < incoming.Count && sameKeys; i++) sameKeys = incoming[i].Key == Filters[i].Key;

        if (sameKeys)
        {
            for (int i = 0; i < incoming.Count; i++)
            {
                Filters[i].IsChecked = incoming[i].IsChecked;
                Filters[i].IsSoundChecked = incoming[i].IsSoundChecked;
            }
            return;
        }

        // La liste s'allonge en cours de partie, a chaque famille rencontree pour la premiere fois.
        bool hadNoFilter = HasNoFilter;

        Filters.Clear();
        foreach (var filter in incoming) Filters.Add(new EventLogFilterViewModel(filter));

        if (hadNoFilter != HasNoFilter) RaisePropertyChanged(nameof(HasNoFilter));
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
