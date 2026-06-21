using System;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Prestige.PrestigeMap;

namespace SettlersOfIdlestan.Model.Prestige;

[Serializable]
public class PrestigeState
{
    public WorldState? WorldState { get; set; }

    public int PrestigePoints { get; set; }

    public int TotalPrestigePointsEarned { get; set; }

    public List<Vertex> PurchasedVertices { get; set; } = new();

    public List<PrestigeRunStats> RunHistory { get; set; } = new();

    public TechnologyTree TechnologyTree { get; set; } = new();

    /// <summary>Niveau de corruption de l'Inframonde. Augmente la sévérité et la chance des zones corrompues. Démarre à 1.</summary>
    public int CurrentCorruptionLevel { get; set; } = 1;

    /// <summary>Niveau de corruption qui déborde en surface une fois <see cref="CurrentCorruptionLevel"/> au-delà de 3.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public int SurfaceCorruptionLevel => Math.Max(0, CurrentCorruptionLevel - 3);

    public PrestigeState() { }

    public PrestigeState(WorldState worldState)
    {
        WorldState = worldState;
    }

    public bool IsResourceDiscovered(Resource resource, PrestigeMap.PrestigeMap map)
    {
        var resourceName = resource.ToString();
        return PurchasedVertices.Any(v =>
            map.GetVertex(v)?.Modifiers.Any(m =>
                m.Category == Modifier.ECategory.UNLOCK_RESOURCE && m.SubCategory == resourceName) == true);
    }
}
