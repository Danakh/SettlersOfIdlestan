using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SettlersOfIdlestan.Model.Ascension;

/// <summary>
/// Pouvoirs divins. Foi est le pouvoir fondateur (toujours disponible, sans prérequis) ; les autres
/// pouvoirs sont organisés en colonnes indépendantes qui ne peuvent être débloquées qu'une fois
/// Foi acquise (voir AscensionPowerDefinition.Column).
/// Sérialisé par nom via <see cref="AscensionPowerIdJsonConverter"/> : l'ajout ou la suppression d'un
/// pouvoir ne décale plus les valeurs des autres, et un pouvoir supprimé lu depuis une ancienne
/// sauvegarde retombe sur <see cref="Removed"/> plutôt que de faire échouer tout le chargement.
/// </summary>
[JsonConverter(typeof(AscensionPowerIdJsonConverter))]
public enum AscensionPowerId
{
    Faith,
    HandOfGod,
    EyeOfGod,
    WalkOfGod,
    ArmOfGod,
    DivineInventory,
    PresenceOfGod,
    FistOfGod,
    MemoryOfGod,
    GreaterPurification,
    HornOfPlenty,
    WrathOfGod,

    // Valeur de repli utilisée par AscensionPowerIdJsonConverter pour tout pouvoir supprimé lu depuis
    // une ancienne sauvegarde. Absente de AscensionPowerDefinitions.All, donc AscensionPowerDefinitions.Get
    // y renvoie null comme pour n'importe quel id inconnu — déjà géré partout où Get est appelé. Rester
    // dans AscensionState.UnlockedPowers sans jamais correspondre à un pouvoir réel est sans effet
    // (AscensionController.IsPowerUnlocked/GetModifiers ne testent que des id connus).
    Removed,
}

/// <summary>
/// Désérialise AscensionPowerId par nom, avec repli sur <see cref="AscensionPowerId.Removed"/> pour
/// toute valeur non reconnue (pouvoir supprimé) au lieu de faire échouer tout le chargement de la
/// sauvegarde. Chaque suppression doit être documentée ici avec la version qui l'a introduite.
/// </summary>
public sealed class AscensionPowerIdJsonConverter : JsonConverter<AscensionPowerId>
{
    public override AscensionPowerId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => Parse(reader.GetString());

    public override void Write(Utf8JsonWriter writer, AscensionPowerId value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString());

    // AscensionState.UnlockedPowers est un HashSet<AscensionPowerId> : ses éléments passent par
    // Read/Write, jamais par ReadAsPropertyName/WriteAsPropertyName (réservés aux clés de
    // dictionnaire) — mais les deux sont fournis par symétrie avec TechnologyIdJsonConverter, au cas
    // où un futur Dictionary<AscensionPowerId, ...> en aurait besoin.
    public override AscensionPowerId ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => Parse(reader.GetString());

    public override void WriteAsPropertyName(Utf8JsonWriter writer, AscensionPowerId value, JsonSerializerOptions options)
        => writer.WritePropertyName(value.ToString());

    private static AscensionPowerId Parse(string? s)
    {
        // [Legacy remap v0.20] "DivineLegacy", "EternalLegacy" et "PrestigiousAscension" supprimés,
        // remplacés par des jalons gratuits équivalents (voir AscensionMilestoneId).
        if (Enum.TryParse<AscensionPowerId>(s, out var value)) return value;
        return AscensionPowerId.Removed;
    }
}
