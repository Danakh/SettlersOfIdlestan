using System.Text.Json.Serialization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.Localization;

namespace SettlersOfIdlestan.Model.Monsters;

/// <summary>
/// Tentacule — excroissance de l'Abysse enracinée dans son hex : les statistiques d'un
/// <see cref="MajorDemon"/> (PV, armure, dégâts, régénération) mais aucune mobilité, ce qui
/// en fait la seule cible de haut niveau que le joueur peut assiéger à son rythme. En échange elle
/// frappe à distance et à 3 hexes, ce qui lui laisse des emplacements que ni les soldats (portée 2
/// avec Surveillance et Tour de guet) ni les Spires de Défense (portée 2) ne peuvent atteindre en
/// retour : elle harcèle de loin, et il faut avancer sur elle pour la réduire au silence. Elle alterne
/// en outre deux salves — un coup sur chacune de ses cibles, Aventurier compris, puis cinq coups sur
/// une seule —, ce qui la rend aussi dangereuse pour un front étalé que pour une ville isolée. Enfin
/// elle puise dans la Corruption de son hex — qu'elle sème elle-même — autant d'armure et de régénération
/// que celle-ci compte de niveaux : l'assainir (Temple, Spire de Corruption) l'affaiblit avant même
/// de l'attaquer. Apparaît sur les
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

    /// <summary>1 comme le démon majeur, plus le niveau de Corruption de son hex (voir CorruptionBonus).</summary>
    public override double Armor => 1 + CorruptionBonus;

    // Enracinée : pas de CanMove, donc pas d'intervalle ni de portée de déplacement à déclarer.
    public override bool CanMove => false;

    /// <summary>Enracinée dans la Corruption : fait monter d'un point celle de son hex toutes les 10 s, jusqu'à 2× le niveau de corruption de l'île (voir CorruptionController.ProcessMonsterCorruptionGrowth).</summary>
    public override bool GeneratesCorruption => true;

    /// <summary>… et s'en nourrit : la Corruption qu'elle sème lui rend armure et régénération, l'assainir la ramène à ses statistiques de base.</summary>
    public override bool EmpoweredByCorruption => true;

    /// <summary>Celle du démon majeur, plus le niveau de Corruption de son hex (voir CorruptionBonus).</summary>
    public override double HpRegenAmount => 3 + MajorDemon.MajorDemonHpRegenPerLevel * (Level - 1) + CorruptionBonus;
    public override long HpRegenIntervalTicks => Dragon.DragonHpRegenIntervalTicks;

    /// <summary>Portée 3 (son hex + deux anneaux) : elle frappe plus loin que ne portent les soldats et les Spires de Défense, il faut venir la chercher.</summary>
    public override int AttackRangeInHexes => 3;
    public override long AttackIntervalTicks => MajorDemon.MajorDemonAttackIntervalTicks;
    public override bool IgnoresPalisade => true;
    public override int AttackDamage => 7 + MajorDemon.MajorDemonAttackDamagePerLevel * (Level - 1);
    public override int AttackResources => 10;

    /// <summary>Frappe à distance : ne subit pas la riposte de l'Expédition Punitive et lance une boule de feu au lieu de se jeter sur la ville.</summary>
    public override bool HasRangedAttack => true;

    /// <summary>Coups de la salve concentrée, un intervalle d'attaque sur deux (voir AlternatesAttackPatterns).</summary>
    public const int TentacleFocusedAttackStrikes = 5;

    /// <summary>
    /// Balaie tout ce qui l'entoure un intervalle, se concentre sur une seule cible le suivant : à
    /// portée 3, la salve de zone touche facilement plusieurs villes et l'Aventurier en même temps,
    /// et la salve concentrée transforme n'importe laquelle d'entre elles en cible prioritaire.
    /// </summary>
    public override bool AlternatesAttackPatterns => true;
    public override int FocusedAttackStrikes => TentacleFocusedAttackStrikes;

    /// <summary>
    /// Vrai quand la mort de cette Tentacule vient réellement de faire surgir le Portail du
    /// Pandémonium. Posé par <c>PandemoniumGateController.OnFeatureRemoved</c>, qui est notifié
    /// pendant <c>WorldState.RemoveFeature</c> — donc avant que l'appelant ne journalise
    /// <see cref="RemovedEventType"/> : c'est ce qui permet au journal de distinguer la première
    /// Tentacule abattue (le portail s'ouvre) des suivantes (il est déjà là).
    ///
    /// Hors sauvegarde : la Tentacule est retirée du monde dans la foulée, l'information ne vit que
    /// le temps de la journalisation.
    /// </summary>
    [JsonIgnore]
    public bool OpenedPandemoniumGate { get; set; }

    public override GameEventType DiscoveredEventType => GameEventType.TentacleDiscovered;

    public override GameEventType RemovedEventType =>
        OpenedPandemoniumGate ? GameEventType.TentacleDefeated : GameEventType.TentacleDefeatedNoGate;

    public override string? SvgIconResourceName => "Resources.icons.military.kraken-tentacule.svg";
    public override float IconSizeFactor => 1.8f;

    public override LocalizedEntry GetTooltipEntry() => Level > 1
        ? new("hex_tooltip_tentacle_info_leveled", [Hp, MaxHp, Level])
        : new("hex_tooltip_tentacle_info", [Hp, MaxHp]);

    public Tentacle(HexCoord position, int level = 1) : base(position) { Level = level; Hp = MaxHp; }

    [JsonConstructor]
    public Tentacle() : base() { Hp = MaxHp; }
}
