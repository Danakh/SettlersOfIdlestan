using SettlersOfIdlestan.Model.Localization;
using System.Collections.Generic;

namespace SettlersOfIdlestan.Model.Game;

public class GameSettings
{
    public Language Language { get; set; } = Language.English;
    public bool PauseAfterPrestige { get; set; } = false;
    public bool ShowHarvestParticles { get; set; } = true;
    public bool ShowCityMilitaryStats { get; set; } = true;
    public bool ShowHarvestCooldown { get; set; } = true;
    public bool ShowCorruptionDominion { get; set; } = true;
    public bool Fullscreen { get; set; } = false;
    public bool DemoMode { get; set; } = false;
    public bool CloudSaveEnabled { get; set; } = true;

    /// <summary>
    /// Interrupteur global de toutes les automatisations (routes, bâtiments, monuments, militaire —
    /// voir AutomationSettings). Persiste entre les îles/prestiges, contrairement aux réglages
    /// individuels par automatisation. Désactiver ceci ne modifie pas les préférences individuelles :
    /// elles restent en mémoire et reprennent effet dès la réactivation (voir AutomationSettings.Bind).
    /// </summary>
    public bool AutomationsEnabled { get; set; } = true;
    public float UiScale { get; set; } = 1f;
    public MenuPosition ForceMenuPosition { get; set; } = MenuPosition.Auto;
    public NumberFormatMode NumberFormat { get; set; } = NumberFormatMode.Classic;

    /// <summary>
    /// Clés des contrôles d'automatisation épinglés au panel de civilisation. Persiste entre les
    /// îles et les redémarrages du jeu (contrairement à AutomationSettings, réinitialisé à chaque île).
    /// </summary>
    public HashSet<string> PinnedCivPanelKeys { get; set; } = [];

    /// <summary>
    /// Familles d'événements masquées dans le Journal (voir <see cref="EventLogFilter"/>). Vit ici
    /// plutôt que dans le WorldState : c'est une préférence d'affichage, qui doit survivre aux
    /// nouvelles îles, aux prestiges et aux ascensions. Câblé sur le journal de l'île courante à
    /// chaque initialisation — voir MainGameController.InitializeControllersForCurrentIsland.
    /// </summary>
    public EventLogFilter EventLogFilter { get; set; } = new();
}
