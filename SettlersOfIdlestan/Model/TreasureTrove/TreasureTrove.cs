using SettlersOfIdlestan.Model.HexGrid;

namespace SettlersOfIdlestan.Model.TreasureTroves
{
    public class TreasureTrove
    {
        public HexCoord Position { get; set; }
        public bool Claimed { get; set; }

        public TreasureTrove(HexCoord position)
        {
            Position = position;
            Claimed = false;
        }

        [System.Text.Json.Serialization.JsonConstructor]
        public TreasureTrove()
        {
            Position = new HexCoord(0, 0);
        }
    }
}
