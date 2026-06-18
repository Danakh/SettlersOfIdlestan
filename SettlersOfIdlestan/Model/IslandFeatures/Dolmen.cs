using System.Text.Json.Serialization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;

namespace SettlersOfIdlestan.Model.IslandFeatures
{
    /// <summary>
    /// Dolmen — feature retirée du jeu (le vertex de prestige qui la débloquait est devenu Achat Automatique).
    /// Conservée uniquement pour la désérialisation des anciennes sauvegardes.
    /// </summary>
    public class Dolmen : IslandFeature
    {
        public override GameEventType DiscoveredEventType => GameEventType.NoEvent;
        public override GameEventType RemovedEventType    => GameEventType.NoEvent;

        public Dolmen(HexCoord position) : base(position) { }

        [JsonConstructor]
        public Dolmen() : base() { }
    }
}
