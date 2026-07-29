using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SettlersOfIdlestan.Model.Game
{
    /// <summary>
    /// Générateur Lehmer / Park-Miller "minimal standard" (a = 16807, m = 2^31 - 1).
    /// La mise à jour de l'état est calculée par décomposition 16 bits (hi/lo) plutôt que par un
    /// modulo 64 bits direct, pour rester fidèle à l'implémentation de référence. L'état complet
    /// (un seul <see cref="uint"/>) est sérialisé dans <see cref="State"/> : recharger une
    /// sauvegarde reprend exactement la même séquence sans rejouer aucun appel.
    /// </summary>
    [Serializable]
    public class GamePRNG
    {
        private const uint Modulus = 0x7FFFFFFF; // 2^31 - 1
        private const uint Multiplier = 16807;

        /// <summary>Graine originale, conservée pour référence (debug, affichage).</summary>
        public int Seed { get; set; }

        /// <summary>
        /// État interne du générateur, dans (0, 2^31 - 1]. Zéro uniquement pour les sauvegardes
        /// antérieures à ce format ; dans ce cas Step() reconstruit un état depuis Seed au premier
        /// appel.
        /// </summary>
        [JsonConverter(typeof(LegacyStateConverter))]
        public uint State { get; set; }

        /// <summary>
        /// [Legacy remap v0.12] Avant v0.12, State était un tableau de 26 ulong (état ACORN). Un
        /// tableau (ou toute valeur non numérique) est ignoré silencieusement : State reste à 0,
        /// ce qui force Step() à reconstruire un état Lehmer/Park-Miller depuis Seed au prochain
        /// appel. La séquence exacte de l'ancienne sauvegarde n'est pas reproductible (changement
        /// d'algorithme), mais le chargement ne plante plus.
        /// </summary>
        private sealed class LegacyStateConverter : JsonConverter<uint>
        {
            public override uint Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                if (reader.TokenType == JsonTokenType.Number && reader.TryGetUInt32(out uint value))
                    return value;
                reader.Skip();
                return 0;
            }

            public override void Write(Utf8JsonWriter writer, uint value, JsonSerializerOptions options)
                => writer.WriteNumberValue(value);
        }

        /// <summary>Constructeur sans paramètre requis par la désérialisation JSON.</summary>
        public GamePRNG()
        {
            Seed = 42;
            // State initialisé lazily dans Step() pour que la désérialisation puisse
            // écraser Seed avant le premier appel.
        }

        public GamePRNG(int seed)
        {
            Seed = seed;
            State = BuildState(seed);
        }

        private static uint BuildState(int seed)
        {
            // L'état doit être non nul dans [0, 2^31 - 1) — contrainte du générateur.
            uint s = (uint)seed & Modulus;
            if (s == 0) s = 1;
            return s;
        }

        private double Step()
        {
            if (State == 0)
                State = BuildState(Seed);

            uint hi = Multiplier * (State >> 16);
            uint lo = Multiplier * (State & 0xFFFF) + ((hi & 0x7FFF) << 16) + (hi >> 15);
            State = lo > Modulus ? lo - Modulus : lo;
            return (double)(State - 1) / Modulus;
        }

        /// <summary>Retourne un entier dans [0, maxExclusive).</summary>
        public int Next(int maxExclusive)
        {
            if (maxExclusive <= 1) return 0;
            return (int)(Step() * maxExclusive);
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

        /// <summary>
        /// Dérive un entier utilisable comme seed pour un autre <see cref="GamePRNG"/> (typiquement
        /// le PRNG de gameplay, dérivé une fois du PRNG de génération d'île).
        /// </summary>
        public int NextSeed() => Next(int.MaxValue);
    }
}
