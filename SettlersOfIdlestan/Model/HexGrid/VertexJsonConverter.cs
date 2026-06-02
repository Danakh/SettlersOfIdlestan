using SettlersOfIdlestan.Model.HexGrid;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SettlersOfIdlestan.Model.HexGrid;

public class VertexJsonConverter : JsonConverter<Vertex>
{
    public override Vertex Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;
        if (root.ValueKind == JsonValueKind.Null) return null!;

        if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() != 3)
            throw new JsonException("Vertex must be an array of three hex coordinates");

        var hexes = new HexCoord[3];
        for (int i = 0; i < 3; i++)
        {
            hexes[i] = ReadHexCoord(root[i]);
        }

        return Vertex.Create(hexes[0], hexes[1], hexes[2]);
    }

    public override void Write(Utf8JsonWriter writer, Vertex value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();

        WriteHexCoord(writer, value.Hex1);
        WriteHexCoord(writer, value.Hex2);
        WriteHexCoord(writer, value.Hex3);

        writer.WriteEndArray();
    }

    private static HexCoord ReadHexCoord(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array ||
            (element.GetArrayLength() != 2 && element.GetArrayLength() != 3))
            throw new JsonException("Each hex coord must be [q, r] or [q, r, z]");

        return new HexCoord(
            element[0].GetInt32(),
            element[1].GetInt32(),
            element.GetArrayLength() == 3 ? element[2].GetInt32() : 0);
    }

    private static void WriteHexCoord(Utf8JsonWriter writer, HexCoord value)
    {
        writer.WriteStartArray();
        writer.WriteNumberValue(value.Q);
        writer.WriteNumberValue(value.R);
        writer.WriteNumberValue(value.Z);
        writer.WriteEndArray();
    }
}
