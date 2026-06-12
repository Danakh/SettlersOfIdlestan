using System.Collections.Generic;
using SettlersOfIdlestan.Model.GameplayModifier;

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

    /// <summary>
    /// Modificateurs persistants spécifiques à ce NPC (ex: civilisations agressives underworld).
    /// Quand non-null, remplace les modificateurs NPC standard lors du SetupModifierAggregator.
    /// </summary>
    public List<Modifier>? ExtraModifiers { get; set; }

    /// <summary>
    /// Nombre de villes cible pour ce NPC. Quand non-null, remplace la valeur par défaut
    /// dérivée de EvolutionLevel (1/3/5/7).
    /// </summary>
    public int? CityCount { get; set; }

    /// <summary>
    /// Distance minimale en edges entre toute ville de ce NPC et la ville initiale du joueur.
    /// Null = utilise la valeur par défaut du placer (8).
    /// </summary>
    public int? MinDistanceFromPlayer { get; set; }
}
