using System;

namespace SettlersOfIdlestan.Model.PrestigeMap;

[Serializable]
public class PrestigeRunStats
{
    public int IslandId { get; set; }
    public long TickDuration { get; set; }
    public int CityCount { get; set; }
    public int BuildingCount { get; set; }
    public int TotalBuildingLevels { get; set; }
    public int PrestigePoints { get; set; }
}
