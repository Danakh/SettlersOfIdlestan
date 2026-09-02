using SettlersOfIdlestan.Model.HexGrid;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SettlersOfIdlestan.Model.IslandMap
{
    public class IslandMapJsonConverter : JsonConverter<IslandMap>
    {
        // HexTile uses a [JsonConstructor] with camelCase parameters — needs case-insensitive matching.
        // JsonStringEnumConverter handles both legacy numeric TerrainType values and new string values.
        // Statique : construire un JsonSerializerOptions reconstruit tout son cache de métadonnées
        // par réflexion, et cet objet était recréé à chaque couche de chaque chargement.
        private static readonly JsonSerializerOptions _tilesOptions = MakeTilesOptions();

        private static JsonSerializerOptions MakeTilesOptions()
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            options.Converters.Add(new SettlersOfIdlestan.Model.HexGrid.HexCoordJsonConverter());
            options.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
            return options;
        }

        public override IslandMap Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using var doc = JsonDocument.ParseValue(ref reader);
            var root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.Null)
                return null!;

            if (!root.TryGetProperty("Tiles", out var tilesElem))
                throw new JsonException("Missing 'Tiles' property for IslandMap.");

            Dictionary<HexCoord, HexTile> dict;
            try
            {
                dict = tilesElem.Deserialize<Dictionary<HexCoord, HexTile>>(_tilesOptions)
                       ?? new Dictionary<HexCoord, HexTile>();
            }
            catch (Exception ex)
            {
                throw new JsonException($"Failed to deserialize Tiles dictionary. Raw JSON: {tilesElem.GetRawText()}", ex);
            }

            int z = IslandMap.SurfaceLayer;
            if (root.TryGetProperty("Z", out var zElem))
                z = zElem.GetInt32();

            return new IslandMap(dict.Values, z);
        }

        public override void Write(Utf8JsonWriter writer, IslandMap value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteNumber("Z", value.Z);
            writer.WritePropertyName("Tiles");
            JsonSerializer.Serialize(writer, value.Tiles, options);
            writer.WriteEndObject();
        }
    }
}
