using SettlersOfIdlestan.Model.HexGrid;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SettlersOfIdlestan.Model.IslandMap
{
    public class IslandMapJsonConverter : JsonConverter<IslandMap>
    {
        public override IslandMap Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using var doc = JsonDocument.ParseValue(ref reader);
            var root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.Null)
                return null!;

            if (!root.TryGetProperty("Tiles", out var tilesElem))
                throw new JsonException("Missing 'Tiles' property for IslandMap.");

            var dict = JsonSerializer.Deserialize<Dictionary<HexCoord, HexTile>>(tilesElem.GetRawText(), options)
                       ?? new Dictionary<HexCoord, HexTile>();

            return new IslandMap(dict.Values);
        }

        public override void Write(Utf8JsonWriter writer, IslandMap value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("Tiles");
            JsonSerializer.Serialize(writer, value.Tiles, options);
            writer.WriteEndObject();
        }
    }
}
