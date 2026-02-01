using System;
using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestan.Model.PrestigeMap
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
