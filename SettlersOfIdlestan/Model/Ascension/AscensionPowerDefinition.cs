using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestan.Model.Ascension;

/// <summary>
/// Définition statique d'un pouvoir divin : identité, textes de localisation et coût.
/// </summary>
public class AscensionPowerDefinition
{
    public AscensionPowerId Id { get; }
    public string NameKey { get; }
    public string DescKey { get; }
    public ResourceSet Cost { get; }

    public AscensionPowerDefinition(AscensionPowerId id, string nameKey, string descKey, ResourceSet? cost = null)
    {
        Id = id;
        NameKey = nameKey;
        DescKey = descKey;
        Cost = cost ?? new ResourceSet();
    }
}
