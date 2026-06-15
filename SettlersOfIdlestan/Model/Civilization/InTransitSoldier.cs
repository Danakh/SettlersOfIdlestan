using System;

namespace SettlersOfIdlestan.Model.Civilization;

[Serializable]
public class InTransitSoldier
{
    public long ArrivalTick { get; set; }

    public InTransitSoldier() { }
    public InTransitSoldier(long arrivalTick) { ArrivalTick = arrivalTick; }
}
