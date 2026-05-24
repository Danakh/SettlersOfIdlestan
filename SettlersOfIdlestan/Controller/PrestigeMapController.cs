using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Prestige.PrestigeMap;
using SettlersOfIdlestan.Model.Prestige;

namespace SettlersOfIdlestan.Controller;

public class PrestigeMapController
{
    public static readonly PrestigeMap DefaultMap = PrestigeMap.CreateDefault();

    public bool CanPurchaseVertex(PrestigeState prestigeState, PrestigeVertexId vertexId)
    {
        var vertex = DefaultMap.GetVertex(vertexId);
        if (vertex == null) return false;

        if (prestigeState.PurchasedVertices.Contains(vertexId)) return false;

        foreach (var prereq in vertex.Prerequisites)
            if (!prestigeState.PurchasedVertices.Contains(prereq)) return false;

        return prestigeState.PrestigePoints >= vertex.Cost;
    }

    public bool PurchaseVertex(PrestigeState prestigeState, PrestigeVertexId vertexId)
    {
        if (!CanPurchaseVertex(prestigeState, vertexId)) return false;

        var vertex = DefaultMap.GetVertex(vertexId)!;
        prestigeState.PrestigePoints -= vertex.Cost;
        prestigeState.PurchasedVertices.Add(vertexId);
        return true;
    }

    /// <summary>
    /// Applies one-time prestige bonuses (starting resources and buildings) at the start of a new run.
    /// Modifier bonuses are handled dynamically by <see cref="PrestigeModifierProvider"/>.
    /// Must be called after the island is fully generated and civilizations are initialized.
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
            foreach (var resource in new[] { Resource.Food, Resource.Wood, Resource.Brick, Resource.Stone })
                civ.AddResource(resource, bonus);
        }

        // Starting buildings granted by purchased vertices
        var startingCity = civ.Cities.FirstOrDefault();
        if (startingCity == null) return;

        foreach (var vertexId in purchased)
        {
            var vertex = DefaultMap.GetVertex(vertexId);
            if (vertex == null) continue;
            foreach (var buildingType in vertex.StartingBuildings)
            {
                if (!startingCity.Buildings.Any(b => b.Type == buildingType))
                {
                    var building = CreateBuildingInstance(buildingType);
                    if (building != null)
                    {
                        building.Level = 1;
                        startingCity.Buildings.Add(building);
                    }
                }
            }
        }
    }

    private static Building? CreateBuildingInstance(BuildingType type) => type switch
    {
        BuildingType.Seaport => new Seaport(),
        BuildingType.Market => new Market(),
        BuildingType.Laboratory => new Laboratory(),
        BuildingType.Barracks => new Barracks(),
        _ => null
    };
}
