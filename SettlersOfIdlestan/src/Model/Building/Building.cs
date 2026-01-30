using SettlersOfIdlestan.Model.Buildings;
using System.Collections.Generic;
using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestan.Model.Buildings;

/// <summary>
/// Represents the type of a building.
/// </summary>
public enum BuildingType
{
    /// <summary>
    /// Hôtel de ville - Permet l'amélioration de la ville
    /// </summary>
    TownHall,
    /// <summary>
    /// Marché - Permet le commerce (4:1)
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
    /// Moulin - Produit du blé
    /// </summary>
    Mill,
    /// <summary>
    /// Bergerie - Produit du mouton
    /// </summary>
    Sheepfold,
    /// <summary>
    /// Mine - Produit du minerai
    /// </summary>
    Mine,
    /// <summary>
    /// Port maritime - Permet le commerce maritime (3:1), nécessite de l'eau. Disponible au niveau Ville (2). Niveau 4 débloque l'action Prestige.
    /// </summary>
    Seaport,
    /// <summary>
    /// Entrepôt - Augmente la capacité de stockage des ressources
    /// </summary>
    Warehouse,
    /// <summary>
    /// Forge - Améliore la production de minerai et permet la création d'outils
    /// </summary>
    Forge,
    /// <summary>
    /// Bibliothèque - Augmente la production de connaissances et permet des améliorations
    /// </summary>
    Library,
    /// <summary>
    /// Temple - Ajoute des points de civilisation
    /// </summary>
    Temple,
    /// <summary>
    /// Guilde des batisseurs - Permet l'automatisation de constructions. Disponible au niveau Capitale (4).
    /// </summary>
    BuildersGuild
}

/// <summary>
/// Represents a building in the game.
/// </summary>
public class Building
{
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
    public virtual Dictionary<Resource, int> GetBuildCost() => new();

    /// <summary>
    /// Gets the upgrade cost of the building for the specified level.
    /// </summary>
    /// <param name="level">The level to upgrade to.</param>
    /// <returns>The upgrade cost.</returns>
    public virtual Dictionary<Resource, int> GetUpgradeCost(int level) => new();

    /// <summary>
    /// Gets the production of the building.
    /// </summary>
    public Dictionary<Resource, int> Production { get; } = new();

    /// <summary>
    /// Gets or sets the maximum level of the building.
    /// </summary>
    public int MaxLevel { get; set; }

    /// <summary>
    /// Gets or sets the description of the building.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets whether the building requires water.
    /// </summary>
    public bool RequiresWater { get; set; }

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
    protected Building(BuildingType type, int level = 1)
    {
        Type = type;
        Level = level;
    }
}