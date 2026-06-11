using System;
using System.Collections.Generic;

namespace SettlersOfIdlestan.Model.Magic;

/// <summary>
/// Un rituel actuellement actif : puissance courante et tick du dernier entretien payé.
/// </summary>
[Serializable]
public class ActiveRitual
{
    public RitualId Id { get; set; }
    public int Power { get; set; } = 1;
    public long LastUpkeepTick { get; set; }

    public ActiveRitual() { }

    public ActiveRitual(RitualId id, int power, long launchTick)
    {
        Id = id;
        Power = power;
        LastUpkeepTick = launchTick;
    }
}

/// <summary>
/// État de la magie du joueur pour le run en cours (réinitialisé à chaque prestige).
/// Sérialisé avec le WorldState.
/// </summary>
[Serializable]
public class MagicState
{
    public List<ActiveRitual> ActiveRituals { get; set; } = new();
}
