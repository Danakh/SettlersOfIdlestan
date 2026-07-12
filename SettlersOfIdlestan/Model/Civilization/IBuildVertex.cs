using SettlersOfIdlestan.Model.HexGrid;

namespace SettlersOfIdlestan.Model.Civilization;

/// <summary>
/// Tout ce qui est construit sur un vertex du plateau et en garantit l'unicité — voir
/// <see cref="City"/>, <see cref="WarFleet"/> et <see cref="MaritimeBeacon"/>. Les contrôleurs de
/// construction (CityBuilderController, MaritimeBeaconController) s'appuient sur cette interface
/// pour vérifier de façon uniforme qu'un vertex est déjà occupé, plutôt que d'énumérer chaque type
/// concret séparément. Note : une Flotte de Guerre se construit par-dessus une Balise Maritime
/// existante (voir WarFleetController) — ces deux-là coexistent volontairement sur un même vertex.
/// </summary>
public interface IBuildVertex
{
    Vertex Position { get; }
    int CivilizationIndex { get; }
}
