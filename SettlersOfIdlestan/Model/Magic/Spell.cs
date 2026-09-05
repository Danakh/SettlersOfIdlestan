using System.Text.Json.Serialization;

namespace SettlersOfIdlestan.Model.Magic;

/// <summary>
/// Identifiant d'un sort instantané.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<SpellId>))]
public enum SpellId
{
    /// <summary>Abondance — consomme des cristaux pour produire de l'or immédiatement.</summary>
    Abundance,
    /// <summary>Invocation de Troupes — consomme des cristaux pour faire apparaître des soldats dans une ville alliée ciblée.</summary>
    SummonTroops,
    /// <summary>Édification Arcanique — fait apparaître une ville entièrement développée sur un vertex libre
    /// desservi par une route, sans aucune des restrictions de placement habituelles.</summary>
    ArcaneEdification,
    /// <summary>Pont du Vide — bâtit gratuitement une route du Vide ciblée.</summary>
    VoidBridge,
}

/// <summary>
/// Cible requise pour lancer un sort. <see cref="None"/> : effet immédiat sans ciblage.
/// <see cref="AllyCity"/> : le joueur doit désigner une de ses propres villes.
/// <see cref="BuildableVertex"/> : le joueur doit désigner un vertex libre touché par une de ses routes
/// (les autres règles de placement — distances entre villes, terrain racial — ne s'appliquent pas).
/// <see cref="VoidRoad"/> : le joueur doit désigner une arête séparant deux hexagones de Vide.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<SpellTargetKind>))]
public enum SpellTargetKind
{
    None,
    AllyCity,
    BuildableVertex,
    VoidRoad,
}

/// <summary>
/// Définition statique d'un sort instantané : coût en cristaux et récompense (or et/ou troupes),
/// appliquée en une seule fois au moment du lancement (pas d'entretien, pas de puissance).
/// </summary>
public class SpellDefinition
{
    public SpellId Id { get; }
    public string NameKey { get; }
    public string DescKey { get; }
    public int CrystalCost { get; }
    public int GoldReward { get; }
    public int TroopReward { get; }
    public SpellTargetKind TargetKind { get; }

    /// <summary>
    /// Quand supérieur à 1, le coût en cristaux est multiplié par cette valeur pour chaque cran
    /// d'épuisement accumulé (voir <c>MagicState.SpellExhaustionStacks</c> et
    /// <c>MagicController.GetSpellCost</c>). Chaque lancement réussi ajoute un cran ; le cooldown
    /// <see cref="CooldownTicks"/> en retire un à chaque cycle écoulé.
    /// </summary>
    public int CostMultiplierPerCast { get; }

    /// <summary>
    /// Durée en ticks du cooldown qui retire un cran d'épuisement (voir <see cref="CostMultiplierPerCast"/>).
    /// Le décompte démarre dès que le sort est connu (voir <c>MagicController.ProcessSpellExhaustion</c>),
    /// indépendamment des lancements — un sort jamais lancé mais connu depuis longtemps peut donc avoir
    /// déjà consommé un ou plusieurs cycles sans effet visible (rien à retirer à 0 cran).
    /// </summary>
    public long CooldownTicks { get; }

    public SpellDefinition(SpellId id, int crystalCost, int goldReward = 0, int troopReward = 0,
        SpellTargetKind targetKind = SpellTargetKind.None, int costMultiplierPerCast = 1, long cooldownTicks = 0)
    {
        Id = id;
        NameKey = $"spell_{id.ToString().ToLower()}_name";
        DescKey = $"spell_{id.ToString().ToLower()}_desc";
        CrystalCost = crystalCost;
        GoldReward = goldReward;
        TroopReward = troopReward;
        TargetKind = targetKind;
        CostMultiplierPerCast = costMultiplierPerCast;
        CooldownTicks = cooldownTicks;
    }
}
