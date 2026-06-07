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

    public bool CanPurchaseVertex(PrestigeState prestigeState, Vertex vertexCoord)
    {
        var vertex = DefaultMap.GetVertex(vertexCoord);
        if (vertex == null) return false;
        if (prestigeState.PurchasedVertices.Contains(vertexCoord)) return false;

        // Central vertex is always reachable; all others require a purchased neighbor.
        if (!vertexCoord.Equals(PrestigeMap.CentralVertex))
        {
            var neighbors = DefaultMap.GetNeighbors(vertexCoord);
            if (!neighbors.Any(n => prestigeState.PurchasedVertices.Contains(n.Coord)))
                return false;
        }

        return prestigeState.PrestigePoints >= vertex.Cost;
    }

    public bool PurchaseVertex(PrestigeState prestigeState, Vertex vertexCoord)
    {
        if (!CanPurchaseVertex(prestigeState, vertexCoord)) return false;

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

        var purchased = prestigeState.PurchasedVertices;
        if (purchased.Count == 0) return;

        var civ = WorldState.PlayerCivilization;

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

        var startingCity = civ.Cities.FirstOrDefault();
        if (startingCity == null) return;

        // STARTING_CITY_BUILDING: initial city only
        foreach (var bt in civ.ModifierAggregator.GetGrantedBuildingTypes(ECategory.STARTING_CITY_BUILDING))
            GrantBuildingToCity(startingCity, bt);

        // NEW_CITY_BUILDING: every outpost — apply to initial city here, BuildCity handles the rest
        foreach (var city in civ.Cities)
            foreach (var bt in civ.ModifierAggregator.GetGrantedBuildingTypes(ECategory.NEW_CITY_BUILDING))
                GrantBuildingToCity(city, bt);
    }

    private static void GrantBuildingToCity(City city, BuildingType bt)
    {
        if (!city.Buildings.Any(b => b.Type == bt))
        {
            var building = BuildingController.CreateBuilding(bt);
            if (building != null) { building.Level = 1; city.Buildings.Add(building); }
        }
    }
}
