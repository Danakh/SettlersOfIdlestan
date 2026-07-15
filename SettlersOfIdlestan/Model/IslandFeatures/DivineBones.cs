using System;
using System.Text.Json.Serialization;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Localization;

namespace SettlersOfIdlestan.Model.IslandFeatures;

/// <summary>
/// Os Divins — Monument généré sur chaque île des Abysses créée après la première (voir
/// AutoExtendController.OnHexesRevealed), révélé une fois la recherche Boussole du Vide acquise
/// (ECategory.UNLOCK_DIVINE_BONES). Investissement "Purification" à coût double (Cristal + points
/// de recherche), comme une Merveille de niveau 0 à objectif unique. Une fois purifié, ne peut plus
/// être investi. La Purification octroie toujours 1 os divin (WorldState.DivineBoneCount) ;
/// <see cref="BonesPerEssence"/> os se convertissent automatiquement en 1 essence divine (GodState).
/// Les os sont stockés sur l'île courante et donc perdus au prestige : il faut en réunir
/// <see cref="BonesPerEssence"/> sur la même île. Le nombre d'essences détenues est plafonné par le
/// niveau de corruption (voir <see cref="GetEssenceCap"/>) — au-delà, il faut prestige pour
/// augmenter ce plafond.
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
        return new("hex_tooltip_divine_bones", new object[] { BonesPerEssence, GetEssenceCap() });
    }

    /// <summary>Niveau de corruption de l'île au moment de la génération de cette feature (fige le coût de Purification).</summary>
    public int CorruptionLevel { get; set; } = 1;

    /// <summary>True une fois la Purification terminée (essence octroyée ou non), plus rien à investir.</summary>
    public bool Purified { get; set; } = false;

    /// <summary>True si l'os divin octroyé par cette Purification a complété une conversion de <see cref="BonesPerEssence"/> os en essence divine.</summary>
    public bool EssenceGranted { get; set; } = false;

    /// <summary>
    /// Nombre d'essences divines détenues depuis la dernière Ascension, resynchronisé à chaque tick
    /// par DivineBonesController depuis GodState.DivineEssence. Pilote le multiplicateur de coût
    /// (N dans la formule) — l'Ascension, en remettant DivineEssence à zéro, réinitialise donc le
    /// coût de Purification. Stocké sur la feature car GetInvestmentCost n'a accès qu'à la
    /// civilisation, pas au GodState cross-prestige.
    /// </summary>
    public int EssenceAlreadyCollected { get; set; } = 0;

    /// <summary>Points de recherche déjà investis vers la Purification (pool séparé de InvestedResources, qui ne couvre que les Resource).</summary>
    public long InvestedResearch { get; set; } = 0;

    /// <summary>True si le joueur a activé le prélèvement progressif de points de recherche.</summary>
    public bool ResearchInvestmentEnabled { get; set; } = false;

    /// <summary>Tick du dernier cycle d'investissement en recherche (indépendant de LastInvestmentTick, dédié au Cristal).</summary>
    public long LastResearchInvestmentTick { get; set; } = 0;

    public const long BaseCrystalCost = 500;
    public const long BaseResearchCost = 500_000;

    /// <summary>Nombre d'os divins (octroyés à 100% par chaque Purification) à réunir sur la même île pour obtenir 1 essence divine.</summary>
    public const int BonesPerEssence = 4;

    /// <summary>Le plafond d'essences divines détenues démarre au niveau de corruption 4 (plafond de 1), voir <see cref="GetEssenceCap"/>.</summary>
    public const int EssenceCapCorruptionLevelOffset = 3;

    /// <summary>
    /// Nombre maximum d'essences divines que le joueur peut détenir (GodState.DivineEssence) au
    /// niveau de corruption de cette feature : une seule par niveau de corruption à partir du
    /// niveau 4 (0 en dessous). Pour en obtenir davantage, il faut prestige afin d'augmenter le
    /// niveau de corruption (voir PrestigeState.CurrentCorruptionLevel).
    /// </summary>
    public int GetEssenceCap() => Math.Max(0, CorruptionLevel - EssenceCapCorruptionLevelOffset);

    /// <summary>(niveau de corruption + 2) ^ N, N = nombre d'essences divines détenues depuis la dernière Ascension.</summary>
    public static long GetCostMultiplier(int corruptionLevel, int essenceAlreadyCollected)
    {
        double multiplier = Math.Pow(corruptionLevel + 2, essenceAlreadyCollected);
        return (long)Math.Min(multiplier, 1e15);
    }

    public long GetRequiredResearch() => BaseResearchCost * GetCostMultiplier(CorruptionLevel, EssenceAlreadyCollected);

    public override ResourceSet GetInvestmentCost(SettlersOfIdlestan.Model.Civilization.Civilization playerCiv) => new()
    {
        { Resource.Crystal, (int)Math.Min(int.MaxValue, BaseCrystalCost * GetCostMultiplier(CorruptionLevel, EssenceAlreadyCollected)) },
    };

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
