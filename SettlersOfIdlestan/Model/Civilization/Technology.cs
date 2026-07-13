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
    // Un tier au-dessus des Chroniques du Guet (seul prérequis)
    Diplomatie,
    // Branche de la Volcanologie (convergence Sidérurgie × risque volcanique)
    Volcanologie,
    VolcanicMetallurgy,
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
    public int Cost { get; }
    public IReadOnlyList<TechnologyId> Prerequisites { get; }
    public IReadOnlyList<Modifier> Modifiers { get; }
    public int Tier { get; }
    public int Line { get; }

    public Technology(
        TechnologyId id,
        string nameKey,
        string descKey,
        int cost,
        IReadOnlyList<TechnologyId> prerequisites,
        IReadOnlyList<Modifier> modifiers,
        int tier,
        int line)
    {
        Id = id;
        NameKey = nameKey;
        DescKey = descKey;
        Cost = cost;
        Prerequisites = prerequisites;
        Modifiers = modifiers;
        Tier = tier;
        Line = line;
    }
}
