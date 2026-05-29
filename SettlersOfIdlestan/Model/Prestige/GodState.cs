using System;

namespace SettlersOfIdlestan.Model.Prestige
{
    /// <summary>
    /// Repr�sente l'�tat du 'Dieu' qui contient l'�tat de prestige.
    /// S�rialisable pour la persistance ou le transport.
    /// </summary>
    [Serializable]
    public class GodState
    {
        /// <summary>
        /// L'�tat de prestige associ� au dieu.
        /// </summary>
        public PrestigeState? PrestigeState { get; set; }

        /// <summary>
        /// Constructeur parameterless requis par certains s�rialiseurs.
        /// </summary>
        public GodState() { }

        public GodState(PrestigeState prestigeState)
        {
            PrestigeState = prestigeState;
        }
    }
}
