using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.HexGrid;

namespace SettlersOfIdlestan.Model.Civilization;

/// <summary>
/// Represents a city in the game.
/// </summary>
[Serializable]
public class City
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

    /// <summary>
    /// Défense actuelle (dynamique). Se régénère jusqu'à la valeur issue des bâtiments.
    /// </summary>
    public int CurrentDefense { get; set; }

    /// <summary>
    /// Tick du dernier point de régénération de défense.
    /// </summary>
    public long LastDefenseRegenTick { get; set; }

    /// <summary>
    /// Tick de la dernière attaque lancée par cette ville contre une ville adverse.
    /// </summary>
    public long LastCityAttackTick { get; set; }

    /// <summary>
    /// Gets the effective level of the city.
    /// Level 1 is the base level (outpost) when no TownHall is built.
    /// When a TownHall is present the city level is TownHall.Level + 1.
    /// Levels map as: 1=outpost, 2=colony, 3=town, 4=metropolis, 5=capital.
    /// </summary>
    public int Level
    {
        get
        {
            var th = Buildings.FirstOrDefault(b => b.Type == BuildingType.TownHall);
            return th != null ? th.Level : 0;
        }
    }

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