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
/// de recherche), comme une Merveille de niveau 0 à objectif unique. Une fois purifié, octroie une
/// essence divine (GodState) et ne peut plus être investi.
/// </summary>
public class DivineBones : Monument
{
    public override string? TextIcon => "🦴";
    public override float SvgIconSize => 24f;

    /// <summary>Toujours généré avec la carte, mais révélé par recherche (voir ShouldRenderIconFor) plutôt que par visibilité de brouillard de guerre.</summary>
    public override bool IsDiscoverable => false;

    public override bool ShouldRenderIconFor(SettlersOfIdlestan.Model.Civilization.Civilization civ) =>
        civ.ModifierAggregator.HasModifier(SettlersOfIdlestan.Model.GameplayModifier.Modifier.ECategory.UNLOCK_DIVINE_BONES);

    public override LocalizedEntry GetTooltipEntry() => new(Purified ? "hex_tooltip_divine_bones_purified" : "hex_tooltip_divine_bones");

    /// <summary>Niveau de corruption de l'île au moment de la génération de cette feature (fige le coût de Purification).</summary>
    public int CorruptionLevel { get; set; } = 1;

    /// <summary>True une fois la Purification terminée : l'essence divine a été octroyée, plus rien à investir.</summary>
    public bool Purified { get; set; } = false;

    /// <summary>
    /// Nombre total d'essences divines déjà collectées, resynchronisé à chaque tick par
    /// DivineBonesController depuis GodState.TotalDivineEssenceEarned. Pilote le multiplicateur de
    /// coût (N dans la formule) — stocké sur la feature car GetInvestmentCost n'a accès qu'à la
    /// civilisation, pas au GodState cross-prestige.
    /// </summary>
    public int EssenceAlreadyCollected { get; set; } = 0;

    /// <summary>Points de recherche déjà investis vers la Purification (pool séparé de InvestedResources, qui ne couvre que les Resource).</summary>
    public long InvestedResearch { get; set; } = 0;

    /// <summary>True si le joueur a activé le prélèvement progressif de points de recherche.</summary>
    public bool ResearchInvestmentEnabled { get; set; } = false;

    /// <summary>Tick du dernier cycle d'investissement en recherche (indépendant de LastInvestmentTick, dédié au Cristal).</summary>
    public long LastResearchInvestmentTick { get; set; } = 0;

    public const long BaseCrystalCost = 1000;
    public const long BaseResearchCost = 1_000_000;

    /// <summary>(niveau de corruption + 2) ^ N, N = nombre d'essences divines déjà collectées.</summary>
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
