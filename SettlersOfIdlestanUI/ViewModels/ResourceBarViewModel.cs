using System.Collections.ObjectModel;
using SkiaLayer = SettlersOfIdlestanSkia.Services;

namespace SettlersOfIdlestanUI.ViewModels;

/// <summary>Une pastille de ressource dans la barre du haut.</summary>
public sealed class ResourceItemViewModel : ViewModelBase
{
    private string _quantityLabel;
    private string _maxLabel;
    private bool _isFlickering;
    private bool _isAtMax;

    public ResourceItemViewModel(SkiaLayer.ResourceSnapshot snapshot)
    {
        IconName = snapshot.IconName;
        _quantityLabel = snapshot.QuantityLabel;
        _maxLabel = snapshot.MaxLabel;
        _isFlickering = snapshot.IsFlickering;
        _isAtMax = snapshot.IsAtMax;
    }

    /// Nom d'enum de la ressource, resolu en icone par SvgIconCache.
    public string IconName { get; }

    public string QuantityLabel { get => _quantityLabel; private set => SetProperty(ref _quantityLabel, value); }

    public string MaxLabel { get => _maxLabel; private set => SetProperty(ref _maxLabel, value); }

    /// Stock recemment tombe bas : la pastille clignote quelques secondes.
    public bool IsFlickering { get => _isFlickering; private set => SetProperty(ref _isFlickering, value); }

    /// Stock plein : la quantite change de couleur.
    public bool IsAtMax { get => _isAtMax; private set => SetProperty(ref _isAtMax, value); }

    internal void Apply(SkiaLayer.ResourceSnapshot snapshot)
    {
        QuantityLabel = snapshot.QuantityLabel;
        MaxLabel = snapshot.MaxLabel;
        IsFlickering = snapshot.IsFlickering;
        IsAtMax = snapshot.IsAtMax;
    }
}

/// <summary>
/// Barre de ressources. Le filtrage (ressources decouvertes, consommables debloques, capacite
/// non nulle) reste dans PlayerResourcesOverlayRenderer : ce ViewModel reflete son instantane.
/// </summary>
public sealed class ResourceBarViewModel : ViewModelBase
{
    private readonly GameRuntimeHost _host;
    private bool _isAvailable;

    public ResourceBarViewModel(GameRuntimeHost host) => _host = host;

    public ObservableCollection<ResourceItemViewModel> Resources { get; } = [];

    public bool IsAvailable { get => _isAvailable; private set => SetProperty(ref _isAvailable, value); }

    public void Refresh()
    {
        var snapshot = _host.GetResourceBarSnapshot();
        IsAvailable = snapshot.IsAvailable;

        // La composition ne change qu'a un deblocage, alors que les quantites changent en
        // permanence : on met a jour en place pour ne pas recreer les pastilles a chaque tick.
        if (SameComposition(snapshot.Resources))
        {
            for (int i = 0; i < Resources.Count; i++) Resources[i].Apply(snapshot.Resources[i]);
            return;
        }

        Resources.Clear();
        foreach (var resource in snapshot.Resources) Resources.Add(new ResourceItemViewModel(resource));
    }

    private bool SameComposition(IReadOnlyList<SkiaLayer.ResourceSnapshot> incoming)
    {
        if (incoming.Count != Resources.Count) return false;
        for (int i = 0; i < incoming.Count; i++)
            if (incoming[i].IconName != Resources[i].IconName) return false;
        return true;
    }
}
