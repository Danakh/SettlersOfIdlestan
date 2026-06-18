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
    /// Académie militaire - Augmente la capacité de soldats et la vitesse de production. Débloqué par le prestige.
    /// </summary>
    MilitaryAcademy,
    /// <summary>
    /// Mine Profonde - [Legacy] Conservé uniquement pour la désérialisation des anciennes sauvegardes.
    /// La Mine Profonde est désormais une IslandFeature placée comme une Merveille.
    /// </summary>
    DeepestMine,
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
}

/// <summary>
/// Represents a building in the game.
/// </summary>
[Serializable]
public class Building
{
    /// <summary>
    /// Gets or sets the localized name of the building.
    /// </summary>
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
    public virtual Resource? AutomaticHarvestCapability(TerrainType terrain) => null;

    /// <summary>
    /// Building level at which automatic harvest is unlocked. Override in subclasses.
    /// </summary>
    public virtual int AutomaticHarvestUnlockLevel => int.MaxValue;

    /// <summary>
    /// Resource that this building can manually harvest, independent of terrain. Null if none.
    /// Used for tooltip display only.
    /// </summary>
    public virtual Resource? ManualHarvestResource => null;

    /// <summary>
    /// Resource that this building can automatically harvest, independent of terrain. Null if none.
    /// Auto harvest is active when Level >= AutomaticHarvestUnlockLevel.
    /// Used for tooltip display only.
    /// </summary>
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
    /// Gets or sets the description of the building.
    /// </summary>
    public string DescriptionKey { get; protected set; }

    /// <summary>
    /// Whether this building is unique: only one can be built per civilization per island.
    /// </summary>
    public virtual bool IsUnique => false;

    /// <summary>
    /// Whether this building unlocks entries in the Automation tab.
    /// Override to true in any building that contributes automation rows.
    /// </summary>
    public virtual bool ProvidesAutomation => false;

    /// <summary>
    /// Gets or sets the activation state of the building.
    /// NON_ACTIVABLE buildings cannot be toggled; INACTIVE/ACTIVE buildings can.
    /// </summary>
    public ActivationStatus ActivationStatus { get; set; } = ActivationStatus.NON_ACTIVABLE;

    /// <summary>
    /// Gets or sets the city level at which the building becomes available.
    /// </summary>
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
    /// Gets the list of actions associated with the building.
    /// </summary>
    public IReadOnlyList<string> Actions => _actions;

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
    /// Determines if the building is available for the specified city.
    /// Default implementation checks AvailableAtLevel only.
    /// Derived classes can override to add additional requirements.
    /// </summary>
    /// <param name="map">The island map.</param>
    /// <param name="city">The city.</param>
    /// <returns>True if the building is available for the city, false otherwise.</returns>
    public virtual bool IsBuildingAvailableForCity(IslandMap.IslandMap map, IBuildingContext city)
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
    public virtual bool HasBuildPrerequisites(IBuildingContext city) => true;

    /// <summary>
    /// Overload of <see cref="HasBuildPrerequisites(IBuildingContext)"/> with access to the WorldState,
    /// for prerequisites depending on map features (e.g. adjacency to a discovered IslandFeature).
    /// Default implementation ignores state and falls back to the simple overload.
    /// </summary>
    public virtual bool HasBuildPrerequisites(IBuildingContext city, WorldState state) => HasBuildPrerequisites(city);

    /// <summary>
    /// Returns the localization key describing the missing prerequisite, or null if none.
    /// Used by the UI to show a tooltip warning when HasBuildPrerequisites is false.
    /// </summary>
    public virtual string? GetMissingPrerequisiteKey(IBuildingContext city) => null;

    /// <summary>
    /// Overload of <see cref="GetMissingPrerequisiteKey(IBuildingContext)"/> with access to the WorldState.
    /// Default implementation ignores state and falls back to the simple overload.
    /// </summary>
    public virtual string? GetMissingPrerequisiteKey(IBuildingContext city, WorldState state) => GetMissingPrerequisiteKey(city);
}