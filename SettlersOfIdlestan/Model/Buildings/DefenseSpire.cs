using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestan.Model.Buildings;

/// <summary>
/// Spire de Défense — bâtiment magique de défense passive. Une fois par seconde, chaque spire active
/// frappe un monstre situé à <see cref="AttackRangeInHexes"/> hexs ou moins de sa ville : 1 Cristal
/// consommé, 1 dégât infligé, sans réduction d'armure (voir DefenseSpireEngine).
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

    /// <summary>Dégâts par tir, appliqués sans réduction d'armure.</summary>
    public const int DamagePerAttack = 1;

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

    // Niveau max 1 : aucune amélioration à payer.
    public override ResourceSet GetUpgradeCost(int level) => new ResourceSet();
}
