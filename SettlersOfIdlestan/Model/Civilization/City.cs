using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.HexGrid;

namespace SettlersOfIdlestan.Model.Civilization;

/// <summary>
/// Represents a city in the game.
/// </summary>
[Serializable]
public class City : IBuildingContext, IMilitaryVertex
{
    /// <summary>
    /// Gets or sets the position of the city on the hex grid.
    /// </summary>
    public Vertex Position { get; set; }

    /// <summary>
    /// Gets or sets the index of the civilization this city belongs to.
    /// </summary>
    public int CivilizationIndex { get; set; }

    /// <summary>
    /// Gets or sets the list of buildings in the city.
    /// </summary>
    public List<Building> Buildings { get; set; } = new();

    IReadOnlyList<Building> IBuildingContext.Buildings => Buildings;

    /// <summary>
    /// Défense actuelle (dynamique). Se régénère jusqu'à MaxDefense.
    /// </summary>
    public int CurrentDefense { get; set; }

    /// <summary>
    /// Défense maximale calculée depuis les bâtiments (Palissade=10, Caserne=5, …).
    /// </summary>
    public int MaxDefense => Buildings.Sum(b => b.GetDefenseBonus());

    /// <summary>
    /// Nombre de soldats en garnison dans cette ville.
    /// </summary>
    public int Soldiers { get; set; }

    [NonSerialized]
    private int _cachedMaxSoldiers;
    [NonSerialized]
    private bool _maxSoldiersCacheValid;

    /// <summary>
    /// Capacité maximale de soldats calculée depuis les bâtiments (Caserne, Garnison, Arsenal…).
    /// Caché car lu sur le chemin chaud (production/renfort de soldats, combats). Invalidé
    /// explicitement à chaque changement de bâtiment (voir <see cref="InvalidateMaxSoldiersCache"/>)
    /// et, plus largement, à chaque gain de prestige ou de recherche via
    /// <see cref="Civilization.InvalidateAllCityMaxSoldiersCaches"/>.
    /// </summary>
    public int MaxSoldiers
    {
        get
        {
            if (!_maxSoldiersCacheValid)
            {
                _cachedMaxSoldiers = Buildings.Sum(b => b.GetMaxSoldiersBonus());
                _maxSoldiersCacheValid = true;
            }
            return _cachedMaxSoldiers;
        }
    }

    /// <summary>
    /// Invalide le cache de <see cref="MaxSoldiers"/>. À appeler après toute construction/amélioration
    /// de bâtiment pouvant affecter la capacité de garnison (Caserne, Garnison, Arsenal).
    /// </summary>
    internal void InvalidateMaxSoldiersCache()
    {
        _maxSoldiersCacheValid = false;
    }

    /// <summary>
    /// Tick de la dernière production de soldat pour cette ville.
    /// </summary>
    public long LastSoldierProductionTick { get; set; }

    /// <summary>
    /// Tick de la dernière production de soldats par l'Arsenal de cette ville (voir SoldierProductionEngine.ProduceArsenalSoldiers).
    /// </summary>
    public long LastArsenalProductionTick { get; set; }

    /// <summary>
    /// Tick du dernier point de régénération de défense.
    /// </summary>
    public long LastDefenseRegenTick { get; set; }

    /// <summary>
    /// Tick de la dernière attaque lancée par cette ville, contre une ville adverse ou une MonsterFeature.
    /// Cooldown commun aux deux types de cible : une ville ne peut pas attaquer trop vite, peu importe la cible,
    /// mais plusieurs villes peuvent attaquer la même cible simultanément.
    /// </summary>
    public long LastAttackTick { get; set; }

    /// <summary>
    /// Tick du dernier renfort envoyé par cette ville vers une ville alliée.
    /// </summary>
    public long LastReinforcementTick { get; set; }

    /// <summary>
    /// Soldats en transit vers cette ville. Leur slot est réservé dès le départ de la ville source.
    /// </summary>
    public List<InTransitSoldier> IncomingSoldiers { get; set; } = new();

    /// <summary>
    /// Flux défini par le joueur : cité cible à attaquer ou à renforcer. Null si aucun flux.
    /// </summary>
    public Vertex? FlowTarget { get; set; }

    /// <summary>
    /// Flux défini par le joueur : MonsterFeature ciblée pour une attaque à distance. Null si aucune cible.
    /// Mutuellement exclusif avec <see cref="FlowTarget"/>.
    /// </summary>
    public HexCoord? MonsterAttackTarget { get; set; }

    /// <summary>
    /// Déclenchements Ziggourat déjà consommés par cette ville (production instantanée de Dominion
    /// à la construction/amélioration d'un Temple, max Ziggurat.MaxTriggersPerCity — voir
    /// CorruptionController.ApplyZigguratInstantProduction).
    /// </summary>
    public int ZigguratTriggersUsed { get; set; }

    [NonSerialized]
    private Building? _cachedTownHall;
    [NonSerialized]
    private bool _townHallCacheValid;

    /// <summary>
    /// Gets the effective level of the city, i.e. the TownHall's own Level (0 if no TownHall is
    /// built yet). AvailableAtLevel checks on every other building type compare directly against
    /// this value — no offset.
    /// </summary>
    public int Level
    {
        get
        {
            if (!_townHallCacheValid)
            {
                _cachedTownHall = Buildings.FirstOrDefault(b => b.Type == BuildingType.TownHall);
                _townHallCacheValid = true;
            }
            return _cachedTownHall?.Level ?? 0;
        }
    }

    internal void InvalidateLevelCache()
    {
        _cachedTownHall = null;
        _townHallCacheValid = false;
    }

    /// <summary>
    /// Bâtiment de ce type dans la ville, ou null. Une ville ne contient jamais deux bâtiments du même
    /// type (BuildingController.BuildBuilding améliore l'existant au lieu d'en
    /// ajouter un second), donc ce parcours équivaut exactement à un
    /// <c>Buildings.OfType&lt;T&gt;().FirstOrDefault()</c> — mais sans itérateur LINQ, sans délégué et
    /// avec une simple comparaison d'enum au lieu d'un test de type. Les contrôleurs périodiques
    /// (récolte, routes, recherche, militaire…) font cette recherche pour chaque ville à chaque tick :
    /// c'est un des chemins les plus chauds du jeu.
    /// </summary>
    public Building? FindBuilding(BuildingType type)
    {
        var buildings = Buildings;
        for (int i = 0; i < buildings.Count; i++)
            if (buildings[i].Type == type)
                return buildings[i];
        return null;
    }

    /// <summary>
    /// Variante typée de <see cref="FindBuilding(BuildingType)"/> — <paramref name="type"/> doit être le
    /// <see cref="Building.Type"/> correspondant à <typeparamref name="T"/>.
    /// </summary>
    public T? FindBuilding<T>(BuildingType type) where T : Building => FindBuilding(type) as T;

    /// <summary>
    /// Gets the textual name of the city level used for sprite selection.
    /// </summary>
    public string LevelName => Level switch
    {
        1 => "outpost",
        2 => "colony",
        3 => "town",
        4 => "metropolis",
        5 => "capital",
        _ => "outpost",
    };

    /// <summary>
    /// Fired just before the city is removed from its civilization. Subscribers can still read city properties.
    /// </summary>
    [field: NonSerialized]
    public event EventHandler<EventArgs>? Destroyed;

    internal void RaiseDestroyed() => Destroyed?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// Initializes a new instance of the <see cref="City"/> class.
    /// </summary>
    public City()
    {
        Position = null!;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="City"/> class with the specified position.
    /// </summary>
    /// <param name="position">The position of the city on the hex grid.</param>
    public City(Vertex position)
    {
        Position = position;
    }
}