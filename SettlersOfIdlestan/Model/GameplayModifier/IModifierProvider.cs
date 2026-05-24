namespace SettlersOfIdlestan.Model.GameplayModifier;

public interface IModifierProvider
{
    IEnumerable<Modifier> GetModifiers();
}
