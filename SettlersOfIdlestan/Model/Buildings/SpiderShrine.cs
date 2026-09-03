using System.Collections.Generic;
using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.IslandMap;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SettlersOfIdlestan.Model.Buildings;

/// <summary>
/// Sanctuaire de l'Araignée : bâtiment unique racial des Elfes noirs (voir RaceDefinitions).
/// Complète le Pacte des Profondeurs — la race est déjà épargnée par les Trolls et les Ogres — en
/// réduisant de 1 les dégâts de toute attaque de monstre visant les villes de la civilisation
/// (MONSTER_DAMAGE_REDUCTION_ON_CITIES, voir MonsterController.ApplyMonsterAttack), en réduisant de
/// 1 niveau le malus effectif de la Corruption sur la récolte (CORRUPTION_LEVEL_REDUCTION, comme la
/// recherche Résistance à la Corruption), en accélérant de 25% la production de Nourriture des
/// Fermes fongiques sur les Cavernes aux Champignons et en augmentant de 1 les dégâts d'attaque de
/// l'Aventurier. Souterrain uniquement, comme la Guilde des Aventuriers. Niveau max par défaut 0 :
/// constructible uniquement quand la race Elfes noirs fournit son BUILDING_MAX_LEVEL +1.
/// </summary>
public class SpiderShrine : Building, IUniqueBuilding
{
    public SpiderShrine() : base(BuildingType.SpiderShrine)
    {
        AvailableAtLevel = 3;
    }

    public override bool IsUnique => true;
    public override int GetDefaultMaxLevel() => 0;

    public override bool IsAvailableInLayer(int z) => z != IslandMap.IslandMap.SurfaceLayer;

    public override bool IsBuildingAvailableForCity(IslandMap.IslandMap map, IBuildingContext city, Model.Civilization.Civilization? civ)
        => IsAvailableInLayer(map.Z) && base.IsBuildingAvailableForCity(map, city, civ);

    public override ResourceSet GetBuildCost() => new ResourceSet
    {
        { Resource.Stone, 400 },
        { Resource.Ore,   250 },
        { Resource.Food,  200 },
    };

    public override ResourceSet GetUpgradeCost(int level) => new ResourceSet();

    public IEnumerable<Modifier> GetUniqueBuildingModifiers()
    {
        if (Level <= 0) yield break;
        yield return new Modifier(ECategory.MONSTER_DAMAGE_REDUCTION_ON_CITIES, EType.ADDITIVE, 1);
        yield return new Modifier(ECategory.CORRUPTION_LEVEL_REDUCTION, EType.ADDITIVE, 1);
        yield return new Modifier(ECategory.HARVEST_SPEED, nameof(BuildingType.MushroomFarm), EType.ADDITIVE, 0.25);
        yield return new Modifier(ECategory.ADVENTURER_ATTACK_DAMAGE_BONUS, EType.ADDITIVE, 1);
    }
}
