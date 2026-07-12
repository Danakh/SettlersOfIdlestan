using SettlersOfIdlestan.Controller.Island;
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

    public void AddCity(City city)
    {
        _cities.Add(city);
        BuildingController.RecalculateStorageCapacity(this);
    }

    public void RemoveCity(City city)
    {
        _cities.Remove(city);
        RebuildUniqueBuildingsModifiers();
        RebuildUniqueBuildingCache();
        BuildingController.RecalculateStorageCapacity(this);
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

    private List<MaritimeBeacon> _maritimeBeacons = new();

    /// <summary>
    /// Liste des balises maritimes de la civilisation — lecture seule ; utiliser AddMaritimeBeacon /
    /// RemoveMaritimeBeacon pour muter.
    /// </summary>
    [JsonIgnore]
    public IReadOnlyList<MaritimeBeacon> MaritimeBeacons => _maritimeBeacons;

    [JsonPropertyName("MaritimeBeacons")]
    [JsonInclude]
    public List<MaritimeBeacon> MaritimeBeaconsSerialized
    {
        get => _maritimeBeacons;
        private set => _maritimeBeacons = value ?? new();
    }

    public void AddMaritimeBeacon(MaritimeBeacon beacon) => _maritimeBeacons.Add(beacon);
    public void RemoveMaritimeBeacon(MaritimeBeacon beacon) => _maritimeBeacons.Remove(beacon);

    private List<WarFleet> _fleets = new();

    /// <summary>
    /// Liste des Flottes de Guerre de la civilisation — lecture seule ; utiliser AddFleet / RemoveFleet
    /// pour muter.
    /// </summary>
    [JsonIgnore]
    public IReadOnlyList<WarFleet> Fleets => _fleets;

    [JsonPropertyName("Fleets")]
    [JsonInclude]
    public List<WarFleet> FleetsSerialized
    {
        get => _fleets;
        private set => _fleets = value ?? new();
    }

    public void AddFleet(WarFleet fleet) => _fleets.Add(fleet);
    public void RemoveFleet(WarFleet fleet) => _fleets.Remove(fleet);

    /// <summary>
    /// Tous les emplacements militaires de la civilisation (villes et flottes) — voir IMilitaryVertex.
    /// Utilisé par le système militaire pour traiter les deux types de façon uniforme.
    /// </summary>
    [JsonIgnore]
    public IEnumerable<IMilitaryVertex> MilitaryVertices => Cities.Concat<IMilitaryVertex>(Fleets);

    /// <summary>
    /// Tous les emplacements construits par la civilisation (villes, flottes, balises) — voir
    /// IBuildVertex. Utilisé pour vérifier de façon uniforme l'occupation d'un vertex.
    /// </summary>
    [JsonIgnore]
    public IEnumerable<IBuildVertex> BuildVertices =>
        Cities.Concat<IBuildVertex>(Fleets).Concat<IBuildVertex>(MaritimeBeacons);

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
        ModifierAggregator.Changed += () =>
        {
            BuildingController.RecalculateStorageCapacity(this);
            _maxLevelCache.Clear();
        };
    }

    private readonly Dictionary<BuildingType, int> _maxLevelCache = new();

    /// <summary>
    /// Cache le niveau max par type de bâtiment (BuildingController.GetMaxLevel est sur le chemin chaud
    /// de l'autoplay/des tests). Invalidé automatiquement via ModifierAggregator.Changed dès qu'un
    /// provider de modificateurs change (recherche, prestige, bâtiments uniques…).
    /// </summary>
    public int GetCachedMaxLevel(BuildingType type, Func<int> compute)
    {
        if (_maxLevelCache.TryGetValue(type, out int cached))
            return cached;
        int value = compute();
        _maxLevelCache[type] = value;
        return value;
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

    private readonly Dictionary<BuildingType, Building> _uniqueBuildingCache = new();

    /// <summary>
    /// Retourne l'instance du bâtiment unique de ce type construit dans une ville de la civilisation,
    /// ou null s'il n'existe pas. Sert à éviter de parcourir toutes les villes/bâtiments à chaque appel
    /// (ex: automatisations des guildes). Le cache n'est mis à jour qu'à la construction d'un bâtiment
    /// unique ou à la destruction d'une ville — voir <see cref="RegisterUniqueBuildingInCache"/> et
    /// <see cref="RebuildUniqueBuildingCache"/>.
    /// </summary>
    public Building? GetUniqueBuilding(BuildingType type)
        => _uniqueBuildingCache.TryGetValue(type, out var building) ? building : null;

    /// <summary>
    /// Enregistre un bâtiment unique nouvellement construit dans le cache, sans reparcourir les villes.
    /// </summary>
    public void RegisterUniqueBuildingInCache(Building building)
    {
        if (building.IsUnique)
            _uniqueBuildingCache[building.Type] = building;
    }

    /// <summary>
    /// Reconstruit entièrement le cache des bâtiments uniques à partir des villes actuelles.
    /// À appeler après la perte d'une ville (destruction) ou après chargement d'une sauvegarde.
    /// </summary>
    public void RebuildUniqueBuildingCache()
    {
        _uniqueBuildingCache.Clear();
        foreach (var building in _cities.SelectMany(c => c.Buildings))
            if (building.IsUnique)
                _uniqueBuildingCache[building.Type] = building;
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
    /// Wonder level-up cost reduction fraction (0.0 = no reduction, 0.1 = 10% cheaper). Applies only to the Wonder.
    /// </summary>
    [JsonIgnore]
    public double WonderCostReduction => ModifierAggregator.ApplyModifiers(ECategory.WONDER_COST_REDUCTION, "", 0.0);

    /// <summary>
    /// Investment speed multiplier (base 1.0) applied to a resource's investment amount when its stock
    /// exceeds 50% of its max capacity. Affects the Wonder, the Deepest Mine and the Corruption Spire.
    /// </summary>
    [JsonIgnore]
    public double InvestmentSpeedHighStockBonus => ModifierAggregator.ApplyModifiers(ECategory.INVESTMENT_SPEED_HIGH_STOCK_BONUS, "", 1.0);

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

    /// <summary>
    /// Cache de la capacité de stockage (ressources de base / avancées), recalculé par
    /// <see cref="BuildingController.RecalculateStorageCapacity"/> à chaque construction/destruction
    /// de bâtiment, changement de l'agrégateur de modificateurs, ou ajout/retrait de ville.
    /// </summary>
    [JsonIgnore]
    public int StorageCapacityBasic { get; private set; }

    [JsonIgnore]
    public int StorageCapacityAdvanced { get; private set; }

    /// <summary>
    /// Appelé uniquement par BuildingController après recalcul complet de la capacité de stockage.
    /// </summary>
    public void SetStorageCapacityCache(int basic, int advanced)
    {
        StorageCapacityBasic = basic;
        StorageCapacityAdvanced = advanced;
    }

    public int GetResourceMaxQuantity(Resource resource)
    {
        if (ResourceUtils.ConsumableResources.Contains(resource))
        {
            int totalArsenalLevel = Cities.Sum(city =>
                city.Buildings.OfType<Arsenal>().Sum(a => a.Level));
            return 5 * Cities.Count + 5 * totalArsenalLevel;
        }

        bool isBasic = ResourceUtils.BasicResources.Contains(resource);
        bool isBasicStorage = isBasic || resource == Resource.Gold;
        return isBasicStorage ? StorageCapacityBasic : StorageCapacityAdvanced;
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