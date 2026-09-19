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

    /// <summary>
    /// Affichage du panneau de tutoriel. Masquer le panneau n'arrête pas le tutoriel :
    /// TutorialService continue d'avancer les étapes, si bien que réactiver l'option
    /// réaffiche l'étape en cours plutôt que de repartir du début.
    /// </summary>
    public bool ShowTutorial { get; set; } = true;
    public bool Fullscreen { get; set; } = false;

    /// <summary>
    /// Interrupteur général des bruitages. Distinct de <see cref="SoundVolume"/> : couper le son
    /// puis le rétablir doit rendre le volume que le joueur avait réglé, pas zéro.
    /// </summary>
    public bool SoundEnabled { get; set; } = true;

    /// <summary>
    /// Volume des bruitages, de 0 à 1. Sans effet quand <see cref="SoundEnabled"/> est faux.
    /// La valeur par défaut est celle de <c>GameAudioService.DefaultVolume</c>, qui vit dans la
    /// couche d'affichage — le modèle ne connaît pas le service audio.
    /// </summary>
    public float SoundVolume { get; set; } = 0.6f;

    /// <summary>
    /// Bruitages de combat (coups portés, coups reçus, bâtiment détruit). Une fin de partie où
    /// vingt villes affrontent trente monstres les enchaîne sans fin : le joueur qui veut garder
    /// les annonces peut couper cette famille seule, sans couper le son.
    /// Sans effet quand <see cref="SoundEnabled"/> est faux.
    /// </summary>
    public bool SoundCombatEnabled { get; set; } = true;

    /// <summary>
    /// Bruitages des notifications (toasts et fanfare de succès). Même logique que
    /// <see cref="SoundCombatEnabled"/> : une famille se coupe sans toucher aux autres.
    /// Sans effet quand <see cref="SoundEnabled"/> est faux.
    /// </summary>
    public bool SoundToastEnabled { get; set; } = true;

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

    /// <summary>
    /// Dernier dossier utilisé par l'export ou l'import manuel d'une sauvegarde (« Sauvegarder » /
    /// « Charger » du menu). Les deux commandes partagent la même valeur : le joueur range ses
    /// sauvegardes à un seul endroit, et retrouver ce dossier vaut mieux que rouvrir la boîte sur
    /// le dossier <c>saves</c> à chaque fois. Un dossier disparu (clé USB retirée, dossier
    /// supprimé, réglages venus d'une autre machine) fait simplement retomber la boîte sur le
    /// dossier de la sauvegarde automatique — voir DesktopFileSystemService.StartLocation.
    /// N'a de sens que pour les heads dotés d'un sélecteur de fichier natif (bureau).
    /// </summary>
    public string? LastSaveDirectory { get; set; }
}
