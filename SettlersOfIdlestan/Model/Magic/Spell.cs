namespace SettlersOfIdlestan.Model.Magic;

/// <summary>
/// Identifiant d'un sort instantané.
/// </summary>
public enum SpellId
{
    /// <summary>Abondance — consomme des cristaux pour produire de l'or immédiatement.</summary>
    Abundance,
    /// <summary>Invocation de Troupes — consomme des cristaux pour faire apparaître des soldats dans une ville alliée ciblée.</summary>
    SummonTroops,
    /// <summary>Édification Arcanique — fait apparaître une ville entièrement développée sur un vertex constructible ciblé.</summary>
    ArcaneEdification,
}

/// <summary>
/// Cible requise pour lancer un sort. <see cref="None"/> : effet immédiat sans ciblage.
/// <see cref="AllyCity"/> : le joueur doit désigner une de ses propres villes.
/// <see cref="BuildableVertex"/> : le joueur doit désigner un vertex où il peut fonder une ville.
/// </summary>
public enum SpellTargetKind
{
    None,
    AllyCity,
    BuildableVertex,
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

    public SpellDefinition(SpellId id, int crystalCost, int goldReward = 0, int troopReward = 0,
        SpellTargetKind targetKind = SpellTargetKind.None)
    {
        Id = id;
        NameKey = $"spell_{id.ToString().ToLower()}_name";
        DescKey = $"spell_{id.ToString().ToLower()}_desc";
        CrystalCost = crystalCost;
        GoldReward = goldReward;
        TroopReward = troopReward;
        TargetKind = targetKind;
    }
}
