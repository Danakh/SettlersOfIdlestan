using System.Collections.Generic;
using SettlersOfIdlestan.Model.Buildings;

namespace SettlersOfIdlestan.Model.Ascension;

/// <summary>Un bonus de niveau maximum accorde a un type de batiment par un pouvoir divin.</summary>
public readonly record struct AscensionBuildingMaxLevelGrant(AscensionPowerId Power, BuildingType Type, int Bonus);

/// <summary>
/// Source unique des bonus BUILDING_MAX_LEVEL accordes par les pouvoirs divins. Lue a la fois par
/// AscensionController.GetModifiers (bonus reellement appliques, filtres sur les pouvoirs debloques)
/// et par <see cref="BuildingMaxLevelCalculator"/> (plafond theorique, tous pouvoirs confondus) :
/// ces deux vues etaient auparavant deux copies manuelles du meme fait — un "Temple +3" ecrit d'un
/// cote et pas de l'autre affichait un plafond de preset d'automatisation faux, sans erreur.
///
/// <para>Vit dans le modele, et non dans AscensionController, parce que BuildingMaxLevelCalculator
/// en depend : le modele ne doit jamais dependre des controleurs (voir
/// SOITests.ModelTests.ModelDoesNotDependOnControllerTests).</para>
///
/// <para>Ajouter ici toute nouvelle ligne accordee par un pouvoir divin — rien d'autre a mettre a
/// jour, et AscensionControllerTests.BuildingMaxLevelGrants_MatchAscensionBuildingMaxLevelGrantsTable
/// echoue si un pouvoir en accorde un hors de cette table.</para>
/// </summary>
public static class AscensionBuildingMaxLevelGrants
{
    public static IReadOnlyList<AscensionBuildingMaxLevelGrant> All { get; } = new[]
    {
        // Foi — le Temple est le batiment fondateur du Dominion.
        new AscensionBuildingMaxLevelGrant(AscensionPowerId.Faith, BuildingType.Temple, 3),

        // Magisterium Divin — un niveau de plus sur chaque batiment de recherche et de magie.
        // Bibliotheque/Laboratoire/Academie couvrent la recherche ; Tour de Mages, Hutte d'Alchimie,
        // Tour des Arcanes et Spire de Defense la magie. Les trois derniers plafonnaient jusqu'ici a
        // 1 : c'est ce pouvoir qui leur donne leur premier palier d'amelioration (voir
        // ArcaneTower/DefenseSpire.GetUpgradeCost). Le Temple s'y ajoute : il se cumule au +3 de Foi,
        // portant a 4 le total accorde par les pouvoirs divins.
        new AscensionBuildingMaxLevelGrant(AscensionPowerId.DivineMagisterium, BuildingType.Temple, 1),
        new AscensionBuildingMaxLevelGrant(AscensionPowerId.DivineMagisterium, BuildingType.Library, 1),
        new AscensionBuildingMaxLevelGrant(AscensionPowerId.DivineMagisterium, BuildingType.Laboratory, 1),
        new AscensionBuildingMaxLevelGrant(AscensionPowerId.DivineMagisterium, BuildingType.Academy, 1),
        new AscensionBuildingMaxLevelGrant(AscensionPowerId.DivineMagisterium, BuildingType.MageTower, 1),
        new AscensionBuildingMaxLevelGrant(AscensionPowerId.DivineMagisterium, BuildingType.AlchimistHut, 1),
        new AscensionBuildingMaxLevelGrant(AscensionPowerId.DivineMagisterium, BuildingType.ArcaneTower, 1),
        new AscensionBuildingMaxLevelGrant(AscensionPowerId.DivineMagisterium, BuildingType.DefenseSpire, 1),
    };

    /// <summary>
    /// Somme des bonus accordes a <paramref name="type"/> par l'ensemble des pouvoirs divins, qu'ils
    /// soient debloques ou non : c'est le plafond <b>theorique</b> attendu par
    /// <see cref="BuildingMaxLevelCalculator"/>, jamais le plafond courant d'une partie.
    /// </summary>
    public static int GetTheoreticalBonus(BuildingType type)
    {
        int sum = 0;
        foreach (var grant in All)
            if (grant.Type == type) sum += grant.Bonus;
        return sum;
    }
}
