using System.Text.Json.Serialization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.Localization;

namespace SettlersOfIdlestan.Model.Monsters;

/// <summary>
/// Démon majeur — monstre errant des Abysses, réservé à cette couche (voir
/// AutoExtendController.RollAbyssDemon). Deux fois plus résistant et destructeur que le démon
/// mineur dont il partage la mobilité.
/// </summary>
[Serializable]
public class MajorDemon : MonsterFeature
{
    public const int MajorDemonMaxHp = MinorDemon.MinorDemonMaxHp * 2;
    public const long MajorDemonMovementIntervalTicks = MinorDemon.MinorDemonMovementIntervalTicks;
    public const int MajorDemonMovementRangeInHexes = MinorDemon.MinorDemonMovementRangeInHexes;
    public const long MajorDemonAttackIntervalTicks = MinorDemon.MinorDemonAttackIntervalTicks;

    public override int MaxHp => MajorDemonMaxHp;
    public override bool BlocksHarvest => true;

    public override bool CanMove => true;
    public override long MovementIntervalTicks => MajorDemonMovementIntervalTicks;
    public override int MovementRangeInHexes => MajorDemonMovementRangeInHexes;

    public override int HpRegenAmount => 2;
    public override long HpRegenIntervalTicks => Dragon.DragonHpRegenIntervalTicks;

    public override int AttackRangeInHexes => 2;
    public override long AttackIntervalTicks => MajorDemonAttackIntervalTicks;
    public override bool IgnoresPalisade => true;
    public override int AttackDamage => 4;
    public override int AttackResources => 10;

    public override GameEventType DiscoveredEventType => GameEventType.MajorDemonDiscovered;
    public override GameEventType RemovedEventType => GameEventType.MajorDemonDefeated;

    public override string? SvgIconResourceName => "Resources.icons.military.monster-greater-demon.svg";
    public override float IconSizeFactor => 1.8f;

    public override LocalizedEntry GetTooltipEntry() => new("hex_tooltip_major_demon_info", [Hp, MaxHp]);

    public MajorDemon(HexCoord position) : base(position) { Hp = MajorDemonMaxHp; }

    [JsonConstructor]
    public MajorDemon() : base() { Hp = MajorDemonMaxHp; }
}
