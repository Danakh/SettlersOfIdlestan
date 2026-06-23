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
    public int DurationSeconds { get; set; } = 5;

    /// <summary>1 = temps de jeu réel ; plus grand pour accélérer le temps simulé dans la séquence.</summary>
    public float SimulationSpeedMultiplier { get; set; } = 1f;

    public TrailerCamera Camera { get; set; } = new();
}

/// <summary>Cadrage caméra d'une séquence. Mode "FitMap" cadre toute l'île ; "Fixed" utilise X/Y/Zoom explicites.</summary>
public class TrailerCamera
{
    public TrailerCameraMode Mode { get; set; } = TrailerCameraMode.FitMap;
    public float X { get; set; }
    public float Y { get; set; }
    public float Zoom { get; set; } = 1f;
}

public enum TrailerCameraMode
{
    FitMap,
    Fixed
}
