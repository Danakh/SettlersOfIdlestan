using SettlersOfIdlestan.Model.HexGrid;

namespace SettlersOfIdlestan.Model.Civilization;

/// <summary>
/// Camp Mobile : emplacement militaire terrestre sans bâtiment, analogue à <see cref="WarFleet"/>
/// mais construit sur le réseau routier plutôt que sur une Balise Maritime (voir
/// MobileCampController). Débloqué par la recherche MobileCampConstruction. Doit être à distance
/// >= 2 (arêtes) de tout autre <see cref="IMilitaryVertex"/> de la même civilisation ; aucune
/// restriction vis-à-vis des civilisations adverses. N'est proposé à la construction que là où un
/// avant-poste classique ne peut pas être bâti, et est détruit automatiquement dès qu'une ville
/// (alliée ou ennemie) est construite à distance &lt;= 1.
/// </summary>
[Serializable]
public class MobileCamp : IMilitaryVertex
{
    /// <summary>Défense de base d'un Camp Mobile (il n'a pas de bâtiments pour en fournir).</summary>
    public const int DefenseBonus = 20;

    /// <summary>Capacité de soldats de base d'un Camp Mobile (il n'a pas de bâtiments pour en fournir).</summary>
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

    /// <summary>Fired just before the camp is removed from its civilization.</summary>
    [field: NonSerialized]
    public event EventHandler<EventArgs>? Destroyed;

    internal void RaiseDestroyed() => Destroyed?.Invoke(this, EventArgs.Empty);

    public MobileCamp()
    {
        Position = null!;
    }

    public MobileCamp(Vertex position)
    {
        Position = position;
    }
}
