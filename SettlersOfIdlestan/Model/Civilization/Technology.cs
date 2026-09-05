using SettlersOfIdlestan.Model.GameplayModifier;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SettlersOfIdlestan.Model.Civilization;

// Sérialisé par nom (et non par entier) afin que l'ajout ou la suppression d'une recherche
// ne décale jamais les valeurs des autres recherches dans les sauvegardes existantes.
[JsonConverter(typeof(TechnologyIdJsonConverter))]
public enum TechnologyId
{
    // Tier 0
    HarvestEfficiency,
    Artisanat,
    Agriculture,
    Architecture,
    MilitaryDiscipline,
    // Tier 1
    // [Legacy] Recherche "Récolte améliorée" supprimée (fusionnée dans HarvestEfficiency/MasterHarvest pour limiter
    // le nombre de recherches de vitesse de récolte) — conservée uniquement pour la désérialisation des anciennes sauvegardes.
    ImprovedHarvest,
    StorageOptimization,
    Archivage,
    Orpaillage,
    MilitaryTactics,
    Scouting,
    Fortifications,
    MilitaryBuildings,
    RapidConstruction,
    // [Legacy] Recherche "Compagnonage" supprimée (fusionnée dans HarvestTools pour limiter le nombre
    // de recherches de bâtiments de production) — conservée uniquement pour la désérialisation des anciennes sauvegardes.
    Compagnonage,
    // Tier 2
    HarvestTools,
    AdvancedArchitecture,
    ResearchMethods,
    Metallurgy,
    // [Legacy] Recherche "Maîtrise Militaire" supprimée — conservée uniquement pour la désérialisation des anciennes sauvegardes.
    MilitaryMastery,
    SpecializedMarket,
    // Tier 3
    MasterHarvest,
    GrandArchitecture,
    // [Legacy] Recherche "Académie des sciences" supprimée (fusionnée dans Archivage/ImprovedResearch pour limiter
    // le nombre de recherches de vitesse de recherche) — conservée uniquement pour la désérialisation des anciennes sauvegardes.
    Scholarship,
    MaitriseDesAlliages,
    SteelWeapons,
    AdvancedTactics,
    EfficientTrading,
    Surveillance,
    // Tier 4
    // [Legacy] Recherche "Récolte épique" supprimée (fusionnée dans MasterHarvest/OutilsEnMithril pour limiter
    // le nombre de recherches de vitesse de récolte) — conservée uniquement pour la désérialisation des anciennes sauvegardes.
    EpicHarvest,
    // [Legacy] Recherche "Routes commerciales" supprimée — conservée uniquement pour la désérialisation des anciennes sauvegardes.
    TradeRoutes,
    ImprovedResearch,
    // [Legacy] Recherche "Stratégie avancée" (attaque auto. des villes ennemies) remplacée par Patrol
    // (patrouille anti-monstres) — conservée uniquement pour la désérialisation des anciennes sauvegardes.
    AdvancedStrategy,
    // Débloque la construction du Camp Mobile. Rapprochée de la racine de l'arbre (tier -2, coût /16) :
    // Watchtower et Rail Logistics en dépendent désormais, plutôt que l'inverse (voir TechnologyDefinitions).
    MobileCampConstruction,
    // Répétable à l'infini (comme MasterHarvest) : +5% UNIT_PRODUCTION_SPEED par complétion, coût
    // doublé à chaque relance. Prérequis : Rail Logistics (et non plus le Camp Mobile).
    EntrainementIntensif,
    // Même tier que WarHerald, une ligne au-dessus. Prérequis : EntrainementIntensif. +25% ATTACK_SPEED.
    DeploiementRapide,
    // Un tier au-dessus d'EntrainementIntensif (seul prérequis), également débloquée par le vertex
    // de prestige Raids. Raid gratuit sur une ville alliée : redirige tous les flux de renfort vers
    // la cible, sauf les emplacements ayant un flux d'attaque actif.
    WarHerald,
    // [Legacy] Recherche "Patrouille" (raid anti-monstres automatique) supprimée — conservée
    // uniquement pour la désérialisation des anciennes sauvegardes.
    Patrol,
    // Même ligne que WarHerald, son seul prérequis désormais (Patrol supprimée). Raids
    // automatiques sur une civilisation.
    Vendetta,
    AutomaticMarket,
    Speleologie,
    // Tier 5
    MasterResearch,
    GreatLighthouseConstruction,
    AdvancedTradingPosts,
    Siderurgie,
    CultureFongique,
    // Suite de CultureFongique : permet aux Scieries de récolter du Bois sur les Cavernes aux
    // Champignons (Inframonde), à moitié vitesse par rapport aux forêts.
    BoisDeChampignon,
    CartographieSouterraine,
    WatchtowerConstruction,
    // Tier 6
    SteelArmor,
    TemperedSteel,
    RailLogistics,
    OutilsEnMithril,
    RempartsDeFer,
    // Un tier au-dessus du Grand Temple (seul prérequis) — accélère la construction automatique
    // des bâtiments par les guildes, proportionnellement au nombre de villes de l'empire.
    AdvancedGuilds,
    // Se place désormais entre Grande Architecture et Guildes Avancées (tier 5) — débloque le Grand
    // Temple, bâtiment unique qui automatise la construction des Temples.
    GrandTempleConstruction,
    // Prend l'ancien emplacement du Grand Temple (tier 7, un prérequis : Guildes Avancées). Débloque
    // une nouvelle feature (à implémenter) — pas encore de modificateur associé.
    AutomationPreset,
    // Tier 7
    ProspectionAvancee,
    RempartsDeMithril,
    // [Legacy] Recherche "Aciers Spéciaux" supprimée — conservée uniquement pour la désérialisation des anciennes sauvegardes.
    SpecialSteels,
    // Branche de la Magie (débloquée par le vertex de prestige Secret de la Magie)
    MagicInitiation,
    ArdentForgeRitual,
    ClairvoyanceRitual,
    MartialBlessingRitual,
    ArcaneShieldRitual,
    // Branche des Sorts Instantanés (débloquée par le vertex de prestige Invocations)
    Invocation,
    TroopSummoning,
    ArcaneEdification,
    // Recherches de bonus de prestige (capstones de branches existantes)
    ChroniquesDuGuet,
    // [Legacy] Recherche "Renommée Commerciale" supprimée — conservée uniquement pour la désérialisation des anciennes sauvegardes.
    RenommeeCommerciale,
    SagesseSouterraine,
    // Racine de la branche des Abysses (débloquée par le vertex de prestige Brèche Abyssale, sans
    // prérequis). Permet de construire des routes entre deux hexagones de Vide (comme les routes
    // maritimes entre deux hexagones d'eau), moyennant un coût croissant en points de recherche par
    // route déjà bâtie.
    VoidWalking,
    // Suite de VoidWalking : révèle la feature Os Divins sur les îles des Abysses générées après la
    // première. Reprend la place de l'ancienne EtudeDesAbysses (supprimée) : tier -1, coût / 4.
    // Baissée d'un tier supplémentaire (coût / 4) pour la rendre accessible plus tôt.
    VoidCompass,
    // Un tier au-dessus des Chroniques du Guet (seul prérequis)
    Diplomatie,
    // Branche de la Volcanologie (convergence Sidérurgie × risque volcanique)
    Volcanologie,
    VolcanicMetallurgy,
    // Suite de la Cartographie Souterraine : les Tours de Guet étant interdites dans l'Outremonde,
    // cette recherche ralentit les apparitions de monstres de bordure sans bâtiment dédié.
    VeilleSouterraine,
    // [Legacy] Recherche "Étude des Abysses" supprimée (remplacée par VoidCompass) — conservée
    // uniquement pour la désérialisation des anciennes sauvegardes.
    EtudeDesAbysses,
    Demonologie,
    ResistanceALaCorruption,
    PacteAbyssal,
    // [Legacy] Recherche "Secrets of the Rift" supprimée (Théologie de l'Ascension ne dépend plus
    // que de PacteAbyssal) — conservée uniquement pour la désérialisation des anciennes sauvegardes.
    SecretsDeLaFaille,
    TheologieDeLAscension,
    // Capstones des branches existantes (tiers 12-13)
    AcierAbyssal,
    MagieDuVide,
    // Suite de la Magie du Vide : débloque le sort Pont du Vide, qui bâtit d'un coup les trois routes
    // autour d'un vertex bordé de Vide contre des cristaux, sans coût en points de recherche.
    PontDuVide,
    // Seconde suite de la Magie du Vide : débloque l'Observatoire, monument de Montagne dont chaque
    // niveau abaisse le multiplicateur du coût en points de recherche des routes du Vide (×3 → ×2).
    CartesDesEtoiles,
    CoeurDeLaTerre,
    // Un tier au-dessus de l'Acier Abyssal et du Cœur de la Terre, dont elle dépend : chaque Fonderie
    // gagne 3% de vitesse par niveau de Dominion sur les 3 hexs de sa ville (voir
    // ECategory.DOMINION_SMELTER_SPEED_PER_LEVEL). Verrouillée derrière le pouvoir divin Foi comme
    // les autres recherches du Dominion.
    CreusetDuDominion,
    // Baissée de 2 tiers (coût / 16) puis d'un tier supplémentaire (coût / 4), tier 9, pour la rendre
    // accessible plus tôt.
    Omniscience,
    LegionEternelle,
    // Suite de la ligne du Vide (VoidWalking → VoidCompass) : boucle d'Ascension et routes du Vide.
    ReliquaireSacre,
    CartographieDuVide,
    // Un tier au-dessus de ReliquaireSacre, prérequis ReliquaireSacre + AcierAbyssal : conserve une
    // seconde essence divine lors du prestige (voir ECategory.DIVINE_ESSENCE_KEPT_ON_PRESTIGE).
    ReliquaireRenforce,
    // Suite du Reliquaire Sacré : débloque la Nécropole, monument bâti sur des Os Divins non purifiés
    // (qu'il consomme) dont chaque niveau augmente de 10% les points divins gagnés à l'Ascension.
    NecropoleDivine,
    // Suite de la Nécropole Divine : le coût de Purification des Os Divins croît 20% moins vite avec
    // le nombre d'essences divines déjà collectées (voir ECategory.DIVINE_BONES_SCALING_REDUCTION).
    LiturgieFuneraire,
    // Branche de la Théocratie (tiers 14-15) — recherches du Dominion, visibles uniquement une fois
    // le pouvoir divin Foi débloqué (RequiresDominionUnlock, voir ResearchController).
    DogmeDeLEmprise,
    // [Legacy] Recherche "Communion Abyssale" supprimée (son bonus de prestige a été repris par la
    // Théologie de l'Ascension, remontée à sa place) — conservée uniquement pour la désérialisation
    // des anciennes sauvegardes.
    CommunionAbyssale,
    Evangelisation,
    TerreConsacree,
    BastionConsacre,

    // Valeur de repli utilisée par TechnologyIdJsonConverter pour toute recherche supprimée lue
    // depuis une ancienne sauvegarde. Absente de TechnologyDefinitions.All, donc TechnologyDefinitions.Get
    // y renvoie null comme pour n'importe quel id inconnu — déjà géré partout où Get est appelé.
    Removed,
}

/// <summary>
/// Désérialise TechnologyId par nom, avec repli sur <see cref="TechnologyId.Removed"/> pour toute
/// valeur non reconnue (recherche supprimée) au lieu de faire échouer tout le chargement de la
/// sauvegarde. Chaque suppression doit être documentée ici avec la version qui l'a introduite.
/// </summary>
public sealed class TechnologyIdJsonConverter : JsonConverter<TechnologyId>
{
    public override TechnologyId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => Parse(reader.GetString());

    public override void Write(Utf8JsonWriter writer, TechnologyId value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString());

    // RepeatCounts (TechnologyTree) et BestRepeatCounts (AscensionState) sont des Dictionary<TechnologyId, int> :
    // les clés d'un dictionnaire passent par ReadAsPropertyName/WriteAsPropertyName, pas par Read/Write.
    public override TechnologyId ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => Parse(reader.GetString());

    public override void WriteAsPropertyName(Utf8JsonWriter writer, TechnologyId value, JsonSerializerOptions options)
        => writer.WritePropertyName(value.ToString());

    private static TechnologyId Parse(string? s)
    {
        // [Legacy remap v0.15] "DeepLightRitual" supprimée avec le rituel Lumière des Profondeurs.
        if (Enum.TryParse<TechnologyId>(s, out var value)) return value;
        return TechnologyId.Removed;
    }
}

[JsonConverter(typeof(JsonStringEnumConverter<TechnologyStatus>))]
public enum TechnologyStatus
{
    Inactive,
    Available,
    InProgress,
    Completed,
}

public class Technology
{
    public TechnologyId Id { get; }
    public string NameKey { get; }
    public string DescKey { get; }
    // long : les coûts des tiers 13+ (100 × 4^tier) dépassent int.MaxValue.
    public long Cost { get; }
    public IReadOnlyList<TechnologyId> Prerequisites { get; }
    public IReadOnlyList<Modifier> Modifiers { get; }
    public int Tier { get; }
    public int Line { get; }

    /// <summary>
    /// Vraie si la recherche reste cachée et inaccessible tant que le pouvoir divin Foi n'a pas été
    /// débloqué (modificateur UNLOCK_DOMINION) — même verrou que les vertex/hexes de prestige du
    /// Dominion (voir PrestigeVertex.RequiresDominionUnlock et ResearchController).
    /// </summary>
    public bool RequiresDominionUnlock { get; }

    /// <summary>
    /// Vraie si la recherche peut être relancée indéfiniment une fois terminée. Chaque relance double
    /// le coût (par rapport au coût de base, voir ResearchController.GetEffectiveCost) et ses modificateurs
    /// s'accumulent une fois par complétion (voir TechnologyTree.RepeatCounts / RebuildModifiers).
    /// </summary>
    public bool Repeatable { get; }

    public Technology(
        TechnologyId id,
        string nameKey,
        string descKey,
        long cost,
        IReadOnlyList<TechnologyId> prerequisites,
        IReadOnlyList<Modifier> modifiers,
        int tier,
        int line,
        bool requiresDominionUnlock = false,
        bool repeatable = false)
    {
        Id = id;
        NameKey = nameKey;
        DescKey = descKey;
        Cost = cost;
        Prerequisites = prerequisites;
        Modifiers = modifiers;
        Tier = tier;
        Line = line;
        RequiresDominionUnlock = requiresDominionUnlock;
        Repeatable = repeatable;
    }
}
