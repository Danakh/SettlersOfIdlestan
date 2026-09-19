using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace SettlersOfIdlestan.Model.Game;

[JsonConverter(typeof(JsonStringEnumConverter<GameEventType>))]
public enum GameEventType
{
    NoEvent,
    BanditDiscovered,
    BanditDefeated,
    TreasureTroveDiscovered,
    TreasureTroveClaimed,
    BanditHideoutDiscovered,
    BanditHideoutDestroyed,
    CivilizationDiscovered,
    CivilizationDestroyed,
    SoldierStarved,
    WonderPlaced,
    WonderLevelUp,
    GreatLighthousePlaced,
    GreatLighthouseLevelUp,
    ObservatoryPlaced,
    ObservatoryLevelUp,
    NecropolisPlaced,
    NecropolisLevelUp,
    RuntimeError,
    DragonDiscovered,
    DragonDefeated,
    RatsDiscovered,
    RatsDefeated,
    TrollDiscovered,
    TrollDefeated,
    OgreDiscovered,
    OgreDefeated,
    MinorDemonDiscovered,
    MinorDemonDefeated,
    MajorDemonDiscovered,
    MajorDemonDefeated,
    DeepestMinePlaced,
    DeepestMineDug,
    UnderworldLost,
    FairyCircleDiscovered,
    /// <summary>Obsolète — Dolmen retiré du jeu. Conservé pour la désérialisation des anciennes sauvegardes.</summary>
    DolmenDiscovered,
    RitualCollapsed,
    CorruptionSpirePlaced,
    CorruptionSpireBuilt,
    /// <summary>Obsolète — le rayon de la Spire n'est plus améliorable. Conservé pour la désérialisation des anciennes sauvegardes.</summary>
    CorruptionSpireRadiusUpgraded,
    AbyssGateEligible,
    AbyssGatePlaced,
    AbyssGateBuilt,
    AdventurerDiscovered,
    AdventurerDefeated,
    RaidMissingBarracks,
    WarHeraldAutoReinforcementConflict,
    VolcanoDiscovered,
    DivineBonesPurified,
    DivineBonesPurifiedNoEssence,
    SurfaceBreachPlaced,
    SurfaceBreachDug,
    SurfaceLost,

    /// <summary>
    /// Une de nos villes a été détruite parce que le terrain sous elle a changé — voir
    /// CityBuilderController.DestroyCitiesInvalidatedByTerrain. En pratique : sa propre Marche de Dieu.
    /// </summary>
    CityLostToTerrain,

    /// <summary>Spire de Corruption démolie volontairement par le joueur pour en replacer une ailleurs.</summary>
    CorruptionSpireDestroyed,

    /// <summary>
    /// Le plafond d'essences divines vient d'être atteint (DivineBones.GetEssenceCap,
    /// AscensionController.GetDivineEssenceCap) : les Purifications suivantes n'accorderont plus
    /// rien tant qu'il n'aura pas été relevé — prestige pour monter la Corruption, ou nouveau
    /// pouvoir divin. Message = plafond atteint. Voir DivineBonesController.GrantPurificationEssence.
    /// </summary>
    DivineEssenceCapReached,

    /// <summary>
    /// Le plafond d'essences divines atteint est inférieur aux points divins requis pour
    /// ascensionner (AscensionController.MinDivineEssenceForAscension) : rappel des trois leviers
    /// qui permettent malgré tout d'atteindre le seuil (Reliquaire, Nécropole, ou monter la
    /// Corruption). Journal seul, sans toast : il accompagne <see cref="DivineEssenceCapReached"/>,
    /// qui, lui, en produit un. Message = plafond atteint.
    /// </summary>
    DivineEssenceCapBelowAscension,

    TentacleDiscovered,
    TentacleDefeated,
    DemonGodDiscovered,

    /// <summary>
    /// Dieu démon abattu alors que le joueur en avait déjà vaincu un d'un niveau au moins égal :
    /// une victoire de plus, sans record. Message = niveau du boss, essence divine reçue et record
    /// en cours (voir DemonGod.RemovedEventMessage).
    /// </summary>
    DemonGodDefeated,

    /// <summary>
    /// Tout premier Dieu démon abattu de la partie — le joueur peut considérer qu'il a gagné. Seul
    /// des trois à ouvrir la modale de victoire (voir GameScreen). Même message que
    /// <see cref="DemonGodDefeated"/>.
    /// </summary>
    DemonGodDefeatedFirst,

    /// <summary>
    /// Dieu démon abattu à un niveau supérieur à tous les précédents : le record
    /// (GodState.HighestDemonGodLevelDefeated) vient d'être battu. Même message que
    /// <see cref="DemonGodDefeated"/>.
    /// </summary>
    DemonGodDefeatedRecord,

    PandemoniumGatePlaced,
    PandemoniumGateBuilt,

    /// <summary>
    /// Le joueur a perdu sa dernière ville dans le Pandémonium : toute l'arène est détruite et le
    /// portail retombe à 50 % d'investissement, comme la Faille des Abysses — voir
    /// PandemoniumGateController.OnCityDestroyed.
    /// </summary>
    PandemoniumGateLost,

    /// <summary>
    /// Le joueur a perdu sa dernière ville dans les Abysses : les essences divines récoltées pendant
    /// le run sont perdues, hormis celles garanties par le Reliquaire (voir
    /// AbyssGateController.OnCityDestroyed et GodState.DivineEssenceReliquaryFloor). Message = nombre
    /// d'essences perdues.
    /// </summary>
    AbyssLostDivineEssence,

    /// <summary>
    /// Le joueur a perdu sa dernière ville dans les Abysses : la Faille retombe à 50 % d'investissement
    /// (comme la Mine Profonde/la Percée de Surface) — voir AbyssGateController.OnCityDestroyed.
    /// </summary>
    AbyssGateLost,

    /// <summary>
    /// La ville qui rendait un Monument (Merveille, Os Divins…) éligible à l'investissement a été
    /// détruite, et aucune autre ville ne touche plus son hex : l'investissement en cours se fige
    /// silencieusement (MonumentInvestment.ProcessTick refuse tant qu'aucune ville n'est adjacente)
    /// sans qu'aucun système ne le signale autrement. Message = clé de localisation du titre du
    /// panneau du Monument concerné (Monument.PanelTitleKey) — voir MonumentInvestment.OnCityDestroyed.
    /// </summary>
    MonumentInvestmentBlockedByCityLoss,

    /// <summary>
    /// Une Balise Maritime, une Flotte de Guerre ou un Camp Mobile a été détruit parce que le terrain
    /// sous lui a changé — voir MaritimeBeaconController.DestroyBeaconsInvalidatedByTerrain,
    /// WarFleetController.DestroyFleetsInvalidatedByTerrain,
    /// MobileCampController.DestroyCampsInvalidatedByTerrain. En pratique : sa propre Marche de Dieu.
    /// Message = clé de localisation nommant la structure perdue (voir AscensionController.ApplyWalkOfGod).
    /// </summary>
    MilitaryVertexLostToTerrain,

    /// <summary>Socle du Titan d'Acier posé sur la carte — voir SteelTitanController.</summary>
    SteelTitanPlaced,

    /// <summary>Le Titan d'Acier est achevé : le colosse allié se dresse sur son socle.</summary>
    SteelTitanBuilt,

    /// <summary>Le Titan d'Acier est tombé au combat. Le socle peut en refondre un.</summary>
    SteelTitanDefeated,

    /// <summary>Le joueur a démantelé son Titan d'Acier depuis le panneau du socle.</summary>
    SteelTitanDismantled,

    /// <summary>Le joueur a démantelé le socle du Titan d'Acier : un nouveau peut être posé ailleurs.</summary>
    SteelTitanSiteDestroyed,

    /// <summary>
    /// Tentacule repérée alors qu'un Portail du Pandémonium existe déjà sur l'île : même annonce que
    /// <see cref="TentacleDiscovered"/>, sans la promesse d'un portail. Un seul portail par île
    /// (voir PandemoniumGateController), les suivantes ne sont donc que des monstres de plus.
    /// </summary>
    TentacleDiscoveredNoGate,

    /// <summary>
    /// Tentacule abattue sans qu'un Portail du Pandémonium en surgisse — il en existait déjà un, ou
    /// elle ne gardait pas l'Abysse. Même annonce que <see cref="TentacleDefeated"/> sans l'ouverture.
    /// </summary>
    TentacleDefeatedNoGate,

    /// <summary>
    /// Une Spire de Corruption ou une Faille des Abysses a été détruite parce que son hexagone n'est
    /// plus visible du joueur — plus aucune ville ni route pour l'éclairer, typiquement après la
    /// perte de l'Inframonde. Voir CorruptionSpireController.DestroyMonumentLostToDarkness.
    /// Message = clé de localisation nommant la structure perdue.
    /// </summary>
    MonumentLostToDarkness,
}

/// <param name="Message">
/// Complément figé à l'instant de la journalisation, quand le libellé de l'événement ne suffit pas
/// (un niveau, un montant, une clé de localisation...). Une entrée qui en porte plusieurs les
/// assemble avec <see cref="GameLogEntry.JoinMessageArgs"/> et les relit avec
/// <see cref="GameLogEntry.SplitMessageArgs"/>.
/// </param>
public record GameLogEntry(GameEventType Type, string? Message = null, bool Toast = false)
{
    /// <summary>Séparateur des valeurs d'un message multi-valeurs — absent de tout nombre formaté.</summary>
    private const char ArgSeparator = '|';

    /// <summary>Assemble plusieurs valeurs en un seul <see cref="Message"/>.</summary>
    public static string JoinMessageArgs(params object[] args) => string.Join(ArgSeparator, args);

    /// <summary>
    /// Relit un <see cref="Message"/> assemblé par <see cref="JoinMessageArgs"/>. Renvoie toujours
    /// au moins <paramref name="expectedCount"/> éléments, complétés par "?" : une sauvegarde
    /// antérieure à l'ajout d'une valeur garde ses anciennes entrées de journal, et l'affichage ne
    /// doit pas tomber dessus.
    /// </summary>
    public static string[] SplitMessageArgs(string? message, int expectedCount)
    {
        var parts = (message ?? "").Split(ArgSeparator);
        if (parts.Length >= expectedCount) return parts;

        var padded = new string[expectedCount];
        for (int i = 0; i < expectedCount; i++)
            padded[i] = i < parts.Length && parts[i].Length > 0 ? parts[i] : "?";
        return padded;
    }
}

/// <summary>
/// Un toast en attente de traitement par la couche d'affichage. L'image et le son sont deux
/// consignes distinctes parce que le joueur les règle séparément, famille par famille (voir
/// <see cref="EventLogFilter"/>) : une famille masquée mais audible sort d'ici avec
/// <paramref name="Show"/> faux et <paramref name="PlaySound"/> vrai — rien à l'écran, le bruitage
/// quand même. Une entrée dont ni l'un ni l'autre n'est demandé n'est jamais mise en file.
/// </summary>
/// <param name="Show">Afficher le toast à l'écran.</param>
/// <param name="PlaySound">Jouer le bruitage de notification correspondant.</param>
public readonly record struct PendingToast(GameLogEntry Entry, bool Show, bool PlaySound);

public class GameEventLog
{
    private const int MaxEntries = 50;
    public List<GameLogEntry> Entries { get; } = new();

    private readonly Queue<PendingToast> _pendingToasts = new();

    private EventLogFilter? _filter;

    /// <summary>
    /// Câble les préférences d'affichage du joueur (voir <see cref="EventLogFilter"/>). Elles
    /// vivent dans <see cref="GameSettings"/>, donc hors du WorldState : ce câblage est refait à
    /// chaque île/prestige/ascension/chargement, comme celui d'AutomationSettings.
    ///
    /// Sans câblage, rien n'est filtré : un journal non lié (tests, génération) journalise tout.
    /// </summary>
    public void Bind(EventLogFilter? filter) => _filter = filter;

    /// <summary>
    /// Ajoute une entrée au journal.
    ///
    /// <para><see cref="GameEventType.NoEvent"/> veut dire « rien à annoncer » : c'est ce que
    /// déclarent les features dépourvues d'événement de découverte ou de disparition (Monument,
    /// Corruption, Dominion, CorruptionSource, ContestedTerritory...). Un appelant qui le transmet a
    /// donc relayé un type d'événement non renseigné — typiquement un
    /// <c>feature.DiscoveredEventType</c> passé sans filtre. L'entrée était alors ajoutée telle
    /// quelle et le journal affichait « ? NoEvent », sans la moindre indication de son origine.
    /// Elle est désormais refusée et remplacée par une erreur d'exécution qui nomme le fichier, la
    /// ligne et la méthode appelante ; <see cref="GameLog"/> la déduplique (une occurrence par tick
    /// reste une seule ligne) et la route vers ce même journal en RuntimeError.</para>
    /// </summary>
    public void Add(GameEventType type, string? message = null, bool toast = false,
        [CallerFilePath] string? callerFile = null,
        [CallerLineNumber] int callerLine = 0,
        [CallerMemberName] string? callerMember = null)
    {
        if (type == GameEventType.NoEvent)
        {
            GameLog.Error(nameof(GameEventLog), callerMember ?? nameof(Add),
                $"NoEvent journalisé depuis {Path.GetFileName(callerFile) ?? "?"}:{callerLine}"
                + $" (toast={toast}, message={message ?? "aucun"})");
            return;
        }

        // La famille est désormais connue du joueur, et sa case apparaît dans les réglages. Marqué
        // avant le filtrage : une famille masquée doit rester listée, sans quoi la décocher la
        // ferait disparaître de l'écran qui sert à la recocher.
        _filter?.MarkKnown(type);

        // Famille masquée par le joueur : on refuse l'entrée à la source plutôt que de la filtrer
        // à l'affichage. C'est ce qui éteint d'un coup ses trois manifestations visibles — la
        // ligne du journal, la pulsation de l'onglet (qui compte Entries) et l'image du toast.
        bool visible = _filter?.IsEventVisible(type) != false;

        // Le son se règle à part : masquée, une famille peut rester audible. L'entrée ne rejoint
        // alors pas le journal mais passe quand même par la file des toasts, muette d'image.
        bool audible = _filter?.IsEventAudible(type) != false;

        if (!visible && !audible) return;

        var entry = new GameLogEntry(type, message, toast);

        if (visible)
        {
            Entries.Insert(0, entry);
            if (Entries.Count > MaxEntries)
                Entries.RemoveAt(MaxEntries);
        }

        if (toast) _pendingToasts.Enqueue(new PendingToast(entry, visible, audible));
    }

    public bool TryDequeueToast(out PendingToast toast) => _pendingToasts.TryDequeue(out toast);

    public bool HasEntries => Entries.Count > 0;
}
