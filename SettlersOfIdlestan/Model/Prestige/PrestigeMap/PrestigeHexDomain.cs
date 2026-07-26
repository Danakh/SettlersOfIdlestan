using System.Text.Json.Serialization;

namespace SettlersOfIdlestan.Model.Prestige.PrestigeMap;

[JsonConverter(typeof(JsonStringEnumConverter<PrestigeHexDomain>))]
public enum PrestigeHexDomain
{
    None,
    Exploit,
    Explore,
    Expand,
    Exterminate,
}
