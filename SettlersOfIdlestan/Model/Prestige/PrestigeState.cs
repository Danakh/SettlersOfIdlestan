using System;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Prestige.PrestigeMap;

namespace SettlersOfIdlestan.Model.Prestige
{
    /// <summary>
    /// Représente l'état lié au prestige et contient l'état de l'île.
    /// Sérialisable pour la persistance ou le transport.
    /// </summary>
    [Serializable]
    public class PrestigeState
    {
        /// <summary>
        /// L'état de l'île associé au prestige.
        /// </summary>
        public IslandState? IslandState { get; set; }

        public int PrestigePoints { get; set; }

        public List<PrestigeVertexId> PurchasedVertices { get; set; } = new();

        /// <summary>
        /// Historique des 5 derniers prestiges effectués.
        /// </summary>
        public List<PrestigeRunStats> RunHistory { get; set; } = new();

        /// <summary>
        /// Constructeur parameterless requis par certains sérialiseurs.
        /// </summary>
        public PrestigeState() { }

        public PrestigeState(IslandState islandState)
        {
            IslandState = islandState;
        }
    }
}

