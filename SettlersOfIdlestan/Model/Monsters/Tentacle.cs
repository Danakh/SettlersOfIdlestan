using System.Text.Json.Serialization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.Localization;

namespace SettlersOfIdlestan.Model.Monsters;

/// <summary>
/// Tentacule — excroissance de l'Abysse enracinée dans son hex : exactement les statistiques d'un
/// <see cref="MajorDemon"/> (PV, armure, dégâts, régénération, portée) mais aucune mobilité, ce qui
/// en fait la seule cible de haut niveau que le joueur peut assiéger à son rythme. Apparaît sur les
/// îles de l'Abysse à partir du niveau de corruption
/// <see cref="Controller.Island.AutoExtendController.TentacleMinCorruptionLevel"/> (voir
/// AutoExtendController.PlaceTentacle) et garde le Pandémonium : en tuer une dans l'Abysse ouvre un
/// Portail du Pandémonium à construire (voir PandemoniumGateController).
/// </summary>
[Serializable]
public class Tentacle : MonsterFeature
{
    public override int MaxHp => MajorDemon.MajorDemonMaxHp + MajorDemon.MajorDemonMaxHpPerLevel * (Level - 1);
    public override bool BlocksHarvest => true;
    public override double Armor => 1;

    // Enracinée : pas de CanMove, donc pas d'intervalle ni de portée de déplacement à déclarer.
    public override bool CanMove => false;

    public override double HpRegenAmount => 3 + MajorDemon.MajorDemonHpRegenPerLevel * (Level - 1);
    public override long HpRegenIntervalTicks => Dragon.DragonHpRegenIntervalTicks;

    public override int AttackRangeInHexes => 2;
    public override long AttackIntervalTicks => MajorDemon.MajorDemonAttackIntervalTicks;
    public override bool IgnoresPalisade => true;
    public override int AttackDamage => 7 + MajorDemon.MajorDemonAttackDamagePerLevel * (Level - 1);
    public override int AttackResources => 10;

    public override GameEventType DiscoveredEventType => GameEventType.TentacleDiscovered;
    public override GameEventType RemovedEventType => GameEventType.TentacleDefeated;

    public override string? SvgIconResourceName => "Resources.icons.military.kraken-tentacule.svg";
    public override float IconSizeFactor => 1.8f;

    public override LocalizedEntry GetTooltipEntry() => Level > 1
        ? new("hex_tooltip_tentacle_info_leveled", [Hp, MaxHp, Level])
        : new("hex_tooltip_tentacle_info", [Hp, MaxHp]);

    public Tentacle(HexCoord position, int level = 1) : base(position) { Level = level; Hp = MaxHp; }

    [JsonConstructor]
    public Tentacle() : base() { Hp = MaxHp; }
}
