using System;
using System.Collections.Generic;
using SettlersOfIdlestan.Model.IslandMap;
using System.Text.Json.Serialization;

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
    public List<SettlersOfIdlestan.Model.City.City> Cities { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of roads in the civilization.
    /// </summary>
    public List<SettlersOfIdlestan.Model.Road.Road> Roads { get; set; } = new();

    /// <summary>
    /// Liste des ressources détenues par la civilisation.
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

        if (_resources.TryGetValue(resource, out var current))
        {
            _resources[resource] = current + quantity;
        }
        else
        {
            _resources[resource] = quantity;
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
}