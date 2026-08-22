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
    public const int AdventurerMaxHpBase = 80;
    public const int AdventurerMaxHpPerLevel = 20;
    public const int AdventurerAttackDamageBase = 7;
    public const int AdventurerAttackDamagePerLevel = 3;
    public const double AdventurerHpRegenBase = 2;
    public const double AdventurerHpRegenPerLevel = 0.5;
    public const long AdventurerHpRegenIntervalTicks = 100L;
    public const int AdventurerWaterCrossingMinLevel = 4;

    public override int MaxHp => AdventurerMaxHpBase + AdventurerMaxHpPerLevel * Level;
    public override bool BlocksHarvest => false;
    public override double Armor => Level - 1;

    public override bool CanMove => true;
    public override long MovementIntervalTicks => 300L;
    public override long DepartureCooldownTicks => 1_000L;
    public override bool CanCrossWater => Level >= AdventurerWaterCrossingMinLevel;

    public override double HpRegenAmount => AdventurerHpRegenBase + AdventurerHpRegenPerLevel * Level;
    public override long HpRegenIntervalTicks => AdventurerHpRegenIntervalTicks;

    public override int AttackRangeInHexes => 1;
    public override long AttackIntervalTicks => 200L;
    public override int AttackDamage => AdventurerAttackDamageBase + AdventurerAttackDamagePerLevel * Level;
    public override bool AttacksOtherMonsters => true;

    public override GameEventType DiscoveredEventType => GameEventType.AdventurerDiscovered;
    public override GameEventType RemovedEventType => GameEventType.AdventurerDefeated;

    public override string? SvgIconResourceName => "Resources.icons.military.hero-armor.svg";

    public override LocalizedEntry GetTooltipEntry() => Level >= AdventurerWaterCrossingMinLevel
        ? new("hex_tooltip_adventurer_info_leveled_water", [Hp, MaxHp, Level])
        : Level > 1
            ? new("hex_tooltip_adventurer_info_leveled", [Hp, MaxHp, Level])
            : new("hex_tooltip_adventurer_info", [Hp, MaxHp]);

    /// <summary>Position de la ville dont la Guilde a invoqué cet Aventurier, pour réinitialiser le cooldown de réapparition de la bonne Guilde à sa mort.</summary>
    public Vertex? SpawnCityPosition { get; set; }

    public Adventurer(HexCoord position, int level = 1) : base(position) { Level = level; Hp = MaxHp; }

    [JsonConstructor]
    public Adventurer() : base() { Hp = MaxHp; }
}
