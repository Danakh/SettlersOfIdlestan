using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Prestige;
using SettlersOfIdlestan.Model.Prestige.PrestigeMap;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SettlersOfIdlestan.Controller.Expand;

public class PrestigeMapController
{
    public static readonly PrestigeMap DefaultMap = PrestigeMap.CreateDefault();

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
        return true;
    }

    /// <summary>
    /// Applies one-time prestige bonuses (starting resources and buildings) at the start of a new run.
    /// Modifier bonuses are handled dynamically by <see cref="PrestigeModifierProvider"/>.
    /// Must be called after the island is fully generated, civilizations initialized, and
    /// ModifierAggregators set up (so the aggregator already contains the PrestigeModifierProvider).
    /// </summary>
    public void ApplyPrestigeToNewGame(IslandState islandState, PrestigeState? prestigeState)
    {
        if (prestigeState == null || islandState.Civilizations.Count == 0) return;

        var purchased = prestigeState.PurchasedVertices;
        if (purchased.Count == 0) return;

        var civ = islandState.PlayerCivilization;

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
