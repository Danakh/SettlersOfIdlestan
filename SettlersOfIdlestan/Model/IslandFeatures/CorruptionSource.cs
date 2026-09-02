using System;
using System.Text.Json.Serialization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.Localization;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SettlersOfIdlestan.Model.IslandFeatures;

/// <summary>
/// Source de Corruption — feature de l'Inframonde, semée avec 50% de chance quand la Corruption
/// posée sur un nouvel hex par AutoExtendController.TrySpawnUnderworldDenizen atteint le niveau
/// maximal de l'île au moment du tirage (voir <see cref="CorruptionLevel"/>). Tant qu'elle subsiste,
/// fait monter d'un point par intervalle la Corruption de son propre hex — miroir exact des Os Divins
/// (voir CorruptionController.ProcessCorruptionSourceGrowth / DivineBones), mais sans le doublement de
/// plafond de ces derniers : <see cref="GetCorruptionCap"/> vaut exactement le niveau de corruption de
/// l'île figé à sa génération, jamais son double.
/// Seule case sur laquelle une Spire de Corruption peut désormais être placée (voir
/// CorruptionSpireController.GetPlaceableHexes, qui ne considère plus n'importe quel hex corrompu) :
/// la construction de la Spire détruit la Source sur son hex dès qu'elle atteint son premier niveau
/// (Built = true, voir CorruptionSpireController.ProcessInvestment).
/// </summary>
public class CorruptionSource : IslandFeature
{
    public override string? TextIcon => "🌀";

    public override bool BlocksHarvest => false;
    public override bool BlocksMonumentPlacement => false;

    public override GameEventType DiscoveredEventType => GameEventType.NoEvent;
    public override GameEventType RemovedEventType => GameEventType.NoEvent;

    /// <summary>Niveau de corruption de l'île au moment de la génération de cette feature (fige le plafond, comme DivineBones.CorruptionLevel).</summary>
    public int CorruptionLevel { get; set; } = 1;

    /// <summary>
    /// Niveau de Corruption au-delà duquel cette Source cesse d'alimenter son hex : exactement le
    /// niveau de corruption de l'île à sa génération (voir <see cref="CorruptionLevel"/>) —
    /// contrairement à <see cref="DivineBones.GetCorruptionCap"/>, jamais doublé.
    /// </summary>
    public int GetCorruptionCap() => Math.Max(1, CorruptionLevel);

    public override LocalizedEntry? GetTooltipEntry() =>
        new("hex_tooltip_corruption_source", new object[] { GetCorruptionCap() });

    /// <summary>
    /// Tant que les 3 vertex de prestige des Abysses (Porte Planaire / Faille des Abysses / Rituel de
    /// l'Éclipse Noire) ne sont pas achetés (seuil dupliqué de
    /// CorruptionSpireController.AbyssUnlockThreshold — le Modèle ne référence pas le Contrôleur), la
    /// Spire de Corruption ne peut pas encore être bâtie : le tooltip ne doit donc pas la nommer, pour
    /// ne pas spoiler une mécanique inaccessible.
    /// </summary>
    public override LocalizedEntry? GetTooltipEntry(SettlersOfIdlestan.Model.Civilization.Civilization civ) =>
        civ.ModifierAggregator.ApplyModifiers(ECategory.UNLOCK_ABYSS, "", 0) >= 3
            ? GetTooltipEntry()
            : new LocalizedEntry("hex_tooltip_corruption_source_locked", new object[] { GetCorruptionCap() });

    public CorruptionSource(HexCoord position, int corruptionLevel) : base(position)
    {
        CorruptionLevel = corruptionLevel;
    }

    [JsonConstructor]
    public CorruptionSource() : base() { }
}
