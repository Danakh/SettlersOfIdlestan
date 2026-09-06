using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestan.Model.Buildings;

/// <summary>
/// Grande Forge de Mithril — bâtiment unique de l'Inframonde, constructible uniquement dans une ville
/// qui exploite déjà une Mine de Mithril niveau 2.
///
/// <para>Activable : tant qu'elle est active et que le Mithril suit, elle équipe <b>tous</b> les
/// Aventuriers de la civilisation (+1 dégât, +1 armure) en échange de
/// <see cref="MithrilPerAdventurerPerSecond"/> Mithril par seconde et par Aventurier en vie — voir
/// MithrilGreatForgeEngine. Désactivée, ou à court de Mithril, elle ne coûte rien et n'apporte
/// rien.</para>
///
/// <para>Verrouillée par défaut ; débloquée par le vertex de prestige Grande Forge de Mithril
/// (+1 niveau max).</para>
/// </summary>
public class MithrilGreatForge : Building
{
    /// <summary>Niveau minimum de la Mine de Mithril de la ville pour pouvoir bâtir la Grande Forge.</summary>
    public const int RequiredMithrilMineLevel = 2;

    /// <summary>Mithril consommé par cycle (1 seconde) et par Aventurier en vie.</summary>
    public const int MithrilPerAdventurerPerSecond = 1;

    /// <summary>Intervalle entre deux prélèvements : 1 seconde.</summary>
    public const long UpkeepIntervalTicks = 100L;

    /// <summary>Dégâts supplémentaires accordés à chaque Aventurier tant que la forge tourne.</summary>
    public const int AdventurerAttackDamageBonus = 1;

    /// <summary>Armure supplémentaire accordée à chaque Aventurier tant que la forge tourne.</summary>
    public const int AdventurerArmorBonus = 1;

    /// <summary>Dernier tick où la forge a réellement prélevé son Mithril. Persiste : sans cela, un rechargement remettrait le cooldown à zéro.</summary>
    public long LastUpkeepTick { get; set; } = 0;

    public MithrilGreatForge() : base(BuildingType.MithrilGreatForge)
    {
        AvailableAtLevel = 4;
        ActivationStatus = ActivationStatus.ACTIVE;
    }

    public override bool IsUnique => true;

    // Verrouillée par défaut ; débloquée par le vertex de prestige Grande Forge de Mithril (+1 niveau max).
    public override int GetDefaultMaxLevel() => 0;

    public override ResourceSet GetBuildCost() => new ResourceSet
    {
        { Resource.Stone,   1200 },
        { Resource.Steel,    400 },
        { Resource.Mithril,  200 },
        { Resource.Gold,    2000 },
    };

    // Niveau max 1 : aucune amélioration à payer.
    public override ResourceSet GetUpgradeCost(int level) => new ResourceSet();

    public override bool HasBuildPrerequisites(IBuildingContext city, WorldState? state) =>
        city.HasBuildingAtLevel(BuildingType.MithrilMine, RequiredMithrilMineLevel);

    public override string? GetMissingPrerequisiteKey(IBuildingContext city, WorldState? state) =>
        HasBuildPrerequisites(city, state) ? null : "tooltip_requires_mithril_mine_2";
}
