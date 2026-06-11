using SettlersOfIdlestan.Model.GameplayModifier;
using System.Text.Json.Serialization;

namespace SettlersOfIdlestan.Model.Magic;

/// <summary>
/// Identifiant d'un rituel. Sérialisé en string dans les sauvegardes.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RitualId
{
    /// <summary>Croissance — accélère toutes les récoltes.</summary>
    Growth,
    /// <summary>Forge Ardente — chance de doubler les récoltes automatiques.</summary>
    ArdentForge,
    /// <summary>Bénédiction Martiale — capacité de soldats et production militaire.</summary>
    MartialBlessing,
    /// <summary>Bouclier Arcanique — défense des villes et régénération.</summary>
    ArcaneShield,
    /// <summary>Clairvoyance — vitesse de recherche.</summary>
    Clairvoyance,
    /// <summary>Lumière des Profondeurs — bonus dédiés à l'Inframonde.</summary>
    DeepLight,
}

/// <summary>
/// Définition statique d'un rituel : effets par point de puissance, coûts de lancement
/// et d'entretien en cristaux. Effet linéaire (× puissance), coût quadratique (× puissance²).
/// </summary>
public class RitualDefinition
{
    public RitualId Id { get; }
    public string NameKey { get; }
    public string DescKey { get; }

    /// <summary>Cristaux consommés au lancement pour puissance 1 (multiplié par puissance²).</summary>
    public int BaseLaunchCost { get; }

    /// <summary>Cristaux consommés par cycle d'entretien pour puissance 1 (multiplié par puissance²).</summary>
    public int BaseUpkeepCost { get; }

    /// <summary>Modificateurs appliqués par point de puissance (Value × puissance).</summary>
    public IReadOnlyList<Modifier> ModifiersPerPower { get; }

    public RitualDefinition(RitualId id, int baseLaunchCost, int baseUpkeepCost, IReadOnlyList<Modifier> modifiersPerPower)
    {
        Id = id;
        NameKey = $"ritual_{id.ToString().ToLower()}_name";
        DescKey = $"ritual_{id.ToString().ToLower()}_desc";
        BaseLaunchCost = baseLaunchCost;
        BaseUpkeepCost = baseUpkeepCost;
        ModifiersPerPower = modifiersPerPower;
    }
}
