using SettlersOfIdlestan.Model.Buildings;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SettlersOfIdlestan.Model.GameplayModifier;

public class ModifierAggregator
{
    private readonly List<IModifierProvider> _providers = new();

    public void Register(IModifierProvider provider) => _providers.Add(provider);
    public void Clear() => _providers.Clear();

    public int ApplyModifiers(ECategory category, string subCategory, int baseValue)
    {
        int result = baseValue;
        foreach (var provider in _providers)
            foreach (var modifier in provider.GetModifiers())
                if (modifier.AppliesTo(category, subCategory))
                    result = modifier.Apply(result);
        return result;
    }

    public double ApplyModifiers(ECategory category, string subCategory, double baseValue)
    {
        double result = baseValue;
        foreach (var provider in _providers)
            foreach (var modifier in provider.GetModifiers())
                if (modifier.AppliesTo(category, subCategory))
                    result = modifier.Apply(result);
        return result;
    }

    /// <summary>
    /// Collects all distinct BuildingType values from modifiers of the given category
    /// (SubCategory holds the BuildingType enum name). Aggregates across all registered providers.
    /// </summary>
    public IReadOnlyList<BuildingType> GetGrantedBuildingTypes(ECategory category)
    {
        var result = new HashSet<BuildingType>();
        foreach (var provider in _providers)
            foreach (var modifier in provider.GetModifiers())
                if (modifier.IsActive && modifier.Category == category
                    && Enum.TryParse<BuildingType>(modifier.SubCategory, out var bt))
                    result.Add(bt);
        return result.ToList();
    }
}
