using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.Localization;

namespace SettlersOfIdlestan.Model.Monsters;

[Serializable]
public class BanditHideout : MonsterFeature
{
    public const int MaxBanditsOnIsland = 10;
    public const long SpawnIntervalTicks = 5_000L;

    public override int MaxHp => 50;
    public override bool BlocksHarvest => true;

    public override string? SvgIconResourceName => "Resources.icons.features.skullcave.svg";
    public override float SvgIconSize => 32f;

    public override GameEventType DiscoveredEventType => GameEventType.BanditHideoutDiscovered;
    public override GameEventType RemovedEventType    => GameEventType.BanditHideoutDestroyed;

    public override LocalizedEntry? GetTooltipEntry() =>
        Found ? new("hex_tooltip_bandit_hideout_info", [Hp, MaxHp]) : null;

    public override MonsterFeature? TrySpawn(IReadOnlyList<MonsterFeature> existingMonsters, long tick)
    {
        if (!Found) return null;
        if (tick - LastSpawnTick < SpawnIntervalTicks) return null;
        if (existingMonsters.Count(m => m is Bandit) >= MaxBanditsOnIsland) return null;
        LastSpawnTick = tick;
        return new Bandit(Position, tick);
    }

    public BanditHideout(HexCoord position) : base(position) { Hp = MaxHp; }

    [JsonConstructor]
    public BanditHideout() : base() { Hp = MaxHp; }
}
