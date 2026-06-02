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
    RuntimeError,
    DragonDiscovered,
    DragonDefeated,
}

public record GameLogEntry(GameEventType Type, string? Message = null);

public class GameEventLog
{
    private const int MaxEntries = 50;
    public List<GameLogEntry> Entries { get; } = new();

    public void Add(GameEventType type, string? message = null)
    {
        Entries.Insert(0, new GameLogEntry(type, message));
        if (Entries.Count > MaxEntries)
            Entries.RemoveAt(MaxEntries);
    }

    public bool HasEntries => Entries.Count > 0;
}
