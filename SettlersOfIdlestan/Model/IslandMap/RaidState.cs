using SettlersOfIdlestan.Model.HexGrid;

namespace SettlersOfIdlestan.Model.IslandMap;

/// <summary>
/// Un raid en cours sur un layer donné : sa cible et l'entretien qu'il coûte à la seconde. Une
/// instance par layer raidé, rangée dans <see cref="AutomationSettings.RaidsByLayer"/> — les raids
/// des différents layers sont indépendants et se déroulent en parallèle (voir
/// Controller.Military.RaidEngine). Exactement une des deux cibles est renseignée : un raid vise soit
/// un emplacement militaire ennemi (<see cref="TargetVertex"/>), soit une MonsterFeature
/// (<see cref="TargetHex"/>).
/// </summary>
public class RaidState
{
    /// <summary>Position de l'emplacement militaire ennemi ciblé. Null si la cible est une MonsterFeature.</summary>
    public Vertex? TargetVertex { get; set; }

    /// <summary>Position de la MonsterFeature ciblée. Null si la cible est un emplacement militaire.</summary>
    public HexCoord? TargetHex { get; set; }

    /// <summary>Coût en or par seconde du raid. Commence à <see cref="Controller.Military.RaidEngine.InitialUpkeep"/>, monte de 2 par seconde.</summary>
    public int CurrentUpkeep { get; set; }
}
