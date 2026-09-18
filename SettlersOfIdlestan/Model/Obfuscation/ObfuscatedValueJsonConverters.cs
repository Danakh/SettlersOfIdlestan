using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SettlersOfIdlestan.Model.Obfuscation;

/// <summary>
/// Sérialise un <see cref="ObfInt"/> comme le nombre qu'il représente. Le découpage en deux moitiés
/// n'a de sens qu'en mémoire vive : l'écrire ne protégerait rien de plus — la sauvegarde est chiffrée
/// — et rendrait illisibles les sauvegardes antérieures au brouillage, qui portent un nombre nu.
///
/// <para>Déclaré en attribut sur le type plutôt qu'enregistré dans les options de
/// <c>SaveController</c> : le convertisseur suit alors le type partout, y compris dans les clés et
/// valeurs de dictionnaires et dans les outils hors jeu (bancs de mesure, testeur de stratégie) qui
/// construisent leurs propres options.</para>
/// </summary>
public sealed class ObfIntJsonConverter : JsonConverter<ObfInt>
{
    public override ObfInt Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => ObfInt.From(reader.GetInt32());

    public override void Write(Utf8JsonWriter writer, ObfInt value, JsonSerializerOptions options)
        => writer.WriteNumberValue(value.Value);

    /// <summary>
    /// Une quantité brouillée peut servir de valeur dans un dictionnaire sérialisé
    /// (<c>Civilization.Resources</c>) ; JSON n'ayant que des chaînes en clé, ces deux surcharges
    /// couvrent le cas où le type se retrouverait en position de clé.
    /// </summary>
    public override ObfInt ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => ObfInt.From(int.Parse(reader.GetString() ?? "0", System.Globalization.CultureInfo.InvariantCulture));

    public override void WriteAsPropertyName(Utf8JsonWriter writer, ObfInt value, JsonSerializerOptions options)
        => writer.WritePropertyName(value.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
}

/// <summary>Pendant 64 bits d'<see cref="ObfIntJsonConverter"/>.</summary>
public sealed class ObfLongJsonConverter : JsonConverter<ObfLong>
{
    public override ObfLong Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => ObfLong.From(reader.GetInt64());

    public override void Write(Utf8JsonWriter writer, ObfLong value, JsonSerializerOptions options)
        => writer.WriteNumberValue(value.Value);

    public override ObfLong ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => ObfLong.From(long.Parse(reader.GetString() ?? "0", System.Globalization.CultureInfo.InvariantCulture));

    public override void WriteAsPropertyName(Utf8JsonWriter writer, ObfLong value, JsonSerializerOptions options)
        => writer.WritePropertyName(value.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
}
