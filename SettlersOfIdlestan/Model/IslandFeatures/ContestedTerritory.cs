using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.Localization;

namespace SettlersOfIdlestan.Model.IslandFeatures;

/// <summary>
/// Feature transiente (non sérialisée) marquant un hex adjacent aux villes de deux civilisations différentes.
/// Bloque la récolte et affiche un message dans le tooltip.
/// </summary>
public class ContestedTerritory : IslandFeature
{
    public override bool BlocksHarvest => true;
    public override bool IsDiscoverable => false;
    public override bool ShouldRenderIcon => true;
    public override string TextIcon => "⚔";

    public override LocalizedEntry? GetTooltipEntry() => new("hex_tooltip_contested");

    // Non utilisés (IsDiscoverable = false → jamais appelés par FeatureController)
    public override GameEventType DiscoveredEventType => GameEventType.NoEvent;
    public override GameEventType RemovedEventType => GameEventType.NoEvent;

    public ContestedTerritory(HexCoord position) : base(position) { }
    public ContestedTerritory() { }
}
