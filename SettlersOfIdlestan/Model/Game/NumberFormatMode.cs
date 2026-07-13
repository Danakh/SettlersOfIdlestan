using System.Text.Json.Serialization;

namespace SettlersOfIdlestan.Model.Game;

/// Format d'affichage des grands nombres dans l'interface.
[JsonConverter(typeof(JsonStringEnumConverter<NumberFormatMode>))]
public enum NumberFormatMode
{
    /// Suffixes classiques : 1.5k, 12M, 3.4B…
    Classic,

    /// Notation scientifique normalisée : 1.5e3, 1.2e7…
    Scientific,

    /// Notation ingénieur (exposant multiple de 3) : 1.5e3, 12e6, 340e9…
    Engineering,
}
