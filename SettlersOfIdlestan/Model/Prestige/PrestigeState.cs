using System;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestan.Model.Prestige;

[Serializable]
public class PrestigeState
{
    public IslandState? IslandState { get; set; }

    public int PrestigePoints { get; set; }

    public List<Vertex> PurchasedVertices { get; set; } = new();

    public List<PrestigeRunStats> RunHistory { get; set; } = new();

    public PrestigeState() { }

    public PrestigeState(IslandState islandState)
    {
        IslandState = islandState;
    }
}
