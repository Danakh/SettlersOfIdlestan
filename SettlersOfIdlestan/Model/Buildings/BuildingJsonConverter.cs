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

                // Chaque remap ci-dessous doit porter un commentaire précisant la version qui a
                // introduit le besoin (renommage/suppression du type), pour la traçabilité des
                // anciennes sauvegardes.
                // [Legacy remap v0.11] "MilitaryAcademy" renommé en "Garrison".
                if (s == "MilitaryAcademy") s = "Garrison";

                if (!Enum.TryParse<BuildingType>(s, out bType))
                    throw new JsonException($"Unknown building type: {s}");
            }
            else
            {
                throw new JsonException("Invalid 'Type' property for Building.");
            }

            var raw = root.GetRawText();
            // Correspondance tenue par BuildingFactory, partagée avec BuildingController.CreateBuilding :
            // ces deux switch étaient auparavant maintenus en parallèle, et oublier celui-ci rendait
            // illisible toute sauvegarde contenant le nouveau bâtiment.
            Type concrete = BuildingFactory.GetClrType(bType)
                ?? throw new JsonException($"Unknown building type: {bType}");

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
