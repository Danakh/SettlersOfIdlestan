using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SettlersOfIdlestan.Model.IslandMap;

/// <summary>
/// Represents the resources produced by land tiles.
/// </summary>
[JsonConverter(typeof(ResourceJsonConverter))]
public enum Resource
{
    Food,
    Wood,
    Brick,
    Stone,
    Gold,
    Ore,
    Glass,
    Steel,
    Crystal,
    Mithril,
    SteelWeapon,
    SteelArmor,
    MithrilWeapon,
    MithrilArmor,
    StrengthPotion,
}

/// <summary>
/// Sérialise <see cref="Resource"/> par nom, avec remap des noms des sauvegardes antérieures.
/// Les ressources sont aussi utilisées comme clés de dictionnaire (stocks, coûts, plafonds), qui
/// passent par ReadAsPropertyName/WriteAsPropertyName et non par Read/Write.
/// Chaque renommage doit être documenté ici avec la version qui l'a introduit.
/// </summary>
public sealed class ResourceJsonConverter : JsonConverter<Resource>
{
    public override Resource Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => Parse(reader.GetString());

    public override void Write(Utf8JsonWriter writer, Resource value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString());

    public override Resource ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => Parse(reader.GetString());

    public override void WriteAsPropertyName(Utf8JsonWriter writer, Resource value, JsonSerializerOptions options)
        => writer.WritePropertyName(value.ToString());

    private static Resource Parse(string? s)
    {
        // [Legacy remap v0.22] "HealingPotion" renommé en "StrengthPotion" — la Potion de Soin
        // (sauvetage d'un soldat) est devenue la Potion de Force (dégât supplémentaire en attaque).
        if (s == "HealingPotion") return Resource.StrengthPotion;
        if (Enum.TryParse<Resource>(s, out var value)) return value;
        throw new JsonException($"Unknown Resource value '{s}'.");
    }
}

public class ResourceUtils
{
    public static List<Resource> BasicResources = new List<Resource>
    {
        Resource.Food,
        Resource.Wood,
        Resource.Brick,
        Resource.Stone
    };
    public static List<Resource> IntermediateResources = new List<Resource>
    {
        Resource.Ore,
        Resource.Gold,
        Resource.Glass,
        Resource.Steel
    };
    public static List<Resource> AdvancedResources = new List<Resource>
    {
        Resource.Crystal,
        Resource.Mithril
    };
    /// <summary>Ressources qui nécessitent d'être découvertes via un vertex de prestige (UNLOCK_RESOURCE)
    /// avant d'être visibles/échangeables. Indépendant de la catégorisation Basic/Intermediate/Advanced.</summary>
    public static List<Resource> DiscoverableResources = new List<Resource>
    {
        Resource.Glass,
        Resource.Crystal,
        Resource.Mithril,
        Resource.Steel
    };
    public static List<Resource> ConsumableResources = new List<Resource>
    {
        Resource.SteelWeapon,
        Resource.SteelArmor,
        Resource.MithrilWeapon,
        Resource.MithrilArmor,
        Resource.StrengthPotion,
    };
    /// <summary>Basic + Intermediate + Advanced resources — every resource except the crafted consumables.</summary>
    public static List<Resource> NonConsumableResources = BasicResources
        .Concat(IntermediateResources)
        .Concat(AdvancedResources)
        .ToList();
}