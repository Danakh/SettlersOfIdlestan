using System.Collections.ObjectModel;
using SkiaLayer = SettlersOfIdlestanSkia.Services;

namespace SettlersOfIdlestanUI.ViewModels;

/// <summary>Apparence d'un bouton d'action, resolue depuis son etat.</summary>
public enum CivActionVisual
{
    Normal,

    /// Action indisponible. Le bouton reste actif au sens Avalonia : desactive, il ne recevrait
    /// plus les evenements de pointeur et n'afficherait donc plus son infobulle — or c'est
    /// justement elle qui dit pourquoi l'action est bloquee. Le refus est porte par le renderer.
    Disabled,

    /// Action en cours (pillage actif) : le bouton l'arrete au lieu d'en lancer une.
    Highlighted,
}

/// <summary>Une action du panneau civilisation : bouton de la grille ou bouton icone de l'en-tete.</summary>
public sealed class CivActionViewModel : ViewModelBase
{
    private string _label;
    private bool _isEnabled;
    private bool _isHighlighted;
    private string _tooltip;

    public CivActionViewModel(SkiaLayer.CivActionSnapshot snapshot)
    {
        Key = snapshot.Key;
        IconName = snapshot.IconName;
        Glyph = snapshot.Glyph;
        _label = snapshot.Label;
        _isEnabled = snapshot.IsEnabled;
        _isHighlighted = snapshot.IsHighlighted;
        _tooltip = Join(snapshot.TooltipLines);
    }

    /// Identifiant stable de l'action : sert au routage du clic vers le renderer.
    public string Key { get; }

    /// Ressource SVG de l'icone, pour les boutons de l'en-tete ; null pour la grille.
    public string? IconName { get; }

    /// Glyphe affiche a la place d'une icone SVG (commerce) ; null sinon.
    public string? Glyph { get; }

    public bool HasGlyph => Glyph != null;

    public string Label { get => _label; private set => SetProperty(ref _label, value); }

    public bool IsEnabled
    {
        get => _isEnabled;
        private set { if (SetProperty(ref _isEnabled, value)) RaisePropertyChanged(nameof(Visual)); }
    }

    /// Action en cours (pillage actif) : le bouton change de couleur et arrete au lieu de lancer.
    public bool IsHighlighted
    {
        get => _isHighlighted;
        private set { if (SetProperty(ref _isHighlighted, value)) RaisePropertyChanged(nameof(Visual)); }
    }

    public CivActionVisual Visual => !IsEnabled ? CivActionVisual.Disabled
                                   : IsHighlighted ? CivActionVisual.Highlighted
                                   : CivActionVisual.Normal;

    /// Infobulle deja localisee, lignes jointes par des retours a la ligne. Vide = pas d'infobulle.
    public string Tooltip { get => _tooltip; private set => SetProperty(ref _tooltip, value); }

    internal void Apply(SkiaLayer.CivActionSnapshot snapshot)
    {
        Label = snapshot.Label;
        IsEnabled = snapshot.IsEnabled;
        IsHighlighted = snapshot.IsHighlighted;
        Tooltip = Join(snapshot.TooltipLines);
    }

    private static string Join(IReadOnlyList<string> lines) => string.Join('\n', lines);
}

/// <summary>Une bascule epinglee de la section Controles.</summary>
public sealed class CivToggleViewModel : ViewModelBase
{
    private string _label;
    private bool? _isOn;
    private string _tooltip;

    public CivToggleViewModel(SkiaLayer.CivToggleSnapshot snapshot)
    {
        Key = snapshot.Key;
        Category = snapshot.Category;
        _label = snapshot.Label;
        _isOn = snapshot.IsOn;
        _tooltip = snapshot.Tooltip;
    }

    public string Key { get; }

    /// Famille de l'automatisme (construction, comportement, activation) : la vue s'en sert pour
    /// styler chaque bascule differemment. Fixe pour la duree de vie de la ligne — une bascule
    /// epinglee ne change jamais de famille — donc pas repercutee dans Apply.
    public SkiaLayer.AutomationCategory Category { get; }

    public string Label { get => _label; private set => SetProperty(ref _label, value); }

    /// Trois etats : tout actif, tout inactif, ou null pour un etat mixte.
    public bool? IsOn { get => _isOn; set => SetProperty(ref _isOn, value); }

    public string Tooltip { get => _tooltip; private set => SetProperty(ref _tooltip, value); }

    internal void Apply(SkiaLayer.CivToggleSnapshot snapshot)
    {
        Label = snapshot.Label;
        IsOn = snapshot.IsOn;
        Tooltip = snapshot.Tooltip;
    }
}

/// <summary>
/// Panneau lateral de la civilisation du joueur. Les regles de deblocage, de disponibilite et
/// le repli automatique restent dans PlayerCivilizationPanelRenderer : ce ViewModel reflete
/// l'instantane et relaie les commandes.
/// </summary>
public sealed class CivPanelViewModel : ViewModelBase
{
    private readonly GameRuntimeHost _host;

    private bool _isVisible;
    private bool _isCollapsed;
    private string _actionsTitle = "";
    private string _controlsTitle = "";

    public CivPanelViewModel(GameRuntimeHost host)
    {
        _host = host;

        // Les drapeaux de section derivent des collections plutot que d'etre recopies : recopies,
        // ils se desynchroniseraient de tout ce qui modifie une liste sans passer par Refresh.
        IconActions.CollectionChanged += (_, _) => RaiseSectionFlags();
        Actions.CollectionChanged += (_, _) => RaiseSectionFlags();
        Toggles.CollectionChanged += (_, _) => RaiseSectionFlags();
    }

    private void RaiseSectionFlags()
    {
        RaisePropertyChanged(nameof(HasActions));
        RaisePropertyChanged(nameof(HasToggles));
        RaisePropertyChanged(nameof(HasSeparator));
    }

    public ObservableCollection<CivActionViewModel> IconActions { get; } = [];
    public ObservableCollection<CivActionViewModel> Actions { get; } = [];
    public ObservableCollection<CivToggleViewModel> Toggles { get; } = [];

    public bool IsVisible { get => _isVisible; private set => SetProperty(ref _isVisible, value); }

    /// Detenu par le renderer, pas par la vue : la disposition mobile le replie d'autorite.
    public bool IsCollapsed { get => _isCollapsed; private set => SetProperty(ref _isCollapsed, value); }

    public string ActionsTitle { get => _actionsTitle; private set => SetProperty(ref _actionsTitle, value); }
    public string ControlsTitle { get => _controlsTitle; private set => SetProperty(ref _controlsTitle, value); }

    /// La section Actions n'apparait que si elle a du contenu — titre compris.
    public bool HasActions => IconActions.Count > 0 || Actions.Count > 0;

    public bool HasToggles => Toggles.Count > 0;

    /// Le separateur ne se justifie qu'entre deux sections effectivement presentes.
    public bool HasSeparator => HasActions && HasToggles;

    public void Refresh()
    {
        var snapshot = _host.GetCivPanelSnapshot();

        IsVisible = snapshot.IsVisible;
        IsCollapsed = snapshot.IsCollapsed;
        ActionsTitle = snapshot.ActionsTitle;
        ControlsTitle = snapshot.ControlsTitle;

        SyncActions(IconActions, snapshot.IconActions);
        SyncActions(Actions, snapshot.Actions);
        SyncToggles(snapshot.Toggles);
    }

    /// La composition ne change qu'au deblocage d'une action ou d'un changement de couche ; les
    /// libelles et l'activation changent en continu. Mise a jour en place pour ne pas recreer les
    /// boutons a chaque tick de synchronisation, ce qui ferait perdre le survol et l'infobulle.
    private static void SyncActions(
        ObservableCollection<CivActionViewModel> target,
        IReadOnlyList<SkiaLayer.CivActionSnapshot> incoming)
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
        foreach (var action in incoming) target.Add(new CivActionViewModel(action));
    }

    private void SyncToggles(IReadOnlyList<SkiaLayer.CivToggleSnapshot> incoming)
    {
        bool sameKeys = incoming.Count == Toggles.Count;
        for (int i = 0; i < incoming.Count && sameKeys; i++)
            sameKeys = incoming[i].Key == Toggles[i].Key;

        if (sameKeys)
        {
            for (int i = 0; i < incoming.Count; i++) Toggles[i].Apply(incoming[i]);
            return;
        }

        Toggles.Clear();
        foreach (var toggle in incoming) Toggles.Add(new CivToggleViewModel(toggle));
    }

    public void Execute(CivActionViewModel action)
    {
        _host.ExecuteCivAction(action.Key);
        Refresh();
    }

    public void Toggle(CivToggleViewModel toggle)
    {
        _host.ToggleCivPinned(toggle.Key);
        Refresh();
    }

    public void SetCollapsed(bool collapsed)
    {
        _host.SetCivPanelCollapsed(collapsed);
        Refresh();
    }
}
