using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.IslandMap;
using System.Text.Json.Serialization;

namespace SettlersOfIdlestan.Model.Buildings;

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
    /// March� - Permet le commerce
    /// </summary>
    Market,
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
    /// Mine - Automatise la récolte de minerai
    /// </summary>
    Mine,
    /// <summary>
    /// Port maritime - Permet la r�colte de nourriture et le commerce maritime
    /// </summary>
    Seaport,
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
    /// above AutomaticHarvestUnlockLevel.
    /// </summary>
    public virtual long GetAutomaticHarvestCooldown(long baseCooldownTicks)
    {
        long levelsAbove = Math.Max(0, Level - AutomaticHarvestUnlockLevel);
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
    /// Gets or sets the city level at which the building becomes available.
    /// </summary>
    public int AvailableAtLevel { get; set; }

    /// <summary>
    /// Gets the list of actions associated with the building.
    /// </summary>
    public List<string> Actions { get; } = new();

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
    public virtual bool IsBuildingAvailableForCity(IslandMap.IslandMap map, City city)
    {
        return city.Level >= AvailableAtLevel;
    }

    /// <summary>
    /// Returns true if all build prerequisites (beyond resources) are satisfied.
    /// Override in derived classes to add extra conditions.
    /// </summary>
    public virtual bool HasBuildPrerequisites(City city) => true;

    /// <summary>
    /// Returns the localization key describing the missing prerequisite, or null if none.
    /// Used by the UI to show a tooltip warning when HasBuildPrerequisites is false.
    /// </summary>
    public virtual string? GetMissingPrerequisiteKey(City city) => null;
}