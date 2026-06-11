using SettlersOfIdlestan.Model.GameplayModifier;

namespace SettlersOfIdlestan.Model.Magic;

/// <summary>
/// Expose les modificateurs des rituels actifs (effet linéaire : Value × puissance).
/// Appeler <see cref="NotifyChanged"/> après tout lancement/arrêt/changement de puissance.
/// </summary>
public class MagicModifierProvider : IModifierProvider
{
    private readonly MagicState _state;

    public event Action? OnModifiersChanged;

    public MagicModifierProvider(MagicState state) => _state = state;

    public void NotifyChanged() => OnModifiersChanged?.Invoke();

    public IEnumerable<Modifier> GetModifiers()
    {
        foreach (var active in _state.ActiveRituals)
        {
            var def = RitualDefinitions.Get(active.Id);
            if (def == null) continue;
            foreach (var template in def.ModifiersPerPower)
                yield return new Modifier(template.Category, template.SubCategory, template.Type, template.Value * active.Power);
        }
    }
}
