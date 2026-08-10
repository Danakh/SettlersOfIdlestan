using System.Linq;
using SettlersOfIdlestan.Model.HexGrid;

namespace SettlersOfIdlestan.Model.Buildings;

public interface IBuildingContext
{
    int Level { get; }
    Vertex Position { get; }
    IReadOnlyList<Building> Buildings { get; }

    /// <summary>
    /// Vrai si la ville possède un bâtiment de ce type au niveau <paramref name="minLevel"/> ou plus.
    ///
    /// <para>Tout prérequis de construction exprimé en « niveau d'un autre bâtiment » doit passer par
    /// ici, et non par un scan direct de <see cref="Buildings"/> : c'est le seul point où une
    /// réduction de prérequis s'applique sans être réécrite dans chacun des onze bâtiments uniques
    /// qui en portent un (voir BuildingController.BuildReducedPrerequisiteContext, Grand Terrier
    /// gobelin).</para>
    /// </summary>
    bool HasBuildingAtLevel(BuildingType type, int minLevel) =>
        Buildings.Any(b => b.Type == type && b.Level >= minLevel);

    /// <summary>
    /// Nombre de bâtiments de la ville dont le type figure dans <paramref name="types"/> et dont le
    /// niveau atteint <paramref name="minLevel"/>. Même rôle que
    /// <see cref="HasBuildingAtLevel"/> pour les prérequis qui comptent plusieurs bâtiments (Guilde
    /// des Récolteurs : trois bâtiments de production au niveau 4).
    /// </summary>
    int CountBuildingsAtLevel(IReadOnlyList<BuildingType> types, int minLevel) =>
        Buildings.Count(b => types.Contains(b.Type) && b.Level >= minLevel);
}
