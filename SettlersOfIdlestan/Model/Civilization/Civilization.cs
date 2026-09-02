using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.GameplayModifier;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SettlersOfIdlestan.Model.Civilization;

/// <summary>
/// Represents a civilization with a list of cities and roads.
/// </summary>
[Serializable]
public class Civilization
{
    /// <summary>
    /// Gets or sets the index of the civilization in the island state.
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    /// Indique si cette civilisation est contrôlée par l'IA.
    /// </summary>
    public bool IsNpc { get; set; } = false;

    /// <summary>
    /// Vrai une fois que le joueur a aperçu cette civilisation (évite les doublons dans le log).
    /// </summary>
    public bool DiscoveredByPlayer { get; set; } = false;

    /// <summary>
    /// Paramètres NPC (niveau d'évolution, agressivité). Null pour le joueur.
    /// </summary>
    public NpcParameters? NpcParameters { get; set; }

    /// <summary>
    /// Indices des civilisations qui ont attaqué cette civilisation. Sur la civilisation du joueur,
    /// alimenté quand elle est attaquée pendant l'autoplayer, pour lui permettre de riposter.
    /// Sur un NPC non-Pacifiste, quand non-vide, limite ses attaques à ces civilisations (agressivité
    /// ciblée plutôt que globale) — voir <c>NpcGameController</c>.
    /// </summary>
    public List<int> WarEnemyCivIndices { get; set; } = new();

    private List<City> _cities = new();

    /// <summary>
    /// Liste des villes de la civilisation — lecture seule ; utiliser AddCity / RemoveCity pour muter.
    /// </summary>
    [JsonIgnore]
    public IReadOnlyList<City> Cities => _cities;

    // Utilisé uniquement par la sérialisation JSON.
    [JsonPropertyName("Cities")]
    [JsonInclude]
    public List<City> CitiesSerialized
    {
        get => _cities;
        private set
        {
            foreach (var city in _cities) city.BuildingsChanged -= OnCityBuildingsChanged;
            _cities = value ?? new();
            foreach (var city in _cities) city.BuildingsChanged += OnCityBuildingsChanged;
            InvalidateVertexCaches();
            InvalidateBuildingDerivedCaches();
        }
    }

    public void AddCity(City city)
    {
        _cities.Add(city);
        city.BuildingsChanged += OnCityBuildingsChanged;
        InvalidateVertexCaches();
        InvalidateBuildingDerivedCaches();
        // La ville peut déjà porter des bâtiments (villes de départ, PNJ...) ajoutés avant cet appel,
        // donc avant l'abonnement à BuildingsChanged ci-dessus : sans ce rebuild explicite, un bâtiment
        // unique posé avant AddCity resterait invisible de GetUniqueBuilding.
        RebuildUniqueBuildingCache();
        RebuildUniqueBuildingsModifiers();
        RecalculateStorageCapacity();
    }

    public void RemoveCity(City city)
    {
        if (_cities.Remove(city))
            city.BuildingsChanged -= OnCityBuildingsChanged;
        InvalidateVertexCaches();
        InvalidateBuildingDerivedCaches();
        RebuildUniqueBuildingCache();
        RebuildUniqueBuildingsModifiers();
        RecalculateStorageCapacity();
    }

    /// <summary>
    /// Une ville de cette civilisation a gagné ou perdu un bâtiment. C'est le point unique qui rend
    /// les caches dérivés des bâtiments fiables, quel que soit le chemin de construction emprunté —
    /// y compris ceux qui n'appellent pas <c>BuildingController.BuildBuilding</c>.
    /// </summary>
    private void OnCityBuildingsChanged(object? sender, EventArgs e)
    {
        InvalidateBuildingDerivedCaches();
        RebuildUniqueBuildingCache();
        RebuildUniqueBuildingsModifiers();
    }

    private List<Road> _roads = new();

    /// <summary>
    /// Gets the list of roads in the civilization — lecture seule ; utiliser AddRoad / RemoveRoad pour muter.
    /// </summary>
    [JsonIgnore]
    public IReadOnlyList<Road> Roads => _roads;

    [JsonPropertyName("Roads")]
    [JsonInclude]
    public List<Road> RoadsSerialized
    {
        get => _roads;
        private set => _roads = value ?? new();
    }

    public void AddRoad(Road road) => _roads.Add(road);
    public void RemoveRoad(Road road) => _roads.Remove(road);
    public void RemoveAllRoads(Predicate<Road> match) => _roads.RemoveAll(match);

    private List<MaritimeBeacon> _maritimeBeacons = new();

    /// <summary>
    /// Liste des balises maritimes de la civilisation — lecture seule ; utiliser AddMaritimeBeacon /
    /// RemoveMaritimeBeacon pour muter.
    /// </summary>
    [JsonIgnore]
    public IReadOnlyList<MaritimeBeacon> MaritimeBeacons => _maritimeBeacons;

    [JsonPropertyName("MaritimeBeacons")]
    [JsonInclude]
    public List<MaritimeBeacon> MaritimeBeaconsSerialized
    {
        get => _maritimeBeacons;
        private set { _maritimeBeacons = value ?? new(); InvalidateVertexCaches(); }
    }

    public void AddMaritimeBeacon(MaritimeBeacon beacon) { _maritimeBeacons.Add(beacon); InvalidateVertexCaches(); }
    public void RemoveMaritimeBeacon(MaritimeBeacon beacon) { _maritimeBeacons.Remove(beacon); InvalidateVertexCaches(); }

    private List<WarFleet> _fleets = new();

    /// <summary>
    /// Liste des Flottes de Guerre de la civilisation — lecture seule ; utiliser AddFleet / RemoveFleet
    /// pour muter.
    /// </summary>
    [JsonIgnore]
    public IReadOnlyList<WarFleet> Fleets => _fleets;

    [JsonPropertyName("Fleets")]
    [JsonInclude]
    public List<WarFleet> FleetsSerialized
    {
        get => _fleets;
        private set { _fleets = value ?? new(); InvalidateVertexCaches(); }
    }

    public void AddFleet(WarFleet fleet) { _fleets.Add(fleet); InvalidateVertexCaches(); }
    public void RemoveFleet(WarFleet fleet) { _fleets.Remove(fleet); InvalidateVertexCaches(); }

    private List<MobileCamp> _mobileCamps = new();

    /// <summary>
    /// Liste des Camps Mobiles de la civilisation — lecture seule ; utiliser AddMobileCamp /
    /// RemoveMobileCamp pour muter.
    /// </summary>
    [JsonIgnore]
    public IReadOnlyList<MobileCamp> MobileCamps => _mobileCamps;

    [JsonPropertyName("MobileCamps")]
    [JsonInclude]
    public List<MobileCamp> MobileCampsSerialized
    {
        get => _mobileCamps;
        private set { _mobileCamps = value ?? new(); InvalidateVertexCaches(); }
    }

    public void AddMobileCamp(MobileCamp camp) { _mobileCamps.Add(camp); InvalidateVertexCaches(); }
    public void RemoveMobileCamp(MobileCamp camp) { _mobileCamps.Remove(camp); InvalidateVertexCaches(); }

    private List<LandingSite> _landingSites = new();

    /// <summary>
    /// Sites d'Arrivée réservés par la civilisation (voir <see cref="LandingSite"/>) — lecture seule ;
    /// utiliser AddLandingSite / RemoveLandingSite pour muter.
    /// </summary>
    [JsonIgnore]
    public IReadOnlyList<LandingSite> LandingSites => _landingSites;

    [JsonPropertyName("LandingSites")]
    [JsonInclude]
    public List<LandingSite> LandingSitesSerialized
    {
        get => _landingSites;
        private set { _landingSites = value ?? new(); InvalidateVertexCaches(); }
    }

    public void AddLandingSite(LandingSite site) { _landingSites.Add(site); InvalidateVertexCaches(); }
    public void RemoveLandingSite(LandingSite site) { _landingSites.Remove(site); InvalidateVertexCaches(); }

    [NonSerialized]
    private List<IMilitaryVertex>? _militaryVerticesCache;

    [NonSerialized]
    private List<IBuildVertex>? _buildVerticesCache;

    /// <summary>
    /// Invalide les listes agrégées <see cref="MilitaryVertices"/> / <see cref="BuildVertices"/>.
    /// À appeler depuis toute mutation d'une des listes sources (villes, flottes, balises, camps
    /// mobiles, sites d'arrivée) — pas quand un emplacement change simplement de position, les caches
    /// ne contenant que des références.
    /// </summary>
    private void InvalidateVertexCaches()
    {
        _militaryVerticesCache = null;
        _buildVerticesCache = null;
        InvalidateCityPositionCache();
    }

    [NonSerialized]
    private Dictionary<HexGrid.HexCoord, List<City>>? _citiesByHexCache;

    /// <summary>
    /// Index hexagone → villes de la civilisation touchant cet hexagone. Chaque ville occupe un
    /// vertex, donc borde exactement trois hexagones ; l'index est donc trois entrées par ville.
    ///
    /// <para><b>Pourquoi.</b> La question « quelles villes bordent cet hexagone ? » était posée par un
    /// <c>Cities.Where(c =&gt; c.Position.IsAdjacentTo(hex))</c>, c'est-à-dire un balayage de toutes les
    /// villes — plus une fermeture et un itérateur alloués par appel. Le rendu du plateau la pose une
    /// fois par tuile et par image : en fin de partie, 1 000 tuiles × 200 villes = 200 000 tests
    /// d'adjacence à chaque image, avant le moindre pixel dessiné. Avec l'index, chaque question coûte
    /// une recherche dans un dictionnaire.</para>
    ///
    /// <para><b>Invalidation.</b> Contrairement à <see cref="MilitaryVertices"/> et
    /// <see cref="BuildVertices"/>, qui ne contiennent que des références et survivent donc à un
    /// déplacement, cet index est <b>indexé par la position</b> : il doit être invalidé aussi quand
    /// une ville bouge sans que le compte change — voir
    /// <c>CityBuilderController.RelocateCity</c>, seul chemin qui réaffecte <c>City.Position</c>.</para>
    /// </summary>
    public IReadOnlyList<City> GetCitiesAdjacentTo(HexGrid.HexCoord hex)
    {
        var index = _citiesByHexCache ??= BuildCitiesByHex();
        return index.TryGetValue(hex, out var list) ? list : (IReadOnlyList<City>)Array.Empty<City>();
    }

    /// <summary>
    /// Invalide l'index <see cref="GetCitiesAdjacentTo"/>. Appelé automatiquement à toute mutation de
    /// la liste des villes ; à appeler explicitement quand une ville change de position.
    /// </summary>
    public void InvalidateCityPositionCache() => _citiesByHexCache = null;

    private Dictionary<HexGrid.HexCoord, List<City>> BuildCitiesByHex()
    {
        var index = new Dictionary<HexGrid.HexCoord, List<City>>(_cities.Count * 3);
        for (int i = 0; i < _cities.Count; i++)
        {
            var city = _cities[i];
            // Une ville désérialisée avant que sa position ne soit relue peut avoir Position == null
            // (voir le constructeur sans argument de City) : l'ignorer plutôt que de lever.
            if (city.Position is null) continue;

            var hexes = city.Position.GetHexes();
            for (int h = 0; h < hexes.Length; h++)
            {
                if (!index.TryGetValue(hexes[h], out var list))
                    index[hexes[h]] = list = new List<City>(1);
                list.Add(city);
            }
        }
        return index;
    }

    /// <summary>
    /// Tous les emplacements militaires de la civilisation (villes, flottes et camps mobiles) — voir
    /// IMilitaryVertex. Utilisé par le système militaire pour traiter les trois types de façon uniforme.
    /// Les Sites d'Arrivée en sont volontairement absents : ils ne sont jamais une cible.
    ///
    /// <para>Matérialisé en liste et mis en cache jusqu'à la prochaine mutation. Une chaîne
    /// <c>Concat</c> paraissait gratuite mais réalloue ses itérateurs à <b>chaque</b> énumération, et
    /// cette propriété est parcourue par presque tous les moteurs militaires à chaque tick, parfois
    /// en boucles imbriquées (une énumération complète par emplacement). L'ordre — villes, puis
    /// flottes, puis camps — est celui de l'ancienne chaîne et doit le rester : plusieurs choix de
    /// cible en dépendent, donc le déterminisme de la partie aussi.</para>
    /// </summary>
    [JsonIgnore]
    public IReadOnlyList<IMilitaryVertex> MilitaryVertices
    {
        get
        {
            if (_militaryVerticesCache != null) return _militaryVerticesCache;

            var list = new List<IMilitaryVertex>(_cities.Count + _fleets.Count + _mobileCamps.Count);
            for (int i = 0; i < _cities.Count; i++) list.Add(_cities[i]);
            for (int i = 0; i < _fleets.Count; i++) list.Add(_fleets[i]);
            for (int i = 0; i < _mobileCamps.Count; i++) list.Add(_mobileCamps[i]);
            return _militaryVerticesCache = list;
        }
    }

    /// <summary>
    /// Tous les emplacements construits par la civilisation (villes, flottes, balises, camps mobiles,
    /// sites d'arrivée) — voir IBuildVertex. Utilisé pour vérifier de façon uniforme l'occupation
    /// d'un vertex. Même mise en cache et même contrainte d'ordre que <see cref="MilitaryVertices"/>.
    /// </summary>
    [JsonIgnore]
    public IReadOnlyList<IBuildVertex> BuildVertices
    {
        get
        {
            if (_buildVerticesCache != null) return _buildVerticesCache;

            var list = new List<IBuildVertex>(
                _cities.Count + _fleets.Count + _maritimeBeacons.Count + _mobileCamps.Count + _landingSites.Count);
            for (int i = 0; i < _cities.Count; i++) list.Add(_cities[i]);
            for (int i = 0; i < _fleets.Count; i++) list.Add(_fleets[i]);
            for (int i = 0; i < _maritimeBeacons.Count; i++) list.Add(_maritimeBeacons[i]);
            for (int i = 0; i < _mobileCamps.Count; i++) list.Add(_mobileCamps[i]);
            for (int i = 0; i < _landingSites.Count; i++) list.Add(_landingSites[i]);
            return _buildVerticesCache = list;
        }
    }

    private TechnologyTree _technologyTree = new();

    /// <summary>
    /// Gets or sets the technology tree of the civilization.
    /// Pour le joueur, assigné depuis PrestigeState.TechnologyTree après désérialisation.
    /// Pour les NPCs, toujours un arbre vide.
    /// </summary>
    [JsonIgnore]
    public TechnologyTree TechnologyTree
    {
        get => _technologyTree;
        set
        {
            ModifierAggregator.Replace(_technologyTree, value);
            _technologyTree = value;
        }
    }

    [JsonIgnore]
    public ModifierAggregator ModifierAggregator { get; } = new();

    [JsonIgnore]
    public UniqueBuildingsModifierProvider UniqueBuildingsModifierProvider { get; } = new();

    public Civilization()
    {
        ModifierAggregator.Register(_technologyTree);
        ModifierAggregator.Register(UniqueBuildingsModifierProvider);
        ModifierAggregator.Changed += () =>
        {
            RecalculateStorageCapacity();
            _maxLevelCache.Clear();
            InvalidateAllCityMaxSoldiersCaches();
        };
    }

    /// <summary>
    /// Invalide le cache <see cref="City.MaxSoldiers"/> de toutes les villes de la civilisation.
    /// Appelé automatiquement via <see cref="ModifierAggregator"/>.Changed dès qu'un provider de
    /// modificateurs change (recherche complétée, vertex de prestige acheté, bâtiments uniques…) —
    /// nécessaire au cas où un futur modifier affecterait GetMaxSoldiersBonus par bâtiment.
    /// </summary>
    public void InvalidateAllCityMaxSoldiersCaches()
    {
        foreach (var city in _cities)
            city.InvalidateMaxSoldiersCache();
    }

    private readonly Dictionary<BuildingType, int> _maxLevelCache = new();

    /// <summary>
    /// Cache le niveau max par type de bâtiment (BuildingController.GetMaxLevel est sur le chemin chaud
    /// de l'autoplay/des tests). Invalidé automatiquement via ModifierAggregator.Changed dès qu'un
    /// provider de modificateurs change (recherche, prestige, bâtiments uniques…).
    /// </summary>
    /// <summary>
    /// Variante sans allocation de <see cref="GetCachedMaxLevel"/> : le calcul est laissé à l'appelant,
    /// qui n'a donc pas à allouer une closure à chaque appel — y compris quand le cache répond. Lu sur
    /// un chemin très chaud (l'autoplayer teste le niveau max de chaque type de bâtiment de chaque
    /// ville à chaque passe de stratégie).
    /// </summary>
    public bool TryGetCachedMaxLevel(BuildingType type, out int cached)
        => _maxLevelCache.TryGetValue(type, out cached);

    /// <summary>Mémorise le niveau max calculé par l'appelant de <see cref="TryGetCachedMaxLevel"/>.</summary>
    public void SetCachedMaxLevel(BuildingType type, int value) => _maxLevelCache[type] = value;

    public int GetCachedMaxLevel(BuildingType type, Func<int> compute)
    {
        if (_maxLevelCache.TryGetValue(type, out int cached))
            return cached;
        int value = compute();
        _maxLevelCache[type] = value;
        return value;
    }

    /// <summary>
    /// Ajoute un provider supplémentaire à l'agrégateur (prestige, NPC bonuses…).
    /// Les providers par défaut (TechnologyTree, UniqueBuildingsModifierProvider) sont toujours présents.
    /// </summary>
    public void AddCustomAggregator(IModifierProvider provider)
        => ModifierAggregator.Register(provider);

    /// <summary>Emplacement unique du jeu de modificateurs PNJ — voir <see cref="SetNpcModifiers"/>.</summary>
    private IModifierProvider? _npcModifierProvider;

    /// <summary>
    /// Installe le jeu de modificateurs PNJ de cette civilisation, <b>en remplaçant</b> celui déjà en
    /// place plutôt qu'en s'y ajoutant.
    ///
    /// <para>C'est ce qui garantit qu'une civilisation PNJ n'en porte jamais deux. Le placeur en pose
    /// un pendant la génération de l'île et <c>MainGameController.SetupModifierAggregators</c> en
    /// repose un à chaque <c>SetGame</c> : avec un simple <see cref="AddCustomAggregator"/>, les deux
    /// s'empilaient — <see cref="ModifierAggregator.Register"/> ne dédoublonne que par instance, et
    /// ce sont deux instances distinctes. Le doublon s'appliquait de surcroît <i>après</i> les malus
    /// de récolte, donc sans être réduit par eux.</para>
    ///
    /// <para>Le remplacement conserve la position du provider dans l'agrégateur, donc l'ordre
    /// d'application des modificateurs (voir <see cref="ModifierAggregator.Replace"/>).</para>
    /// </summary>
    public void SetNpcModifiers(IModifierProvider provider)
    {
        if (_npcModifierProvider == null || !ModifierAggregator.Replace(_npcModifierProvider, provider))
            ModifierAggregator.Register(provider);
        _npcModifierProvider = provider;
    }

    /// <summary>
    /// Reconstruit le cache des modifiers issus des bâtiments IUniqueBuilding de toutes les villes,
    /// plus le bâtiment unique permanent accordé par l'Ascension le cas échéant (voir
    /// <see cref="SetAscensionGrantedUniqueBuildings"/>) — ceux-ci ne vivent dans aucune ville, il faut
    /// donc aller le chercher explicitement dans le cache. À appeler après construction/amélioration
    /// d'un IUniqueBuilding, ou après la perte d'une ville.
    /// </summary>
    public void RebuildUniqueBuildingsModifiers()
    {
        var modifiers = _cities
            .SelectMany(c => c.Buildings)
            .OfType<IUniqueBuilding>()
            .SelectMany(b => b.GetUniqueBuildingModifiers());

        foreach (var grantedType in _ascensionGrantedUniqueBuildings)
        {
            if (GetUniqueBuilding(grantedType) is IUniqueBuilding grantedUnique)
                modifiers = modifiers.Concat(grantedUnique.GetUniqueBuildingModifiers());
        }

        UniqueBuildingsModifierProvider.Rebuild(modifiers);
    }

    private readonly Dictionary<BuildingType, Building> _uniqueBuildingCache = new();

    /// <summary>
    /// Types de bâtiments uniques accordés en permanence par l'Ascension (voir AscensionState.
    /// PermanentUniqueBuildings), un par emplacement débloqué. Contrairement aux bâtiments uniques
    /// construits normalement, ceux-ci n'occupent jamais d'emplacement dans une ville — ils vivent
    /// uniquement dans <see cref="_uniqueBuildingCache"/> (voir <see cref="RebuildUniqueBuildingCache"/>),
    /// ce qui les rend increvables (aucune destruction de ville ne peut les faire disparaître).
    /// </summary>
    private readonly HashSet<BuildingType> _ascensionGrantedUniqueBuildings = new();

    /// <summary>
    /// Instances des bâtiments uniques permanents de l'Ascension, conservées d'un appel à l'autre de
    /// <see cref="RebuildUniqueBuildingCache"/> — voir ce dernier pour la raison : recréer l'instance à
    /// chaque appel réinitialise silencieusement l'état runtime de l'automatisation de guilde
    /// (<c>LastRoadBuildTick</c>/<c>LastOutpostBuildTick</c>/<c>LastTownHallBuildTick</c> sur
    /// BuildersGuild) porté par cette instance. Vidé uniquement quand la liste accordée change (nouvelle
    /// île, voir <see cref="SetAscensionGrantedUniqueBuildings"/>) : les types retirés n'ont plus lieu
    /// d'exister, les types ajoutés doivent repartir d'une instance neuve.
    /// </summary>
    private readonly Dictionary<BuildingType, Building> _ascensionGrantedBuildingInstances = new();

    /// <summary>
    /// Retourne l'instance du bâtiment unique de ce type construit dans une ville de la civilisation,
    /// ou null s'il n'existe pas. Sert à éviter de parcourir toutes les villes/bâtiments à chaque appel
    /// (ex: automatisations des guildes). Le cache est reconstruit à chaque <see cref="City.BuildingsChanged"/>
    /// (voir <see cref="OnCityBuildingsChanged"/>) et à l'ajout d'une ville (voir <see cref="AddCity"/>),
    /// quel que soit le chemin d'ajout du bâtiment — <see cref="RegisterUniqueBuildingInCache"/> reste un
    /// raccourci ponctuel utilisé par <c>BuildingController.BuildBuilding</c>.
    /// </summary>
    public Building? GetUniqueBuilding(BuildingType type)
        => _uniqueBuildingCache.TryGetValue(type, out var building) ? building : null;

    /// <summary>
    /// Vrai si ce type de bâtiment unique est accordé en permanence par l'Ascension (voir
    /// <see cref="_ascensionGrantedUniqueBuildings"/>) — il ne vit dans aucune ville, donc
    /// n'apparaîtra jamais comme "bâti ailleurs" : l'UI doit l'afficher différemment (badge
    /// "Perm") plutôt qu'un bouton "aller à la ville" qui ne mènerait nulle part.
    /// </summary>
    public bool IsAscensionGrantedUniqueBuilding(BuildingType type)
        => _ascensionGrantedUniqueBuildings.Contains(type);

    /// <summary>
    /// Enregistre un bâtiment unique nouvellement construit dans le cache, sans reparcourir les villes.
    /// </summary>
    public void RegisterUniqueBuildingInCache(Building building)
    {
        if (building.IsUnique)
            _uniqueBuildingCache[building.Type] = building;
    }

    /// <summary>
    /// Enregistre les bâtiments uniques permanents accordés par l'Ascension pour cette île — voir
    /// AscensionController.ApplyPermanentUniqueBuildingToCivilization, appelé à chaque début d'île.
    /// Marque immédiatement chaque type comme "déjà construit" (bloque sa construction manuelle,
    /// comme tout bâtiment unique) et fait apparaître ses bonus civ-wide.
    /// </summary>
    public void SetAscensionGrantedUniqueBuildings(IEnumerable<BuildingType> types)
    {
        _ascensionGrantedUniqueBuildings.Clear();
        _ascensionGrantedUniqueBuildings.UnionWith(types);
        // Nouvelle île : les instances de l'île précédente n'ont plus lieu d'exister (voir doc du
        // dictionnaire) — repartir propre plutôt que de laisser une instance orpheline d'un type qui
        // ne serait plus accordé continuer à occuper de la mémoire pour rien.
        _ascensionGrantedBuildingInstances.Clear();
        RebuildUniqueBuildingCache();
        RebuildUniqueBuildingsModifiers();
    }

    /// <summary>
    /// Reconstruit entièrement le cache des bâtiments uniques à partir des villes actuelles, plus les
    /// bâtiments uniques permanents accordés par l'Ascension le cas échéant (voir
    /// <see cref="SetAscensionGrantedUniqueBuildings"/>). À appeler après la perte d'une ville
    /// (destruction) ou après chargement d'une sauvegarde.
    ///
    /// <para>Appelé très fréquemment en pratique — à chaque changement de bâtiments de n'importe
    /// quelle ville de la civilisation (voir <see cref="OnCityBuildingsChanged"/>), pas seulement à
    /// l'ajout/retrait d'une ville. Pour un bâtiment unique accordé par l'Ascension (aucune ville ne le
    /// porte), l'instance est donc réutilisée depuis <see cref="_ascensionGrantedBuildingInstances"/>
    /// plutôt que recréée à chaque appel : une nouvelle instance perdrait silencieusement l'état
    /// runtime que l'automatisation de guilde stocke dessus (<c>LastRoadBuildTick</c>,
    /// <c>LastOutpostBuildTick</c>, <c>LastTownHallBuildTick</c> sur BuildersGuild) — chaque
    /// construction/amélioration ailleurs dans la civilisation aurait alors réarmé son cooldown avant
    /// qu'il n'ait pu s'écouler, l'automatisation ne progressant plus qu'au hasard des créneaux libres
    /// entre deux de ces changements plutôt qu'à son rythme normal.</para>
    /// </summary>
    public void RebuildUniqueBuildingCache()
    {
        _uniqueBuildingCache.Clear();
        foreach (var building in _cities.SelectMany(c => c.Buildings))
            if (building.IsUnique)
                _uniqueBuildingCache[building.Type] = building;

        foreach (var grantedType in _ascensionGrantedUniqueBuildings)
        {
            if (_uniqueBuildingCache.ContainsKey(grantedType))
                continue;

            if (!_ascensionGrantedBuildingInstances.TryGetValue(grantedType, out var granted))
            {
                if (BuildingFactory.Create(grantedType) is not { } created)
                    continue;

                // Sans ville pour les faire monter de niveau via les modifiers dynamiques (recherche
                // faite, vertex de prestige achetés, race actuellement jouée...), on les accorde
                // d'emblée au niveau max absolu, en dur par bâtiment (voir Building.GetAbsoluteMaxLevel).
                created.Level = Math.Max(1, created.GetAbsoluteMaxLevel());
                granted = created;
                _ascensionGrantedBuildingInstances[grantedType] = granted;
            }

            _uniqueBuildingCache[grantedType] = granted;
            if (!_uniqueBuildings.Contains(grantedType))
                _uniqueBuildings.Add(grantedType);
        }
    }

    /// <summary>
    /// Research point production speed multiplier (Library/Laboratory generation). 1.0 = normal speed.
    /// Inclut le bonus par Tour de Mages (RESEARCH_SPEED_PER_MAGE_TOWER, ex. Distillation Magique), agrégé
    /// puis multiplié par le nombre de Tours de Mages construites (niveau ≥ 1), avant les autres modificateurs.
    /// </summary>
    [JsonIgnore]
    public double ResearchProductionSpeed
    {
        get
        {
            double perMageTower = ModifierAggregator.ApplyModifiers(ECategory.RESEARCH_SPEED_PER_MAGE_TOWER, "", 0.0);
            int mageTowerCount = perMageTower > 0
                ? _cities.Sum(c => c.Buildings.Count(b => b.Type == BuildingType.MageTower && b.Level >= 1))
                : 0;
            return ModifierAggregator.ApplyModifiers(ECategory.RESEARCH_PRODUCTION_SPEED, "", 1.0 + perMageTower * mageTowerCount);
        }
    }

    /// <summary>
    /// Research point investment speed multiplier (consumption of stored points into active research). 1.0 = normal speed.
    /// </summary>
    [JsonIgnore]
    public double ResearchInvestmentSpeed => ModifierAggregator.ApplyModifiers(ECategory.RESEARCH_INVESTMENT_SPEED, "", 1.0);

    /// <summary>
    /// Unit production speed multiplier. 1.0 = normal speed.
    /// </summary>
    [JsonIgnore]
    public double UnitProductionSpeed => ModifierAggregator.ApplyModifiers(ECategory.UNIT_PRODUCTION_SPEED, "", 1.0);

    /// <summary>
    /// Research cost reduction fraction (0.0 = no reduction, 0.1 = 10% cheaper).
    /// </summary>
    [JsonIgnore]
    public double ResearchCostReduction => ModifierAggregator.ApplyModifiers(ECategory.RESEARCH_COST_REDUCTION, "", 0.0);

    /// <summary>
    /// Additive bonus applied to the base 50% refund rate when canceling active research. 0.0 = no bonus.
    /// </summary>
    [JsonIgnore]
    public double ResearchCancelRefundBonus => ModifierAggregator.ApplyModifiers(ECategory.RESEARCH_CANCEL_REFUND_BONUS, "", 0.0);

    /// <summary>
    /// Fraction de réduction de la croissance du coût des recherches répétables (0.0 = coût ×2 par
    /// relance, 0.5 = ×1,5). Voir ResearchController.GetRepeatCostFactor.
    /// </summary>
    [JsonIgnore]
    public double RepeatableResearchScalingReduction => ModifierAggregator.ApplyModifiers(ECategory.REPEATABLE_RESEARCH_SCALING_REDUCTION, "", 0.0);

    /// <summary>
    /// Wonder level-up cost reduction fraction (0.0 = no reduction, 0.1 = 10% cheaper). Applies only to the Wonder.
    /// </summary>
    [JsonIgnore]
    public double WonderCostReduction => ModifierAggregator.ApplyModifiers(ECategory.WONDER_COST_REDUCTION, "", 0.0);

    /// <summary>
    /// Divine Bones Purification cost reduction fraction (0.0 = no reduction, 0.05 = 5% cheaper).
    /// Applies to both the Crystal and research point costs.
    /// </summary>
    [JsonIgnore]
    public double DivineBonesCostReduction => ModifierAggregator.ApplyModifiers(ECategory.DIVINE_BONES_COST_REDUCTION, "", 0.0);

    /// <summary>
    /// Monument investment cost reduction fraction (0.0 = no reduction, 0.25 = 25% cheaper). Applies
    /// to all Monuments (Wonder, DeepestMine, CorruptionSpire, AbyssGate, Necropolis, Observatory,
    /// PandemoniumGate, GreatLighthouse, DivineBones), on top of any Monument-specific reduction.
    /// </summary>
    [JsonIgnore]
    public double MonumentCostReduction => ModifierAggregator.ApplyModifiers(ECategory.MONUMENT_COST_REDUCTION, "", 0.0);

    /// <summary>
    /// Nombre d'essences divines (GodState.DivineEssence) conservées lors d'un prestige, au lieu
    /// d'être remises à zéro (voir PrestigeController.PerformPrestige).
    /// </summary>
    [JsonIgnore]
    public int DivineEssenceKeptOnPrestige => ModifierAggregator.ApplyModifiers(ECategory.DIVINE_ESSENCE_KEPT_ON_PRESTIGE, "", 0);

    /// <summary>
    /// Volcanic eruption damage reduction fraction (0.0 = no reduction, 0.5 = 50% less damage to cities).
    /// </summary>
    [JsonIgnore]
    public double VolcanoDamageReduction => ModifierAggregator.ApplyModifiers(ECategory.VOLCANO_DAMAGE_REDUCTION, "", 0.0);

    /// <summary>
    /// Investment speed multiplier (base 1.0) applied to a resource's investment amount when its stock
    /// exceeds 50% of its max capacity. Affects the Wonder, the Deepest Mine and the Corruption Spire.
    /// </summary>
    [JsonIgnore]
    public double InvestmentSpeedHighStockBonus => ModifierAggregator.ApplyModifiers(ECategory.INVESTMENT_SPEED_HIGH_STOCK_BONUS, "", 1.0);

    public int GetHarvestProductionBonus(string buildingType) =>
        ModifierAggregator.ApplyModifiers(ECategory.HARVEST_PRODUCTION_BONUS, buildingType, 0);

    public int ForgeDoubleHarvestBonus =>
        ModifierAggregator.ApplyModifiers(ECategory.FORGE_DOUBLE_HARVEST_BONUS, "", 0);

    /// <summary>
    /// Chance (en %) qu'une mine produise de l'or en bonus (en plus du minerai) lors d'une récolte automatique.
    /// </summary>
    [JsonIgnore]
    public int MineGoldChancePercent => ModifierAggregator.ApplyModifiers(ECategory.MINE_GOLD_CHANCE_PERCENT, "", 0);

    /// <summary>Multiplicateur appliqué à la quantité d'or gagnée à chaque déclenchement de MineGoldChancePercent.</summary>
    [JsonIgnore]
    public double MineGoldProductionMultiplier => ModifierAggregator.ApplyModifiers(ECategory.MINE_GOLD_PRODUCTION_MULTIPLIER, "", 1.0);

    /// <summary>Chance (en %) de produire une Arme/Armure en Acier supplémentaire lors d'une production de Forge d'Armes/d'Armures.</summary>
    [JsonIgnore]
    public int SmithDoubleProdChancePercent => ModifierAggregator.ApplyModifiers(ECategory.SMITH_DOUBLE_PROD_CHANCE_PERCENT, "", 0);

    [JsonIgnore]
    public int LaboratoryResearchBonus => ModifierAggregator.ApplyModifiers(ECategory.BUILDING_PRODUCTION, "Laboratory", 0);

    [JsonIgnore]
    public double CityDefenseRegenSpeed => ModifierAggregator.ApplyModifiers(ECategory.CITY_DEFENSE_REGEN_SPEED, "", 1.0);

    [JsonIgnore]
    public int CityMaxSoldiersBonus => ModifierAggregator.ApplyModifiers(ECategory.CITY_MAX_SOLDIERS_BONUS, "", 0);

    /// <summary>
    /// Liste des ressources d�tenues par la civilisation.
    /// </summary>
    // Resources are stored as a map from Resource -> quantity.
    // Made private: access should be done through AddResource/RemoveResource and GetResourceQuantity.
    private readonly Dictionary<Resource, int> _resources = new();

    // Expose resources for serialization. The public property is annotated so System.Text.Json
    // will include it during export/import. The private setter maps values back to the private
    // dictionary to preserve encapsulation for runtime access.
    [JsonInclude]
    public Dictionary<Resource, int> Resources
    {
        get => _resources;
        private set
        {
            _resources.Clear();
            if (value == null) return;
            foreach (var kv in value)
            {
                _resources[kv.Key] = kv.Value;
            }
        }
    }

    /// <summary>
    /// Adds the given quantity of a resource to the civilization's stock.
    /// </summary>
    public void AddResource(Resource resource, int quantity)
    {
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be positive.");
        var max = GetResourceMaxQuantity(resource);

        if (_resources.TryGetValue(resource, out var current))
        {
            _resources[resource] = Math.Min(current + quantity, max);
        }
        else
        {
            _resources[resource] = Math.Min(quantity, max);
        }
    }

    /// <summary>
    /// Removes the given quantity of a resource from the civilization's stock.
    /// Throws InvalidOperationException if not enough resource is available.
    /// </summary>
    public void RemoveResource(Resource resource, int quantity)
    {
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be positive.");

        if (!_resources.TryGetValue(resource, out var current) || current < quantity)
            throw new InvalidOperationException($"Not enough {resource} to remove: requested {quantity}, available {current}.");

        var remaining = current - quantity;
        if (remaining > 0)
            _resources[resource] = remaining;
        else
            _resources.Remove(resource);
    }

    /// <summary>
    /// Gets the current quantity of the given resource (0 if none).
    /// </summary>
    public int GetResourceQuantity(Resource resource)
    {
        return _resources.TryGetValue(resource, out var q) ? q : 0;
    }

    /// <summary>
    /// Cache de la capacité de stockage (ressources de base / avancées), recalculé par
    /// <see cref="RecalculateStorageCapacity"/> à chaque construction/destruction de bâtiment,
    /// changement de l'agrégateur de modificateurs, ou ajout/retrait de ville.
    /// </summary>
    [JsonIgnore]
    public int StorageCapacityBasic { get; private set; }

    [JsonIgnore]
    public int StorageCapacityAdvanced { get; private set; }

    /// <summary>
    /// Cache d'Achat Automatique (vertex de prestige Achat Automatique + au moins un Marché niv.4+),
    /// recalculé par <see cref="RecalculateStorageCapacity"/> aux mêmes points de mutation que
    /// <see cref="StorageCapacityBasic"/> (TradeController.IsAutoBuyUnlocked est sur le chemin chaud
    /// de la vente de ressources en autoplay).
    /// </summary>
    [JsonIgnore]
    public bool AutoBuyUnlockedCache { get; private set; }

    /// <summary>Niveau de Marché minimal ouvrant droit à l'Achat Automatique.</summary>
    private const int AutoBuyMinMarketLevel = 4;

    /// <summary>
    /// Recalcule <see cref="StorageCapacityBasic"/>, <see cref="StorageCapacityAdvanced"/> et
    /// <see cref="AutoBuyUnlockedCache"/> depuis les bâtiments des villes et les modificateurs
    /// actifs. À appeler après toute construction/destruction de bâtiment, tout ajout/retrait de
    /// ville et tout changement de l'agrégateur de modificateurs.
    ///
    /// <para>Vivait auparavant dans <c>BuildingController</c>, ce qui obligeait le modèle à remonter
    /// vers la couche contrôleur — jusque dans le constructeur de cette classe. C'est un calcul pur
    /// sur l'état de la civilisation, sans rien de la logique de contrôle.</para>
    /// </summary>
    public void RecalculateStorageCapacity()
    {
        int basic = 10 * _cities.Count;
        int advanced = 0;
        bool hasHighLevelMarket = false;

        // Boucles indexées : City.Buildings est typée IReadOnlyList, dont l'énumérateur est boxé à
        // chaque foreach. Ce recalcul est déclenché par chaque construction.
        for (int c = 0; c < _cities.Count; c++)
        {
            var buildings = _cities[c].Buildings;
            for (int b = 0; b < buildings.Count; b++)
            {
                var building = buildings[b];
                basic += building.GetStorageCapacityBonusBasic();
                advanced += building.GetStorageCapacityBonusAdvanced();
                if (building.Type == BuildingType.Market && building.Level >= AutoBuyMinMarketLevel)
                    hasHighLevelMarket = true;
            }
        }

        basic += ModifierAggregator.ApplyModifiers(ECategory.STORAGE_CAPACITY_BASIC, "", 0);
        advanced += ModifierAggregator.ApplyModifiers(ECategory.STORAGE_CAPACITY_ADVANCED, "", 0);

        double multiplier = ModifierAggregator.ApplyModifiers(ECategory.STORAGE_CAPACITY_MULTIPLIER, "", 1.0);
        basic = (int)(basic * multiplier);
        advanced = (int)(advanced * multiplier);

        StorageCapacityBasic = basic;
        StorageCapacityAdvanced = advanced;
        AutoBuyUnlockedCache = hasHighLevelMarket
            && ModifierAggregator.HasModifier(ECategory.UNLOCK_AUTO_BUY_TRADE);
    }

    /// <summary>
    /// Force la capacité de stockage sans passer par <see cref="RecalculateStorageCapacity"/>.
    /// Point d'injection des tests, qui ont besoin d'une civilisation capable de stocker sans avoir
    /// à lui bâtir des Entrepôts ; le prochain recalcul écrase ces valeurs.
    /// </summary>
    public void SetStorageCapacityCache(int basic, int advanced)
    {
        StorageCapacityBasic = basic;
        StorageCapacityAdvanced = advanced;
    }


    [NonSerialized]
    private bool? _hasMarket;

    /// <summary>
    /// Vrai si au moins une ville possède un Marché — condition du commerce (voir
    /// <c>TradeController.IsTradeAvailable</c>). Calculé à la demande puis conservé
    /// jusqu'à la prochaine mutation de bâtiments, signalée par <see cref="City.BuildingsChanged"/>.
    ///
    /// <para>Une première version de ce cache, recalculée depuis
    /// <see cref="RecalculateStorageCapacity"/>, était fausse : plusieurs chemins
    /// ajoutent des bâtiments sans passer par le contrôleur, et les tests de commerce l'ont
    /// immédiatement montré. C'est ce qui a motivé l'encapsulation de <see cref="City.Buildings"/> —
    /// sans elle, aucun cache dérivé des bâtiments ne peut être correct.</para>
    /// </summary>
    [JsonIgnore]
    public bool HasMarket
    {
        get
        {
            if (_hasMarket is { } cached) return cached;

            bool found = false;
            for (int i = 0; i < _cities.Count && !found; i++)
                found = _cities[i].FindBuilding(BuildingType.Market) != null;

            _hasMarket = found;
            return found;
        }
    }

    /// <summary>
    /// Compteur incrémenté à chaque <see cref="InvalidateBuildingDerivedCaches"/> — permet à un
    /// appelant externe (ex. BuildingController.TickGuildAutomation) de savoir, par simple
    /// comparaison d'entier, si quelque chose a changé côté bâtiments depuis son dernier passage,
    /// sans avoir à s'abonner/désabonner à un événement (risque de fuite si la civ est créée puis
    /// détruite en cours de partie, ex. AutoExtendController). Non sérialisé : redémarre à 0 à
    /// chaque chargement, ce qui est sans effet puisque les caches qui le consultent redémarrent
    /// vides aussi.
    /// </summary>
    [JsonIgnore]
    public int BuildingsVersion { get; private set; }

    /// <summary>
    /// Invalide les caches dérivés des bâtiments des villes. Appelé automatiquement à toute mutation
    /// de bâtiments et à tout ajout/retrait de ville ; à appeler manuellement après un changement de
    /// <c>Building.Level</c>, que la liste des bâtiments ne reflète pas.
    /// </summary>
    public void InvalidateBuildingDerivedCaches()
    {
        _hasMarket = null;
        _citiesByBuildingType = null;
        BuildingsVersion++;
    }

    [JsonIgnore]
    private Dictionary<BuildingType, List<City>>? _citiesByBuildingType;

    private static readonly List<City> EmptyCities = new();

    /// <summary>
    /// Villes de la civilisation possédant un bâtiment de ce type, dans l'ordre de <see cref="Cities"/>.
    ///
    /// <para>Remplace les balayages « toutes les villes » des contrôleurs qui n'ont affaire qu'à un
    /// seul type de bâtiment : la production des Fonderies, Forges d'Armes et d'Armures, Marchés,
    /// Ports, Huttes d'Alchimie et Casernes fait chacune sa passe complète à <b>chaque</b> événement
    /// d'horloge, alors qu'en fin de partie une poignée de villes seulement portent le bâtiment
    /// concerné. Le profilage donnait ces six passes à ~12 % du budget d'image, pour ne rien faire la
    /// plupart du temps.</para>
    ///
    /// <para><b>L'ordre doit rester celui de <see cref="Cities"/></b> : plusieurs de ces passes
    /// consomment le PRNG (choix de la ressource d'un Port, doublement d'une Forge), donc le
    /// déterminisme de la partie en dépend.</para>
    ///
    /// <para>Retourne volontairement la <see cref="List{T}"/> concrète : les appelants bouclent
    /// dessus à chaque tick, et via <c>IReadOnlyList</c> l'indexeur devient un appel d'interface et
    /// <c>foreach</c> boxe l'énumérateur — voir la note correspondante dans CLAUDE.md.</para>
    /// </summary>
    public List<City> GetCitiesWith(BuildingType type)
    {
        if (_citiesByBuildingType == null)
        {
            var index = new Dictionary<BuildingType, List<City>>();
            for (int i = 0; i < _cities.Count; i++)
            {
                var buildings = _cities[i].Buildings;
                for (int b = 0; b < buildings.Count; b++)
                {
                    var buildingType = buildings[b].Type;
                    if (!index.TryGetValue(buildingType, out var list))
                        index[buildingType] = list = new List<City>();
                    // Une ville ne porte jamais deux bâtiments du même type, mais on ne s'appuie pas
                    // dessus : un doublon ferait produire la ville deux fois.
                    if (list.Count == 0 || !ReferenceEquals(list[^1], _cities[i]))
                        list.Add(_cities[i]);
                }
            }
            _citiesByBuildingType = index;
        }

        return _citiesByBuildingType.TryGetValue(type, out var cities) ? cities : EmptyCities;
    }


    public int GetResourceMaxQuantity(Resource resource)
    {
        if (ResourceUtils.ConsumableResources.Contains(resource))
        {
            return StorageCapacityAdvanced / 2;
        }

        bool isBasic = ResourceUtils.BasicResources.Contains(resource);
        bool isBasicStorage = isBasic || resource == Resource.Gold;
        return isBasicStorage ? StorageCapacityBasic : StorageCapacityAdvanced;
    }

    public void TrimResourcesToMax()
    {
        foreach (var resource in _resources.Keys.ToList())
        {
            var max = GetResourceMaxQuantity(resource);
            if (_resources[resource] > max)
                _resources[resource] = max;
        }
    }

    private List<BuildingType> _uniqueBuildings = new();

    /// <summary>
    /// Bâtiments uniques construits par cette civilisation sur l'île courante.
    /// Empêche de construire deux fois le même bâtiment unique.
    /// </summary>
    [JsonIgnore]
    public IReadOnlyList<BuildingType> UniqueBuildings => _uniqueBuildings;

    [JsonPropertyName("UniqueBuildings")]
    [JsonInclude]
    public List<BuildingType> UniqueBuildingsSerialized
    {
        get => _uniqueBuildings;
        private set => _uniqueBuildings = value ?? new();
    }

    public void AddUniqueBuilding(BuildingType type) => _uniqueBuildings.Add(type);

    public event EventHandler<Resource>? LowStock;

    internal void RaiseLowStock(Resource resource) => LowStock?.Invoke(this, resource);

    public bool CanPayResourceCost(ResourceSet cost)
    {
        foreach (var kvp in cost)
        {
            if (GetResourceQuantity(kvp.Key) < kvp.Value)
                return false;
        }
        return true;
    }
    public void PayResourceCost(ResourceSet cost)
    {
        foreach (var kvp in cost)
        {
            if (kvp.Value > 0)
                RemoveResource(kvp.Key, kvp.Value);
        }
    }
}