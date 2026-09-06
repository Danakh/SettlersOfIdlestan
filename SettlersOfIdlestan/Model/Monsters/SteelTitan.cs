using System.Text.Json.Serialization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.Localization;

namespace SettlersOfIdlestan.Model.Monsters;

/// <summary>
/// Titan d'Acier — colosse allié né de l'achèvement de son chantier (voir
/// <see cref="IslandFeatures.SteelTitanSite"/>). Monstre « ami » comme l'Aventurier : il traque les
/// monstres errants au lieu d'attaquer les villes, n'est jamais ciblé par les soldats, et reste
/// cantonné au territoire déjà exploré (voir MonsterFeatureController).
///
/// <para>Ses statistiques de combat sont exactement celles d'un Aventurier de niveau
/// <see cref="TitanLevel"/>, à ceci près que ses points de vie sont multipliés par
/// <see cref="HpMultiplier"/>. Contrairement à l'Aventurier, aucun Relais ne le fait réapparaître :
/// une fois tombé, il faut rebâtir un chantier.</para>
/// </summary>
[Serializable]
public class SteelTitan : MonsterFeature
{
    /// <summary>Niveau d'Aventurier dont le Titan reprend les statistiques.</summary>
    public const int TitanLevel = 4;

    /// <summary>Facteur appliqué aux points de vie d'un Aventurier de <see cref="TitanLevel"/>.</summary>
    public const int HpMultiplier = 5;

    public override int MaxHp =>
        (Adventurer.AdventurerMaxHpBase + Adventurer.AdventurerMaxHpPerLevel * TitanLevel) * HpMultiplier;

    public override bool BlocksHarvest => false;
    public override double Armor => TitanLevel - 1;

    public override bool CanMove => true;
    public override long MovementIntervalTicks => 300L;
    public override bool CanCrossWater => TitanLevel >= Adventurer.AdventurerWaterCrossingMinLevel;

    public override double HpRegenAmount => Adventurer.AdventurerHpRegenBase + Adventurer.AdventurerHpRegenPerLevel * TitanLevel;
    public override long HpRegenIntervalTicks => Adventurer.AdventurerHpRegenIntervalTicks;

    public override int AttackRangeInHexes => 1;
    public override long AttackIntervalTicks => 200L;
    public override int AttackDamage =>
        Adventurer.AdventurerAttackDamageBase + Adventurer.AdventurerAttackDamagePerLevel * TitanLevel;
    public override bool AttacksOtherMonsters => true;

    public override GameEventType DiscoveredEventType => GameEventType.SteelTitanBuilt;
    public override GameEventType RemovedEventType => GameEventType.SteelTitanDefeated;

    public override string? SvgIconResourceName => "Resources.icons.military.colosse-monstre.svg";
    public override float SvgIconSize => 30f;

    public override LocalizedEntry GetTooltipEntry() => new("hex_tooltip_steel_titan", [Hp, MaxHp]);

    public SteelTitan(HexCoord position) : base(position)
    {
        // Bâti par le joueur, donc jamais « à découvrir » : sans cela le colosse resterait figé sur
        // sa case tant qu'un passage de FeatureController ne l'aurait pas révélé.
        Found = true;
        Level = TitanLevel;
        Hp = MaxHp;
    }

    [JsonConstructor]
    public SteelTitan() : base() { Level = TitanLevel; Hp = MaxHp; }
}
