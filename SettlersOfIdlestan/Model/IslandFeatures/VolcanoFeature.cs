using System.Text.Json.Serialization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.Localization;

namespace SettlersOfIdlestan.Model.IslandFeatures;

/// <summary>
/// Volcan — feature permanente qui bloque la récolte et érupe périodiquement,
/// infligeant des dégâts en cascade aux villes proches.
/// Anneau 1 (vertex touchant le hex volcan) : 50 % de chance d'être touché.
/// Anneau 2 (vertex touchant un voisin du hex volcan) : 25 % de chance.
/// 10 dégâts par éruption. Présent à partir de l'île 4.
/// </summary>
[Serializable]
public class VolcanoFeature : IslandFeature
{
    /// <summary>Tick de la dernière éruption (utilisé par VolcanoController).</summary>
    public long LastEruptionTick { get; set; } = 0;

    public override GameEventType DiscoveredEventType => GameEventType.VolcanoDiscovered;
    public override GameEventType RemovedEventType    => GameEventType.NoEvent;

    public override bool BlocksHarvest => true;

    // VolcanoRenderer gère l'affichage (icône dynamique + tremblement) — GameBoardRenderer ignoré.
    public override bool ShouldRenderIcon => false;

    public override LocalizedEntry? GetTooltipEntry() => new("hex_tooltip_volcano_info");

    public VolcanoFeature(HexCoord position) : base(position) { }

    [JsonConstructor]
    public VolcanoFeature() : base() { }
}
