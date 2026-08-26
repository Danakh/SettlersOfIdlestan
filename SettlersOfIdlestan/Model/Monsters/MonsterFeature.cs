using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandFeatures;

namespace SettlersOfIdlestan.Model.Monsters;

/// <summary>
/// Classe de base pour toutes les features de type "monstre" : présence sur la carte,
/// points de vie, combat avec les soldats, capacité optionnelle d'attaquer les villes.
/// </summary>
[Serializable]
public abstract class MonsterFeature : IslandFeature
{
    public int Hp { get; set; }
    // Ignorées en JSON : propriétés calculées sans setter, constantes par sous-type de monstre —
    // jamais restaurées à la lecture, les sérialiser gonflait chaque feature pour rien (cf.
    // IslandFeature pour le même constat sur la classe de base).
    [JsonIgnore]
    public abstract int MaxHp { get; }

    /// <summary>Niveau du monstre (1 = stats de base). Voir MonsterLeveling pour le calcul au spawn.</summary>
    public int Level { get; set; } = 1;

    /// <summary>
    /// Réduit les dégâts subis (soldats, Aventurier, rituels) : voir <see cref="ApplyArmorReduction"/>
    /// pour la formule. 0 = aucune réduction (valeur par défaut).
    /// </summary>
    [JsonIgnore]
    public virtual double Armor => 0;

    /// <summary>
    /// Applique la réduction d'armure à un dégât entrant : réduction = Armor / 2, arrondie à l'entier
    /// inférieur, avec une chance supplémentaire — proportionnelle au reste fractionnaire — d'arrondir
    /// au supérieur. Pour une Armure paire, la réduction est déterministe (ex. Armure 2 → -1 toujours).
    /// Pour une Armure impaire (ou une valeur à mi-palier, comme le 0.5×niveau du Troll/Ogre), le
    /// reste fractionnaire (0.5, 0.25...) devient une probabilité, pour que la réduction moyenne sur
    /// la durée reste exactement égale à Armor / 2 malgré des PV entiers.
    /// </summary>
    public static int ApplyArmorReduction(int rawDamage, double armor, GamePRNG prng)
    {
        if (armor <= 0 || rawDamage <= 0) return rawDamage;

        double half = armor / 2.0;
        int reduction = (int)Math.Floor(half);
        double fractional = half - reduction;
        if (fractional > 0 && prng.Next(1000) < (int)Math.Round(fractional * 1000))
            reduction++;

        return Math.Max(0, rawDamage - reduction);
    }

    /// <summary>Tick du dernier combat initié par les soldats ennemis.</summary>
    public long LastAttackedByMilitaryTick { get; set; } = 0;

    /// <summary>Index de la civilisation qui a porté le coup fatal. -1 = inconnu / non tué par un joueur.</summary>
    [JsonIgnore]
    public int KilledByCivilizationIndex { get; set; } = -1;

    [JsonIgnore]
    public override float SvgIconSize => 24f;
    [JsonIgnore]
    public override bool ShouldRenderIcon => false;

    // ── Mouvement (opt-in) ─────────────────────────────────────────────────
    [JsonIgnore]
    public override bool CanMove => false;
    [JsonIgnore]
    public virtual long MovementIntervalTicks => long.MaxValue;
    /// <summary>Nombre d'hexes parcourus à chaque déplacement (1 = un seul pas).</summary>
    [JsonIgnore]
    public virtual int MovementRangeInHexes => 1;
    /// <summary>Ticks de blocage de récolte laissés sur l'ancien hex après départ. 0 = pas de cooldown.</summary>
    [JsonIgnore]
    public virtual long DepartureCooldownTicks => 0L;
    /// <summary>Tick du dernier déplacement (utilisé pour la grâce après mouvement).</summary>
    public long LastMovedTick { get; set; } = 0;
    /// <summary>Peut se déplacer sur les hex d'Eau/Eau profonde, normalement infranchissables.</summary>
    [JsonIgnore]
    public virtual bool CanCrossWater => false;
    /// <summary>Peut se déplacer sur les hex de Void (bordure de l'Abysse), normalement infranchissables.</summary>
    [JsonIgnore]
    public virtual bool CanCrossVoid => false;

    // ── Régénération de PV (opt-in) ────────────────────────────────────────
    /// <summary>Peut être fractionnaire (bonus de +0.5/niveau) — voir MonsterFeatureController.RegenHp pour l'accumulation.</summary>
    [JsonIgnore]
    public virtual double HpRegenAmount => 0;
    [JsonIgnore]
    public virtual long HpRegenIntervalTicks => long.MaxValue;
    public long LastHpRegenTick { get; set; } = 0;
    /// <summary>Reste fractionnaire accumulé entre deux régénérations, pour appliquer correctement un HpRegenAmount non entier.</summary>
    public double HpRegenCarry { get; set; } = 0;

    // ── Attaque des villes (opt-in) ────────────────────────────────────────
    /// <summary>Portée en hexes : 0 = n'attaque pas, 1 = hex propre, 2 = hex propre + voisins.</summary>
    [JsonIgnore]
    public virtual int AttackRangeInHexes => 0;
    [JsonIgnore]
    public virtual long AttackIntervalTicks => long.MaxValue;
    [JsonIgnore]
    public virtual bool IgnoresPalisade => false;
    /// <summary>
    /// Dégâts infligés en cascade : soldats d'abord, puis défense, puis niveaux de Townhall (1 par point).
    /// Quand le Townhall tombe à 0 il est détruit ; les dégâts restants détruisent la ville.
    /// </summary>
    [JsonIgnore]
    public virtual int AttackDamage => 0;
    /// <summary>Ressources volées (une par tirage) lors d'une attaque réussie.</summary>
    [JsonIgnore]
    public virtual int AttackResources => 0;
    public long LastAttackTick { get; set; } = 0;
    public Vertex? LastAttackTargetVertex { get; set; } = null;
    /// <summary>Hex cible de la dernière attaque contre un autre monstre (Aventurier), pour l'animation.</summary>
    public HexCoord? LastAttackTargetHex { get; set; } = null;
    /// <summary>Noms des ressources volées lors de la dernière attaque (séparés par virgule), pour l'animation.</summary>
    public string? LastAttackResourcesString { get; set; } = null;

    /// <summary>
    /// Opt-in : ce monstre est « ami » et combat les autres monstres au lieu des villes
    /// (utilise quand même AttackRangeInHexes/AttackIntervalTicks/AttackDamage ci-dessus).
    /// Les monstres amis ne s'attaquent jamais entre eux et sont ignorés par le combat
    /// automatique des soldats (cf. MonsterCombatEngine).
    /// </summary>
    [JsonIgnore]
    public virtual bool AttacksOtherMonsters => false;

    /// <summary>
    /// Opt-in : ce monstre se déplace et attaque normalement même hors du champ de vision du joueur
    /// (<see cref="IslandFeature.Found"/> à false), au lieu de rester figé sur sa case de spawn comme
    /// les monstres errants classiques (Bandit, Troll, Ogre — gardiens de butin immobiles tant que non
    /// découverts, voir MonsterFeatureController.Update). Pensé pour les Démons (mineur/majeur) : posés
    /// en bordure de zone explorée par TrySpawnBorderMonsters, ils doivent activement traquer le joueur
    /// plutôt que de s'accumuler invisiblement jusqu'à apparaître en paquet à la prochaine révélation.
    /// </summary>
    [JsonIgnore]
    public virtual bool ActiveWhileHidden => false;

    // ── Génération de Corruption (opt-in) ──────────────────────────────────
    /// <summary>
    /// Opt-in : ce monstre fait monter d'un point la Corruption de son propre hex à chaque
    /// intervalle, en la semant à niveau 1 si l'hex est sain, jusqu'au plafond
    /// <see cref="Controller.Island.CorruptionController.GetMonsterCorruptionCap"/> — deux fois le
    /// niveau de corruption de l'île (voir CorruptionController.ProcessMonsterCorruptionGrowth).
    /// Le tuer tarit la source, comme purifier des Os Divins.
    /// </summary>
    [JsonIgnore]
    public virtual bool GeneratesCorruption => false;

    // ── Invocation de nouvelles créatures (opt-in) ─────────────────────────
    /// <summary>Tente de générer une nouvelle MonsterFeature. Retourne null si aucune invocation n'a lieu.</summary>
    public virtual MonsterFeature? TrySpawn(IReadOnlyList<MonsterFeature> existingMonsters, long tick, int level = 1) => null;
    public long LastSpawnTick { get; set; } = 0;

    protected MonsterFeature(HexCoord position) : base(position) { }
    protected MonsterFeature() : base() { }
}
