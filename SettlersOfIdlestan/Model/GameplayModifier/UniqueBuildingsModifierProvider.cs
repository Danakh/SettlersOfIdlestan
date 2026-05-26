namespace SettlersOfIdlestan.Model.GameplayModifier;

/// <summary>
/// Aggregates modifiers from all unique buildings in a civilization that implement IModifierProvider.
/// Registered once at startup; queries buildings dynamically so modifiers activate as soon as a
/// building is constructed, without requiring a re-initialization of the aggregator.
/// </summary>
public class UniqueBuildingsModifierProvider : IModifierProvider
{
    private readonly Civilization.Civilization _civ;

    public UniqueBuildingsModifierProvider(Civilization.Civilization civ)
    {
        _civ = civ;
    }

    public IEnumerable<Modifier> GetModifiers()
    {
        return _civ.Cities
            .SelectMany(c => c.Buildings)
            .OfType<IModifierProvider>()
            .SelectMany(p => p.GetModifiers());
    }
}
