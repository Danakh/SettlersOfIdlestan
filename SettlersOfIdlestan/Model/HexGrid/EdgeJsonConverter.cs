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

            // Expecting [[q1,r1,z1],[q2,r2,z2]]; legacy [[q,r],...] reads as z=0.
            if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() != 2)
                throw new JsonException("Edge must be an array of two hex coords");

            var h1 = root[0];
            var h2 = root[1];
            var hex1 = ReadHexCoord(h1);
            var hex2 = ReadHexCoord(h2);
            return Edge.Create(hex1, hex2);
        }

        public override void Write(Utf8JsonWriter writer, Edge value, JsonSerializerOptions options)
        {
            writer.WriteStartArray();

            WriteHexCoord(writer, value.Hex1);
            WriteHexCoord(writer, value.Hex2);

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
                element.GetArrayLength() == 3 ? element[2].GetInt32() : HexCoord.SurfaceZ);
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
}
