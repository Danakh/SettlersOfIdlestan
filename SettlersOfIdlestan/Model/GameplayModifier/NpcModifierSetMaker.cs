using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Prestige.PrestigeMap;

namespace SettlersOfIdlestan.Model.GameplayModifier;

/// <summary>
/// Builds a static IModifierProvider that aggregates the cumulative bonuses of:
/// - all technologies with Tier &lt;= maxTechTier,
/// - all prestige vertices within maxPrestigeDistance edge-steps from the central vertex,
///   plus the hex-area bonuses scaled by the number of included adjacent vertices.
/// </summary>
public static class NpcModifierSetMaker
{
    /// <param name="maxTechTier">Include all technologies with Tier &lt;= this value.</param>
    /// <param name="maxPrestigeDistance">Include prestige vertices at EdgeDistance &lt;= this value from the centre.</param>
    public static IModifierProvider Create(int maxTechTier, int maxPrestigeDistance)
    {
        var modifiers = new List<Modifier>();

        foreach (var tech in TechnologyDefinitions.All.Where(t => t.Tier <= maxTechTier))
            modifiers.AddRange(tech.Modifiers);

        var map = PrestigeMap.CreateDefault();

        var includedCoords = map.Vertices
            .Where(v => v.Coord.EdgeDistanceTo(PrestigeMap.CentralVertex) <= maxPrestigeDistance)
            .Select(v => v.Coord)
            .ToHashSet();

        foreach (var vertex in map.Vertices.Where(v => includedCoords.Contains(v.Coord)))
            modifiers.AddRange(vertex.Modifiers);

        foreach (var hex in map.Hexes)
        {
            int adjacentCount = hex.AdjacentVertices.Count(v => includedCoords.Contains(v));
            if (adjacentCount == 0) continue;
            foreach (var template in hex.PerVertexModifiers)
                modifiers.Add(new Modifier(template.Category, template.SubCategory, template.Type, template.Value * adjacentCount));
        }

        return new StaticModifierProvider(modifiers);
    }
}
