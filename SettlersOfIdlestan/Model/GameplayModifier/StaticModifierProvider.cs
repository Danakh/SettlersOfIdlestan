namespace SettlersOfIdlestan.Model.GameplayModifier;

public class StaticModifierProvider : IModifierProvider
{
    private readonly IReadOnlyList<Modifier> _modifiers;

    public StaticModifierProvider(IEnumerable<Modifier> modifiers)
        => _modifiers = modifiers.ToList();

    public IEnumerable<Modifier> GetModifiers() => _modifiers;
#pragma warning disable CS0067
    public event Action? OnModifiersChanged;
#pragma warning restore CS0067
}
