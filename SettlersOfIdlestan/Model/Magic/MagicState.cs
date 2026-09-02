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
    /// Crans d'épuisement accumulés par sort : chaque lancement réussi en ajoute un, le cooldown de
    /// <see cref="SpellDefinition.CooldownTicks"/> en retire un par cycle écoulé (voir
    /// <c>MagicController.ProcessSpellExhaustion</c>). Le coût en cristaux est multiplié par
    /// <see cref="SpellDefinition.CostMultiplierPerCast"/> autant de fois que de crans (voir
    /// <c>MagicController.GetSpellCost</c>). Remis à zéro au prestige, comme tout le <see cref="MagicState"/>.
    /// </summary>
    public Dictionary<SpellId, int> SpellExhaustionStacks { get; set; } = new();

    /// <summary>
    /// Tick de la dernière consommation d'un cycle de cooldown par sort — voir
    /// <see cref="SpellExhaustionStacks"/> et <c>MagicController.ProcessSpellExhaustion</c>. Le décompte
    /// démarre dès la première fois où le sort est observé comme connu, indépendamment des lancements.
    /// </summary>
    public Dictionary<SpellId, long> SpellCooldownLastTick { get; set; } = new();
}
