using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using System.Text.Json.Serialization;

namespace SettlersOfIdlestan.Model.Buildings;

/// <summary>
/// Activation state of a building. Serialized as a string enum.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ActivationStatus
{
    /// <summary>Ce bâtiment ne peut pas être activé/désactivé.</summary>
    NON_ACTIVABLE,
    /// <summary>Le bâtiment est désactivé (production suspendue).</summary>
    INACTIVE,
    /// <summary>Le bâtiment est actif.</summary>
    ACTIVE,
}

/// <summary>
/// Represents the type of a building.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BuildingType
{
    /// <summary>
    /// H�tel de ville - Permet l'am�lioration de la ville
    /// </summary>
    TownHall,
    /// <summary>
    /// Palissade - Protège la ville contre le vol des ressources par les bandits.
    /// </summary>
    Palisade,
    /// <summary>
    /// Port maritime - Permet la r�colte de nourriture et le commerce maritime
    /// </summary>
    Seaport,
    /// <summary>
    /// Scierie - Produit du bois
    /// </summary>
    Sawmill,
    /// <summary>
    /// Briqueterie - Produit de la brique
    /// </summary>
    Brickworks,
    /// <summary>
    /// Moulin - Produit du bl�
    /// </summary>
    Mill,
    /// <summary>
    /// Carrière - Produit de la pierre
    /// </summary>
    Quarry,
    /// <summary>
    /// March� - Permet le commerce
    /// </summary>
    Market,
    /// <summary>
    /// Mine - Automatise la récolte de minerai
    /// </summary>
    Mine,
    /// <summary>
    /// Entrep�t - Augmente la capacit� de stockage des ressources
    /// </summary>
    Warehouse,
    /// <summary>
    /// Forge - Crée des outils pour améliorer les autres bâtiments de production
    /// </summary>
    Forge,
    /// <summary>
    /// Biblioth�que - Augmente la production de connaissances et permet des am�liorations
    /// </summary>
    Library,
    /// <summary>
    /// Temple - Ajoute des points de civilisation
    /// </summary>
    Temple,
    /// <summary>
    /// Guilde des batisseurs - Permet l'automatisation de constructions. Disponible au niveau Capitale (4).
    /// Automatisation surface uniquement par défaut ; étendue à l'Inframonde par la recherche Cartographie Souterraine.
    /// </summary>
    BuildersGuild,
    /// <summary>
    /// Laboratoire - Permet la recherche avancée. Débloqué par le prestige.
    /// </summary>
    Laboratory,
    /// <summary>
    /// Caserne - Permet l'entraînement de troupes. Débloqué par le prestige.
    /// </summary>
    Barracks,
    /// <summary>
    /// Verrerie - Produit du verre dans les déserts. Débloqué par le prestige (Laboratoire).
    /// </summary>
    GlassWorks,
    /// <summary>
    /// Port impérial - Bâtiment unique. Prérequis au prestige. Disponible au niveau Capitale (4).
    /// </summary>
    ImperialPort,
    /// <summary>
    /// Guilde des récolteurs - Bâtiment unique. Débloque le niveau 5 des bâtiments de production
    /// et automatise leur construction/amélioration. Disponible au niveau Capitale (4).
    /// </summary>
    HarvestersGuild,
    /// <summary>
    /// Guilde des artisans - Bâtiment unique. Débloque le niveau 5 de la Forge et automatise
    /// la construction des Forges et Entrepôts. Disponible au niveau Capitale (4).
    /// </summary>
    ArtisansGuild,
    /// <summary>
    /// Tour de guet - Révèle les hexagones dans un rayon de 2 autour de la ville.
    /// </summary>
    Watchtower,
    /// <summary>
    /// Académie - Automatise la construction des Bibliothèques et augmente la vitesse de recherche. Débloqué par le prestige (Académie).
    /// </summary>
    Academy,
    /// <summary>
    /// Guilde des marchands - Bâtiment unique. Automatise la construction/amélioration des Marchés et débloque leurs niveaux supérieurs. Débloqué par le prestige.
    /// </summary>
    TraderGuild,
    /// <summary>
    /// Garnison - Augmente la capacité de soldats et la vitesse de production. Débloqué par le prestige.
    /// </summary>
    Garrison,
    /// <summary>
    /// Fonderie - Convertit du minerai et du bois en acier. Débloqué par le prestige (Secret de l'Acier).
    /// </summary>
    Smelter,
    /// <summary>
    /// Haut-Fourneau - Bâtiment unique. Toutes les Fonderies de la civilisation produisent +1 Acier par cycle. Débloqué par le prestige (Hauts-Fourneaux).
    /// </summary>
    BlastFurnace,
    /// <summary>
    /// Arsenal - Augmente la capacité de soldats et permet de sauver des soldats en consommant de l'Acier (Armures d'Acier). Débloqué par le prestige (Génie Militaire).
    /// </summary>
    Arsenal,
    /// <summary>
    /// Ferme fongique - Produit automatiquement de la nourriture sur les Cavernes aux Champignons adjacentes (Inframonde). Débloqué par le prestige (Cultures Fongiques).
    /// </summary>
    MushroomFarm,
    /// <summary>
    /// Mine de Mithril - Extrait automatiquement du Mithril des Filons adjacents (Inframonde). Débloqué par le prestige (Le Mithril).
    /// </summary>
    MithrilMine,
    /// <summary>
    /// Tour de Mages - Limite le nombre et la puissance des rituels actifs. Extrait des cristaux des Grottes de Cristal adjacentes. Débloqué par le prestige (Secret de la Magie).
    /// </summary>
    MageTower,
    /// <summary>
    /// Salle de Guerre - Bâtiment unique. Débloque l'automatisation des bâtiments militaires (Casernes, Arsenaux) et fournit +50% de vitesse de production de troupes. Débloqué par le prestige.
    /// </summary>
    WarRoom,
    /// <summary>
    /// Hutte d'Alchimie - Permet de récolter les cristaux des Cercles de Fées adjacents et produit des Potions de Soin. Ne peut être construite qu'adjacente à un Cercle de Fées découvert. Débloquée par le prestige (Hutte d'Alchimie).
    /// </summary>
    AlchimistHut,
    /// <summary>
    /// Forge d'Armes - Produit des Armes en Acier en consommant de l'Acier. Débloquée par la recherche Armes en Acier.
    /// </summary>
    WeaponSmith,
    /// <summary>
    /// Forge d'Armures - Produit des Armures en Acier en consommant de l'Acier. Débloquée par la recherche Armures d'Acier.
    /// </summary>
    ArmorSmith,
    /// <summary>
    /// Guilde des Aventuriers - Bâtiment unique de l'Inframonde. Débloque les Relais des Aventuriers
    /// et en accorde automatiquement un dans sa propre ville ; son niveau détermine la puissance des
    /// Aventuriers invoqués par tous les Relais de la civilisation.
    /// </summary>
    AdventurersGuild,
    /// <summary>
    /// Relais des Aventuriers - Constructible une fois la Guilde des Aventuriers bâtie (dans
    /// n'importe quelle ville de la civilisation). Fait apparaître un Aventurier qui combat les
    /// monstres errants sans jamais s'éloigner de plus de 2 hexs de son Relais ; un autre prend sa
    /// place à sa mort. Coût croissant avec le nombre de Relais déjà construits.
    /// </summary>
    AdventurersWaypost,
    /// <summary>
    /// Forge Volcanique - Bâtiment unique. Génère du Verre passivement, augmente la production
    /// de Minerai, d'Acier et de Mithril de la civilisation, et automatise la construction des
    /// Mines de Mithril. Ne peut être construite qu'à côté d'un volcan découvert. Débloquée par la
    /// recherche Métallurgie Volcanique.
    /// </summary>
    VolcanicForge,
    /// <summary>
    /// Ziggourat - Bâtiment unique racial des Humains. Chaque Temple construit ou amélioré produit
    /// instantanément du Dominion sur les hexs de sa ville (jusqu'à 4 fois par ville). Nécessite le
    /// Dominion débloqué (pouvoir divin Foi).
    /// </summary>
    Ziggurat,
    /// <summary>
    /// Arbre-Cœur - Bâtiment unique racial des Elfes. Accélère la recherche et génère du Bois passivement.
    /// </summary>
    HeartTree,
    /// <summary>
    /// Forge Runique - Bâtiment unique racial des Nains. Améliore les Forges, les Fonderies et la
    /// chance d'or des Mines.
    /// </summary>
    RunicForge,
    /// <summary>
    /// Grand Terrier - Bâtiment unique racial des Gobelins. Réduit le coût des nouvelles villes et
    /// augmente le stockage de base.
    /// </summary>
    GreatBurrow,
    /// <summary>
    /// Atelier des Colosses - Bâtiment unique racial des Géants. Chance de doubler le rendement des
    /// récoltes automatiques.
    /// </summary>
    ColossusWorkshop,
    /// <summary>
    /// Fosse aux Crânes - Bâtiment unique racial des Orcs. Offre la nourriture d'entretien de
    /// quelques soldats par ville.
    /// </summary>
    SkullPit,
    /// <summary>
    /// Trône des Vents - Bâtiment unique racial des Garudas. +1 de portée d'attaque des villes et
    /// génère de l'Or passivement.
    /// </summary>
    ThroneOfWinds,
    /// <summary>
    /// Grotte aux Perles - Bâtiment unique racial des Sirènes. +3 de défense des villes et génère
    /// de la Nourriture passivement.
    /// </summary>
    PearlGrotto,
    /// <summary>
    /// Grand Temple - Bâtiment unique. Automatise la construction des Temples et ajoute un bonus de
    /// prestige additif par Temple construit dans la civilisation (cumulable avec les bonus de
    /// recherche). Disponible au niveau Capitale (4). Nécessite un Temple niveau 1 dans la ville et
    /// au moins 10 Temples construits dans la civilisation. Débloqué par la recherche Grand Temple.
    /// </summary>
    GrandTemple,
    /// <summary>
    /// Tour des Arcanes - Bâtiment unique. Automatise la construction/amélioration des Tours de Mages
    /// et des Huttes d'Alchimie, et réduit de 25% le coût d'entretien des rituels. Disponible au
    /// niveau Capitale (4). Nécessite une Tour de Mages niveau 1 dans la ville. Débloquée par le
    /// vertex de prestige Tour des Arcanes.
    /// </summary>
    ArcaneTower,
    /// <summary>
    /// Sanctuaire de l'Araignée - Bâtiment unique racial des Elfes noirs, constructible dans
    /// l'Inframonde uniquement. Étend le Pacte des Profondeurs aux Rats et aux Démons mineurs, qui
    /// cessent eux aussi d'attaquer les villes.
    /// </summary>
    SpiderShrine,
}

/// <summary>
/// Represents a building in the game.
/// </summary>
[Serializable]
public class Building
{
    /// <summary>
    /// Gets or sets the localized name of the building. Ignoré en JSON : dérivé de Type dans le
    /// constructeur (jamais recalculé à la lecture, le setter étant protected), le sérialiser
    /// gonflait chaque bâtiment sauvegardé pour rien.
    /// </summary>
    [JsonIgnore]
    public string NameKey { get; protected set; }

    /// <summary>
    /// Gets the type of the building.
    /// </summary>
    public BuildingType Type { get; }

    /// <summary>
    /// Gets or sets the level of the building.
    /// </summary>
    public int Level { get; set; }

    /// <summary>
    /// Gets the cost of the building.
    /// </summary>
    public virtual ResourceSet GetBuildCost() => new ResourceSet();

    /// <summary>
    /// Gets the upgrade cost of the building for the specified level.
    /// </summary>
    /// <param name="level">The level to upgrade to.</param>
    /// <returns>The upgrade cost.</returns>
    public virtual ResourceSet GetUpgradeCost(int level) => new ResourceSet();

    /// <summary>
    /// Gets the harvest capability of the building.
    /// </summary>
    public virtual Resource? ManualHarvestCapability(TerrainType terrain) => null;

    /// <summary>
    /// Gets the harvest capability of the building.
    /// </summary>
    /// <param name="civ">
    /// Civilisation propriétaire, ou <c>null</c> quand l'appelant n'en a pas : une capacité ouverte par
    /// une recherche (Scierie sur Caverne aux Champignons, cf. Bois de Champignon) reste alors fermée.
    /// </param>
    /// <remarks>Signature unique, voir <see cref="IsBuildingAvailableForCity"/>.</remarks>
    public virtual Resource? AutomaticHarvestCapability(TerrainType terrain, Model.Civilization.Civilization? civ) => null;

    /// <summary>
    /// Multiplier applied to the automatic harvest cooldown for a given terrain, on top of the base
    /// building cooldown and civ-wide HARVEST_SPEED modifiers. Base = 1.0 (no change); 0.5 = twice
    /// slower (half speed).
    /// </summary>
    public virtual double GetAutomaticHarvestTerrainSpeedMultiplier(TerrainType terrain) => 1.0;

    /// <summary>
    /// Building level at which automatic harvest is unlocked. Override in subclasses. Ignoré en
    /// JSON : propriété calculée sans setter (donc jamais restaurée à la lecture), constante par
    /// type de bâtiment — la sérialiser ne fait que gonfler la sauvegarde.
    /// </summary>
    [JsonIgnore]
    public virtual int AutomaticHarvestUnlockLevel => int.MaxValue;

    /// <summary>
    /// Resource that this building can manually harvest, independent of terrain. Null if none.
    /// Used for tooltip display only. Ignoré en JSON (voir AutomaticHarvestUnlockLevel).
    /// </summary>
    [JsonIgnore]
    public virtual Resource? ManualHarvestResource => null;

    /// <summary>
    /// Resource that this building can automatically harvest, independent of terrain. Null if none.
    /// Auto harvest is active when Level >= AutomaticHarvestUnlockLevel.
    /// Used for tooltip display only. Ignoré en JSON (voir AutomaticHarvestUnlockLevel).
    /// </summary>
    [JsonIgnore]
    public virtual Resource? AutomaticHarvestResource => null;

    /// <summary>
    /// Returns the raw auto-harvest cooldown in ticks for this building, before civilization
    /// speed modifiers are applied. Default: baseCooldownTicks minus 0.5 s (50 ticks) per level
    /// above AutomaticHarvestUnlockLevel. Pass <paramref name="atLevel"/> to query a hypothetical level.
    /// </summary>
    public virtual long GetAutomaticHarvestCooldown(long baseCooldownTicks, int? atLevel = null)
    {
        int level = atLevel ?? Level;
        long levelsAbove = Math.Max(0, level - AutomaticHarvestUnlockLevel);
        return Math.Max(1L, baseCooldownTicks - levelsAbove * 50);
    }

    /// <summary>
    /// Gets or sets the maximum level of the building.
    /// </summary>
    public virtual int GetDefaultMaxLevel() => 1;

    /// <summary>
    /// Niveau max en comptant TOUS les bonus BUILDING_MAX_LEVEL atteignables (recherche + prestige +
    /// bâtiments uniques + Ascension), toutes conditions dynamiques ignorées (recherche non faite,
    /// vertex non acheté...) — voir BuildingMaxLevelCalculator pour le détail des sources et
    /// l'exclusion volontaire des bonus de race (mutuellement exclusifs, incohérents à sommer).
    /// Utilisé pour créer un bâtiment unique accordé en permanence par l'Ascension (voir
    /// Civilization.RebuildUniqueBuildingCache : ce bâtiment ne vit dans aucune ville, donc les
    /// modifiers dynamiques ne s'appliqueraient pas forcément) et pour borner le plafond
    /// sélectionnable dans le tableau des presets d'automatisation (voir
    /// AutomationRenderer.GetAutomationPresetPopupSnapshot). Couvert par
    /// BuildingMaxLevelCalculatorTests.TheoreticalMaxLevel_MatchesSumOfAllBonusSources.
    /// </summary>
    public virtual int GetAbsoluteMaxLevel() => BuildingMaxLevelCalculator.GetTheoreticalMaxLevel(Type);

    /// <summary>
    /// Gets or sets the description of the building. Ignoré en JSON (voir NameKey).
    /// </summary>
    [JsonIgnore]
    public string DescriptionKey { get; protected set; }

    /// <summary>
    /// Whether this building is unique: only one can be built per civilization per island.
    /// Ignoré en JSON (voir AutomaticHarvestUnlockLevel).
    /// </summary>
    [JsonIgnore]
    public virtual bool IsUnique => false;

    /// <summary>
    /// Whether this building unlocks entries in the Automation tab.
    /// Override to true in any building that contributes automation rows.
    /// Ignoré en JSON (voir AutomaticHarvestUnlockLevel).
    /// </summary>
    [JsonIgnore]
    public virtual bool ProvidesAutomation => false;

    /// <summary>
    /// Gets or sets the activation state of the building.
    /// NON_ACTIVABLE buildings cannot be toggled; INACTIVE/ACTIVE buildings can.
    /// </summary>
    public ActivationStatus ActivationStatus { get; set; } = ActivationStatus.NON_ACTIVABLE;

    /// <summary>
    /// Gets or sets the city level at which the building becomes available. Ignoré en JSON :
    /// toujours réaffecté à la même constante par le constructeur du type concret, jamais modifié
    /// ailleurs — le sérialiser ne fait que dupliquer une valeur déjà fixée par Type.
    /// </summary>
    [JsonIgnore]
    public int AvailableAtLevel { get; set; }

    private readonly Dictionary<HexCoord, long> _autoHarvestLastTicks = new();

    /// <summary>
    /// Tick de la dernière récolte automatique par hex, pour ce bâtiment spécifique.
    /// Clé = coordonnée hex ; valeur = tick de la dernière récolte.
    /// </summary>
    public IReadOnlyDictionary<HexCoord, long> AutoHarvestLastTicks => _autoHarvestLastTicks;

    public void SetAutoHarvestTick(HexCoord hex, long tick) => _autoHarvestLastTicks[hex] = tick;

    protected readonly List<string> _actions = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="Building"/> class.
    /// </summary>
    /// <param name="type">The type of the building.</param>
    /// <param name="level">The level of the building.</param>
    protected Building(BuildingType type, int level = 0)
    {
        Type = type;
        Level = level;
        NameKey = $"building_{type.ToString().ToLower()}_name";
        DescriptionKey = $"building_{type.ToString().ToLower()}_desc";
    }

    /// <summary>
    /// Determines if the building is available for the specified city (city level, terrain, map layer).
    /// Default implementation checks AvailableAtLevel only.
    /// Derived classes can override to add additional requirements.
    /// </summary>
    /// <param name="map">The island map.</param>
    /// <param name="city">The city.</param>
    /// <param name="civ">
    /// Civilisation propriétaire, ou <c>null</c> quand l'appelant n'en a pas — génération de villes PNJ
    /// hors civilisation câblée, ou ville dont l'index ne résout plus. Une règle de placement ouverte par
    /// une recherche ou une race (voir <see cref="Sawmill"/>, Caverne aux Champignons) ne s'applique donc
    /// que si elle est fournie : un <c>null</c> rend la réponse la plus restrictive.
    /// </param>
    /// <returns>True if the building is available for the city, false otherwise.</returns>
    /// <remarks>
    /// <b>Une seule méthode virtuelle, volontairement.</b> Cette question et les trois suivantes
    /// (<see cref="HasBuildPrerequisites"/>, <see cref="GetMissingPrerequisiteKey"/>,
    /// <see cref="GetBuildWarningKey"/>) étaient chacune deux surcharges, la riche retombant sur la
    /// pauvre. Un appelant qui prenait la surcharge pauvre sautait alors silencieusement toute
    /// redéfinition portée par la riche — le bâtiment devenait constructible là où sa règle l'interdit,
    /// sans erreur ni trace. Avec une signature unique, la redéfinition est toujours consultée et
    /// l'absence d'une donnée est un <c>null</c> visible à l'appel.
    /// <c>BuildingHookOverloadTests</c> (SOITests) échoue si une seconde surcharge réapparaît.
    /// </remarks>
    public virtual bool IsBuildingAvailableForCity(IslandMap.IslandMap map, IBuildingContext city, Model.Civilization.Civilization? civ)
    {
        return city.Level >= AvailableAtLevel;
    }

    /// <summary>
    /// Determines if this building type can exist on the given map layer (e.g. surface vs. underworld).
    /// Used both for normal construction checks and for prestige-granted free buildings,
    /// which otherwise bypass <see cref="IsBuildingAvailableForCity"/>.
    /// </summary>
    public virtual bool IsAvailableInLayer(int z) => true;

    /// <summary>
    /// Returns the max-defense bonus this building contributes to its city.
    /// CurrentDefense is immediately increased by this amount when the building is constructed.
    /// </summary>
    public virtual int GetDefenseBonus() => 0;

    /// <summary>
    /// Returns the soldier-capacity bonus this building contributes to its city.
    /// </summary>
    public virtual int GetMaxSoldiersBonus() => 0;

    /// <summary>
    /// Returns an additive bonus to this city's defense regeneration speed (stacks with civ-wide modifiers).
    /// Base = 0; +0.2 = regen 20% faster for this city.
    /// </summary>
    public virtual double GetDefenseRegenBonus() => 0.0;

    /// <summary>
    /// Returns an additive bonus to this city's own soldier production speed (Barracks/Arsenal), on top
    /// of the civ-wide <see cref="SettlersOfIdlestan.Model.Civilization.Civilization.UnitProductionSpeed"/>.
    /// Base = 0; +0.25 = +25% for this city only (see Garrison).
    /// </summary>
    public virtual double GetUnitProductionSpeedBonus() => 0.0;

    /// <summary>
    /// Returns the bonus this building contributes to the civilization's basic resource storage capacity.
    /// </summary>
    public virtual int GetStorageCapacityBonusBasic() => 0;

    /// <summary>
    /// Returns the bonus this building contributes to the civilization's advanced resource storage capacity.
    /// </summary>
    public virtual int GetStorageCapacityBonusAdvanced() => 0;

    /// <summary>
    /// Returns true if all build prerequisites (beyond resources) are satisfied.
    /// Override in derived classes to add extra conditions.
    /// </summary>
    /// <param name="state">
    /// Monde courant, ou <c>null</c> quand l'appelant n'en a pas. Un prérequis lié à la carte
    /// (adjacence à une IslandFeature découverte : Cercle de Fées, Volcan) ne peut alors pas être
    /// évalué : les redéfinitions concernées le tiennent pour <b>non rempli</b>, comme
    /// <see cref="IsBuildingAvailableForCity"/> referme ses règles sans civilisation. Un prérequis
    /// qu'on ne peut pas vérifier n'est pas un prérequis satisfait.
    /// </param>
    /// <remarks>Signature unique, voir <see cref="IsBuildingAvailableForCity"/>.</remarks>
    public virtual bool HasBuildPrerequisites(IBuildingContext city, WorldState? state) => true;

    /// <summary>
    /// Type du bâtiment unique dont ce bâtiment dépend pour pouvoir être bâti (ex : Relais des
    /// Aventuriers → Guilde des Aventuriers), ou <c>null</c> si son prérequis n'en est pas un.
    /// Sert uniquement à décider si la liste des bâtiments constructibles doit masquer l'entrée
    /// plutôt que l'afficher grisée avec tooltip tant que le prérequis n'est pas rempli (voir
    /// BuildingController.GetBuildingOrBuildableEntry) — même traitement que les prérequis liés à
    /// une feature de carte (Cercle de Fées, Volcan), pour la même raison : ce n'est pas un
    /// bâtiment normal constructible dans cette ville.
    /// </summary>
    public virtual BuildingType? RequiredUniqueBuildingType => null;

    /// <summary>
    /// Returns the localization key describing the missing prerequisite, or null if none.
    /// Used by the UI to show a tooltip warning when HasBuildPrerequisites is false.
    /// </summary>
    /// <remarks>Signature unique, voir <see cref="IsBuildingAvailableForCity"/>.</remarks>
    public virtual string? GetMissingPrerequisiteKey(IBuildingContext city, WorldState? state) => null;

    /// <summary>
    /// Returns the localization key for a non-blocking build warning, or null if none.
    /// Unlike <see cref="GetMissingPrerequisiteKey"/>, the build is still
    /// allowed — this only informs the player of a limitation (e.g. reduced functionality at this spot).
    /// </summary>
    /// <remarks>Signature unique, voir <see cref="IsBuildingAvailableForCity"/>.</remarks>
    public virtual string? GetBuildWarningKey(IBuildingContext city, WorldState? state) => null;
}