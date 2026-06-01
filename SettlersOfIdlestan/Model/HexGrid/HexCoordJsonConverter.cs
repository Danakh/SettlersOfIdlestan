using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SettlersOfIdlestan.Model.HexGrid;

public class HexCoordJsonConverter : JsonConverter<HexCoord>
{
    // For dictionary keys, System.Text.Json calls ReadAsPropertyName/WriteAsPropertyName
    public override HexCoord Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // Expecting either string "q,r[,z]" or start of array [q, r, z].
        if (reader.TokenType == JsonTokenType.String)
        {
            var s = reader.GetString();
            return ParseString(s!);
        }
        else if (reader.TokenType == JsonTokenType.StartArray)
        {
            return ReadArray(ref reader);
        }

        throw new JsonException("Unexpected token when deserializing HexCoord");
    }

    public override void Write(Utf8JsonWriter writer, HexCoord value, JsonSerializerOptions options)
    {
        // Write as array by default
        writer.WriteStartArray();
        writer.WriteNumberValue(value.Q);
        writer.WriteNumberValue(value.R);
        writer.WriteNumberValue(value.Z);
        writer.WriteEndArray();
    }

    // Support using HexCoord as dictionary key
    public override HexCoord ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var s = reader.GetString();
        return ParseString(s!);
    }

    public override void WriteAsPropertyName(Utf8JsonWriter writer, HexCoord value, JsonSerializerOptions options)
    {
        // Write property name as "q,r,z".
        writer.WritePropertyName($"{value.Q},{value.R},{value.Z}");
    }

    private static HexCoord ParseString(string value)
    {
        var parts = value.Split(',');
        if (parts.Length != 2 && parts.Length != 3)
            throw new JsonException("HexCoord string must be 'q,r' or 'q,r,z'");

        return new HexCoord(
            int.Parse(parts[0]),
            int.Parse(parts[1]),
            parts.Length == 3 ? int.Parse(parts[2]) : HexCoord.SurfaceZ);
    }

    private static HexCoord ReadArray(ref Utf8JsonReader reader)
    {
        reader.Read();
        int q = reader.GetInt32();
        reader.Read();
        int r = reader.GetInt32();
        reader.Read();

        int z = HexCoord.SurfaceZ;
        if (reader.TokenType != JsonTokenType.EndArray)
        {
            z = reader.GetInt32();
            reader.Read();
        }

        if (reader.TokenType != JsonTokenType.EndArray)
            throw new JsonException("HexCoord array must contain [q, r] or [q, r, z]");

        return new HexCoord(q, r, z);
    }
}
