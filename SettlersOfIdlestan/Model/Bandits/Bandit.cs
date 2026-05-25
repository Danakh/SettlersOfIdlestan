using System.Text.Json.Serialization;
using SettlersOfIdlestan.Model.HexGrid;

namespace SettlersOfIdlestan.Model.Bandits;

[Serializable]
public class Bandit
{
    public const int MaxHp = 20;

    public HexCoord Position { get; set; }
    public long LastMovedTick { get; set; }
    public int Hp { get; set; } = MaxHp;

    public Bandit(HexCoord position, long lastMovedTick = 0)
    {
        Position = position;
        LastMovedTick = lastMovedTick;
        Hp = MaxHp;
    }

    [JsonConstructor]
    public Bandit()
    {
        Position = new HexCoord(0, 0);
        Hp = MaxHp;
    }
}
