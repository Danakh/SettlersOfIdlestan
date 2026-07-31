using System.Text.Json.Serialization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.Localization;

namespace SettlersOfIdlestan.Model.Monsters;

[Serializable]
public class Bandit : MonsterFeature
{
    /// <summary>Conservé pour la compatibilité avec les tests existants.</summary>
    public const long RaidIntervalTicks = 200L;
    public const int BanditMaxHp = 20;
    public const int BanditMaxHpPerLevel = 5;
    public const int BanditAttackDamagePerLevel = 2;
    public const double BanditHpRegenPerLevel = 0.5;
    public const long BanditHpRegenIntervalTicks = 100L;

    public override int MaxHp => BanditMaxHp + BanditMaxHpPerLevel * (Level - 1);
    public override bool BlocksHarvest => true;
    public override bool CanMove => true;
    public override long MovementIntervalTicks => 3_000L;
    public override long DepartureCooldownTicks => 1_000L;
    public override double HpRegenAmount => BanditHpRegenPerLevel * (Level - 1);
    public override long HpRegenIntervalTicks => BanditHpRegenIntervalTicks;
    public override int AttackRangeInHexes => 1;
    public override long AttackIntervalTicks => RaidIntervalTicks;
    public override int AttackDamage => BanditAttackDamagePerLevel * (Level - 1);
    public override int AttackResources => 1;

    public override string? SvgIconResourceName => "Resources.icons.military.bandit.svg";

    public override GameEventType DiscoveredEventType => GameEventType.BanditDiscovered;
    public override GameEventType RemovedEventType    => GameEventType.BanditDefeated;

    public override LocalizedEntry GetTooltipEntry() => Level > 1
        ? new("hex_tooltip_bandit_info_leveled", [Hp, MaxHp, Level])
        : new("hex_tooltip_bandit_info", [Hp, MaxHp]);

    public Bandit(HexCoord position, long lastMovedTick = 0, int level = 1) : base(position)
    {
        LastMovedTick = lastMovedTick;
        Level = level;
        Hp = MaxHp;
    }

    [JsonConstructor]
    public Bandit() : base()
    {
        Hp = MaxHp;
    }
}
