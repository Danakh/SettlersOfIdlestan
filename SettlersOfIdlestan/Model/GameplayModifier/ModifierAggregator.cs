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
}
