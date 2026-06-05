using SettlersOfIdlestan.Model.GameplayModifier;

namespace SettlersOfIdlestan.Model.Buildings;

public interface IUniqueBuilding
{
    IEnumerable<Modifier> GetUniqueBuildingModifiers();
}
