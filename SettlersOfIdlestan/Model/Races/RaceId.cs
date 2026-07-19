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
    // Races avancées (RaceTier.Advanced) : déclarées pour l'UI et la sérialisation,
    // pas encore sélectionnables (voir RaceDefinitions / AscensionController.GetSelectableRaces).
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
public enum RaceTier
{
    Base,
    Advanced,
}
