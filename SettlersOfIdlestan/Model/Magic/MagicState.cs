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

    /// <summary>
    /// [Legacy v0.21] Ancien emplacement du drapeau d'automatisation, remplacé par
    /// <see cref="MagicState.AutomatedRituals"/> : l'automatisation doit survivre à l'arrêt du rituel
    /// (plus de cristaux), donc elle ne peut pas être portée par le rituel actif. Lue une seule fois au
    /// chargement d'une sauvegarde antérieure (<c>MagicController.MigrateLegacyAutomationFlags</c>),
    /// jamais écrite ailleurs.
    /// </summary>
    public bool IsAutomated { get; set; }

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
    /// Rituels dont la puissance est ajustée automatiquement (case à cocher "auto", pouvoir divin
    /// Rituels Divins — voir <c>MagicController.SetRitualAutomated</c> et
    /// <c>MagicController.ProcessRitualPowerAutomation</c>). Indépendant de
    /// <see cref="ActiveRituals"/> à dessein : un rituel automatisé qui s'arrête faute de cristaux
    /// reste armé et sera relancé dès que le gain net de cristaux le permettra.
    /// </summary>
    public List<RitualId> AutomatedRituals { get; set; } = new();

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

    /// <summary>
    /// Charges de lancement accumulées par sort (pouvoir divin Magie Divine, voir
    /// <c>AscensionState.IsDivineMagicActive</c>) : chaque cycle de cooldown écoulé sans épuisement
    /// accumulé (<see cref="SpellExhaustionStacks"/> à zéro) en ajoute une, jusqu'à
    /// <c>MagicController.MaxSpellCharges</c>. Lancer le sort en consomme une au lieu d'ajouter un cran
    /// d'épuisement (voir <c>MagicController.RegisterSpellCast</c>). Remis à zéro au prestige comme tout
    /// le <see cref="MagicState"/> ; <see cref="GrantInitialSpellCharges"/> en recrédite une aussitôt.
    /// </summary>
    public Dictionary<SpellId, int> SpellCharges { get; set; } = new();

    /// <summary>
    /// Crédite 1 charge de lancement à chaque sort défini — à appeler une fois par prestige/Ascension
    /// sur le <see cref="MagicState"/> fraîchement créé, seulement si Magie Divine est active (voir
    /// <c>AscensionState.IsDivineMagicActive</c>), depuis <c>PrestigeController.PerformPrestige</c> et
    /// <c>AscensionController.FinishAscensionWithRace</c>.
    /// </summary>
    public void GrantInitialSpellCharges()
    {
        foreach (var def in SpellDefinitions.All)
            SpellCharges[def.Id] = 1;
    }
}
