using System.Text.Json.Serialization;

namespace SettlersOfIdlestan.Model.Localization;

[JsonConverter(typeof(JsonStringEnumConverter<Language>))]
public enum Language
{
    French,
    English,
}
