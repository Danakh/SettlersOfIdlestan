using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.Localization;
using SettlersOfIdlestan.Model.Civilization;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SettlersOfIdlestan.Model.IslandFeatures;

/// <summary>
/// Feature transiente (non sérialisée) marquant un hex adjacent aux villes de deux civilisations différentes.
/// Bloque la récolte et affiche un message dans le tooltip, sauf pour une civilisation ayant recherché
/// Diplomatie (récolte autorisée à demi-vitesse — voir <see cref="GetHarvestTimeMultiplier"/>).
/// </summary>
public class ContestedTerritory : IslandFeature
{
    public override bool BlocksHarvest => true;
    public override bool IsDiscoverable => false;
    public override bool ShouldRenderIcon => true;
    public override string TextIcon => "⚔";

    public override bool BlocksHarvestFor(SettlersOfIdlestan.Model.Civilization.Civilization civ) =>
        !civ.ModifierAggregator.HasModifier(ECategory.UNLOCK_CONTESTED_HARVEST);

    /// <summary>Double le temps de récolte (50% de production) pour une civilisation ayant débloqué Diplomatie.</summary>
    public override double GetHarvestTimeMultiplier(SettlersOfIdlestan.Model.Civilization.Civilization civ) => 2.0;

    public override LocalizedEntry? GetTooltipEntry() => new("hex_tooltip_contested");

    // Non utilisés (IsDiscoverable = false → jamais appelés par FeatureController)
    public override GameEventType DiscoveredEventType => GameEventType.NoEvent;
    public override GameEventType RemovedEventType => GameEventType.NoEvent;

    public ContestedTerritory(HexCoord position) : base(position) { }
    public ContestedTerritory() { }
}
