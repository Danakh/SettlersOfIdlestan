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
    public const int MaxBanditsOnIsland = 5;
    public const long SpawnIntervalTicks = 6_000L;

    public override int MaxHp => 50;
    public override bool BlocksHarvest => true;

    public override string? SvgIconResourceName => "Resources.icons.features.skullcave.svg";
    public override float SvgIconSize => 32f;

    public override GameEventType DiscoveredEventType => GameEventType.BanditHideoutDiscovered;
    public override GameEventType RemovedEventType    => GameEventType.BanditHideoutDestroyed;

    public override LocalizedEntry? GetTooltipEntry() =>
        Found ? new("hex_tooltip_bandit_hideout_info", [Hp, MaxHp]) : null;

    public override MonsterFeature? TrySpawn(IReadOnlyList<MonsterFeature> existingMonsters, long tick, int level = 1)
    {
        if (!Found) return null;
        if (LastSpawnTick == 0) { LastSpawnTick = tick; return null; }
        if (tick - LastSpawnTick < SpawnIntervalTicks) return null;
        if (existingMonsters.Count(m => m is Bandit) >= MaxBanditsOnIsland) return null;
        // Avance d'un seul cycle (pas jusqu'à `tick`) : un appelant qui rattrape un saut de temps
        // peut ainsi rappeler TrySpawn plusieurs fois pour le même `tick` et consommer le reliquat de
        // cycles restants (voir MonsterController.UpdateSpawns), au lieu d'un unique spawn par saut.
        LastSpawnTick += SpawnIntervalTicks;
        return new Bandit(Position, tick, level);
    }

    public BanditHideout(HexCoord position) : base(position) { Hp = MaxHp; }

    [JsonConstructor]
    public BanditHideout() : base() { Hp = MaxHp; }
}
