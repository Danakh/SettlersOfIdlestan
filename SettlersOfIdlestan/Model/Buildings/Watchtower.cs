using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestan.Model.Buildings;

public class Watchtower : Building
{
    public Watchtower() : base(BuildingType.Watchtower)
    {
        AvailableAtLevel = 1;
    }

    public override int GetDefaultMaxLevel() => 0;

    public override ResourceSet GetBuildCost() => new ResourceSet
    {
        { Resource.Stone, 20 },
        { Resource.Wood, 10 },
    };

    /// <summary>
    /// Interdite dans l'Inframonde uniquement (voir TechnologyId.VeilleSouterraine, qui y transpose
    /// l'effet). Autorisée en Surface, dans l'Abysse et dans le Pandémonium : sans elle, aucune ville
    /// de la couche ne peut attaquer les monstres (voir MonsterCombatEngine.GetAttackAvailability).
    /// </summary>
    public override bool IsAvailableInLayer(int z) =>
        z == IslandMap.IslandMap.SurfaceLayer || z == LayerState.AbyssZ || z == LayerState.PandemoniumZ;

    public override bool IsBuildingAvailableForCity(IslandMap.IslandMap map, IBuildingContext city, Model.Civilization.Civilization? civ)
    {
        return IsAvailableInLayer(map.Z) && base.IsBuildingAvailableForCity(map, city, civ);
    }
}
