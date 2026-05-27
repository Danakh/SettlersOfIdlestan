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
}
