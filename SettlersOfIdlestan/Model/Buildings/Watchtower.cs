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
    ///
    /// <para><b>Si l'Inframonde est un jour ouvert à ce bâtiment</b>, l'auto-extension de la couche
    /// doit être prévenue : elle génère le terrain jusqu'au rayon de vision des villes du joueur et
    /// n'est appelée qu'aux sources connues de ce rayon (voir
    /// AutoExtendController.TryExtendMapsToPlayerVision, qui détaille où brancher l'appel). Aujourd'hui
    /// ce rayon ne peut plus changer une fois la ville posée, faute de Tour de Guet possible sous terre ;
    /// une Tour de Guet souterraine le ferait passer de 1 à 2 sans que rien ne fasse pousser la carte,
    /// et le joueur verrait un anneau vide au lieu du terrain promis.</para>
    /// </summary>
    public override bool IsAvailableInLayer(int z) =>
        z == IslandMap.IslandMap.SurfaceLayer || z == LayerState.AbyssZ || z == LayerState.PandemoniumZ;

    public override bool IsBuildingAvailableForCity(IslandMap.IslandMap map, IBuildingContext city, Model.Civilization.Civilization? civ)
    {
        return IsAvailableInLayer(map.Z) && base.IsBuildingAvailableForCity(map, city, civ);
    }
}
