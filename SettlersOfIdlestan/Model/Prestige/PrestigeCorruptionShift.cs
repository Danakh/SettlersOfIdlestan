using System.Text.Json.Serialization;

namespace SettlersOfIdlestan.Model.Prestige;

/// <summary>
/// Effet d'un prestige sur le niveau de corruption du monde
/// (<see cref="PrestigeState.CurrentCorruptionLevel"/>). Le joueur choisit entre les trois au
/// moment de prestiger : un bouton par valeur dans le popup de prestige (voir PrestigeRenderer).
/// Un enum plutôt qu'un booléen « corrompu » : les trois états se propagent tels quels de la vue
/// jusqu'à PrestigeController.PerformPrestige, où deux booléens dont une combinaison n'a aucun
/// sens finiraient par diverger.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<PrestigeCorruptionShift>))]
public enum PrestigeCorruptionShift
{
    /// <summary>Prestige normal : la corruption reste au niveau où elle est.</summary>
    Unchanged,

    /// <summary>Prestige Corrompu : +1 niveau, à condition qu'une Spire de Corruption soit bâtie.</summary>
    Increase,

    /// <summary>Prestige Purifié : -1 niveau (jamais sous 1), sans condition de Spire.</summary>
    Decrease,
}
