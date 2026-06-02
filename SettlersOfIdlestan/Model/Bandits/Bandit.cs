using System.Text.Json.Serialization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.Monsters;
using SettlersOfIdlestan.Services.Localization;

namespace SettlersOfIdlestan.Model.Bandits;

[Serializable]
public class Bandit : MonsterFeature
{
    /// <summary>Conservé pour la compatibilité avec les tests existants.</summary>
    public const long RaidIntervalTicks = 100L;

    public override int MaxHp => 20;
    public override bool BlocksHarvest => true;
    public override bool CanMove => true;
    public override long MovementIntervalTicks => 3_000L;
    public override int AttackRangeInHexes => 1;
    public override long AttackIntervalTicks => RaidIntervalTicks;
    public override int AttackResources => 1;

    public override GameEventType DiscoveredEventType => GameEventType.BanditDiscovered;
    public override GameEventType RemovedEventType    => GameEventType.BanditDefeated;

    public override LocalizedEntry GetTooltipEntry() => new("hex_tooltip_bandit_info", [Hp, MaxHp]);

    public Bandit(HexCoord position, long lastMovedTick = 0) : base(position)
    {
        LastMovedTick = lastMovedTick;
        Hp = MaxHp;
    }

    [JsonConstructor]
    public Bandit() : base()
    {
        Hp = MaxHp;
    }
}
