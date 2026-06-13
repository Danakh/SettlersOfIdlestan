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
    public const long TrollHpRegenIntervalTicks = 200L;

    public override int MaxHp => TrollMaxHp;
    public override bool BlocksHarvest => true;

    public override bool CanMove => true;
    public override long MovementIntervalTicks => 4_000L;
    public override long DepartureCooldownTicks => 1_000L;

    public override int HpRegenAmount => 2;
    public override long HpRegenIntervalTicks => TrollHpRegenIntervalTicks;

    public override int AttackRangeInHexes => 1;
    public override long AttackIntervalTicks => 200L;
    public override bool IgnoresPalisade => true;
    public override int AttackDamage => 1;
    public override int AttackResources => 2;

    public override GameEventType DiscoveredEventType => GameEventType.TrollDiscovered;
    public override GameEventType RemovedEventType => GameEventType.TrollDefeated;

    public override string? SvgIconResourceName => "Resources.icons.military.monster-troll.svg";
    public override float IconSizeFactor => 1.4f;

    public override LocalizedEntry GetTooltipEntry() => new("hex_tooltip_troll_info", [Hp, MaxHp]);

    public Troll(HexCoord position) : base(position) { Hp = TrollMaxHp; }

    [JsonConstructor]
    public Troll() : base() { Hp = TrollMaxHp; }
}
