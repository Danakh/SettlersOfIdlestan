using SettlersOfIdlestan.Model.Localization;

namespace SettlersOfIdlestan.Model.Game;

public class GameSettings
{
    public Language Language { get; set; } = Language.English;
    public bool PauseAfterPrestige { get; set; } = false;
    public bool ShowHarvestParticles { get; set; } = true;
}
