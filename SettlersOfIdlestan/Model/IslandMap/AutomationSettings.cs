namespace SettlersOfIdlestan.Model.IslandMap;

public class AutomationSettings
{
    public bool RoadAutomationEnabled { get; set; } = true;
    public bool OutpostAutomationEnabled { get; set; } = false;
    public bool ProductionBuildingAutomationEnabled { get; set; } = false;
    public bool ArtisanBuildingAutomationEnabled { get; set; } = false;
    public bool LibraryBuildingAutomationEnabled { get; set; } = false;
    public bool MarketBuildingAutomationEnabled { get; set; } = false;
    public bool MilitaryReinforcementAutomationEnabled { get; set; } = false;
    public bool MilitaryAttackAutomationEnabled { get; set; } = false;
}
