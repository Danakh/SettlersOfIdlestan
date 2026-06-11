using System.Text.Json.Serialization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.Localization;

namespace SettlersOfIdlestan.Model.IslandFeatures
{
    /// <summary>
    /// Dolmen — feature de surface débloquée par le vertex de prestige Dolmens.
    /// Une fois découvert, génère passivement des cristaux pour le joueur.
    /// </summary>
    public class Dolmen : IslandFeature
    {
        /// <summary>Cristaux générés par cycle d'entretien magique une fois découvert.</summary>
        public const int CrystalsPerCycle = 2;

        public override GameEventType DiscoveredEventType => GameEventType.DolmenDiscovered;
        public override GameEventType RemovedEventType    => GameEventType.NoEvent;

        public override LocalizedEntry? GetTooltipEntry() => new("hex_tooltip_dolmen");

        public override string? TextIcon => "🗿";
        public override bool ShouldRenderIcon => Found;

        public Dolmen(HexCoord position) : base(position) { }

        [JsonConstructor]
        public Dolmen() : base() { }
    }
}
