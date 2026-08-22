using SettlersOfIdlestan.Model.GameplayModifier;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SettlersOfIdlestan.Model.Magic;

/// <summary>
/// Identifiant d'un rituel. Sérialisé en string dans les sauvegardes.
/// </summary>
[JsonConverter(typeof(RitualIdJsonConverter))]
public enum RitualId
{
    /// <summary>Croissance — accélère toutes les récoltes.</summary>
    Growth,
    /// <summary>Forge Ardente — chance de doubler les récoltes automatiques.</summary>
    ArdentForge,
    /// <summary>Bénédiction Martiale — capacité de soldats et production militaire.</summary>
    MartialBlessing,
    /// <summary>Bouclier Arcanique — défense des villes et régénération.</summary>
    ArcaneShield,
    /// <summary>Clairvoyance — vitesse de recherche.</summary>
    Clairvoyance,

    /// <summary>
    /// Valeur de repli utilisée par RitualIdJsonConverter pour tout rituel supprimé lu depuis une
    /// ancienne sauvegarde. Absente de RitualDefinitions.All, donc RitualDefinitions.Get y renvoie
    /// null comme pour n'importe quel id inconnu — déjà géré partout où Get est appelé.
    /// </summary>
    Removed,
}

/// <summary>
/// Désérialise RitualId par nom, avec repli sur <see cref="RitualId.Removed"/> pour toute valeur
/// non reconnue (rituel supprimé) au lieu de faire échouer tout le chargement de la sauvegarde.
/// Chaque suppression doit être documentée ici avec la version qui l'a introduite.
/// </summary>
public sealed class RitualIdJsonConverter : JsonConverter<RitualId>
{
    public override RitualId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => Parse(reader.GetString());

    public override void Write(Utf8JsonWriter writer, RitualId value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString());

    // Pas de Dictionary<RitualId, ...> aujourd'hui, mais un JsonConverter<T> qui ne gère pas les clés
    // de dictionnaire lève une NotSupportedException dès qu'on essaie — voir TechnologyIdJsonConverter,
    // qui a cassé le chargement des sauvegardes ayant des recherches répétables en dictionnaire.
    public override RitualId ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => Parse(reader.GetString());

    public override void WriteAsPropertyName(Utf8JsonWriter writer, RitualId value, JsonSerializerOptions options)
        => writer.WritePropertyName(value.ToString());

    private static RitualId Parse(string? s)
    {
        // [Legacy remap v0.15] "DeepLight" supprimé avec la recherche Lumière des Profondeurs.
        if (Enum.TryParse<RitualId>(s, out var value)) return value;
        return RitualId.Removed;
    }
}

/// <summary>
/// Définition statique d'un rituel : effets par point de puissance, coûts de lancement
/// et d'entretien en cristaux. Effet linéaire (× puissance), coût quadratique (× puissance²).
/// </summary>
public class RitualDefinition
{
    public RitualId Id { get; }
    public string NameKey { get; }
    public string DescKey { get; }

    /// <summary>Cristaux consommés au lancement pour puissance 1 (multiplié par puissance²).</summary>
    public int BaseLaunchCost { get; }

    /// <summary>Cristaux consommés par cycle d'entretien pour puissance 1 (multiplié par puissance²).</summary>
    public int BaseUpkeepCost { get; }

    /// <summary>Modificateurs appliqués par point de puissance (Value × puissance).</summary>
    public IReadOnlyList<Modifier> ModifiersPerPower { get; }

    public RitualDefinition(RitualId id, int baseLaunchCost, int baseUpkeepCost, IReadOnlyList<Modifier> modifiersPerPower)
    {
        Id = id;
        NameKey = $"ritual_{id.ToString().ToLower()}_name";
        DescKey = $"ritual_{id.ToString().ToLower()}_desc";
        BaseLaunchCost = baseLaunchCost;
        BaseUpkeepCost = baseUpkeepCost;
        ModifiersPerPower = modifiersPerPower;
    }
}
