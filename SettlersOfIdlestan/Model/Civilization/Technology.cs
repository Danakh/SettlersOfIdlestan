using SettlersOfIdlestan.Model.GameplayModifier;
using System.Text.Json.Serialization;

namespace SettlersOfIdlestan.Model.Civilization;

// Sérialisé par nom (et non par entier) afin que l'ajout ou la suppression d'une recherche
// ne décale jamais les valeurs des autres recherches dans les sauvegardes existantes.
[JsonConverter(typeof(JsonStringEnumConverter<TechnologyId>))]
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
    // Débloque la construction du Camp Mobile — prend la place de RailLogistics dans l'arbre (voir
    // TechnologyDefinitions).
    MobileCampConstruction,
    // Patrouille anti-monstres automatique — prend la place d'AdvancedStrategy dans l'arbre (voir
    // TechnologyDefinitions).
    Patrol,
    // Un tier au-dessus de Patrol (seul prérequis) — raids automatiques sur une civilisation.
    Vendetta,
    AutomaticMarket,
    Speleologie,
    // Tier 5
    MasterResearch,
    GreatLighthouseConstruction,
    AdvancedTradingPosts,
    Siderurgie,
    CultureFongique,
    CartographieSouterraine,
    WatchtowerConstruction,
    // Tier 6
    SteelArmor,
    TemperedSteel,
    RailLogistics,
    OutilsEnMithril,
    RempartsDeFer,
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
    DeepLightRitual,
    // Branche des Sorts Instantanés (débloquée par le vertex de prestige Invocations)
    Invocation,
    TroopSummoning,
    ArcaneEdification,
    // Recherches de bonus de prestige (capstones de branches existantes)
    ChroniquesDuGuet,
    RenommeeCommerciale,
    SagesseSouterraine,
    // Deux tiers après Sagesse Souterraine ; débloquée avec la branche des Abysses.
    // Permet de construire des routes entre deux hexagones de Vide (comme les routes maritimes entre
    // deux hexagones d'eau), moyennant un coût croissant en points de recherche par route déjà bâtie.
    VoidWalking,
    // Suite de Void Walking : révèle la feature Os Divins sur les îles des Abysses générées après la première.
    VoidCompass,
    // Un tier au-dessus des Chroniques du Guet (seul prérequis)
    Diplomatie,
    // Branche de la Volcanologie (convergence Sidérurgie × risque volcanique)
    Volcanologie,
    VolcanicMetallurgy,
    // Suite de la Cartographie Souterraine : les Tours de Guet étant interdites dans l'Outremonde,
    // cette recherche ralentit les apparitions de monstres de bordure sans bâtiment dédié.
    VeilleSouterraine,
    // Branche des Abysses (débloquée par le vertex de prestige Brèche Abyssale)
    EtudeDesAbysses,
    Demonologie,
    ResistanceALaCorruption,
    PacteAbyssal,
    SecretsDeLaFaille,
    TheologieDeLAscension,
    // Capstones des branches existantes (tiers 12-13)
    AcierAbyssal,
    MagieDuVide,
    CoeurDeLaTerre,
    Omniscience,
    LegionEternelle,
    // Suite de la ligne du Vide (VoidWalking → VoidCompass) : boucle d'Ascension et routes du Vide.
    ReliquaireSacre,
    CartographieDuVide,
    // Branche de la Théocratie (tiers 14-15) — recherches du Dominion, visibles uniquement une fois
    // le pouvoir divin Foi débloqué (RequiresDominionUnlock, voir ResearchController).
    DogmeDeLEmprise,
    CommunionAbyssale,
    Evangelisation,
    TerreConsacree,
    BastionConsacre,
}

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

    public Technology(
        TechnologyId id,
        string nameKey,
        string descKey,
        long cost,
        IReadOnlyList<TechnologyId> prerequisites,
        IReadOnlyList<Modifier> modifiers,
        int tier,
        int line,
        bool requiresDominionUnlock = false)
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
    }
}
