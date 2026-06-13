using System;

namespace SettlersOfIdlestan.Model.Game
{
    /// <summary>
    /// Générateur ACORN (Additive Congruential Random Numbers), ordre 25, modulus 2^60.
    /// Période ≈ 2^1500. L'état complet est stocké dans <see cref="State"/> (26 ulong),
    /// sérialisé directement en JSON : recharger une sauvegarde reprend exactement la même
    /// séquence sans rejouer aucun appel.
    /// </summary>
    [Serializable]
    public class GamePRNG
    {
        private const int Order = 25;
        private const ulong ModMask = (1UL << 60) - 1; // 2^60 - 1

        /// <summary>Graine originale, conservée pour référence (debug, affichage).</summary>
        public int Seed { get; set; }

        /// <summary>
        /// État interne ACORN : tableau de (Order + 1) valeurs dans [0, 2^60).
        /// Null uniquement pour les sauvegardes antérieures à ce format ; dans ce cas
        /// Step() reconstruit un état depuis Seed au premier appel.
        /// </summary>
        public ulong[]? State { get; set; }

        /// <summary>Constructeur sans paramètre requis par la désérialisation JSON.</summary>
        public GamePRNG()
        {
            Seed = Environment.TickCount;
            // State initialisé lazily dans Step() pour que la désérialisation puisse
            // écraser Seed avant le premier appel.
        }

        public GamePRNG(int seed)
        {
            Seed = seed;
            State = BuildState(seed);
        }

        private static ulong[] BuildState(int seed)
        {
            var s = new ulong[Order + 1];
            // Y[0] doit être un entier impair dans (0, 2^60) — contrainte ACORN
            s[0] = ((ulong)(uint)seed | 1UL) & ModMask;
            if (s[0] == 0) s[0] = 1;
            // Y[1..Order] initialisés par LCG (Knuth) depuis le seed
            ulong v = (ulong)(uint)seed;
            for (int i = 1; i <= Order; i++)
            {
                v = v * 6364136223846793005UL + 1442695040888963407UL;
                s[i] = v & ModMask;
            }
            return s;
        }

        private ulong Step()
        {
            if (State == null || State.Length != Order + 1)
                State = BuildState(Seed);
            for (int i = 1; i <= Order; i++)
                State[i] = (State[i] + State[i - 1]) & ModMask;
            return State[Order];
        }

        /// <summary>Retourne un entier dans [0, maxExclusive).</summary>
        public int Next(int maxExclusive)
        {
            if (maxExclusive <= 1) return 0;
            return (int)(Step() % (ulong)maxExclusive);
        }

        /// <summary>Retourne un entier dans [minInclusive, maxExclusive).</summary>
        public int Next(int minInclusive, int maxExclusive)
        {
            return minInclusive + Next(maxExclusive - minInclusive);
        }

        /// <summary>Mélange la liste en place (Fisher-Yates).</summary>
        public void Shuffle<T>(System.Collections.Generic.List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
