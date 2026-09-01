using System;
using System.Text.Json.Serialization;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Localization;

namespace SettlersOfIdlestan.Model.IslandFeatures;

/// <summary>
/// Os Divins — Monument généré sur chaque île des Abysses créée après la première (voir
/// AutoExtendController.OnHexesRevealed), révélé une fois la recherche Boussole du Vide acquise
/// (ECategory.UNLOCK_DIVINE_BONES). Investissement "Purification" en Cristal, Mithril et Acier,
/// comme une Merveille de niveau 0 à objectif unique. Une fois purifié, la feature est
/// retirée de la carte (voir DivineBonesController.ProcessInvestment) — Purified/EssenceGranted ne
/// sont donc observables que de manière transitoire, avant suppression. La Purification octroie
/// directement 1 essence divine (GodState.DivineEssence),
/// sauf si le plafond de la feature (voir <see cref="GetEssenceCap"/>, lié au niveau de corruption et
/// au nombre de pouvoirs divins débloqués) est déjà atteint — auquel cas la Purification a quand même
/// lieu, mais n'accorde aucune essence.
/// Les essences divines sont normalement perdues au prestige (sauf un nombre limité conservé par
/// ECategory.DIVINE_ESSENCE_KEPT_ON_PRESTIGE — voir Reliquaire Sacré/Renforcé et
/// PrestigeController.PerformPrestige).
/// Tant qu'ils ne sont pas purifiés, les Os Divins sont aussi des générateurs de Corruption : ils
/// font monter d'un point par intervalle la Corruption de leur propre hex, jusqu'à
/// <see cref="GetCorruptionCap"/> (voir CorruptionController.ProcessDivineBonesCorruptionGrowth).
/// </summary>
public class DivineBones : Monument
{
    public override string? TextIcon => "🦴";
    public override float SvgIconSize => 24f;

    /// <summary>Toujours généré avec la carte, mais révélé par recherche (voir ShouldRenderIconFor) plutôt que par visibilité de brouillard de guerre.</summary>
    public override bool IsDiscoverable => false;

    public override bool ShouldRenderIconFor(SettlersOfIdlestan.Model.Civilization.Civilization civ) =>
        civ.ModifierAggregator.HasModifier(SettlersOfIdlestan.Model.GameplayModifier.Modifier.ECategory.UNLOCK_DIVINE_BONES);

    public override LocalizedEntry GetTooltipEntry()
    {
        if (Purified)
            return new(EssenceGranted ? "hex_tooltip_divine_bones_purified" : "hex_tooltip_divine_bones_purified_no_essence");
        return new("hex_tooltip_divine_bones", new object[] { GetEssenceCap(), GetCorruptionCap() });
    }

    /// <summary>Niveau de corruption de l'île au moment de la génération de cette feature (fige le coût de Purification).</summary>
    public int CorruptionLevel { get; set; } = 1;

    /// <summary>True une fois la Purification terminée (essence octroyée ou non), plus rien à investir.</summary>
    public bool Purified { get; set; } = false;

    /// <summary>True si cette Purification a accordé une essence divine (plafond de la feature non atteint, voir <see cref="GetEssenceCap"/>).</summary>
    public bool EssenceGranted { get; set; } = false;

    /// <summary>
    /// Nombre d'essences divines détenues depuis la dernière Ascension, resynchronisé à chaque tick
    /// par DivineBonesController depuis GodState.DivineEssence. Pilote le multiplicateur de coût
    /// (N dans la formule) — l'Ascension, en remettant DivineEssence à zéro, réinitialise donc le
    /// coût de Purification. Stocké sur la feature car GetInvestmentCost n'a accès qu'à la
    /// civilisation, pas au GodState cross-prestige.
    /// </summary>
    public int EssenceAlreadyCollected { get; set; } = 0;

    /// <summary>
    /// Nombre de pouvoirs divins débloqués (GodState.AscensionState.UnlockedPowers.Count), resynchronisé
    /// à chaque tick par DivineBonesController comme <see cref="EssenceAlreadyCollected"/>. Vient
    /// s'ajouter au niveau de corruption dans <see cref="GetEssenceCap"/> — stocké sur la feature pour
    /// la même raison que EssenceAlreadyCollected (GetEssenceCap n'a accès qu'à la civilisation/à
    /// elle-même, pas au GodState cross-prestige, et c'est de cette valeur que dépendent aussi bien le
    /// tooltip que le panneau de Monument sélectionné).
    /// </summary>
    public int UnlockedPowersBonus { get; set; } = 0;

    public const long BaseCrystalCost = 250;

    /// <summary>Coût en Mithril (indépendant du coût en Cristal depuis sa réduction de moitié).</summary>
    public const long BaseMithrilCost = 500;

    /// <summary>Coût en Acier, égal au double du coût en Mithril.</summary>
    public const long BaseSteelCost = 2 * BaseMithrilCost;

    /// <summary>
    /// Nombre maximum d'essences divines que le joueur peut détenir (GodState.DivineEssence) au
    /// niveau de corruption de cette feature : le niveau de corruption lui-même, plus 1 par pouvoir
    /// divin déjà débloqué (voir <see cref="UnlockedPowersBonus"/>, et
    /// AscensionController.GetDivineEssenceCap pour l'équivalent cross-feature). Pour en obtenir
    /// davantage, il faut donc soit prestige pour relever la corruption, soit débloquer un nouveau
    /// pouvoir divin.
    /// </summary>
    public int GetEssenceCap() => Math.Max(0, CorruptionLevel) + UnlockedPowersBonus;

    /// <summary>Multiplicateur appliqué au niveau de corruption de l'île pour obtenir le plafond de génération.</summary>
    public const int CorruptionCapMultiplier = 2;

    /// <summary>
    /// Niveau de Corruption au-delà duquel les Os Divins non purifiés cessent d'alimenter leur hex :
    /// deux fois le niveau de corruption de l'île à leur génération (voir <see cref="CorruptionLevel"/>).
    /// Ce n'est qu'un plafond de génération : une Corruption déjà plus élevée (débordement d'un voisin,
    /// tirage initial de AutoExtendController.PlaceAbyssCorruption) n'est jamais réduite par ce plafond.
    /// </summary>
    public int GetCorruptionCap() => Math.Max(1, CorruptionCapMultiplier * CorruptionLevel);

    /// <summary>(niveau de corruption + 2) ^ (1 + N / 2), N = nombre d'essences divines détenues depuis la dernière Ascension.</summary>
    public static long GetCostMultiplier(int corruptionLevel, int essenceAlreadyCollected)
    {
        double multiplier = Math.Pow(corruptionLevel + 2, 1.0 + essenceAlreadyCollected / 2.0);
        return (long)Math.Min(multiplier, 1e15);
    }

    /// <summary>Coût en Cristal, Mithril et Acier, après DivineBonesCostReduction de la civilisation (hex de prestige Ossuaire).</summary>
    public override ResourceSet GetBaseInvestmentCost(SettlersOfIdlestan.Model.Civilization.Civilization playerCiv)
    {
        long multiplier = GetCostMultiplier(CorruptionLevel, EssenceAlreadyCollected);
        return new()
        {
            { Resource.Crystal, (int)Math.Min(int.MaxValue, ApplyCostReduction(BaseCrystalCost * multiplier, playerCiv)) },
            { Resource.Mithril, (int)Math.Min(int.MaxValue, ApplyCostReduction(BaseMithrilCost * multiplier, playerCiv)) },
            { Resource.Steel, (int)Math.Min(int.MaxValue, ApplyCostReduction(BaseSteelCost * multiplier, playerCiv)) },
        };
    }

    private static long ApplyCostReduction(long baseCost, SettlersOfIdlestan.Model.Civilization.Civilization playerCiv)
    {
        double reduction = playerCiv.DivineBonesCostReduction;
        return reduction <= 0 ? baseCost : Math.Max(1, (long)(baseCost * (1.0 - reduction)));
    }

    [JsonIgnore]
    public override string PanelTitleKey => "divine_bones_panel_title";

    [JsonIgnore]
    public override string? PanelTitleSuffix => null;

    public DivineBones(HexCoord position, int corruptionLevel) : base(position)
    {
        CorruptionLevel = corruptionLevel;
    }

    [JsonConstructor]
    public DivineBones() : base() { }
}
