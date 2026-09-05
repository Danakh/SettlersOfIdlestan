using System.Text.Json.Serialization;

namespace SettlersOfIdlestan.Model.Game;

/// <summary>
/// Famille d'événements que le joueur peut masquer depuis l'onglet Réglages du Journal. Une
/// catégorie regroupe l'apparition ET la disparition d'un même sujet : masquer « Dragon » retire
/// aussi bien « Dragon aperçu » que « Dragon vaincu ».
///
/// Sérialisé par nom (voir la règle « Enum serialization » de CLAUDE.md) : ces valeurs vivent dans
/// <see cref="GameSettings"/>, donc dans les sauvegardes.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<EventLogCategory>))]
public enum EventLogCategory
{
    Bandit,
    BanditHideout,
    Rats,
    Troll,
    Ogre,
    Dragon,
    MinorDemon,
    MajorDemon,
    Tentacle,
    DemonGod,
    Adventurer,
    TreasureTrove,
}

/// <summary>
/// Préférences d'affichage du Journal : quelles familles d'événements le joueur ne veut plus voir.
///
/// Filtre unique pour les trois manifestations d'un événement — l'entrée du journal, la
/// surbrillance de l'onglet et le toast — parce qu'il est appliqué à la source, dans
/// <see cref="GameEventLog.Add"/> : un événement masqué n'est jamais ajouté, donc ni listé, ni
/// compté par la pulsation de l'onglet (<c>TabBarRenderer.UpdateEventNotification</c> lit
/// <c>Entries.Count</c>), ni mis en file d'attente des toasts. Filtrer côté affichage aurait
/// laissé passer les deux derniers.
///
/// On stocke les catégories masquées plutôt que les visibles : une catégorie ajoutée plus tard est
/// ainsi visible par défaut, y compris dans une sauvegarde antérieure.
/// </summary>
public class EventLogFilter
{
    public HashSet<EventLogCategory> HiddenCategories { get; set; } = [];

    /// <summary>
    /// Familles déjà croisées au moins une fois dans la partie. L'onglet Réglages n'affiche que
    /// celles-là : une liste des douze familles dès le premier bandit dévoilerait tout le
    /// bestiaire — dieu démon compris — avant que le joueur en ait rencontré aucun.
    ///
    /// Alimenté par <see cref="GameEventLog.Add"/>, donc par le fait même qu'un événement de cette
    /// famille se produise. Vit ici plutôt que dans le WorldState ou GameRecord : c'est le seul
    /// objet que le journal a sous la main, et il est déjà persisté dans GameSettings — donc
    /// cross-île, cross-prestige et cross-ascension, comme le veut « dans la partie ».
    ///
    /// Une sauvegarde antérieure démarre avec une liste vide : le journal n'est pas persisté
    /// (WorldState.EventLog n'a pas de setter), il n'y a donc rien à rattraper. Elle se remplit
    /// au premier événement de chaque famille.
    /// </summary>
    public HashSet<EventLogCategory> KnownCategories { get; set; } = [];

    public bool IsCategoryVisible(EventLogCategory category) => !HiddenCategories.Contains(category);

    public bool IsCategoryKnown(EventLogCategory category) => KnownCategories.Contains(category);

    /// <summary>
    /// Enregistre la rencontre d'une famille. Appelé avant tout filtrage : une famille masquée
    /// reste connue, sinon la décocher la ferait disparaître de ses propres réglages.
    /// </summary>
    public void MarkKnown(GameEventType type)
    {
        var category = GetCategory(type);
        if (category != null) KnownCategories.Add(category.Value);
    }

    public void SetCategoryVisible(EventLogCategory category, bool visible)
    {
        if (visible) HiddenCategories.Remove(category);
        else HiddenCategories.Add(category);
    }

    public void ToggleCategory(EventLogCategory category) =>
        SetCategoryVisible(category, !IsCategoryVisible(category));

    /// <summary>Vrai si cet événement doit être journalisé. Un type sans catégorie l'est toujours.</summary>
    public bool IsEventVisible(GameEventType type)
    {
        var category = GetCategory(type);
        return category == null || IsCategoryVisible(category.Value);
    }

    /// <summary>
    /// Famille d'un type d'événement, ou <c>null</c> s'il n'est pas filtrable. Seuls les monstres,
    /// les aventuriers et les trésors le sont : les événements de progression (merveilles, portes,
    /// pertes de ville, erreurs d'exécution...) doivent rester visibles.
    /// </summary>
    public static EventLogCategory? GetCategory(GameEventType type) => type switch
    {
        GameEventType.BanditDiscovered or GameEventType.BanditDefeated => EventLogCategory.Bandit,
        GameEventType.BanditHideoutDiscovered or GameEventType.BanditHideoutDestroyed => EventLogCategory.BanditHideout,
        GameEventType.RatsDiscovered or GameEventType.RatsDefeated => EventLogCategory.Rats,
        GameEventType.TrollDiscovered or GameEventType.TrollDefeated => EventLogCategory.Troll,
        GameEventType.OgreDiscovered or GameEventType.OgreDefeated => EventLogCategory.Ogre,
        GameEventType.DragonDiscovered or GameEventType.DragonDefeated => EventLogCategory.Dragon,
        GameEventType.MinorDemonDiscovered or GameEventType.MinorDemonDefeated => EventLogCategory.MinorDemon,
        GameEventType.MajorDemonDiscovered or GameEventType.MajorDemonDefeated => EventLogCategory.MajorDemon,
        GameEventType.TentacleDiscovered or GameEventType.TentacleDefeated => EventLogCategory.Tentacle,
        GameEventType.DemonGodDiscovered or GameEventType.DemonGodDefeated => EventLogCategory.DemonGod,
        GameEventType.AdventurerDiscovered or GameEventType.AdventurerDefeated => EventLogCategory.Adventurer,
        GameEventType.TreasureTroveDiscovered or GameEventType.TreasureTroveClaimed => EventLogCategory.TreasureTrove,
        _ => null,
    };

    /// <summary>
    /// Ordre d'affichage des lignes de l'onglet Réglages, et clé de localisation de chacune.
    /// L'ordre suit la progression de la partie plutôt que celui de l'énumération.
    /// </summary>
    public static readonly IReadOnlyList<EventLogCategory> DisplayOrder =
    [
        EventLogCategory.Bandit,
        EventLogCategory.BanditHideout,
        EventLogCategory.Rats,
        EventLogCategory.Troll,
        EventLogCategory.Ogre,
        EventLogCategory.Dragon,
        EventLogCategory.MinorDemon,
        EventLogCategory.MajorDemon,
        EventLogCategory.Tentacle,
        EventLogCategory.DemonGod,
        EventLogCategory.Adventurer,
        EventLogCategory.TreasureTrove,
    ];

    public static string GetLabelKey(EventLogCategory category) => category switch
    {
        EventLogCategory.Bandit => "event_filter_bandit",
        EventLogCategory.BanditHideout => "event_filter_bandit_hideout",
        EventLogCategory.Rats => "event_filter_rats",
        EventLogCategory.Troll => "event_filter_troll",
        EventLogCategory.Ogre => "event_filter_ogre",
        EventLogCategory.Dragon => "event_filter_dragon",
        EventLogCategory.MinorDemon => "event_filter_minor_demon",
        EventLogCategory.MajorDemon => "event_filter_major_demon",
        EventLogCategory.Tentacle => "event_filter_tentacle",
        EventLogCategory.DemonGod => "event_filter_demon_god",
        EventLogCategory.Adventurer => "event_filter_adventurer",
        EventLogCategory.TreasureTrove => "event_filter_treasure",
        _ => "",
    };
}
