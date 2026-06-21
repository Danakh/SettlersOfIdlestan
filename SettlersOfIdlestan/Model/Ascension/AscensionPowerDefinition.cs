using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestan.Model.Ascension;

/// <summary>
/// Définition statique d'un pouvoir divin : identité, textes de localisation et coût.
/// </summary>
public class AscensionPowerDefinition
{
    /// <summary>Numéro de colonne (0-3) à laquelle ce pouvoir appartient, ou -1 pour Foi (le pouvoir
    /// fondateur, rendu comme un grand bouton sous les 4 colonnes plutôt que dans l'une d'elles).</summary>
    public const int FoundationColumn = -1;

    public AscensionPowerId Id { get; }
    public string NameKey { get; }
    public string DescKey { get; }
    public ResourceSet Cost { get; }
    public int Column { get; }

    public AscensionPowerDefinition(AscensionPowerId id, string nameKey, string descKey, int column, ResourceSet? cost = null)
    {
        Id = id;
        NameKey = nameKey;
        DescKey = descKey;
        Column = column;
        Cost = cost ?? new ResourceSet();
    }
}
