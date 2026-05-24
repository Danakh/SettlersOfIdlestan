using System;

namespace SettlersOfIdlestan.Model.Prestige;

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
