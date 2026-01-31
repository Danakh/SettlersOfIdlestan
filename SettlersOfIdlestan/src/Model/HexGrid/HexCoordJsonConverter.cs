using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SettlersOfIdlestan.Model.HexGrid;

public class HexCoordJsonConverter : JsonConverter<HexCoord>
{
    // For dictionary keys, System.Text.Json calls ReadAsPropertyName/WriteAsPropertyName
    public override HexCoord Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // Expecting either string "q,r" or start of array [q, r]
        if (reader.TokenType == JsonTokenType.String)
        {
            var s = reader.GetString();
            var parts = s!.Split(',');
            return new HexCoord(int.Parse(parts[0]), int.Parse(parts[1]));
        }
        else if (reader.TokenType == JsonTokenType.StartArray)
        {
            reader.Read();
            int q = reader.GetInt32();
            reader.Read();
            int r = reader.GetInt32();
            reader.Read(); // EndArray
            return new HexCoord(q, r);
        }

        throw new JsonException("Unexpected token when deserializing HexCoord");
    }

    public override void Write(Utf8JsonWriter writer, HexCoord value, JsonSerializerOptions options)
    {
        // Write as array by default
        writer.WriteStartArray();
        writer.WriteNumberValue(value.Q);
        writer.WriteNumberValue(value.R);
        writer.WriteEndArray();
    }

    // Support using HexCoord as dictionary key
    public override HexCoord ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var s = reader.GetString();
        var parts = s!.Split(',');
        return new HexCoord(int.Parse(parts[0]), int.Parse(parts[1]));
    }

    public override void WriteAsPropertyName(Utf8JsonWriter writer, HexCoord value, JsonSerializerOptions options)
    {
        // Write property name as "q,r"
        writer.WritePropertyName($"{value.Q},{value.R}");
    }
}
