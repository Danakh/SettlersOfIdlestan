using System.Collections.ObjectModel;
using SkiaLayer = SettlersOfIdlestanSkia.Services;

namespace SettlersOfIdlestanUI.ViewModels;

/// <summary>Un onglet de l'ecran-titre.</summary>
public sealed class TitleTabViewModel : ViewModelBase
{
    private string _label;
    private bool _isActive;

    public TitleTabViewModel(SkiaLayer.TitleTabSnapshot snapshot)
    {
        Key = snapshot.Key;
        _label = snapshot.Label;
        _isActive = snapshot.IsActive;
    }

    public string Key { get; }

    /// Modifiable : l'onglet des reglages permet de changer la langue, qui relocalise ces
    /// libelles — meme raison que pour les options du panneau de reglages.
    public string Label { get => _label; internal set => SetProperty(ref _label, value); }

    public bool IsActive { get => _isActive; internal set => SetProperty(ref _isActive, value); }
}

/// <summary>Un bouton de l'ecran-titre.</summary>
public sealed class TitleActionViewModel : ViewModelBase
{
    private string _label;

    public TitleActionViewModel(SkiaLayer.TitleActionSnapshot snapshot)
    {
        Key = snapshot.Key;
        _label = snapshot.Label;
        Tone = snapshot.Tone;
    }

    public string Key { get; }
    public string Label { get => _label; internal set => SetProperty(ref _label, value); }
    public SkiaLayer.TitleActionTone Tone { get; }
}

/// <summary>
/// Ecran-titre. La presence d'une sauvegarde, la disponibilite du cloud et le contenu du
/// changelog restent dans TitleScreen : ce ViewModel reflete l'instantane et relaie les
/// commandes.
/// </summary>
public sealed class TitleScreenViewModel : ViewModelBase
{
    private readonly GameRuntimeHost _host;

    private bool _isVisible;
    private string _title = "";
    private string _changelogText = "";
    private string _creditsStudio = "";
    private string _creditsDev = "";
    private string _discordUrl = "";
    private string _activeTabKey = SkiaLayer.TitleScreenSnapshot.TabChangelog;

    public TitleScreenViewModel(GameRuntimeHost host)
    {
        _host = host;
        Settings = new SettingsPanelViewModel(
            host.SetTitleSettingToggle, host.SetTitleSettingChoice,
            host.SetTitleSettingSlider, host.SetTitleSettingText);
    }

    /// Le meme panneau que le popup de reglages en jeu, cable sur les reglages de l'ecran-titre.
    public SettingsPanelViewModel Settings { get; }

    public ObservableCollection<TitleTabViewModel> Tabs { get; } = [];
    public ObservableCollection<TitleActionViewModel> Actions { get; } = [];

    public bool IsVisible { get => _isVisible; private set => SetProperty(ref _isVisible, value); }
    public string Title { get => _title; private set => SetProperty(ref _title, value); }
    public string ChangelogText { get => _changelogText; private set => SetProperty(ref _changelogText, value); }
    public string CreditsStudio { get => _creditsStudio; private set => SetProperty(ref _creditsStudio, value); }
    public string CreditsDev { get => _creditsDev; private set => SetProperty(ref _creditsDev, value); }
    public string DiscordUrl { get => _discordUrl; private set => SetProperty(ref _discordUrl, value); }

    // Les trois onglets s'excluent : exactement un contenu est affiche.
    public bool ShowingChangelog => _activeTabKey == SkiaLayer.TitleScreenSnapshot.TabChangelog;
    public bool ShowingCredits => _activeTabKey == SkiaLayer.TitleScreenSnapshot.TabCredits;
    public bool ShowingSettings => _activeTabKey == SkiaLayer.TitleScreenSnapshot.TabSettings;

    public void Refresh()
    {
        var snapshot = _host.GetTitleScreenSnapshot();

        IsVisible = snapshot.IsVisible;
        if (!snapshot.IsVisible) return;

        Title = snapshot.Title;
        ChangelogText = snapshot.ChangelogText;
        CreditsStudio = snapshot.CreditsStudio;
        CreditsDev = snapshot.CreditsDev;
        DiscordUrl = snapshot.DiscordUrl;

        SyncTabs(snapshot.Tabs);
        SyncActions(snapshot.Actions);
        Settings.Apply(snapshot.Settings);
    }

    private void SyncTabs(IReadOnlyList<SkiaLayer.TitleTabSnapshot> incoming)
    {
        if (Tabs.Count != incoming.Count)
        {
            Tabs.Clear();
            foreach (var tab in incoming) Tabs.Add(new TitleTabViewModel(tab));
        }
        else
        {
            for (int i = 0; i < incoming.Count; i++)
            {
                Tabs[i].Label = incoming[i].Label;
                Tabs[i].IsActive = incoming[i].IsActive;
            }
        }

        string active = incoming.FirstOrDefault(t => t.IsActive)?.Key
                        ?? SkiaLayer.TitleScreenSnapshot.TabChangelog;
        if (_activeTabKey == active) return;

        _activeTabKey = active;
        RaisePropertyChanged(nameof(ShowingChangelog));
        RaisePropertyChanged(nameof(ShowingCredits));
        RaisePropertyChanged(nameof(ShowingSettings));
    }

    /// La composition change quand une sauvegarde apparait ou disparait ; les libelles changent
    /// avec la langue. Mise a jour en place tant que les cles concordent.
    private void SyncActions(IReadOnlyList<SkiaLayer.TitleActionSnapshot> incoming)
    {
        bool sameKeys = incoming.Count == Actions.Count;
        for (int i = 0; i < incoming.Count && sameKeys; i++)
            sameKeys = incoming[i].Key == Actions[i].Key;

        if (sameKeys)
        {
            for (int i = 0; i < incoming.Count; i++) Actions[i].Label = incoming[i].Label;
            return;
        }

        Actions.Clear();
        foreach (var action in incoming) Actions.Add(new TitleActionViewModel(action));
    }

    public void SelectTab(TitleTabViewModel tab)
    {
        _host.SetTitleTab(tab.Key);
        Refresh();
    }

    public void Invoke(TitleActionViewModel action)
    {
        _host.InvokeTitleAction(action.Key);
        Refresh();
    }

    public void OpenDiscord()
    {
        _host.InvokeTitleAction(SkiaLayer.TitleScreenSnapshot.ActionDiscord);
        Refresh();
    }
}
