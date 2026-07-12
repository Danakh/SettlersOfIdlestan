using System.Collections.Generic;
using SettlersOfIdlestan.Model.HexGrid;

namespace SettlersOfIdlestan.Model.Civilization;

/// <summary>
/// Tout emplacement militaire ayant une position, une défense et des soldats, et pouvant être la
/// cible de renfort et d'attaque — voir <see cref="City"/> et <see cref="WarFleet"/>. Le système
/// militaire (MilitaryController et ses moteurs internes) opère sur cette interface plutôt que
/// directement sur City, afin de traiter les deux types de façon uniforme.
/// </summary>
public interface IMilitaryVertex
{
    Vertex Position { get; }
    int CivilizationIndex { get; }

    /// <summary>Défense actuelle (dynamique). Se régénère jusqu'à MaxDefense.</summary>
    int CurrentDefense { get; set; }

    /// <summary>Défense maximale.</summary>
    int MaxDefense { get; }

    /// <summary>Nombre de soldats en garnison.</summary>
    int Soldiers { get; set; }

    /// <summary>Capacité maximale de soldats.</summary>
    int MaxSoldiers { get; }

    long LastAttackTick { get; set; }
    long LastReinforcementTick { get; set; }
    long LastDefenseRegenTick { get; set; }

    /// <summary>Soldats en transit vers cet emplacement. Leur slot est réservé dès le départ de la source.</summary>
    List<InTransitSoldier> IncomingSoldiers { get; }

    /// <summary>Flux défini par le joueur : cible à attaquer ou à renforcer. Null si aucun flux.</summary>
    Vertex? FlowTarget { get; set; }

    /// <summary>
    /// Flux défini par le joueur : MonsterFeature ciblée pour une attaque à distance. Null si aucune cible.
    /// Mutuellement exclusif avec <see cref="FlowTarget"/>.
    /// </summary>
    HexCoord? MonsterAttackTarget { get; set; }
}
