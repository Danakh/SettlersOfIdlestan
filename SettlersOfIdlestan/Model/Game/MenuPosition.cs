using System.Text.Json.Serialization;

namespace SettlersOfIdlestan.Model.Game;

/// Position forcée de la barre d'onglets. Auto laisse le jeu décider (bas sur mobile, haut sinon).
[JsonConverter(typeof(JsonStringEnumConverter<MenuPosition>))]
public enum MenuPosition
{
    Auto,
    Top,
    Bottom,
}
