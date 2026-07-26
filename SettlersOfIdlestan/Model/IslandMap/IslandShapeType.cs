using System.Text.Json.Serialization;

namespace SettlersOfIdlestan.Model.IslandMap;

[JsonConverter(typeof(JsonStringEnumConverter<IslandShapeType>))]
public enum IslandShapeType
{
    Compact,
    Crescent,
    Archipelago,
    Elongated,
    Lake,
    InlandSea
}
