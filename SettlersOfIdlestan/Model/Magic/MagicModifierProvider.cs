using SettlersOfIdlestan.Model.GameplayModifier;

namespace SettlersOfIdlestan.Model.Magic;

/// <summary>
/// Expose les modificateurs des rituels actifs (effet linéaire en puissance : Value × puissance).
/// Pour un template MULTIPLICATIVE, Value est la fraction de bonus par point de puissance (ex. 0.10 =
/// +10 % par point) — le modificateur généré vaut 1 + Value × puissance, pour multiplier la valeur de
/// base au lieu de s'additionner aux autres sources (recherches, prestige…) qui restent additives.
/// Un template ADDITIVE (bonus plat, ex. +3 soldats) reste Value × puissance.
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
            {
                double value = template.Type == Modifier.EType.MULTIPLICATIVE
                    ? 1.0 + template.Value * active.Power
                    : template.Value * active.Power;
                yield return new Modifier(template.Category, template.SubCategory, template.Type, value);
            }
        }
    }
}
