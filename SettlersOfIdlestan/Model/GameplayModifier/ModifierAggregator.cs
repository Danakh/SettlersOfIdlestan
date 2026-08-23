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

    /// <summary>
    /// Enregistre un provider. Idempotent pour la même instance : un appelant réinvoqué sur une
    /// civilisation déjà initialisée (ex. MainGameController.SetupModifierAggregators rappelé sur le
    /// même WorldState via SetGame/SetGameFromSave sans régénération d'île) ne doit pas compter ses
    /// modifiers deux fois — un second Register(AscensionController) silencieux ignoré plutôt qu'un
    /// doublon qui fausserait tous les modifiers additifs qu'il fournit (ex. BUILDING_MAX_LEVEL
    /// Ziggurat de la race Humaine, passé de 1 à 2).
    /// </summary>
    public void Register(IModifierProvider provider)
    {
        if (_providers.Contains(provider)) return;
        _providers.Add(provider);
        provider.OnModifiersChanged += Invalidate;
        Invalidate();
    }

    /// <summary>Retourne vrai si <paramref name="old"/> a été trouvé et remplacé, faux sinon (l'appelant
    /// doit alors se rabattre sur <see cref="Register"/> — cas d'une nouvelle civilisation où
    /// l'ancienne instance n'était jamais enregistrée ici).</summary>
    public bool Replace(IModifierProvider old, IModifierProvider newProvider)
    {
        int idx = _providers.IndexOf(old);
        if (idx < 0) return false;
        old.OnModifiersChanged -= Invalidate;
        _providers[idx] = newProvider;
        newProvider.OnModifiersChanged += Invalidate;
        Invalidate();
        return true;
    }

    private void Invalidate()
    {
        _dirty = true;
        Changed?.Invoke();
    }

    private static readonly List<Modifier> EmptyModifiers = new();

    /// <summary>
    /// Retourne volontairement le <see cref="List{T}"/> concret et non un <c>IReadOnlyList</c> : les
    /// appelants font un <c>foreach</c> dessus, et via l'interface le compilateur passe par
    /// <c>IEnumerable&lt;T&gt;</c>, ce qui <b>boxe</b> l'énumérateur de structure de la liste — une
    /// allocation à chaque appel. <see cref="ApplyModifiers(ECategory, string, int)"/> est appelé des
    /// milliers de fois par tick (vitesse de récolte par bâtiment et par hexagone, niveaux max,
    /// bonus militaires…) : ces énumérateurs boxés pesaient ~2,5 % des allocations de la simulation.
    /// </summary>
    private List<Modifier> GetCached(ECategory category)
    {
        if (_dirty) Rebuild();
        return _cache.TryGetValue(category, out var list) ? list : EmptyModifiers;
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

    /// <summary>
    /// Retourne les modifiers actifs bruts d'une catégorie (SubCategory + Value inclus) — pour les
    /// catégories où la seule présence/SubCategory ne suffit pas (ex. CITY_PLACEMENT_TERRAIN_RANGE,
    /// INLAND_CITY_LEVEL_CAP : la portée/le plafond varie selon Value).
    /// </summary>
    public IReadOnlyList<Modifier> GetActiveModifiers(ECategory category)
    {
        var result = new List<Modifier>();
        foreach (var modifier in GetCached(category))
            if (modifier.IsActive)
                result.Add(modifier);
        return result;
    }

    /// <summary>
    /// Variante sans allocation de <see cref="GetActiveModifiers"/> : rend la liste cachée telle
    /// quelle, à charge de l'appelant de sauter les modifiers inactifs. Le type de retour est le
    /// <see cref="List{T}"/> concret pour la raison décrite sur <see cref="GetCached"/> (via
    /// l'interface, le <c>foreach</c> boxerait l'énumérateur), et une catégorie absente rend une
    /// liste vide plutôt que null — un appelant peut donc tester <c>Count == 0</c> pour se retirer
    /// avant tout autre travail. À préférer sur les chemins répétés : GetActiveModifiers alloue une
    /// List par appel, ce que <see cref="Island.BuildingController.GetMaxLevel(Buildings.Building,
    /// Civilization.Civilization, Civilization.City)"/> payait par ville et par tick une fois
    /// l'autoplay branché dessus (BuildingLevelObjective.IsDone).
    /// </summary>
    public List<Modifier> GetActiveModifiersUnfiltered(ECategory category) => GetCached(category);
}
