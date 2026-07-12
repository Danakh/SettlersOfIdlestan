using SettlersOfIdlestan.Model.HexGrid;

namespace SettlersOfIdlestan.Model.Civilization;

/// <summary>
/// Flotte de Guerre : une entité militaire sans bâtiment, construite sur une Balise Maritime
/// existante (voir WarFleetController). Contrairement à City, elle n'a jamais de bâtiment — sa
/// défense et sa capacité de soldats sont des valeurs fixes. Elle reste une cible normale de
/// renfort et d'attaque via <see cref="IMilitaryVertex"/>.
/// </summary>
[Serializable]
public class WarFleet : IMilitaryVertex
{
    /// <summary>Défense de base d'une Flotte de Guerre (elle n'a pas de bâtiments pour en fournir).</summary>
    public const int DefenseBonus = 20;

    /// <summary>Capacité de soldats de base d'une Flotte de Guerre (elle n'a pas de bâtiments pour en fournir).</summary>
    public const int MaxSoldiersBonus = 20;

    public Vertex Position { get; set; }

    public int CivilizationIndex { get; set; }

    /// <summary>Défense actuelle (dynamique). Se régénère jusqu'à MaxDefense.</summary>
    public int CurrentDefense { get; set; }

    public int MaxDefense => DefenseBonus;

    public int Soldiers { get; set; }

    public int MaxSoldiers => MaxSoldiersBonus;

    public long LastSoldierProductionTick { get; set; }
    public long LastDefenseRegenTick { get; set; }
    public long LastAttackTick { get; set; }
    public long LastReinforcementTick { get; set; }

    public List<InTransitSoldier> IncomingSoldiers { get; set; } = new();

    public Vertex? FlowTarget { get; set; }
    public HexCoord? MonsterAttackTarget { get; set; }

    /// <summary>Fired just before the fleet is removed from its civilization.</summary>
    [field: NonSerialized]
    public event EventHandler<EventArgs>? Destroyed;

    internal void RaiseDestroyed() => Destroyed?.Invoke(this, EventArgs.Empty);

    public WarFleet()
    {
        Position = null!;
    }

    public WarFleet(Vertex position)
    {
        Position = position;
    }
}
