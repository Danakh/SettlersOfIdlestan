using System.Collections.Generic;

namespace SettlersOfIdlestan.Model.Civilization;

public enum NpcEvolutionLevel
{
    Minimum,
    Low,
    Medium,
    Strong
}

public enum NpcAggressivityLevel
{
    Pacifist,
    Cautious,
    Expansionist,
    Warlike
}

public class NpcParameters
{
    public NpcEvolutionLevel EvolutionLevel { get; set; } = NpcEvolutionLevel.Minimum;
    public NpcAggressivityLevel AggressivityLevel { get; set; } = NpcAggressivityLevel.Cautious;

    /// <summary>
    /// Indices des civilisations qui ont attaqué ce NPC. Quand non-vide, les attaques
    /// sont limitées à ces civilisations (agressivité ciblée plutôt que globale).
    /// </summary>
    public List<int> WarEnemyCivIndices { get; set; } = new();
}
