using System.Text.Json.Serialization;

namespace SettlersOfIdlestan.Controller.Store;

[JsonConverter(typeof(JsonStringEnumConverter<StoreConnectionStatus>))]
public enum StoreConnectionStatus
{
    NotDetected,
    Connected,
    Failed,
}
