namespace SOITrailerGenerator.Trailer;

/// <summary>
/// Description déclarative d'une bande-annonce : résolution/fps communs et liste de séquences,
/// chacune rejouant une save depuis assets/Trailer/saves/. Désérialisé depuis TrailerDefinition.json.
/// </summary>
public class TrailerDefinition
{
    public int Width { get; set; } = 1920;
    public int Height { get; set; } = 1080;
    public int Fps { get; set; } = 30;
    public List<TrailerSequence> Sequences { get; set; } = new();
}

/// <summary>Une séquence = une save rejouée pendant une durée donnée, avec son propre cadrage caméra.</summary>
public class TrailerSequence
{
    /// <summary>Nom de fichier dans assets/Trailer/saves/ (ex: "Island2_Cities10.json").</summary>
    public string Save { get; set; } = "";

    /// <summary>Si true, ignore Save et rend un carton titre (fond noir + titre du jeu centré).</summary>
    public bool IsTitleCard { get; set; } = false;
    public int DurationSeconds { get; set; } = 5;

    /// <summary>1 = temps de jeu réel ; plus grand pour accélérer le temps simulé dans la séquence.</summary>
    public float SimulationSpeedMultiplier { get; set; } = 1f;

    public TrailerCamera Camera { get; set; } = new();

    /// <summary>Textes incrustés affichés pendant cette séquence (temps relatifs au début de la séquence).</summary>
    public List<TrailerTextCue> TextCues { get; set; } = new();

    /// <summary>Layout UI utilisé pour le rendu de cette séquence (HUD desktop ou mobile).</summary>
    public TrailerDeviceProfile DeviceProfile { get; set; } = TrailerDeviceProfile.Desktop;

    /// <summary>Onglet HUD affiché pendant toute la séquence (ex: insert arbre de prestige/recherche).</summary>
    public TrailerForcedTab ForcedTab { get; set; } = TrailerForcedTab.None;

    /// <summary>Couche d'île affichée (surface ou Inframonde) ; la save chargée doit déjà l'avoir débloquée.</summary>
    public TrailerViewedLayer ViewedLayer { get; set; } = TrailerViewedLayer.Surface;

    /// <summary>Pilote CivilizationAutoplayer pendant la séquence pour montrer de vraies constructions/combats.</summary>
    public TrailerAutoplayProfile AutoplayProfile { get; set; } = TrailerAutoplayProfile.None;

    /// <summary>
    /// Intervalle minimum entre deux appels successifs de l'autoplayer (en secondes).
    /// 0.5 = une action de construction toutes les 500 ms — les constructions apparaissent
    /// progressivement plutôt que toutes en une fraction de seconde.
    /// La valeur 0 ou négative déclenche l'autoplayer à chaque frame.
    /// </summary>
    public float AutoplayIntervalSeconds { get; set; } = 0.5f;

    /// <summary>
    /// Délai avant le premier appel de l'autoplayer (en secondes). Par défaut 0 (premier appel à la frame 0).
    /// Utile pour laisser le temps au spectateur de voir l'état initial avant la première action.
    /// </summary>
    public float AutoplayInitialDelaySeconds { get; set; } = 0f;

    /// <summary>Points de prestige injectés dans la save au démarrage de la séquence (triche de présentation).</summary>
    public int CheatPrestigePoints { get; set; } = 0;

    /// <summary>
    /// Points de recherche injectés dans la save au démarrage de la séquence (triche de présentation).
    /// Si > 0 et que la recherche n'est pas encore débloquée, achète automatiquement le vertex Central.
    /// </summary>
    public int CheatResearchPoints { get; set; } = 0;
}

/// <summary>Mappe un beat du storyboard (cf. STORYBOARD.md) sur les méthodes atomiques de CivilizationAutoplayer.</summary>
public enum TrailerAutoplayProfile
{
    None,
    Expand,
    Exploit,
    Exterminate,
    PrestigePurchase,
    ResearchPurchase
}

public enum TrailerDeviceProfile
{
    Desktop,
    Mobile
}

public enum TrailerForcedTab
{
    None,
    Island,
    Prestige,
    Research
}

public enum TrailerViewedLayer
{
    Surface,
    Underworld
}

/// <summary>
/// Texte incrusté affiché pendant une fenêtre de temps de la séquence, avec fondu d'entrée/sortie.
/// </summary>
public class TrailerTextCue
{
    public string Text { get; set; } = "";
    public float StartSeconds { get; set; }
    public float EndSeconds { get; set; }

    /// <summary>Durée du fondu d'entrée et de sortie, en secondes.</summary>
    public float FadeSeconds { get; set; } = 0.3f;

    public TrailerTextPosition Position { get; set; } = TrailerTextPosition.Bottom;
    public float FontSizePx { get; set; } = 64f;
}

public enum TrailerTextPosition
{
    Top,
    Center,
    Bottom
}

/// <summary>
/// Cadrage caméra d'une séquence. Mode "FitMap" cadre toute l'île ; "Fixed" utilise X/Y/Zoom explicites
/// constants ; "Keyframes" interpole <see cref="Keyframes"/> sur la durée de la séquence (travelling/zoom).
/// </summary>
public class TrailerCamera
{
    public TrailerCameraMode Mode { get; set; } = TrailerCameraMode.FitMap;
    public float X { get; set; }
    public float Y { get; set; }
    public float Zoom { get; set; } = 1f;

    /// <summary>Utilisé uniquement en mode <see cref="TrailerCameraMode.Keyframes"/>.</summary>
    public List<TrailerCameraKeyframe> Keyframes { get; set; } = new();
}

public enum TrailerCameraMode
{
    FitMap,
    Fixed,
    Keyframes
}

/// <summary>Point de passage caméra à un instant donné (secondes depuis le début de la séquence).</summary>
public class TrailerCameraKeyframe
{
    public float TimeSeconds { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float Zoom { get; set; } = 1f;

    /// <summary>Courbe d'interpolation appliquée entre le keyframe précédent et celui-ci.</summary>
    public TrailerCameraEasing Easing { get; set; } = TrailerCameraEasing.Linear;
}

public enum TrailerCameraEasing
{
    Linear,
    EaseIn,
    EaseOut,
    EaseInOut
}
