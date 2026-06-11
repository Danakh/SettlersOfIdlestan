using System.Text.Json.Serialization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.Localization;

namespace SettlersOfIdlestan.Model.IslandFeatures
{
    /// <summary>
    /// Cercle de Fées — feature de surface débloquée par le vertex de prestige Cercles de Fées.
    /// Une fois découvert, génère passivement des cristaux pour le joueur.
    /// </summary>
    public class FairyCircle : IslandFeature
    {
        /// <summary>Cristaux générés par cycle d'entretien magique une fois découvert.</summary>
        public const int CrystalsPerCycle = 1;

        public override GameEventType DiscoveredEventType => GameEventType.FairyCircleDiscovered;
        public override GameEventType RemovedEventType    => GameEventType.NoEvent;

        public override LocalizedEntry? GetTooltipEntry() => new("hex_tooltip_fairy_circle");

        public override string? TextIcon => "✨";
        public override bool ShouldRenderIcon => Found;

        public FairyCircle(HexCoord position) : base(position) { }

        [JsonConstructor]
        public FairyCircle() : base() { }
    }
}
