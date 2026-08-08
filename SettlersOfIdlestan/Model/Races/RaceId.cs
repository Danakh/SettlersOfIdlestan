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
    // Races avancées (RaceTier.Advanced) : sélectionnables une fois la seconde rangée de pouvoirs
    // divins complète (voir RaceDefinitions / AscensionController.GetSelectableRaces).
    Mermaid,
    DarkElf,
    Giant,
    Garuda,
}

/// <summary>
/// Palier de déblocage d'une race : Base = première rangée de pouvoirs divins complète,
/// Advanced = seconde rangée complète (voir AscensionController.IsRaceSelectionUnlocked /
/// AreAdvancedRacesUnlocked).
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<RaceTier>))]
public enum RaceTier
{
    Base,
    Advanced,
}
