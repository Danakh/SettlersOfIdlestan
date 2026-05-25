namespace SettlersOfIdlestan.Model.Game;

public enum GameEventType
{
    BanditDiscovered,
    BanditDefeated,
    TreasureTroveDiscovered,
    TreasureTroveClaimed,
}

public record GameLogEntry(GameEventType Type);

public class GameEventLog
{
    private const int MaxEntries = 50;
    public List<GameLogEntry> Entries { get; } = new();

    public void Add(GameEventType type)
    {
        Entries.Insert(0, new GameLogEntry(type));
        if (Entries.Count > MaxEntries)
            Entries.RemoveAt(MaxEntries);
    }

    public bool HasEntries => Entries.Count > 0;
}
