namespace SettlersOfIdlestan.Model.GameplayModifier;

public class StaticModifierProvider : IModifierProvider
{
    private readonly IReadOnlyList<Modifier> _modifiers;

    public StaticModifierProvider(IEnumerable<Modifier> modifiers)
        => _modifiers = modifiers.ToList();

    public IEnumerable<Modifier> GetModifiers() => _modifiers;
}
