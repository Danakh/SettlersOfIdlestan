using SettlersOfIdlestan.Model.Localization;
using System.Collections.Generic;

namespace SettlersOfIdlestan.Model.Game;

public class GameSettings
{
    public Language Language { get; set; } = Language.English;
    public bool PauseAfterPrestige { get; set; } = false;
    public bool ShowHarvestParticles { get; set; } = true;
    public bool ShowCityMilitaryStats { get; set; } = true;
    public bool Fullscreen { get; set; } = false;
    public bool DemoMode { get; set; } = false;
    public bool CloudSaveEnabled { get; set; } = true;
    public float UiScale { get; set; } = 1f;
    public MenuPosition ForceMenuPosition { get; set; } = MenuPosition.Auto;

    /// <summary>
    /// Clés des contrôles d'automatisation épinglés au panel de civilisation. Persiste entre les
    /// îles et les redémarrages du jeu (contrairement à AutomationSettings, réinitialisé à chaque île).
    /// </summary>
    public HashSet<string> PinnedCivPanelKeys { get; set; } = [];
}
