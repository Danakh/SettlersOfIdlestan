using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.GameplayModifier;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SettlersOfIdlestan.Model.Civilization;

/// <summary>
/// Represents a civilization with a list of cities and roads.
/// </summary>
[Serializable]
public class Civilization
{
    /// <summary>
    /// Gets or sets the index of the civilization in the island state.
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    /// Gets or sets the list of cities in the civilization.
    /// </summary>
    public List<City> Cities { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of roads in the civilization.
    /// </summary>
    public List<Road> Roads { get; set; } = new();

    /// <summary>
    /// Gets or sets the technology tree of the civilization.
    /// </summary>
    public TechnologyTree TechnologyTree { get; set; } = new();

    /// <summary>
    /// Aggregates modifiers from all providers (TechnologyTree, Prestige, …).
    /// Must be initialized via <see cref="SetupModifierAggregator"/> before use.
    /// </summary>
    [JsonIgnore]
    public ModifierAggregator ModifierAggregator { get; } = new();

    /// <summary>
    /// Registers the given providers on the aggregator, replacing any previous registration.
    /// Call this after deserialization or when the set of providers changes.
    /// </summary>
    public void SetupModifierAggregator(params IModifierProvider[] providers)
    {
        ModifierAggregator.Clear();
        foreach (var p in providers)
            ModifierAggregator.Register(p);
    }

    /// <summary>
    /// Research speed multiplier. 1.0 = normal speed.
    /// </summary>
    [JsonIgnore]
    public double ResearchSpeed => ModifierAggregator.ApplyModifiers(ECategory.RESEARCH_SPEED, "", 1.0);

    /// <summary>
    /// Unit production speed multiplier. 1.0 = normal speed.
    /// </summary>
    [JsonIgnore]
    public double UnitProductionSpeed => ModifierAggregator.ApplyModifiers(ECategory.UNIT_PRODUCTION_SPEED, "", 1.0);

    /// <summary>
    /// Research cost reduction fraction (0.0 = no reduction, 0.1 = 10% cheaper).
    /// </summary>
    [JsonIgnore]
    public double ResearchCostReduction => ModifierAggregator.ApplyModifiers(ECategory.RESEARCH_COST_REDUCTION, "", 0.0);

    /// <summary>
    /// Liste des ressources d�tenues par la civilisation.
    /// </summary>
    // Resources are stored as a map from Resource -> quantity.
    // Made private: access should be done through AddResource/RemoveResource and GetResourceQuantity.
    private readonly Dictionary<Resource, int> _resources = new();

    // Expose resources for serialization. The public property is annotated so System.Text.Json
    // will include it during export/import. The private setter maps values back to the private
    // dictionary to preserve encapsulation for runtime access.
    [JsonInclude]
    public Dictionary<Resource, int> Resources
    {
        get => _resources;
        private set
        {
            _resources.Clear();
            if (value == null) return;
            foreach (var kv in value)
            {
                _resources[kv.Key] = kv.Value;
            }
        }
    }

    /// <summary>
    /// Adds the given quantity of a resource to the civilization's stock.
    /// </summary>
    public void AddResource(Resource resource, int quantity)
    {
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be positive.");
        var max = GetResourceMaxQuantity(resource);

        if (_resources.TryGetValue(resource, out var current))
        {
            _resources[resource] = Math.Min(current + quantity, max);
        }
        else
        {
            _resources[resource] = Math.Min(quantity, max);
        }
    }

    /// <summary>
    /// Removes the given quantity of a resource from the civilization's stock.
    /// Throws InvalidOperationException if not enough resource is available.
    /// </summary>
    public void RemoveResource(Resource resource, int quantity)
    {
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be positive.");

        if (!_resources.TryGetValue(resource, out var current) || current < quantity)
            throw new InvalidOperationException($"Not enough {resource} to remove: requested {quantity}, available {current}.");

        var remaining = current - quantity;
        if (remaining > 0)
            _resources[resource] = remaining;
        else
            _resources.Remove(resource);
    }

    /// <summary>
    /// Gets the current quantity of the given resource (0 if none).
    /// </summary>
    public int GetResourceQuantity(Resource resource)
    {
        return _resources.TryGetValue(resource, out var q) ? q : 0;
    }

    public int GetResourceMaxQuantity(Resource resource)
    {
        int baseCityResourceMax = 2 * Cities.Count + Cities.Sum(city => city.Level);
        int advancedCityResourceMax = Cities.Sum(city => Math.Max(0, city.Level - 2));
        int cityWithWarehouseCount = Cities.Count(city => city.Buildings.Any(building => building.Type == BuildingType.Warehouse));

        bool isBasic = ResourceUtils.BasicResources.Contains(resource);
        int storageBonus = isBasic
            ? ModifierAggregator.ApplyModifiers(ECategory.STORAGE_CAPACITY_BASIC, "", 0)
            : ModifierAggregator.ApplyModifiers(ECategory.STORAGE_CAPACITY_ADVANCED, "", 0);

        int result = isBasic
            ? 5 * baseCityResourceMax + 30 * cityWithWarehouseCount
            : 5 * advancedCityResourceMax + 10 * cityWithWarehouseCount;

        return result + storageBonus;
    }

    public bool CanPayResourceCost(ResourceCost cost)
    {
        foreach (var kvp in cost)
        {
            if (GetResourceQuantity(kvp.Key) < kvp.Value)
                return false;
        }
        return true;
    }
    public void PayResourceCost(ResourceCost cost)
    {
        foreach (var kvp in cost)
        {
            RemoveResource(kvp.Key, kvp.Value);
        }
    }
}