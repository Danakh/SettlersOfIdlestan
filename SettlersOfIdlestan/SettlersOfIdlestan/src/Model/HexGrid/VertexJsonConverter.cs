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
            var h = root[i];
            if (h.ValueKind != JsonValueKind.Array || h.GetArrayLength() != 2)
                throw new JsonException("Each hex coord must be an array of two integers");
            hexes[i] = new HexCoord(h[0].GetInt32(), h[1].GetInt32());
        }

        return Vertex.Create(hexes[0], hexes[1], hexes[2]);
    }

    public override void Write(Utf8JsonWriter writer, Vertex value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();

        writer.WriteStartArray();
        writer.WriteNumberValue(value.Hex1.Q);
        writer.WriteNumberValue(value.Hex1.R);
        writer.WriteEndArray();

        writer.WriteStartArray();
        writer.WriteNumberValue(value.Hex2.Q);
        writer.WriteNumberValue(value.Hex2.R);
        writer.WriteEndArray();

        writer.WriteStartArray();
        writer.WriteNumberValue(value.Hex3.Q);
        writer.WriteNumberValue(value.Hex3.R);
        writer.WriteEndArray();

        writer.WriteEndArray();
    }
}
