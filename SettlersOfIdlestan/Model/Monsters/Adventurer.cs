using System.Text.Json.Serialization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.Localization;

namespace SettlersOfIdlestan.Model.Monsters;

/// <summary>
/// Aventurier — invoqué par la Guilde des Aventuriers (Inframonde). Se déplace activement vers le
/// monstre errant le plus proche (au lieu d'errer au hasard comme les autres monstres) et le
/// combat au lieu des villes. Pas de régénération. Remplacé par un nouvel Aventurier à sa mort.
/// </summary>
[Serializable]
public class Adventurer : MonsterFeature
{
    public const int AdventurerMaxHp = 40;

    public override int MaxHp => AdventurerMaxHp;
    public override bool BlocksHarvest => false;

    public override bool CanMove => true;
    public override long MovementIntervalTicks => 100L;
    public override long DepartureCooldownTicks => 1_000L;

    public override int AttackRangeInHexes => 1;
    public override long AttackIntervalTicks => 200L;
    public override int AttackDamage => 3;
    public override bool AttacksOtherMonsters => true;

    public override GameEventType DiscoveredEventType => GameEventType.AdventurerDiscovered;
    public override GameEventType RemovedEventType => GameEventType.AdventurerDefeated;

    public override string? SvgIconResourceName => "Resources.icons.military.hero-armor.svg";

    public override LocalizedEntry GetTooltipEntry() => new("hex_tooltip_adventurer_info", [Hp, MaxHp]);

    /// <summary>Position de la ville dont la Guilde a invoqué cet Aventurier, pour réinitialiser le cooldown de réapparition de la bonne Guilde à sa mort.</summary>
    public Vertex? SpawnCityPosition { get; set; }

    public Adventurer(HexCoord position) : base(position) { Hp = AdventurerMaxHp; }

    [JsonConstructor]
    public Adventurer() : base() { Hp = AdventurerMaxHp; }
}
