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
    /// <summary>
    /// Le jeu de modificateurs complet d'une civilisation PNJ d'île : bonus de tier, routes maritimes,
    /// puis les deux malus de récolte. <b>Unique source de vérité</b> — la génération l'installe, et le
    /// chargement d'une sauvegarde le reconstruit à l'identique depuis
    /// <c>NpcParameters.ModifierTier</c> et <c>AggressivityLevel</c>, tous deux persistés.
    ///
    /// <para>Les deux étaient auparavant construits à deux endroits, avec deux formules différentes
    /// (<c>tier + 1</c> à la génération, <c>tier</c> au chargement) et enregistrés côte à côte plutôt
    /// qu'à la place l'un de l'autre. Un PNJ Pacifiste de tier 1 sortait de la génération à ×0,50 de
    /// vitesse de récolte au lieu des ×0,30 voulus, et repassait à ×1,20 après un simple
    /// rechargement — les malus n'étant, eux, jamais reconstruits. Voir
    /// <c>NpcModifierSetSingleRegistrationTests</c>.</para>
    ///
    /// <para>L'ordre compte : <see cref="ModifierAggregator"/> applique les modificateurs dans l'ordre
    /// où ils arrivent, donc les malus multiplicatifs doivent venir en dernier pour réduire le total
    /// accumulé, et non la seule base.</para>
    /// </summary>
    public static IModifierProvider CreateForNpc(int tier, NpcAggressivityLevel aggressivity)
    {
        var modifiers = CreateModifiers(maxTechTier: tier + 1, maxPrestigeDistance: tier);

        modifiers.Add(new Modifier(Modifier.ECategory.UNLOCK_MARITIME_ROUTES, Modifier.EType.ADDITIVE, 1));

        double aggressivityFactor = aggressivity switch
        {
            NpcAggressivityLevel.Pacifist => 0.5,
            NpcAggressivityLevel.Cautious => 0.75,
            _                             => 1.0,
        };
        if (aggressivityFactor < 1.0)
            modifiers.Add(new Modifier(Modifier.ECategory.HARVEST_SPEED, Modifier.EType.MULTIPLICATIVE, aggressivityFactor));

        // Tier 1 (moins de 2500 prestige total) -50%, Tier 2 (moins de 25000) -25%, au-delà rien.
        double tierFactor = tier switch
        {
            1 => 0.5,
            2 => 0.75,
            _ => 1.0,
        };
        if (tierFactor < 1.0)
            modifiers.Add(new Modifier(Modifier.ECategory.HARVEST_SPEED, Modifier.EType.MULTIPLICATIVE, tierFactor));

        return new StaticModifierProvider(modifiers);
    }

    /// <param name="maxTechTier">Include all technologies with Tier &lt;= this value.</param>
    /// <param name="maxPrestigeDistance">Include prestige vertices at EdgeDistance &lt;= this value from the centre.</param>
    public static IModifierProvider Create(int maxTechTier, int maxPrestigeDistance)
        => new StaticModifierProvider(CreateModifiers(maxTechTier, maxPrestigeDistance));

    private static List<Modifier> CreateModifiers(int maxTechTier, int maxPrestigeDistance)
    {
        var modifiers = new List<Modifier>();

        foreach (var tech in TechnologyDefinitions.All.Where(t => t.Tier < maxTechTier))
            modifiers.AddRange(tech.Modifiers);

        var map = PrestigeMapFactory.CreateDefault();

        var includedCoords = map.Vertices
            .Where(v => v.Coord.EdgeDistanceTo(PrestigeMap.CentralVertex) < maxPrestigeDistance)
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

        return modifiers;
    }
}
