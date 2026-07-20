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
                BuildingType.Mine => typeof(Mine),
                BuildingType.Quarry => typeof(Quarry),
                BuildingType.Seaport => typeof(Seaport),
                BuildingType.Warehouse => typeof(Warehouse),
                BuildingType.Forge => typeof(Forge),
                BuildingType.Library => typeof(Library),
                BuildingType.Temple => typeof(Temple),
                BuildingType.TownHall => typeof(TownHall),
                BuildingType.BuildersGuild => typeof(BuildersGuild),
                BuildingType.Laboratory => typeof(Laboratory),
                BuildingType.Barracks => typeof(Barracks),
                BuildingType.GlassWorks => typeof(GlassWorks),
                BuildingType.Palisade => typeof(Palisade),
                BuildingType.ImperialPort => typeof(ImperialPort),
                BuildingType.HarvestersGuild => typeof(HarvestersGuild),
                BuildingType.ArtisansGuild => typeof(ArtisansGuild),
                BuildingType.Watchtower => typeof(Watchtower),
                BuildingType.Academy => typeof(Academy),
                BuildingType.TraderGuild => typeof(TraderGuild),
                BuildingType.MilitaryAcademy => typeof(MilitaryAcademy),
                BuildingType.DeepestMine => typeof(DeepestMine),
                BuildingType.Smelter => typeof(Smelter),
                BuildingType.BlastFurnace => typeof(BlastFurnace),
                BuildingType.Arsenal => typeof(Arsenal),
                BuildingType.MushroomFarm => typeof(MushroomFarm),
                BuildingType.MithrilMine => typeof(MithrilMine),
                BuildingType.MageTower => typeof(MageTower),
                BuildingType.WarRoom => typeof(WarRoom),
                BuildingType.AlchimistHut => typeof(AlchimistHut),
                BuildingType.WeaponSmith => typeof(WeaponSmith),
                BuildingType.ArmorSmith => typeof(ArmorSmith),
                BuildingType.AdventurersGuild => typeof(AdventurersGuild),
                BuildingType.VolcanicForge => typeof(VolcanicForge),
                BuildingType.Ziggurat => typeof(Ziggurat),
                BuildingType.HeartTree => typeof(HeartTree),
                BuildingType.RunicForge => typeof(RunicForge),
                BuildingType.GreatBurrow => typeof(GreatBurrow),
                BuildingType.ColossusWorkshop => typeof(ColossusWorkshop),
                BuildingType.SkullPit => typeof(SkullPit),
                BuildingType.ThroneOfWinds => typeof(ThroneOfWinds),
                BuildingType.PearlGrotto => typeof(PearlGrotto),
                _ => throw new JsonException($"Unknown building type: {bType}")
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
