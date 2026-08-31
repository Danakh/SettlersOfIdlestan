using System.Collections.Generic;
using System.Text.Json.Serialization;
using SettlersOfIdlestan.Model.GameplayModifier;

namespace SettlersOfIdlestan.Model.Civilization;

[JsonConverter(typeof(JsonStringEnumConverter<NpcEvolutionLevel>))]
public enum NpcEvolutionLevel
{
    Minimum,
    Low,
    Medium,
    Strong
}

[JsonConverter(typeof(JsonStringEnumConverter<NpcAggressivityLevel>))]
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
    /// Modificateurs persistants spécifiques à ce NPC (ex: civilisations agressives underworld).
    /// Quand non-null, remplace les modificateurs NPC standard lors du SetupModifierAggregator.
    /// </summary>
    public List<Modifier>? ExtraModifiers { get; set; }

    /// <summary>
    /// Tier de l'île au moment où ce PNJ a été placé, retenu pour que le chargement d'une sauvegarde
    /// reconstruise exactement le jeu de modificateurs de la génération
    /// (<see cref="NpcModifierSetMaker.CreateForNpc"/>).
    ///
    /// <para>Le relire depuis <c>PrestigeState.Tier</c> au chargement serait faux : le joueur peut
    /// choisir le tier de sa prochaine île (<c>PrestigeState.EffectiveNextIslandTier</c>), qui n'est
    /// donc pas toujours son tier courant. Null = sauvegarde antérieure à ce champ, on retombe alors
    /// sur le tier courant.</para>
    /// </summary>
    public int? ModifierTier { get; set; }

    /// <summary>
    /// Nombre de villes cible pour ce NPC. Quand non-null, remplace la valeur par défaut
    /// dérivée de EvolutionLevel (1/3/5/7).
    /// </summary>
    public int? CityCount { get; set; }

    /// <summary>
    /// Distance minimale en edges entre toute ville de ce NPC et la ville initiale du joueur.
    /// Null = utilise la valeur par défaut du placer (10).
    /// </summary>
    public int? MinDistanceFromPlayer { get; set; }
}
