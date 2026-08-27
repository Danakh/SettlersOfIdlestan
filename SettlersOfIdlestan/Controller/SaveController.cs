using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using System;
using System.Numerics;
using System.Text;
using System.Text.Json;

namespace SettlersOfIdlestan.Controller
{
    /// <summary>
    /// Gère la sérialisation/désérialisation du MainGameState avec brouillage XOR.
    /// Pipeline export (v2) : JSON → XOR brouillé → Base64 (un seul encodage Base64).
    /// Pipeline import : tente le format v2 (XOR direct sur le JSON), puis retombe sur l'ancien
    /// format v1 (qui appliquait le XOR sur la représentation Base64 du JSON, donc un encodage
    /// Base64 redondant), puis sur du JSON brut pour les sauvegardes antérieures au chiffrement.
    /// <para>
    /// <see cref="Encrypt"/> et <see cref="DecodeToJson"/> sont génériques (aucune dépendance à
    /// MainGameState) et sont réutilisés tels quels par les fichiers settings.json et
    /// playerstats.json dans les implémentations d'IFileSystemService, pour leur appliquer le
    /// même brouillage. DecodeToJson retombe sur le texte brut si le contenu n'est pas du Base64
    /// XOR valide, ce qui permet de lire sans erreur les fichiers écrits avant ce chiffrement.
    /// </para>
    /// </summary>
    public class SaveController
    {
        // Clé construite par fragments pour ne pas apparaître en clair dans le binaire
        private static readonly byte[] _key = BuildKey();

        private static byte[] BuildKey()
        {
            var parts = new[] { "b64", typeof(SaveController).Name, "SoI" };
            return Encoding.UTF8.GetBytes(string.Concat(parts));
        }

        private static readonly JsonSerializerOptions _serializationOptions = MakeSerializationOptions();

        public static JsonSerializerOptions SerializationOptions() => _serializationOptions;

        private static JsonSerializerOptions MakeSerializationOptions()
        {
            // WriteIndented=false : cette sortie n'est jamais lue par un humain (elle passe ensuite
            // par XOR+Base64), l'indentation ne fait que doubler la taille de la sauvegarde pour rien.
            var options = new JsonSerializerOptions { WriteIndented = false };
            options.Converters.Add(new HexCoordJsonConverter());
            options.Converters.Add(new EdgeJsonConverter());
            options.Converters.Add(new BuildingJsonConverter());
            options.Converters.Add(new IslandMapJsonConverter());
            options.Converters.Add(new VertexJsonConverter());
            return options;
        }

        public string Export(MainGameState state)
        {
            state.Clock.LastSaveTime = DateTimeOffset.UtcNow;
            state.Clock.WasPausedAtSave = state.Clock.SpeedMultiplier == 0;
            var json = JsonSerializer.Serialize(state, _serializationOptions);
            return Encrypt(json);
        }

        public MainGameState Import(string data)
        {
            return DeserializeJson(DecodeToJson(data));
        }

        /// <summary>
        /// Débrouille une sauvegarde en JSON en essayant successivement le format v2 (actuel), le
        /// format v1 (avec son encodage Base64 redondant hérité), puis un fallback JSON brut. Le XOR
        /// étant appliqué au même endroit (juste après le décodage Base64 externe) dans les deux
        /// formats, un seul débrouillage suffit : seule l'interprétation du résultat diffère (JSON
        /// direct en v2, chaîne Base64 à décoder une seconde fois en v1).
        /// </summary>
        public static string DecodeToJson(string data)
        {
            byte[] unXored;
            try
            {
                unXored = XorCycle(Convert.FromBase64String(data), _key);
            }
            catch
            {
                // Pas du Base64 valide du tout — sauvegarde en JSON brut (avant chiffrement).
                return data;
            }

            var candidate = Encoding.UTF8.GetString(unXored);
            if (LooksLikeJson(candidate)) return candidate; // v2

            try
            {
                return Encoding.UTF8.GetString(Convert.FromBase64String(candidate)); // v1
            }
            catch
            {
                return data; // Fallback JSON brut
            }
        }

        private static bool LooksLikeJson(string s)
        {
            var trimmed = s.TrimStart();
            return trimmed.Length > 0 && trimmed[0] == '{';
        }

        public static string Encrypt(string json)
        {
            var data = Encoding.UTF8.GetBytes(json);
            var xored = XorCycle(data, _key);
            return Convert.ToBase64String(xored);
        }

        /// <summary>
        /// Brouillage XOR à clé cyclique. Sur une sauvegarde de fin de partie (~3 Mo de JSON), la
        /// version octet-par-octet avec modulo (<c>i % key.Length</c>) coûtait à elle seule ~14 ms —
        /// quasiment autant que la sérialisation JSON — car la longueur de clé (21 octets) n'est pas
        /// une puissance de 2 et chaque octet payait une division. On matérialise ici la clé répétée
        /// sur toute la longueur du buffer (copie doublante, O(n) séquentiel) puis on XOR par blocs
        /// SIMD (<see cref="Vector{T}"/>) ; le résultat reste identique octet pour octet à l'ancienne
        /// implémentation, seule la méthode de calcul change.
        /// </summary>
        private static byte[] XorCycle(byte[] data, byte[] key)
        {
            int len = data.Length;
            var result = new byte[len];
            if (len == 0) return result;

            var keyStream = new byte[len];
            int filled = Math.Min(key.Length, len);
            Array.Copy(key, keyStream, filled);
            while (filled < len)
            {
                int toCopy = Math.Min(filled, len - filled);
                Array.Copy(keyStream, 0, keyStream, filled, toCopy);
                filled += toCopy;
            }

            int vectorSize = Vector<byte>.Count;
            int i = 0;
            for (; i <= len - vectorSize; i += vectorSize)
            {
                var vData = new Vector<byte>(data, i);
                var vKey = new Vector<byte>(keyStream, i);
                (vData ^ vKey).CopyTo(result, i);
            }
            for (; i < len; i++)
                result[i] = (byte)(data[i] ^ keyStream[i]);

            return result;
        }

        private static MainGameState DeserializeJson(string json)
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            options.Converters.Add(new HexCoordJsonConverter());
            options.Converters.Add(new EdgeJsonConverter());
            options.Converters.Add(new BuildingJsonConverter());
            options.Converters.Add(new IslandMapJsonConverter());
            options.Converters.Add(new VertexJsonConverter());

            return JsonSerializer.Deserialize<MainGameState>(json, options)
                   ?? throw new InvalidOperationException("Échec de la désérialisation du MainGameState.");
        }
    }
}
