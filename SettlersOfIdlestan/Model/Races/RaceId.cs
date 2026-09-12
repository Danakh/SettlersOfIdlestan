using System.Text.Json.Serialization;

namespace SettlersOfIdlestan.Model.Races;

/// <summary>
/// Identifiant d'une race jouable, choisie au moment de l'Ascension (voir
/// AscensionController.PerformAscension). Persisté par nom dans les sauvegardes
/// (AscensionState.SelectedRace / AscendedRaces) — ne jamais renommer une valeur existante.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<RaceId>))]
public enum RaceId
{
    Human,
    Elf,
    Dwarf,
    Goblin,
    Orc,
    // Races avancées (RaceTier.Advanced) : sélectionnables une fois leur propre combinaison de 3
    // pouvoirs divins de second rang acquise (voir RaceDefinitions / AscensionController.GetSelectableRaces).
    Mermaid,
    DarkElf,
    Giant,
    Garuda,
}

/// <summary>
/// Palier de déblocage d'une race : dans les deux cas, sa propre combinaison de 3 pouvoirs divins
/// (RaceDefinition.RequiredPowers, Humains exceptés — toujours sélectionnables ; voir
/// AscensionController.IsRaceUnlocked). Base pioche dans les 6 pouvoirs de premier rang, Advanced
/// strictement dans les 6 pouvoirs de second rang (jamais de troisième rang), répartis en graphe
/// complet à 4 sommets entre les 4 races avancées : chaque pouvoir sert à exactement 2 races.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<RaceTier>))]
public enum RaceTier
{
    Base,
    Advanced,
}
