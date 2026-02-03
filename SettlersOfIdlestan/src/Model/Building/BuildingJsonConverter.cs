using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SettlersOfIdlestan.Model.Buildings
{
    /// <summary>
    /// Handles polymorphic (de)serialization of Building and its derived types.
    /// </summary>
    public class BuildingJsonConverter : JsonConverter<Building>
    {
        public override Building? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using var doc = JsonDocument.ParseValue(ref reader);
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.Null) return null;

            // Read the "Type" discriminator which may be a number or a string
            if (!root.TryGetProperty("Type", out var typeProp))
                throw new JsonException("Missing 'Type' property for Building.");

            BuildingType bType;
            if (typeProp.ValueKind == JsonValueKind.Number)
            {
                bType = (BuildingType)typeProp.GetInt32();
            }
            else if (typeProp.ValueKind == JsonValueKind.String)
            {
                var s = typeProp.GetString();
                if (!Enum.TryParse<BuildingType>(s, out bType))
                    throw new JsonException($"Unknown building type: {s}");
            }
            else
            {
                throw new JsonException("Invalid 'Type' property for Building.");
            }

            var raw = root.GetRawText();
            Type concrete = bType switch
            {
                BuildingType.Market => typeof(Market),
                BuildingType.Sawmill => typeof(Sawmill),
                BuildingType.Brickworks => typeof(Brickworks),
                BuildingType.Mill => typeof(Mill),
                BuildingType.Sheepfold => typeof(Sheepfold),
                BuildingType.Mine => typeof(Mine),
                BuildingType.Seaport => typeof(Seaport),
                BuildingType.Warehouse => typeof(Warehouse),
                BuildingType.Forge => typeof(Forge),
                BuildingType.Library => typeof(Library),
                BuildingType.Temple => typeof(Temple),
                BuildingType.TownHall => typeof(TownHall),
                BuildingType.BuildersGuild => typeof(BuildersGuild),
                _ => typeof(Building)
            };

            var result = (Building?)JsonSerializer.Deserialize(raw, concrete, options);
            return result;
        }

        public override void Write(Utf8JsonWriter writer, Building value, JsonSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNullValue();
                return;
            }

            // Serialize using the concrete runtime type so the discriminator and specific properties are preserved
            JsonSerializer.Serialize(writer, value, value.GetType(), options);
        }
    }
}
