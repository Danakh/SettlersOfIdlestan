using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestan.Model.Buildings;

/// <summary>
/// Spire de Défense — bâtiment magique de défense passive. Une fois par seconde, chaque spire active
/// frappe un monstre situé à <see cref="AttackRangeInHexes"/> hexs ou moins de sa ville : 1 Cristal
/// consommé, <see cref="GetDamage"/> dégâts infligés (1 par niveau), sans réduction d'armure (voir
/// DefenseSpireEngine).
///
/// <para>Contrairement aux attaques de soldats, la spire ne coûte aucun soldat et ne dépend d'aucune
/// garnison : c'est la seule défense qui fonctionne sur une ville vide, au prix d'un flux de Cristal.
/// Elle est activable une par une, et en bloc depuis l'écran d'automatisation (ligne Spire de Défense).</para>
///
/// <para>Verrouillée par défaut ; débloquée par le vertex de prestige Spire de Défense.</para>
/// </summary>
public class DefenseSpire : Building
{
    /// <summary>Portée d'attaque, en hexs, comptée depuis le plus éloigné des 3 hexs de la ville (voir DefenseSpireEngine.DistanceTo).</summary>
    public const int AttackRangeInHexes = 2;

    /// <summary>Intervalle entre deux tirs : 1 seconde.</summary>
    public const long AttackIntervalTicks = 100L;

    /// <summary>Cristal consommé par tir.</summary>
    public const int CrystalCostPerAttack = 1;

    /// <summary>
    /// Dégâts par tir <b>et par niveau</b>, appliqués sans réduction d'armure : une spire niveau 1 en
    /// inflige 1, une spire niveau 2 en inflige 2 (voir <see cref="GetDamage"/>). Le coût en Cristal,
    /// lui, ne bouge pas avec le niveau.
    /// </summary>
    public const int DamagePerAttackPerLevel = 1;

    /// <summary>Dégâts infligés par un tir de cette spire, à son niveau courant (voir <see cref="DamagePerAttackPerLevel"/>).</summary>
    public int GetDamage() => DamagePerAttackPerLevel * Level;

    /// <summary>Dernier tick où cette spire a tiré. Persiste : sans cela, un rechargement remettrait le cooldown à zéro.</summary>
    public long LastAttackTick { get; set; } = 0;

    public DefenseSpire() : base(BuildingType.DefenseSpire)
    {
        AvailableAtLevel = 1;
        ActivationStatus = ActivationStatus.ACTIVE;
    }

    // Verrouillée par défaut ; débloquée par le vertex de prestige Spire de Défense (+1 niveau max).
    public override int GetDefaultMaxLevel() => 0;

    public override ResourceSet GetBuildCost() => new ResourceSet
    {
        { Resource.Stone,   80 },
        { Resource.Glass,   30 },
        { Resource.Crystal, 10 },
        { Resource.Gold,    60 },
    };

    // Chaque niveau coûte son rang en coûts de construction (niveau 2 = deux fois le coût initial) et
    // ajoute 1 dégât par tir, sans toucher au coût en Cristal du tir. Le niveau 2 n'est ouvert que par
    // le pouvoir divin Magisterium Divin (voir AscensionBuildingMaxLevelGrants) ; les suivants par le
    // bonus racial des Géants, qui donnait jusqu'ici des niveaux gratuits faute de coût défini ici.
    public override ResourceSet GetUpgradeCost(int level) => new ResourceSet
    {
        { Resource.Stone,   80 * level },
        { Resource.Glass,   30 * level },
        { Resource.Crystal, 10 * level },
        { Resource.Gold,    60 * level },
    };
}
