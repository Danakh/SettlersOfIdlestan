namespace SettlersOfIdlestan.Model.IslandMap;

public class AutomationSettings
{
    public bool RoadAutomationEnabled { get; set; } = true;
    public bool OutpostAutomationEnabled { get; set; } = false;
    public bool ProductionBuildingAutomationEnabled { get; set; } = false;
    public bool ArtisanBuildingAutomationEnabled { get; set; } = false;
}
