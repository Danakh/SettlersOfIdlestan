using System.Text.Json.Serialization;
using SettlersOfIdlestan.Model.HexGrid;

namespace SettlersOfIdlestan.Model.Bandits;

[Serializable]
public class Bandit
{
    public HexCoord Position { get; set; }
    public long LastMovedTick { get; set; }

    public Bandit(HexCoord position, long lastMovedTick = 0)
    {
        Position = position;
        LastMovedTick = lastMovedTick;
    }

    [JsonConstructor]
    public Bandit()
    {
        Position = new HexCoord(0, 0);
    }
}
