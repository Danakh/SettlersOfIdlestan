using System.Collections.Concurrent;
using SettlersOfIdlestan.Controller.Expand;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Services;

namespace SettlersOfIdlestanSkia.Renderers.Overlay;

/// <summary>
/// Etat de la barre de ressources du joueur. Ne dessine plus rien : la barre est une vue
/// Avalonia, alimentee par <see cref="GetSnapshot"/>. Ne reste ici que ce que la vue ne peut pas
/// calculer elle-meme — le filtrage des ressources visibles et le clignotement de stock bas.
/// </summary>
public class PlayerResourcesOverlayRenderer
{
    /// Hauteur de la barre du haut. Les onglets plein ecran encore dessines en Skia s'y ancrent.
    public const float BarHeight = 50;

    private const long LowStockFlickerDurationMs = 5000;

    private static readonly Dictionary<Resource, Modifier.ECategory> ConsumableUnlockCategories = new()
    {
        { Resource.SteelWeapon, Modifier.ECategory.UNLOCK_STEEL_WEAPONS },
        { Resource.SteelArmor, Modifier.ECategory.UNLOCK_STEEL_ARMOR },
        { Resource.HealingPotion, Modifier.ECategory.UNLOCK_HEALING_POTION },
    };

    /// Ecrit depuis l'evenement LowStock de la civilisation, lu pendant la construction de
    /// l'instantane : ConcurrentDictionary parce que les deux n'arrivent pas forcement du meme
    /// tour de boucle.
    private readonly ConcurrentDictionary<Resource, long> _lowStockTimestamps = new();

    /// <summary>
    /// Rebranche le clignotement de stock bas sur la civilisation courante. Appele a chaque
    /// changement de monde : la civilisation du joueur est alors une nouvelle instance.
    /// </summary>
    public void ConnectLowStock(Civilization? previous, Civilization next)
    {
        if (previous != null)
            previous.LowStock -= OnLowStock;
        next.LowStock += OnLowStock;
    }

    private void OnLowStock(object? sender, Resource resource)
    {
        _lowStockTimestamps[resource] = Environment.TickCount64;
    }

    private bool IsFlickering(Resource resource)
    {
        if (!_lowStockTimestamps.TryGetValue(resource, out long ts)) return false;
        return Environment.TickCount64 - ts < LowStockFlickerDurationMs;
    }

    private static bool IsConsumableUnlocked(Resource resource, Civilization civilization)
        => ConsumableUnlockCategories.TryGetValue(resource, out var category)
            && civilization.ModifierAggregator.HasModifier(category);

    /// <summary>
    /// Instantané pour la barre de ressources portée par l'hôte : ressources découvertes,
    /// consommables débloqués, capacité non nulle.
    /// </summary>
    public ResourceBarSnapshot GetSnapshot(
        SettlersOfIdlestan.Model.Civilization.Civilization? civilization,
        SettlersOfIdlestan.Model.Prestige.PrestigeState? prestigeState)
    {
        if (civilization == null) return ResourceBarSnapshot.Unavailable;

        var map = PrestigeMapController.DefaultMap;
        var items = new List<ResourceSnapshot>();

        foreach (var resource in Enum.GetValues<Resource>())
        {
            if (ResourceUtils.DiscoverableResources.Contains(resource)
                && !(prestigeState?.IsResourceDiscovered(resource, map) ?? false)) continue;

            if (ResourceUtils.ConsumableResources.Contains(resource)
                && !IsConsumableUnlocked(resource, civilization)) continue;

            long maxQuantity = civilization.GetResourceMaxQuantity(resource);
            if (maxQuantity <= 0) continue;

            long quantity = civilization.GetResourceQuantity(resource);

            items.Add(new ResourceSnapshot(
                IconName: resource.ToString(),
                QuantityLabel: SkiaTextUtils.FormatNumber(quantity),
                MaxLabel: SkiaTextUtils.FormatNumber(maxQuantity),
                IsFlickering: IsFlickering(resource),
                IsAtMax: quantity >= maxQuantity));
        }

        return new ResourceBarSnapshot(true, items);
    }
}
