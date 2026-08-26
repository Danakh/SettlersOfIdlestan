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

    private List<Building> _buildings = new();

    /// <summary>
    /// Bâtiments de la ville — lecture seule ; utiliser <see cref="AddBuilding"/>,
    /// <see cref="RemoveBuilding"/> ou <see cref="ClearBuildings"/> pour muter.
    ///
    /// <para>L'encapsulation existe pour rendre les caches dérivés fiables. Tant que la liste était
    /// publiquement mutable, tout cache calculé « à la construction » pouvait être faux : plusieurs
    /// chemins ajoutent des bâtiments sans passer par
    /// <see cref="Controller.Island.BuildingController.BuildBuilding"/> — bâtiments de départ
    /// accordés à la création d'une ville, bâtiment racial de l'Ascension, générateur de PNJ. Toute
    /// mutation lève désormais <see cref="BuildingsChanged"/>, ce à quoi la civilisation propriétaire
    /// s'abonne.</para>
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public IReadOnlyList<Building> Buildings => _buildings;

    /// <summary>Utilisé uniquement par la sérialisation JSON — conserve la clé « Buildings » des sauvegardes.</summary>
    [System.Text.Json.Serialization.JsonPropertyName("Buildings")]
    [System.Text.Json.Serialization.JsonInclude]
    public List<Building> BuildingsSerialized
    {
        get => _buildings;
        private set { _buildings = value ?? new(); OnBuildingsChanged(); }
    }

    /// <summary>Levé après tout ajout, retrait ou remplacement en masse des bâtiments de la ville.</summary>
    [field: NonSerialized]
    public event EventHandler? BuildingsChanged;

    public void AddBuilding(Building building)
    {
        _buildings.Add(building);
        OnBuildingsChanged();
    }

    public bool RemoveBuilding(Building building)
    {
        if (!_buildings.Remove(building)) return false;
        OnBuildingsChanged();
        return true;
    }

    public void ClearBuildings()
    {
        if (_buildings.Count == 0) return;
        _buildings.Clear();
        OnBuildingsChanged();
    }

    /// <summary>
    /// Invalide les caches propres à la ville puis prévient la civilisation. Les appels manuels à
    /// <see cref="InvalidateLevelCache"/> restent nécessaires après un changement de <c>Level</c>
    /// d'un bâtiment déjà présent : la liste n'a alors pas changé.
    /// </summary>
    private void OnBuildingsChanged()
    {
        _buildingByType = null;
        InvalidateLevelCache();
        InvalidateMaxSoldiersCache();
        BuildingsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Défense actuelle (dynamique). Se régénère jusqu'à MaxDefense.
    /// </summary>
    public int CurrentDefense { get; set; }

    [NonSerialized]
    private int _cachedMaxDefense;
    [NonSerialized]
    private bool _maxDefenseCacheValid;

    /// <summary>
    /// Défense maximale calculée depuis les bâtiments (Palissade=10, Caserne=5, …).
    ///
    /// <para>Caché pour la même raison que <see cref="MaxSoldiers"/> :
    /// <c>MilitaryController.ResolveDefenseRegen</c> la lit pour chaque emplacement militaire de
    /// chaque civilisation à chaque événement d'horloge, et le parcours des bâtiments — appels
    /// virtuels compris — pesait à lui seul plusieurs pourcents du budget d'image en fin de
    /// partie.</para>
    /// </summary>
    public int MaxDefense
    {
        get
        {
            if (!_maxDefenseCacheValid)
            {
                int total = 0;
                for (int i = 0; i < _buildings.Count; i++) total += _buildings[i].GetDefenseBonus();
                _cachedMaxDefense = total;
                _maxDefenseCacheValid = true;
            }
            return _cachedMaxDefense;
        }
    }

    [NonSerialized]
    private double _cachedDefenseRegenBonus;
    [NonSerialized]
    private bool _defenseRegenBonusCacheValid;

    /// <summary>
    /// Bonus de vitesse de régénération de défense apporté par les bâtiments de la ville. Lu sur le
    /// même chemin chaud que <see cref="MaxDefense"/>, et caché pour la même raison.
    /// </summary>
    public double DefenseRegenBonus
    {
        get
        {
            if (!_defenseRegenBonusCacheValid)
            {
                double total = 0;
                for (int i = 0; i < _buildings.Count; i++) total += _buildings[i].GetDefenseRegenBonus();
                _cachedDefenseRegenBonus = total;
                _defenseRegenBonusCacheValid = true;
            }
            return _cachedDefenseRegenBonus;
        }
    }

    [NonSerialized]
    private double _cachedUnitProductionSpeedBonus;
    [NonSerialized]
    private bool _unitProductionSpeedBonusCacheValid;

    /// <summary>
    /// Bonus de vitesse de production de soldats propre à cette ville (Garnison), qui s'ajoute au
    /// <see cref="Civilization.UnitProductionSpeed"/> civ-wide uniquement pour SES propres Caserne/Arsenal
    /// (voir SoldierProductionEngine, MilitaryController.GetSoldierProductionRate). Lu sur le même chemin
    /// chaud que <see cref="MaxDefense"/>, et caché pour la même raison.
    /// </summary>
    public double UnitProductionSpeedBonus
    {
        get
        {
            if (!_unitProductionSpeedBonusCacheValid)
            {
                double total = 0;
                for (int i = 0; i < _buildings.Count; i++) total += _buildings[i].GetUnitProductionSpeedBonus();
                _cachedUnitProductionSpeedBonus = total;
                _unitProductionSpeedBonusCacheValid = true;
            }
            return _cachedUnitProductionSpeedBonus;
        }
    }

    /// <summary>
    /// Invalide les caches dérivés de la défense (et de la vitesse de production de soldats, même
    /// logique de dépendance). Appelé par <see cref="InvalidateLevelCache"/> <b>et</b>
    /// par <see cref="InvalidateMaxSoldiersCache"/> : les deux invalidations manuelles existantes ne
    /// couvrent pas les mêmes sites — le prestige améliore une Caserne en n'appelant que la seconde,
    /// un combat qui rétrograde un bâtiment n'appelle que la première — alors que les bonus de défense
    /// dépendent du niveau dans les deux cas. Les brancher aux deux garantit qu'aucun chemin de
    /// changement de niveau ne laisse la défense (ou la vitesse de production) périmée.
    /// </summary>
    private void InvalidateDefenseCaches()
    {
        _maxDefenseCacheValid = false;
        _defenseRegenBonusCacheValid = false;
        _unitProductionSpeedBonusCacheValid = false;
    }

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
                int total = 0;
                for (int i = 0; i < _buildings.Count; i++) total += _buildings[i].GetMaxSoldiersBonus();
                _cachedMaxSoldiers = total;
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
        InvalidateDefenseCaches();
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
                _cachedTownHall = FindBuilding(BuildingType.TownHall);
                _townHallCacheValid = true;
            }
            return _cachedTownHall?.Level ?? 0;
        }
    }

    internal void InvalidateLevelCache()
    {
        _cachedTownHall = null;
        _townHallCacheValid = false;
        InvalidateDefenseCaches();
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
    /// <summary>
    /// Index type → bâtiment, reconstruit à la demande et invalidé par <see cref="OnBuildingsChanged"/>
    /// comme les autres caches dérivés de la ville. Null tant qu'aucun <see cref="FindBuilding"/> n'a
    /// été fait depuis la dernière mutation.
    ///
    /// <para>Le scan linéaire qu'il remplace était appelé une dizaine de fois par ville et par
    /// événement d'horloge — production de soldats, forges, marché, port, hutte d'alchimie ont chacun
    /// leur passe sur toutes les villes — soit, en fin de partie, plusieurs dizaines de milliers de
    /// comparaisons par événement pour n'aboutir la plupart du temps qu'à « pas de bâtiment » ou
    /// « pas encore l'heure ».</para>
    /// </summary>
    [NonSerialized]
    private Dictionary<BuildingType, Building>? _buildingByType;

    public Building? FindBuilding(BuildingType type)
    {
        if (_buildingByType == null)
        {
            var index = new Dictionary<BuildingType, Building>(_buildings.Count);
            // Premier gagnant, comme l'ancien scan linéaire : si une ville portait deux bâtiments du
            // même type, c'est le premier de la liste qui était retourné.
            for (int i = 0; i < _buildings.Count; i++)
                index.TryAdd(_buildings[i].Type, _buildings[i]);
            _buildingByType = index;
        }

        return _buildingByType.TryGetValue(type, out var building) ? building : null;
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