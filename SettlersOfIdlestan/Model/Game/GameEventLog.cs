namespace SettlersOfIdlestan.Model.Game;

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
    SoldierStarved,
    WonderPlaced,
    WonderLevelUp,
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
    DeepestMinePlaced,
    DeepestMineDug,
    UnderworldLost,
    FairyCircleDiscovered,
    /// <summary>Obsolète — Dolmen retiré du jeu. Conservé pour la désérialisation des anciennes sauvegardes.</summary>
    DolmenDiscovered,
    RitualCollapsed,
    CorruptionSpirePlaced,
    CorruptionSpireBuilt,
    AbyssGateEligible,
    AbyssGatePlaced,
    AbyssGateBuilt,
    AdventurerDiscovered,
    AdventurerDefeated,
    RaidMissingBarracks,
}

public record GameLogEntry(GameEventType Type, string? Message = null, bool Toast = false);

public class GameEventLog
{
    private const int MaxEntries = 50;
    public List<GameLogEntry> Entries { get; } = new();

    private readonly Queue<GameLogEntry> _pendingToasts = new();

    public void Add(GameEventType type, string? message = null, bool toast = false)
    {
        var entry = new GameLogEntry(type, message, toast);
        Entries.Insert(0, entry);
        if (Entries.Count > MaxEntries)
            Entries.RemoveAt(MaxEntries);
        if (toast) _pendingToasts.Enqueue(entry);
    }

    public bool TryDequeueToast(out GameLogEntry entry) => _pendingToasts.TryDequeue(out entry!);

    public bool HasEntries => Entries.Count > 0;
}
