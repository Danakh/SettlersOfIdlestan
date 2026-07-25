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

        /// <summary>Nombre maximal de Cercles de Fées invisibles pré-placés par masse continentale à la
        /// génération (voir IslandMapGenerator.PlaceInvisibleFairyCircles), toutes sources de
        /// MAGIC_FEATURE_COUNT confondues (actuellement : vertex Cercles de Fées +2, vertex Bosquet
        /// Flétri +1). À incrémenter si une nouvelle source de MAGIC_FEATURE_COUNT est ajoutée.</summary>
        public const int MaxPerLandmass = 3;

        /// <summary>Index de la masse continentale (île séparée des autres par de l'eau) sur laquelle ce
        /// Cercle a été placé à la génération — permet à MagicController.EnsureMagicFeatures de révéler
        /// le bon nombre de cercles par masse indépendamment des autres, sans recalculer les masses
        /// continentales à l'exécution.</summary>
        public int LandmassIndex { get; set; }

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
