using System.Collections.Generic;
using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Monsters;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SettlersOfIdlestan.Model.Buildings;

/// <summary>
/// Sanctuaire de l'Araignée : bâtiment unique racial des Elfes noirs (voir RaceDefinitions).
/// Prolonge le Pacte des Profondeurs — la race est déjà épargnée par les Trolls et les Ogres — aux
/// autres habitants de l'Inframonde : Rats et Démons mineurs cessent à leur tour de prendre les
/// villes pour cible (ils continuent d'occuper et de stériliser leurs hexes, voir BlocksHarvest).
/// Souterrain uniquement, comme la Guilde des Aventuriers. Niveau max par défaut 0 : constructible
/// uniquement quand la race Elfes noirs fournit son BUILDING_MAX_LEVEL +1.
/// </summary>
public class SpiderShrine : Building, IUniqueBuilding
{
    public SpiderShrine() : base(BuildingType.SpiderShrine)
    {
        AvailableAtLevel = 3;
    }

    public override bool IsUnique => true;
    public override int GetDefaultMaxLevel() => 0;

    // Race Elfes noirs : +1 (voir RaceDefinitions.cs).
    public override int GetAbsoluteMaxLevel() => 1;

    public override bool IsAvailableInLayer(int z) => z != IslandMap.IslandMap.SurfaceLayer;

    public override bool IsBuildingAvailableForCity(IslandMap.IslandMap map, IBuildingContext city)
        => IsAvailableInLayer(map.Z) && base.IsBuildingAvailableForCity(map, city);

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
        yield return new Modifier(ECategory.MONSTER_ATTACK_IMMUNITY, nameof(Rats), EType.ADDITIVE, 1);
        yield return new Modifier(ECategory.MONSTER_ATTACK_IMMUNITY, nameof(MinorDemon), EType.ADDITIVE, 1);
    }
}
