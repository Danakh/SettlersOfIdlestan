using System.Text.Json.Serialization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.Localization;

namespace SettlersOfIdlestan.Model.Monsters;

/// <summary>
/// Démon mineur — monstre errant de l'Inframonde lié à la corruption de l'île.
/// Aussi puissant qu'un Dragon (PV, dégâts, vol de ressources), mais beaucoup plus mobile :
/// il se déplace souvent et de plusieurs hexes à la fois, au prix d'un cooldown de pillage allongé.
/// </summary>
[Serializable]
public class MinorDemon : MonsterFeature
{
    public const int MinorDemonMaxHp = Dragon.DragonMaxHp;
    public const long MinorDemonMovementIntervalTicks = 1_000L;
    public const int MinorDemonMovementRangeInHexes = 2;
    public const long MinorDemonAttackIntervalTicks = 2_000L;

    public override int MaxHp => MinorDemonMaxHp;
    public override bool BlocksHarvest => true;

    public override bool CanMove => true;
    public override long MovementIntervalTicks => MinorDemonMovementIntervalTicks;
    public override int MovementRangeInHexes => MinorDemonMovementRangeInHexes;

    public override int HpRegenAmount => 1;
    public override long HpRegenIntervalTicks => Dragon.DragonHpRegenIntervalTicks;

    public override int AttackRangeInHexes => 2;
    public override long AttackIntervalTicks => MinorDemonAttackIntervalTicks;
    public override bool IgnoresPalisade => true;
    public override int AttackDamage => 2;
    public override int AttackResources => 5;

    public override GameEventType DiscoveredEventType => GameEventType.MinorDemonDiscovered;
    public override GameEventType RemovedEventType => GameEventType.MinorDemonDefeated;

    public override string? SvgIconResourceName => "Resources.icons.military.monster-demon.svg";
    public override float IconSizeFactor => 1.8f;

    public override LocalizedEntry GetTooltipEntry() => new("hex_tooltip_minor_demon_info", [Hp, MaxHp]);

    public MinorDemon(HexCoord position) : base(position) { Hp = MinorDemonMaxHp; }

    [JsonConstructor]
    public MinorDemon() : base() { Hp = MinorDemonMaxHp; }
}
