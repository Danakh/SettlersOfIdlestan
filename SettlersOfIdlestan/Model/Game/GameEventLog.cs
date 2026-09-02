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

    TentacleDiscovered,
    TentacleDefeated,
    DemonGodDiscovered,
    DemonGodDefeated,
    PandemoniumGatePlaced,
    PandemoniumGateBuilt,

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
}

public record GameLogEntry(GameEventType Type, string? Message = null, bool Toast = false);

public class GameEventLog
{
    private const int MaxEntries = 50;
    public List<GameLogEntry> Entries { get; } = new();

    private readonly Queue<GameLogEntry> _pendingToasts = new();

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

        var entry = new GameLogEntry(type, message, toast);
        Entries.Insert(0, entry);
        if (Entries.Count > MaxEntries)
            Entries.RemoveAt(MaxEntries);
        if (toast) _pendingToasts.Enqueue(entry);
    }

    public bool TryDequeueToast(out GameLogEntry entry) => _pendingToasts.TryDequeue(out entry!);

    public bool HasEntries => Entries.Count > 0;
}
