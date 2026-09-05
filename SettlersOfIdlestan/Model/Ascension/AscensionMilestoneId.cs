using System.Text.Json.Serialization;

namespace SettlersOfIdlestan.Model.Ascension;

/// <summary>
/// Jalons d'Ascension : pouvoirs divins accordés automatiquement et gratuitement (aucun point divin
/// dépensé) en fonction du nombre de races différentes ayant déjà accompli une Ascension
/// (AscensionState.AscendedRaces) — voir AscensionMilestoneDefinitions et
/// AscensionController.IsMilestoneUnlocked. Jamais persisté : l'état débloqué se déduit entièrement
/// d'AscensionState à chaque lecture.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<AscensionMilestoneId>))]
public enum AscensionMilestoneId
{
    PermanentUniqueBuildings,
    PrestigiousAscension,
    ResearchProduction,
    FreeRelocation
}
