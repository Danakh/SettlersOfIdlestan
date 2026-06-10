using System.Text.Json.Serialization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.Localization;

namespace SettlersOfIdlestan.Model.Monsters;

/// <summary>
/// Ogre des profondeurs — monstre errant de l'Inframonde. Lent mais dévastateur :
/// ses coups écrasent les palissades et démolissent les avant-postes mal défendus.
/// </summary>
[Serializable]
public class Ogre : MonsterFeature
{
    public const int OgreMaxHp = 60;

    public override int MaxHp => OgreMaxHp;
    public override bool BlocksHarvest => true;

    public override bool CanMove => true;
    public override long MovementIntervalTicks => 6_000L;
    public override long DepartureCooldownTicks => 1_500L;

    public override int AttackRangeInHexes => 2;
    public override long AttackIntervalTicks => 300L;
    public override bool IgnoresPalisade => true;
    public override int AttackDamage => 3;
    public override int AttackResources => 3;

    public override GameEventType DiscoveredEventType => GameEventType.OgreDiscovered;
    public override GameEventType RemovedEventType => GameEventType.OgreDefeated;

    public override string? SvgIconResourceName => "Resources.icons.military.monster-ogre.svg";
    public override float IconSizeFactor => 1.8f;

    public override LocalizedEntry GetTooltipEntry() => new("hex_tooltip_ogre_info", [Hp, MaxHp]);

    public Ogre(HexCoord position) : base(position) { Hp = OgreMaxHp; }

    [JsonConstructor]
    public Ogre() : base() { Hp = OgreMaxHp; }
}
