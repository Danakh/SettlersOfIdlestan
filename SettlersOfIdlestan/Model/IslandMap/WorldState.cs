using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model;
using System;
using System.Collections.Generic;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.IslandFeatures;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.Tasks;
using System.Text.Json.Serialization;
using System.Linq;

namespace SettlersOfIdlestan.Model.IslandMap;

/// <summary>
/// Represents the state of a world run, containing all layers and civilizations.
/// </summary>
[Serializable]
public class WorldState : IJsonOnDeserialized
{
    private Dictionary<int, LayerState> _layers = new();

    /// <summary>
    /// All map layers indexed by Z coordinate. Use GetMapForZ(z) to retrieve a layer's map.
    /// </summary>
    public Dictionary<int, LayerState> Layers
    {
        get => _layers;
        set => _layers = value ?? new Dictionary<int, LayerState>();
    }

    public void AddLayer(int z, LayerState layer) => _layers[z] = layer;
    public void RemoveLayer(int z) => _layers.Remove(z);

    /// <summary>
    /// Z-coordinate of the layer currently displayed. Not persisted.
    /// </summary>
    [JsonIgnore]
    public int CurrentViewedLayer { get; set; } = IslandMap.SurfaceLayer;

    public int WorldId { get; set; }

    /// <summary>
    /// True une fois que le joueur a affiché la vue Inframonde au moins une fois sur cette île
    /// (voir OverlayRenderer.ApplyLayerForActiveTab). Sert à faire clignoter l'onglet Inframonde
    /// tant qu'il n'a pas encore été consulté après le creusement de la Mine Profonde.
    /// </summary>
    public bool HasVisitedUnderworld { get; set; }

    /// <summary>
    /// Tick de simulation au moment où ce monde a démarré (pour calculer la durée de jeu).
    /// </summary>
    public long StartTick { get; set; } = 0;

    private List<SettlersOfIdlestan.Model.Civilization.Civilization> _civilizations;

    /// <summary>
    /// Civilisations présentes sur ce monde — <b>lecture seule</b> ; utiliser
    /// <see cref="AddCivilization"/> / <see cref="RemoveCivilization"/> / <see cref="RemoveCivilizations"/>
    /// pour muter. C'est ce qui rend fiables les caches indexés par <c>Civilization.Index</c> : sans
    /// point d'accroche au retrait, ils gardaient indéfiniment les entrées des civilisations
    /// éliminées (voir <see cref="CivilizationRemoved"/>).
    ///
    /// <para>Rend volontairement la <see cref="List{T}"/> concrète, et non un <c>IReadOnlyList</c>
    /// comme <c>Civilization.Cities</c> : c'est la collection la plus parcourue du jeu — une
    /// vingtaine de contrôleurs font un <c>foreach</c> dessus à <b>chaque</b> tick — et via
    /// l'interface le <c>foreach</c> boxe l'énumérateur de structure de la liste (voir la note
    /// correspondante dans CLAUDE.md). Le contrat de lecture seule est donc tenu par convention ici,
    /// et non par le type.</para>
    /// </summary>
    [JsonIgnore]
    public List<SettlersOfIdlestan.Model.Civilization.Civilization> Civilizations => _civilizations;

    // Utilisé uniquement par la sérialisation JSON : conserve le nom de propriété historique
    // "Civilizations" dans les sauvegardes.
    [JsonPropertyName("Civilizations")]
    [JsonInclude]
    public List<SettlersOfIdlestan.Model.Civilization.Civilization> CivilizationsSerialized
    {
        get => _civilizations;
        private set => _civilizations = value ?? new();
    }

    /// <summary>
    /// Déclenché après le retrait d'une civilisation, avec son <c>Index</c>. Les caches indexés par
    /// civilisation s'y raccrochent pour se purger — voir
    /// <c>MainGameController.OnCivilizationRemoved</c>, unique abonné, qui répercute sur tous les
    /// contrôleurs concernés.
    ///
    /// <para>Sans cette purge, ces caches ne faisaient que croître sur toute la durée d'une île :
    /// un nouvel index vaut toujours <c>Max(Index) + 1</c> (voir
    /// <c>AutoExtendController.SpawnAggressiveCivilization</c>), donc aucune entrée périmée n'était
    /// jamais réutilisée ni écrasée. Sur une partie longue où l'Inframonde régénère des PNJ à mesure
    /// que les précédents sont éliminés, plusieurs de ces caches retiennent en plus des objets lourds
    /// (villes, bâtiments, routes) d'un monde qui n'existe plus.</para>
    /// </summary>
    public event EventHandler<int>? CivilizationRemoved;

    public void AddCivilization(SettlersOfIdlestan.Model.Civilization.Civilization civilization)
        => _civilizations.Add(civilization);

    /// <summary>Retire une civilisation et purge les caches indexés sur elle. Faux si absente.</summary>
    public bool RemoveCivilization(SettlersOfIdlestan.Model.Civilization.Civilization civilization)
    {
        if (!_civilizations.Remove(civilization)) return false;
        RaiseCivilizationRemoved(civilization.Index);
        return true;
    }

    /// <summary>
    /// Retire toutes les civilisations correspondant au prédicat et purge les caches indexés sur
    /// chacune. Retourne le nombre de civilisations retirées.
    /// </summary>
    public int RemoveCivilizations(Predicate<SettlersOfIdlestan.Model.Civilization.Civilization> match)
    {
        // Les index sont relevés avant le retrait : après RemoveAll, les instances sont perdues.
        List<int>? removedIndices = null;
        for (int i = 0; i < _civilizations.Count; i++)
            if (match(_civilizations[i]))
                (removedIndices ??= new()).Add(_civilizations[i].Index);

        if (removedIndices == null) return 0;

        _civilizations.RemoveAll(match);
        foreach (int index in removedIndices)
            RaiseCivilizationRemoved(index);
        return removedIndices.Count;
    }

    private void RaiseCivilizationRemoved(int index)
    {
        _harvestLastTimesByCivilization.Remove(index);
        CivilizationRemoved?.Invoke(this, index);
    }

    /// <summary>
    /// Gets the player's civilization (always at index 0). Ignoré en JSON : c'est un raccourci vers
    /// Civilizations[0], pas une donnée distincte — le sérialiser dupliquait toute la civilisation
    /// du joueur dans la sauvegarde (cf. MainGameState.PrestigeState/CurrentWorldState, même bug).
    /// </summary>
    [JsonIgnore]
    public SettlersOfIdlestan.Model.Civilization.Civilization PlayerCivilization => Civilizations[0];

    [JsonIgnore]
    public WorldVisibility Visibility { get; }

    [JsonIgnore]
    public IslandMap CurrentViewedMap => GetMapForZ(
        Layers.ContainsKey(CurrentViewedLayer) ? CurrentViewedLayer : IslandMap.SurfaceLayer)!;

    /// <summary>
    /// Transient event log for the current session. Not persisted.
    /// </summary>
    [JsonIgnore]
    public GameEventLog EventLog { get; } = new();

    /// <summary>
    /// Compteur transient incrémenté à chaque changement de type de terrain (Marche de Dieu,
    /// conversion des déserts en Filons de Mithril...) — voir <see cref="NotifyTerrainChanged"/>.
    /// Sert de clé d'invalidation aux caches dépendant du terrain, comme celui de
    /// CityBuilderController.GetBuildableVertices (restrictions raciales de placement). Non persisté.
    /// </summary>
    [JsonIgnore]
    public int TerrainVersion { get; private set; }

    /// <summary>À appeler après toute mutation de HexTile.TerrainType sur une carte de ce monde.</summary>
    public void NotifyTerrainChanged() => TerrainVersion++;

    public WorldState(IslandMap map, List<SettlersOfIdlestan.Model.Civilization.Civilization> civilizations, int worldId)
    {
        Visibility = new WorldVisibility(this);
        _layers[IslandMap.SurfaceLayer] = new LayerState(map);
        _civilizations = civilizations ?? new();
        WorldId = worldId;
        _features = new List<IslandFeature>();
        PlunderCooldownDuration = new Dictionary<HexCoord, long>();
        Visibility.Recalculate();
    }

    /// <summary>
    /// Parameterless constructor for deserialization.
    /// </summary>
    [System.Text.Json.Serialization.JsonConstructor]
    public WorldState()
    {
        Visibility = new WorldVisibility(this);
        _layers[IslandMap.SurfaceLayer] = new LayerState();
        _civilizations = new List<SettlersOfIdlestan.Model.Civilization.Civilization>();
        _features = new List<IslandFeature>();
        PlunderCooldownDuration = new Dictionary<HexCoord, long>();
    }

    public void OnDeserialized()
    {
        RebuildFeatureCaches();
        Visibility.Recalculate();
    }

    public IslandMap? GetMapForZ(int z)
    {
        if (Layers.TryGetValue(z, out var layer))
            return layer.Map;

        return null;
    }

    public IslandMap? GetMapFor(HexCoord coord) => GetMapForZ(coord.Z);
    public IslandMap? GetMapFor(Vertex vertex) => GetMapForZ(vertex.Z);
    public IslandMap? GetMapFor(Edge edge) => GetMapForZ(edge.Z);

    public bool TryGetMapForZ(int z, out IslandMap map)
    {
        if (Layers.TryGetValue(z, out var layer))
        {
            map = layer.Map;
            return true;
        }

        map = null!;
        return false;
    }

    public IEnumerable<KeyValuePair<int, IslandMap>> GetMapsByZ()
    {
        foreach (var (z, layer) in Layers)
            yield return new KeyValuePair<int, IslandMap>(z, layer.Map);
    }

    private readonly Dictionary<int, Dictionary<HexCoord, long>> _harvestLastTimesByCivilization = new();

    /// <summary>
    /// Tick de simulation de la dernière récolte manuelle par civilisation et par hex (1 tick = 0.01 s).
    /// </summary>
    public IReadOnlyDictionary<int, Dictionary<HexCoord, long>> HarvestLastTimesByCivilization => _harvestLastTimesByCivilization;

    public Dictionary<HexCoord, long> GetOrCreateHarvestTimesForCiv(int civilizationIndex)
    {
        if (!_harvestLastTimesByCivilization.TryGetValue(civilizationIndex, out var perHex))
        {
            perHex = new Dictionary<HexCoord, long>();
            _harvestLastTimesByCivilization[civilizationIndex] = perHex;
        }
        return perHex;
    }

    private List<IslandFeature> _features;

    /// <summary>
    /// Toutes les features de l'île (Bandit, BanditHideout, TreasureTrove) — <b>lecture seule</b> ;
    /// utiliser <see cref="AddFeature"/> / <see cref="RemoveFeature"/> pour muter. C'est ce qui rend
    /// fiables les deux index dérivés (<see cref="_featuresByHex"/> et <see cref="_featuresByType"/>) :
    /// une insertion directe dans la liste les laisserait silencieusement incomplets, et une question
    /// aussi banale que « y a-t-il une Nécropole ? » répondrait non alors qu'elle existe.
    ///
    /// <para>Rend volontairement la <see cref="List{T}"/> concrète, pour la même raison que
    /// <see cref="Civilizations"/> : c'est une collection parcourue à chaque tick, et via un
    /// <c>IReadOnlyList</c> le <c>foreach</c> boxe l'énumérateur de structure. Le contrat de lecture
    /// seule est donc tenu par convention ici, et non par le type.</para>
    /// </summary>
    [JsonIgnore]
    public List<IslandFeature> Features => _features;

    // Utilisé uniquement par la sérialisation JSON : conserve le nom de propriété historique
    // "Features" dans les sauvegardes.
    [JsonPropertyName("Features")]
    [JsonInclude]
    public List<IslandFeature> FeaturesSerialized
    {
        get => _features;
        private set => _features = value ?? new();
    }

    [JsonIgnore]
    private Dictionary<HexCoord, List<IslandFeature>> _featuresByHex = new();

    /// <summary>
    /// Index type → features, peuplé pour <b>chaque</b> type de la chaîne d'héritage de la feature
    /// (jusqu'à <see cref="IslandFeature"/> incluse) : une <c>Necropolis</c> figure donc sous
    /// <c>Necropolis</c>, <c>Monument</c> et <c>IslandFeature</c>. C'est ce qui permet à
    /// <see cref="GetFeaturesOfType{T}"/> de répondre aussi bien pour un type concret que pour une
    /// base commune, là où un index par type exact ne servirait que le premier.
    ///
    /// <para>Sans lui, chaque « existe-t-il une feature de type X ? » balayait les
    /// <see cref="Features"/> — plusieurs milliers en fin de partie — en allouant au passage
    /// l'itérateur de <c>OfType</c>. Ces questions sont posées sur les chemins les plus chauds du jeu
    /// (prédicats de tâches réévalués à chaque récolte et à chaque vente, monuments interrogés à
    /// chaque tick) : c'était le premier poste d'un saut de temps d'une heure sur une grosse
    /// sauvegarde.</para>
    /// </summary>
    [JsonIgnore]
    private readonly Dictionary<Type, List<IslandFeature>> _featuresByType = new();

    private void AddToHexCache(IslandFeature feature)
    {
        if (!_featuresByHex.TryGetValue(feature.Position, out var list))
        {
            list = new List<IslandFeature>();
            _featuresByHex[feature.Position] = list;
        }
        list.Add(feature);
    }

    private void RemoveFromHexCache(IslandFeature feature)
    {
        if (_featuresByHex.TryGetValue(feature.Position, out var list))
        {
            list.Remove(feature);
            if (list.Count == 0)
                _featuresByHex.Remove(feature.Position);
        }
    }

    private void AddToTypeCache(IslandFeature feature)
    {
        for (Type? type = feature.GetType(); type != null && type != typeof(object); type = type.BaseType)
        {
            if (!_featuresByType.TryGetValue(type, out var list))
            {
                list = new List<IslandFeature>();
                _featuresByType[type] = list;
            }
            list.Add(feature);
        }
    }

    private void RemoveFromTypeCache(IslandFeature feature)
    {
        for (Type? type = feature.GetType(); type != null && type != typeof(object); type = type.BaseType)
            if (_featuresByType.TryGetValue(type, out var list))
                list.Remove(feature);
    }

    private void RebuildFeatureCaches()
    {
        _featuresByHex.Clear();
        _featuresByType.Clear();
        foreach (var f in Features)
        {
            AddToHexCache(f);
            AddToTypeCache(f);
        }
    }

    /// <summary>
    /// Features de type <typeparamref name="T"/> (concret ou base commune), sans allocation ni
    /// balayage — voir <see cref="_featuresByType"/>. Liste vide si aucune. Remplace
    /// <c>Features.OfType&lt;T&gt;()</c> sur les chemins appelés à chaque tick.
    ///
    /// <para>La liste rendue est celle de l'index, pas une copie : ne pas la muter, et ne pas
    /// l'énumérer pendant un <see cref="AddFeature"/> / <see cref="RemoveFeature"/> du même type.</para>
    /// </summary>
    public IReadOnlyList<IslandFeature> GetFeaturesOfType<T>() where T : IslandFeature
        => _featuresByType.TryGetValue(typeof(T), out var list) ? list : Array.Empty<IslandFeature>();

    /// <summary>Première feature de type <typeparamref name="T"/>, ou null — équivalent de <c>Features.OfType&lt;T&gt;().FirstOrDefault()</c>.</summary>
    public T? GetFirstFeature<T>() where T : IslandFeature
    {
        var list = GetFeaturesOfType<T>();
        return list.Count > 0 ? (T)list[0] : null;
    }

    /// <summary>Vrai si au moins une feature de type <typeparamref name="T"/> existe — équivalent de <c>Features.OfType&lt;T&gt;().Any()</c>.</summary>
    public bool HasFeature<T>() where T : IslandFeature => GetFeaturesOfType<T>().Count > 0;

    /// <summary>Retourne les features présentes sur cet hex (liste vide si aucune).</summary>
    public IReadOnlyList<IslandFeature> GetFeaturesAt(HexCoord hex)
        => _featuresByHex.TryGetValue(hex, out var list) ? list : Array.Empty<IslandFeature>();

    /// <summary>
    /// Première feature de type <typeparamref name="T"/> sur cet hex, ou null — équivalent sans
    /// allocation de <c>GetFeaturesAt(hex).OfType&lt;T&gt;().FirstOrDefault()</c>, dont la chaîne
    /// LINQ alloue son itérateur à chaque appel. La propagation Corruption/Dominion pose cette
    /// question quatre fois par source et par cycle, sur des milliers de sources.
    /// </summary>
    public T? GetFirstFeatureAt<T>(HexCoord hex) where T : IslandFeature
    {
        if (!_featuresByHex.TryGetValue(hex, out var list)) return null;
        for (int i = 0; i < list.Count; i++)
            if (list[i] is T match) return match;
        return null;
    }

    /// <summary>Retourne true si au moins une feature est présente sur cet hex.</summary>
    public bool HasFeaturesAt(HexCoord hex) => _featuresByHex.ContainsKey(hex);

    /// <summary>
    /// Somme des niveaux de Dominion présents sur les 3 hexs d'un emplacement. C'est la mesure
    /// commune à tous les bonus indexés sur l'emprise divine autour d'une ville : régénération de
    /// défense (Foi Protectrice, Bastion Consacré — voir MilitaryController.GetDefenseRegenSpeed) et
    /// vitesse de Fonderie (Creuset du Dominion — voir HarvestController.GetEffectiveSmelterCooldown).
    ///
    /// <para>Appelée à chaque événement d'horloge pour chaque emplacement : boucles indexées, pas de
    /// LINQ.</para>
    /// </summary>
    public int GetDominionLevelSumAround(Vertex position)
    {
        int total = 0;
        var hexes = position.GetHexes();
        for (int h = 0; h < hexes.Length; h++)
        {
            var features = GetFeaturesAt(hexes[h]);
            for (int f = 0; f < features.Count; f++)
                if (features[f] is Dominion dominion)
                    total += dominion.Level;
        }
        return total;
    }

    /// <summary>
    /// Vrai si cette feature fait toujours partie du monde. Passe par l'index par hexagone (quelques
    /// entrées) plutôt que par <c>Features.Contains</c>, qui balaie la liste entière : une passe qui
    /// vérifie ainsi chacune de ses sources — la propagation Corruption/Dominion le fait — était
    /// quadratique en nombre de features.
    ///
    /// <para>La position fait foi, et c'est <see cref="MoveFeature"/> seule qui la change : une
    /// feature déplacée autrement serait déjà invisible à <see cref="GetFeaturesAt"/>.</para>
    /// </summary>
    public bool ContainsFeature(IslandFeature feature)
        => _featuresByHex.TryGetValue(feature.Position, out var list) && list.Contains(feature);

    /// <summary>
    /// Retourne true si au moins une feature de cet hex empêche d'y bâtir un monument (voir
    /// <see cref="IslandFeature.BlocksMonumentPlacement"/>). Contrairement à <see cref="HasFeaturesAt"/>,
    /// ignore Corruption et Dominion qui se superposent au terrain sans l'occuper.
    /// </summary>
    public bool HasMonumentBlockingFeaturesAt(HexCoord hex)
        => GetFeaturesAt(hex).Any(f => f.BlocksMonumentPlacement);

    /// <summary>Déclenché quand une feature est ajoutée via AddFeature.</summary>
    public event EventHandler<IslandFeature>? FeatureAdded;

    /// <summary>Déclenché quand une feature est supprimée via RemoveFeature.</summary>
    public event EventHandler<IslandFeature>? FeatureRemoved;

    public void AddFeature(IslandFeature feature)
    {
        _features.Add(feature);
        AddToHexCache(feature);
        AddToTypeCache(feature);
        FeatureAdded?.Invoke(this, feature);
    }

    public bool RemoveFeature(IslandFeature feature)
    {
        if (!_features.Remove(feature)) return false;
        RemoveFromHexCache(feature);
        RemoveFromTypeCache(feature);
        FeatureRemoved?.Invoke(this, feature);
        return true;
    }

    public void MoveFeature(IslandFeature feature, HexCoord newPosition)
    {
        RemoveFromHexCache(feature);
        feature.Position = newPosition;
        AddToHexCache(feature);
    }

    private readonly Dictionary<HexCoord, long> _plunderCooldownUntil = new();

    /// <summary>
    /// Tick jusqu'auquel la récolte est bloquée sur un hex après le départ d'un monstre mobile.
    /// </summary>
    public IReadOnlyDictionary<HexCoord, long> PlunderCooldownUntil => _plunderCooldownUntil;

    public void SetPlunderCooldown(HexCoord hex, long untilTick) => _plunderCooldownUntil[hex] = untilTick;

    /// <summary>
    /// Durée totale du cooldown (en ticks) enregistrée au moment du départ du monstre.
    /// Utilisée pour calculer la progression de l'anneau dans les renderers.
    /// </summary>
    public Dictionary<HexCoord, long> PlunderCooldownDuration { get; set; }

    /// <summary>
    /// Player-controlled automation toggles. Cette propriété est réassignée à
    /// GodState.AutomationSettings (voir sa doc) par MainGameController.InitializeControllersForCurrentIsland
    /// à chaque île/prestige/ascension/chargement — l'instance elle-même est cross-prestige ET
    /// cross-ascension, cette valeur par défaut n'est qu'un filet de sécurité avant ce câblage.
    /// </summary>
    public AutomationSettings AutomationSettings { get; set; } = new();

    /// <summary>
    /// Statistiques du run en cours (réinitialisées à chaque prestige).
    /// </summary>
    public RunRecord RunRecord { get; set; } = new();

    /// <summary>
    /// État de la magie du joueur (rituels actifs). Réinitialisé à chaque prestige.
    /// </summary>
    public Magic.MagicState Magic { get; set; } = new();

    /// <summary>
    /// Tick du dernier cycle de nourrissage des soldats (toutes civilisations, global).
    /// </summary>
    public long LastSoldierFeedTick { get; set; } = 0;

    /// <summary>
    /// Civilisation d'index donné, ou null. Remplace le
    /// <c>Civilizations.FirstOrDefault(c => c.Index == …)</c> qui parsemait les contrôleurs : ce
    /// lambda capture son index, donc chaque appel allouait une classe de fermeture, un délégué et
    /// l'itérateur LINQ. C'est une des recherches les plus fréquentes du jeu (chaque bâtiment, chaque
    /// route, chaque combat commence par là), et l'échantillonnage d'allocations la plaçait autour de
    /// 4 % du total de la simulation.
    /// </summary>
    public SettlersOfIdlestan.Model.Civilization.Civilization? GetCivilization(int index)
    {
        var civilizations = Civilizations;
        for (int i = 0; i < civilizations.Count; i++)
            if (civilizations[i].Index == index)
                return civilizations[i];
        return null;
    }

    public IEnumerable<City> GetAllCities()
    {
        return Civilizations.SelectMany(c => c.Cities);
    }

    public City? FindCityAt(Vertex vertex)
    {
        return GetAllCities().FirstOrDefault(c => c.Position.Equals(vertex));
    }

    public IEnumerable<MaritimeBeacon> GetAllMaritimeBeacons()
    {
        return Civilizations.SelectMany(c => c.MaritimeBeacons);
    }

    public MaritimeBeacon? FindMaritimeBeaconAt(Vertex vertex)
    {
        return GetAllMaritimeBeacons().FirstOrDefault(b => b.Position.Equals(vertex));
    }

    public IEnumerable<WarFleet> GetAllFleets()
    {
        return Civilizations.SelectMany(c => c.Fleets);
    }

    public WarFleet? FindFleetAt(Vertex vertex)
    {
        return GetAllFleets().FirstOrDefault(f => f.Position.Equals(vertex));
    }

    public IEnumerable<MobileCamp> GetAllMobileCamps()
    {
        return Civilizations.SelectMany(c => c.MobileCamps);
    }

    public MobileCamp? FindMobileCampAt(Vertex vertex)
    {
        return GetAllMobileCamps().FirstOrDefault(c => c.Position.Equals(vertex));
    }

    /// <summary>Tous les emplacements militaires (villes, flottes et camps mobiles) de toutes les civilisations — voir IMilitaryVertex.</summary>
    public IEnumerable<IMilitaryVertex> GetAllMilitaryVertices()
    {
        return Civilizations.SelectMany(c => c.MilitaryVertices);
    }

    public IMilitaryVertex? FindMilitaryVertexAt(Vertex vertex)
    {
        return GetAllMilitaryVertices().FirstOrDefault(v => v.Position.Equals(vertex));
    }

    /// <summary>Tous les emplacements construits (villes, flottes, balises) de toutes les civilisations — voir IBuildVertex.</summary>
    public IEnumerable<IBuildVertex> GetAllBuildVertices()
    {
        return Civilizations.SelectMany(c => c.BuildVertices);
    }

    public IBuildVertex? FindBuildVertexAt(Vertex vertex)
    {
        return GetAllBuildVertices().FirstOrDefault(v => v.Position.Equals(vertex));
    }
}
