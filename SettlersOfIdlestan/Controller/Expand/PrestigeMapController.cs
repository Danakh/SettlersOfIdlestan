using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Prestige;
using SettlersOfIdlestan.Model.Prestige.PrestigeMap;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;
using System;

namespace SettlersOfIdlestan.Controller.Expand;

public class VertexPurchasedEventArgs : EventArgs
{
    public Vertex Vertex { get; }
    public int Cost { get; }

    public VertexPurchasedEventArgs(Vertex vertex, int cost)
    {
        Vertex = vertex;
        Cost = cost;
    }
}

public class PrestigeMapController
{
    public static readonly PrestigeMap DefaultMap = PrestigeMapFactory.CreateDefault();

    public event EventHandler<VertexPurchasedEventArgs>? OnVertexPurchased;

    public bool CanPurchaseVertex(PrestigeState prestigeState, Vertex vertexCoord, bool demoMode = false)
    {
        var vertex = DefaultMap.GetVertex(vertexCoord);
        if (vertex == null) return false;
        if (prestigeState.PurchasedVertices.Contains(vertexCoord)) return false;
        if (demoMode && vertex.Cost > 100) return false;

        // Central vertex is always reachable; all others require a purchased neighbor.
        if (!vertexCoord.Equals(PrestigeMap.CentralVertex))
        {
            var neighbors = DefaultMap.GetNeighbors(vertexCoord);
            if (!neighbors.Any(n => prestigeState.PurchasedVertices.Contains(n.Coord)))
                return false;
        }

        return prestigeState.PrestigePoints >= vertex.Cost;
    }

    public bool PurchaseVertex(PrestigeState prestigeState, Vertex vertexCoord, bool demoMode = false)
    {
        if (!CanPurchaseVertex(prestigeState, vertexCoord, demoMode)) return false;

        var vertex = DefaultMap.GetVertex(vertexCoord)!;
        prestigeState.PrestigePoints -= vertex.Cost;
        prestigeState.PurchasedVertices.Add(vertexCoord);
        DefaultMap.RaiseVertexPurchased(vertexCoord);
        OnVertexPurchased?.Invoke(this, new VertexPurchasedEventArgs(vertexCoord, vertex.Cost));
        return true;
    }

    /// <summary>
    /// Applies one-time prestige bonuses (starting resources and buildings) at the start of a new run.
    /// Modifier bonuses are handled dynamically by <see cref="PrestigeModifierProvider"/>.
    /// Must be called after the island is fully generated, civilizations initialized, and
    /// ModifierAggregators set up (so the aggregator already contains the PrestigeModifierProvider).
    /// </summary>
    public void ApplyPrestigeToNewGame(WorldState WorldState, PrestigeState? prestigeState)
    {
        if (prestigeState == null || WorldState.Civilizations.Count == 0) return;

        var civ = WorldState.PlayerCivilization;

        ApplyStartingResearch(civ);

        var purchased = prestigeState.PurchasedVertices;
        var startingCity = civ.Cities.FirstOrDefault();

        if (purchased.Count > 0)
        {
            // Starting resource bonuses scaled by adjacent purchased vertices
            foreach (var hex in DefaultMap.Hexes)
            {
                if (hex.StartingResourceBonusPerVertex <= 0) continue;
                int adjacentPurchased = hex.AdjacentVertices.Count(v => purchased.Contains(v));
                if (adjacentPurchased == 0) continue;
                int bonus = hex.StartingResourceBonusPerVertex * adjacentPurchased;
                foreach (var resource in ResourceUtils.BasicResources)
                    civ.AddResource(resource, bonus);
            }

            if (startingCity != null)
            {
                // STARTING_CITY_BUILDING: initial city only
                foreach (var bt in civ.ModifierAggregator.GetGrantedBuildingTypes(ECategory.STARTING_CITY_BUILDING))
                    GrantBuildingToCity(WorldState, startingCity, bt);

                // NEW_CITY_BUILDING: every outpost — apply to initial city here, BuildCity handles the rest
                foreach (var city in civ.Cities)
                    foreach (var bt in civ.ModifierAggregator.GetGrantedBuildingTypes(ECategory.NEW_CITY_BUILDING))
                        GrantBuildingToCity(WorldState, city, bt);
            }
        }

        // Construction Divine : la ville de départ ne passe pas par CityBuilderController (elle naît
        // avec l'île, voir IslandMapGenerator.PopulatePlayerCivilization), son Marché gratuit est donc
        // accordé ici — après les bâtiments de vertex, pour s'empiler sur celui de Port & Marché quand
        // les deux sont actifs (Marché niveau 2). Voir CityBuilderController.GrantDivineConstructionMarket.
        if (startingCity != null && civ.ModifierAggregator.HasModifier(ECategory.NEW_CITY_DIVINE_CONSTRUCTION))
        {
            var startingMap = WorldState.GetMapFor(startingCity.Position);
            if (startingMap != null)
                CityBuilderController.GrantDivineConstructionMarket(startingCity, startingMap);
        }

        // InitializeControllersForCurrentIsland() computes the initial visibility cache before this
        // method grants buildings (e.g. a free Watchtower) to the starting city — refresh it here so
        // the extended vision radius takes effect immediately instead of staying stuck at radius 1.
        WorldState.Visibility.RecalculateFor(civ.Index);
    }

    /// <summary>
    /// Complète d'office les recherches offertes par un modifier STARTING_RESEARCH (kit de départ
    /// racial — voir RaceDefinitions). Les prérequis ne gardent que le <em>lancement</em> d'une
    /// recherche, pas sa complétion : une recherche peut donc être offerte sans l'être de tout son
    /// arbre amont, ce qui laisse volontairement les maillons manquants comme objectifs de début de
    /// partie. Ré-appliqué à chaque début d'île, sans effet si la recherche est déjà acquise.
    /// </summary>
    private static void ApplyStartingResearch(Civilization civ)
    {
        var tree = civ.TechnologyTree;
        bool granted = false;

        foreach (var name in civ.ModifierAggregator.GetActiveSubCategories(ECategory.STARTING_RESEARCH))
        {
            if (!Enum.TryParse<TechnologyId>(name, out var techId)) continue;
            if (tree.IsCompleted(techId)) continue;
            tree.CompleteResearch(techId);
            granted = true;
        }

        if (granted)
            tree.NotifyModifiersChanged();
    }

    /// <summary>
    /// Accorde à <paramref name="city"/> tous les bâtiments offerts par les modifiers NEW_CITY_BUILDING
    /// actifs pour <paramref name="civ"/> (recherches/prestige débloquant p. ex. une Tour de Guet
    /// gratuite pour tout nouvel avant-poste). À appeler pour tout avant-poste créé hors du chemin
    /// normal <see cref="Island.CityBuilderController.BuildCity"/> — celui-ci applique déjà ces
    /// modifiers lui-même (voir CreateCityAt) — typiquement le premier avant-poste d'une couche
    /// auto-étendue posé directement via <see cref="Model.IslandMap.LayerState.EstablishOupostInNewAutoExpandLayer"/>
    /// (Inframonde, Abysse), qui contourne CityBuilderController.
    /// </summary>
    public static void GrantNewCityBuildings(WorldState worldState, City city, Civilization civ)
    {
        foreach (var bt in civ.ModifierAggregator.GetGrantedBuildingTypes(ECategory.NEW_CITY_BUILDING))
            GrantBuildingToCity(worldState, city, bt);
    }

    private static void GrantBuildingToCity(WorldState worldState, City city, BuildingType bt)
    {
        if (!city.Buildings.Any(b => b.Type == bt))
        {
            var building = BuildingFactory.Create(bt);
            if (building == null) return;
            var map = worldState.GetMapFor(city.Position);
            if (map == null || !building.IsAvailableInLayer(map.Z)) return;
            building.Level = 1;
            city.AddBuilding(building);
            if (bt == BuildingType.TownHall) city.InvalidateLevelCache();
            city.InvalidateMaxSoldiersCache();
        }
    }
}
