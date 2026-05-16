using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SettlersOfIdlestan.Model.HexGrid
{
    public class EdgeJsonConverter : JsonConverter<Edge>
    {
        public override Edge? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using var doc = JsonDocument.ParseValue(ref reader);
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.Null) return null;

            // Expecting [[q1,r1],[q2,r2]]
            if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() != 2)
                throw new JsonException("Edge must be an array of two hex coords");

            var h1 = root[0];
            var h2 = root[1];
            if (h1.ValueKind != JsonValueKind.Array || h1.GetArrayLength() != 2 || h2.ValueKind != JsonValueKind.Array || h2.GetArrayLength() != 2)
                throw new JsonException("Each hex coord must be an array of two integers");

            var hex1 = new HexCoord(h1[0].GetInt32(), h1[1].GetInt32());
            var hex2 = new HexCoord(h2[0].GetInt32(), h2[1].GetInt32());
            return Edge.Create(hex1, hex2);
        }

        public override void Write(Utf8JsonWriter writer, Edge value, JsonSerializerOptions options)
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

            writer.WriteEndArray();
        }
    }
}
