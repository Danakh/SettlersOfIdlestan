using System.Text.Json.Serialization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.Localization;

namespace SettlersOfIdlestan.Model.Monsters;

/// <summary>
/// Troll des cavernes — monstre errant de l'Inframonde. Régénère rapidement ses PV :
/// il faut le tuer vite ou il revient toujours à pleine santé.
/// </summary>
[Serializable]
public class Troll : MonsterFeature
{
    public const int TrollMaxHp = 40;
    public const int TrollMaxHpPerLevel = 10;
    public const int TrollAttackDamagePerLevel = 2;
    public const double TrollHpRegenPerLevel = 0.5;
    public const long TrollHpRegenIntervalTicks = 100L;

    public override int MaxHp => TrollMaxHp + TrollMaxHpPerLevel * (Level - 1);
    public override bool BlocksHarvest => true;
    public override double Armor => 0.5 * Level;

    public override bool CanMove => true;
    public override long MovementIntervalTicks => 4_000L;
    public override long DepartureCooldownTicks => 1_000L;

    public override double HpRegenAmount => 2 + TrollHpRegenPerLevel * (Level - 1);
    public override long HpRegenIntervalTicks => TrollHpRegenIntervalTicks;

    public override int AttackRangeInHexes => 1;
    public override long AttackIntervalTicks => 200L;
    public override bool IgnoresPalisade => true;
    public override int AttackDamage => 3 + TrollAttackDamagePerLevel * (Level - 1);
    public override int AttackResources => 2;

    public override GameEventType DiscoveredEventType => GameEventType.TrollDiscovered;
    public override GameEventType RemovedEventType => GameEventType.TrollDefeated;

    public override string? SvgIconResourceName => "Resources.icons.military.monster-troll.svg";
    public override float IconSizeFactor => 1.4f;

    public override LocalizedEntry GetTooltipEntry() => Level > 1
        ? new("hex_tooltip_troll_info_leveled", [Hp, MaxHp, Level])
        : new("hex_tooltip_troll_info", [Hp, MaxHp]);

    public Troll(HexCoord position, int level = 1) : base(position) { Level = level; Hp = MaxHp; }

    [JsonConstructor]
    public Troll() : base() { Hp = MaxHp; }
}
