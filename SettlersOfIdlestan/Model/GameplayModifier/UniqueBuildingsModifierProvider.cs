namespace SettlersOfIdlestan.Model.GameplayModifier;

/// <summary>
/// Fournit les modifiers issus des bâtiments uniques d'une civilisation.
/// Le cache est reconstruit explicitement via <see cref="Rebuild"/> plutôt que par requête dynamique.
/// </summary>
public class UniqueBuildingsModifierProvider : IModifierProvider
{
    private List<Modifier> _cache = new();

    public event Action? OnModifiersChanged;

    public IEnumerable<Modifier> GetModifiers() => _cache;

    public void Rebuild(IEnumerable<Modifier> modifiers)
    {
        _cache = modifiers.ToList();
        OnModifiersChanged?.Invoke();
    }
}
