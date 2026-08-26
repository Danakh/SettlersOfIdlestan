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

    /// <summary>
    /// Nombre de lancements réussis de chaque sort depuis le début du run. Seuls les sorts dont
    /// <see cref="SpellDefinition.CostMultiplierPerCast"/> est supérieur à 1 (Pont du Vide) s'en servent :
    /// leur coût en cristaux est multiplié par ce facteur à chaque entrée de ce compteur (voir
    /// <c>MagicController.GetSpellCost</c>). Remis à zéro au prestige, comme tout le <see cref="MagicState"/>.
    /// </summary>
    public Dictionary<SpellId, int> SpellCastCounts { get; set; } = new();
}
