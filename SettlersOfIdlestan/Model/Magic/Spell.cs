namespace SettlersOfIdlestan.Model.Magic;

/// <summary>
/// Identifiant d'un sort instantané.
/// </summary>
public enum SpellId
{
    /// <summary>Abondance — consomme des cristaux pour produire de l'or immédiatement.</summary>
    Abundance,
}

/// <summary>
/// Définition statique d'un sort instantané : coût en cristaux et récompense en or,
/// appliqués en une seule fois au moment du lancement (pas d'entretien, pas de puissance).
/// </summary>
public class SpellDefinition
{
    public SpellId Id { get; }
    public string NameKey { get; }
    public string DescKey { get; }
    public int CrystalCost { get; }
    public int GoldReward { get; }

    public SpellDefinition(SpellId id, int crystalCost, int goldReward)
    {
        Id = id;
        NameKey = $"spell_{id.ToString().ToLower()}_name";
        DescKey = $"spell_{id.ToString().ToLower()}_desc";
        CrystalCost = crystalCost;
        GoldReward = goldReward;
    }
}
