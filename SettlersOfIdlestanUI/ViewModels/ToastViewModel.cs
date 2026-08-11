using System.Collections.ObjectModel;
using SkiaLayer = SettlersOfIdlestanSkia.Services;
using SkiaOverlay = SettlersOfIdlestanSkia.Renderers.Overlay;

namespace SettlersOfIdlestanUI.ViewModels;

/// <summary>Un toast de notification affiche en bas a droite.</summary>
public sealed class ToastItemViewModel : ViewModelBase
{
    private double _opacity;

    public ToastItemViewModel(SkiaLayer.ToastSnapshot snapshot)
    {
        Id = snapshot.Id;
        Title = snapshot.Title;
        Message = snapshot.Message;
        Icon = snapshot.Icon;
        _opacity = snapshot.Opacity;
    }

    /// Stable pour la duree de vie du toast : sert a la mise a jour en place et a la fermeture.
    public long Id { get; }

    // Titre, message et icone sont fixes : un toast ne change pas de contenu une fois affiche.
    public string Title { get; }
    public string Message { get; }
    public SkiaOverlay.NotificationIcon Icon { get; }

    /// Fondu d'entree et de sortie, calcule par le renderer.
    public double Opacity { get => _opacity; private set => SetProperty(ref _opacity, value); }

    internal void Apply(SkiaLayer.ToastSnapshot snapshot) => Opacity = snapshot.Opacity;
}

/// <summary>
/// Pile de toasts. Le renderer reste la machine a etats : il decide de leur apparition, de leur
/// duree de vie et de leur ordre — ce ViewModel ne fait que refleter l'instantane.
/// </summary>
public sealed class ToastViewModel : ViewModelBase
{
    private readonly GameRuntimeHost _host;
    private bool _tabsAtBottom;

    public ToastViewModel(GameRuntimeHost host) => _host = host;

    public ObservableCollection<ToastItemViewModel> Toasts { get; } = [];

    /// Disposition mobile : les toasts remontent au-dessus de la barre d'onglets du bas.
    public bool TabsAtBottom { get => _tabsAtBottom; private set => SetProperty(ref _tabsAtBottom, value); }

    public void Refresh()
    {
        var snapshot = _host.GetToastSnapshot();
        TabsAtBottom = snapshot.TabsAtBottom;
        Sync(snapshot.Toasts);
    }

    /// La composition change a chaque apparition et expiration, mais l'opacite change en
    /// permanence : on ne recree une ligne que lorsque l'identite de la pile a change, sinon le
    /// fondu repartirait de zero a chaque tick de synchronisation.
    private void Sync(IReadOnlyList<SkiaLayer.ToastSnapshot> incoming)
    {
        bool sameIds = incoming.Count == Toasts.Count;
        for (int i = 0; i < incoming.Count && sameIds; i++)
            sameIds = incoming[i].Id == Toasts[i].Id;

        if (sameIds)
        {
            for (int i = 0; i < incoming.Count; i++) Toasts[i].Apply(incoming[i]);
            return;
        }

        Toasts.Clear();
        foreach (var toast in incoming) Toasts.Add(new ToastItemViewModel(toast));
    }

    public void Dismiss(ToastItemViewModel toast)
    {
        _host.DismissToast(toast.Id);
        Refresh();
    }
}
