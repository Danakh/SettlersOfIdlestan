using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.HexGrid;

namespace SettlersOfIdlestan.Model.Prestige.PrestigeMap;

public class PrestigeVertex
{
    public Vertex Coord { get; }
    public string LocalizationKey { get; }
    public int Cost { get; }
    public IReadOnlyList<Modifier> Modifiers { get; }

    /// <summary>Buildings from STARTING_CITY_BUILDING modifiers — granted to the initial city only.</summary>
    public IReadOnlyList<BuildingType> StartingCityBuildings =>
        Modifiers
            .Where(m => m.Category == Modifier.ECategory.STARTING_CITY_BUILDING
                        && Enum.TryParse<BuildingType>(m.SubCategory, out _))
            .Select(m => Enum.Parse<BuildingType>(m.SubCategory))
            .ToList();

    /// <summary>Buildings from NEW_CITY_BUILDING modifiers — granted to every new outpost (including the initial city).</summary>
    public IReadOnlyList<BuildingType> NewCityBuildings =>
        Modifiers
            .Where(m => m.Category == Modifier.ECategory.NEW_CITY_BUILDING
                        && Enum.TryParse<BuildingType>(m.SubCategory, out _))
            .Select(m => Enum.Parse<BuildingType>(m.SubCategory))
            .ToList();

    public PrestigeVertex(
        Vertex coord,
        string localizationKey,
        int cost,
        IReadOnlyList<Modifier> modifiers)
    {
        Coord = coord;
        LocalizationKey = localizationKey;
        Cost = cost;
        Modifiers = modifiers;
    }
}
