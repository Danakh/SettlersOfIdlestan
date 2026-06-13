using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.GameplayModifier;
using System;
using System.Collections.Generic;
using System.Linq;
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
    /// Indique si cette civilisation est contrôlée par l'IA.
    /// </summary>
    public bool IsNpc { get; set; } = false;

    /// <summary>
    /// Vrai une fois que le joueur a aperçu cette civilisation (évite les doublons dans le log).
    /// </summary>
    public bool DiscoveredByPlayer { get; set; } = false;

    /// <summary>
    /// Paramètres NPC (niveau d'évolution, agressivité). Null pour le joueur.
    /// </summary>
    public NpcParameters? NpcParameters { get; set; }

    private List<City> _cities = new();

    /// <summary>
    /// Liste des villes de la civilisation — lecture seule ; utiliser AddCity / RemoveCity pour muter.
    /// </summary>
    [JsonIgnore]
    public IReadOnlyList<City> Cities => _cities;

    // Utilisé uniquement par la sérialisation JSON.
    [JsonPropertyName("Cities")]
    [JsonInclude]
    public List<City> CitiesSerialized
    {
        get => _cities;
        private set => _cities = value ?? new();
    }

    public void AddCity(City city) => _cities.Add(city);

    public void RemoveCity(City city)
    {
        _cities.Remove(city);
        RebuildUniqueBuildingsModifiers();
    }

    private List<Road> _roads = new();

    /// <summary>
    /// Gets the list of roads in the civilization — lecture seule ; utiliser AddRoad / RemoveRoad pour muter.
    /// </summary>
    [JsonIgnore]
    public IReadOnlyList<Road> Roads => _roads;

    [JsonPropertyName("Roads")]
    [JsonInclude]
    public List<Road> RoadsSerialized
    {
        get => _roads;
        private set => _roads = value ?? new();
    }

    public void AddRoad(Road road) => _roads.Add(road);
    public void RemoveRoad(Road road) => _roads.Remove(road);
    public void RemoveAllRoads(Predicate<Road> match) => _roads.RemoveAll(match);

    private TechnologyTree _technologyTree = new();

    /// <summary>
    /// Gets or sets the technology tree of the civilization.
    /// Pour le joueur, assigné depuis PrestigeState.TechnologyTree après désérialisation.
    /// Pour les NPCs, toujours un arbre vide.
    /// </summary>
    [JsonIgnore]
    public TechnologyTree TechnologyTree
    {
        get => _technologyTree;
        set
        {
            ModifierAggregator.Replace(_technologyTree, value);
            _technologyTree = value;
        }
    }

    [JsonIgnore]
    public ModifierAggregator ModifierAggregator { get; } = new();

    [JsonIgnore]
    public UniqueBuildingsModifierProvider UniqueBuildingsModifierProvider { get; } = new();

    public Civilization()
    {
        ModifierAggregator.Register(_technologyTree);
        ModifierAggregator.Register(UniqueBuildingsModifierProvider);
    }

    /// <summary>
    /// Ajoute un provider supplémentaire à l'agrégateur (prestige, NPC bonuses…).
    /// Les providers par défaut (TechnologyTree, UniqueBuildingsModifierProvider) sont toujours présents.
    /// </summary>
    public void AddCustomAggregator(IModifierProvider provider)
        => ModifierAggregator.Register(provider);

    /// <summary>
    /// Reconstruit le cache des modifiers issus des bâtiments IUniqueBuilding de toutes les villes.
    /// À appeler après construction/amélioration d'un IUniqueBuilding, ou après la perte d'une ville.
    /// </summary>
    public void RebuildUniqueBuildingsModifiers()
    {
        var modifiers = _cities
            .SelectMany(c => c.Buildings)
            .OfType<IUniqueBuilding>()
            .SelectMany(b => b.GetUniqueBuildingModifiers());
        UniqueBuildingsModifierProvider.Rebuild(modifiers);
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

    public int GetHarvestProductionBonus(string buildingType) =>
        ModifierAggregator.ApplyModifiers(ECategory.HARVEST_PRODUCTION_BONUS, buildingType, 0);

    public int ForgeDoubleHarvestBonus =>
        ModifierAggregator.ApplyModifiers(ECategory.FORGE_DOUBLE_HARVEST_BONUS, "", 0);

    /// <summary>
    /// Chance (en %) qu'une mine produise de l'or en bonus (en plus du minerai) lors d'une récolte automatique.
    /// </summary>
    [JsonIgnore]
    public int MineGoldChancePercent => ModifierAggregator.ApplyModifiers(ECategory.MINE_GOLD_CHANCE_PERCENT, "", 0);

    [JsonIgnore]
    public int LaboratoryResearchBonus => ModifierAggregator.ApplyModifiers(ECategory.BUILDING_PRODUCTION, "Laboratory", 0);

    [JsonIgnore]
    public double CityDefenseRegenSpeed => ModifierAggregator.ApplyModifiers(ECategory.CITY_DEFENSE_REGEN_SPEED, "", 1.0);

    [JsonIgnore]
    public int CityMaxSoldiersBonus => ModifierAggregator.ApplyModifiers(ECategory.CITY_MAX_SOLDIERS_BONUS, "", 0);

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
        int warehouseLevelCount = Cities.Sum(city => Math.Max(0, city.Buildings.Sum(building => (building.Type == BuildingType.Warehouse) ? building.Level : 0)));

        bool isBasic = ResourceUtils.BasicResources.Contains(resource);
        bool isBasicStorage = isBasic || resource == Resource.Gold;
        int storageBonus = isBasicStorage
            ? ModifierAggregator.ApplyModifiers(ECategory.STORAGE_CAPACITY_BASIC, "", 0)
            : ModifierAggregator.ApplyModifiers(ECategory.STORAGE_CAPACITY_ADVANCED, "", 0);

        int result = isBasicStorage
            ? 5 * baseCityResourceMax + 20 * cityWithWarehouseCount + 10 * warehouseLevelCount
            : 5 * advancedCityResourceMax + 5 * cityWithWarehouseCount + 5 * warehouseLevelCount;

        return result + storageBonus;
    }

    public void TrimResourcesToMax()
    {
        foreach (var resource in _resources.Keys.ToList())
        {
            var max = GetResourceMaxQuantity(resource);
            if (_resources[resource] > max)
                _resources[resource] = max;
        }
    }

    private List<BuildingType> _uniqueBuildings = new();

    /// <summary>
    /// Bâtiments uniques construits par cette civilisation sur l'île courante.
    /// Empêche de construire deux fois le même bâtiment unique.
    /// </summary>
    [JsonIgnore]
    public IReadOnlyList<BuildingType> UniqueBuildings => _uniqueBuildings;

    [JsonPropertyName("UniqueBuildings")]
    [JsonInclude]
    public List<BuildingType> UniqueBuildingsSerialized
    {
        get => _uniqueBuildings;
        private set => _uniqueBuildings = value ?? new();
    }

    public void AddUniqueBuilding(BuildingType type) => _uniqueBuildings.Add(type);

    private List<Resource> _seaportEnhancedResources = new();

    /// <summary>Resources for which a level-3 Seaport has unlocked a 4:1 trade rate (permanent).</summary>
    [JsonIgnore]
    public IReadOnlyList<Resource> SeaportEnhancedResources => _seaportEnhancedResources;

    [JsonPropertyName("SeaportEnhancedResources")]
    [JsonInclude]
    public List<Resource> SeaportEnhancedResourcesSerialized
    {
        get => _seaportEnhancedResources;
        private set => _seaportEnhancedResources = value ?? new();
    }

    public void AddSeaportEnhancedResource(Resource resource) => _seaportEnhancedResources.Add(resource);

    private List<Resource> _seaportAutoTradeResources = new();

    /// <summary>Resources for which a level-4 Seaport has activated permanent auto-trade (one slot per level-4 Seaport).</summary>
    [JsonIgnore]
    public IReadOnlyList<Resource> SeaportAutoTradeResources => _seaportAutoTradeResources;

    [JsonPropertyName("SeaportAutoTradeResources")]
    [JsonInclude]
    public List<Resource> SeaportAutoTradeResourcesSerialized
    {
        get => _seaportAutoTradeResources;
        private set => _seaportAutoTradeResources = value ?? new();
    }

    public void AddSeaportAutoTradeResource(Resource resource) => _seaportAutoTradeResources.Add(resource);

    public event EventHandler<Resource>? LowStock;

    internal void RaiseLowStock(Resource resource) => LowStock?.Invoke(this, resource);

    public bool CanPayResourceCost(ResourceSet cost)
    {
        foreach (var kvp in cost)
        {
            if (GetResourceQuantity(kvp.Key) < kvp.Value)
                return false;
        }
        return true;
    }
    public void PayResourceCost(ResourceSet cost)
    {
        foreach (var kvp in cost)
        {
            if (kvp.Value > 0)
                RemoveResource(kvp.Key, kvp.Value);
        }
    }
}