using SettlersOfIdlestan.Model.Buildings;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SettlersOfIdlestan.Model.GameplayModifier;

public class ModifierAggregator
{
    private readonly List<IModifierProvider> _providers = new();
    private readonly Dictionary<ECategory, List<Modifier>> _cache = new();
    private bool _dirty = true;

    /// <summary>
    /// Déclenché chaque fois qu'un provider notifie un changement (recherche, prestige, rituel…)
    /// ou qu'un provider est enregistré/remplacé. Permet aux caches dérivés (ex: capacité de
    /// stockage) de se recalculer sans que la civilisation n'ait à connaître la logique de calcul.
    /// </summary>
    public event Action? Changed;

    public void Register(IModifierProvider provider)
    {
        _providers.Add(provider);
        provider.OnModifiersChanged += Invalidate;
        Invalidate();
    }

    public void Replace(IModifierProvider old, IModifierProvider newProvider)
    {
        int idx = _providers.IndexOf(old);
        if (idx < 0) return;
        old.OnModifiersChanged -= Invalidate;
        _providers[idx] = newProvider;
        newProvider.OnModifiersChanged += Invalidate;
        Invalidate();
    }

    private void Invalidate()
    {
        _dirty = true;
        Changed?.Invoke();
    }

    private IReadOnlyList<Modifier> GetCached(ECategory category)
    {
        if (_dirty) Rebuild();
        return _cache.TryGetValue(category, out var list) ? list : [];
    }

    private void Rebuild()
    {
        _cache.Clear();
        foreach (var provider in _providers)
            foreach (var modifier in provider.GetModifiers())
            {
                if (!_cache.TryGetValue(modifier.Category, out var list))
                    _cache[modifier.Category] = list = new();
                list.Add(modifier);
            }
        _dirty = false;
    }

    public int ApplyModifiers(ECategory category, string subCategory, int baseValue)
    {
        int result = baseValue;
        foreach (var modifier in GetCached(category))
            if (modifier.AppliesTo(category, subCategory))
                result = modifier.Apply(result);
        return result;
    }

    public double ApplyModifiers(ECategory category, string subCategory, double baseValue)
    {
        double result = baseValue;
        foreach (var modifier in GetCached(category))
            if (modifier.AppliesTo(category, subCategory))
                result = modifier.Apply(result);
        return result;
    }

    /// <summary>
    /// Returns true if any registered provider has an active modifier of the given category
    /// (and optionally matching subCategory).
    /// </summary>
    public bool HasModifier(ECategory category, string subCategory = "")
    {
        foreach (var modifier in GetCached(category))
            if (modifier.IsActive && (subCategory == "" || modifier.SubCategory == subCategory))
                return true;
        return false;
    }

    /// <summary>
    /// Collecte les SubCategory (non vides) distincts des modifiers actifs de la catégorie donnée —
    /// ex. les TerrainType exigés par CITY_PLACEMENT_REQUIRES_TERRAIN.
    /// </summary>
    public IReadOnlyList<string> GetActiveSubCategories(ECategory category)
    {
        var result = new HashSet<string>();
        foreach (var modifier in GetCached(category))
            if (modifier.IsActive && modifier.SubCategory != "")
                result.Add(modifier.SubCategory);
        return result.ToList();
    }

    /// <summary>
    /// Collects all distinct BuildingType values from modifiers of the given category
    /// (SubCategory holds the BuildingType enum name). Aggregates across all registered providers.
    /// </summary>
    public IReadOnlyList<BuildingType> GetGrantedBuildingTypes(ECategory category)
    {
        var result = new HashSet<BuildingType>();
        foreach (var modifier in GetCached(category))
            if (modifier.IsActive && Enum.TryParse<BuildingType>(modifier.SubCategory, out var bt))
                result.Add(bt);
        return result.ToList();
    }
}
