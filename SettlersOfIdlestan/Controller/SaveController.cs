using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using System;
using System.Buffers;
using System.Buffers.Text;
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
        private static readonly JsonSerializerOptions _deserializationOptions = MakeDeserializationOptions();

        /// <summary>
        /// Taille initiale du tampon d'écriture JSON. Une sauvegarde de fin de partie pèse ~1 Mo :
        /// partir de 2 Mo évite la dizaine de doublements de <see cref="ArrayBufferWriter{T}"/>, dont
        /// les derniers recopient chacun tout le tampon.
        /// </summary>
        private const int InitialJsonBufferBytes = 2 * 1024 * 1024;

        public static JsonSerializerOptions SerializationOptions() => _serializationOptions;

        private static void AddSaveConverters(JsonSerializerOptions options)
        {
            options.Converters.Add(new HexCoordJsonConverter());
            options.Converters.Add(new EdgeJsonConverter());
            options.Converters.Add(new BuildingJsonConverter());
            options.Converters.Add(new IslandMapJsonConverter());
            options.Converters.Add(new VertexJsonConverter());
        }

        private static JsonSerializerOptions MakeSerializationOptions()
        {
            // WriteIndented=false : cette sortie n'est jamais lue par un humain (elle passe ensuite
            // par XOR+Base64), l'indentation ne fait que doubler la taille de la sauvegarde pour rien.
            var options = new JsonSerializerOptions
            {
                WriteIndented = false,
                // Retire de la sauvegarde tout ce qui ne peut de toute façon pas en revenir — voir
                // SavePropertyTrimmer. Uniquement à l'écriture : la lecture garde le résolveur par
                // défaut, pour ne rien changer à la façon dont les anciennes sauvegardes sont relues.
                TypeInfoResolver = SavePropertyTrimmer.Resolver,
            };
            AddSaveConverters(options);
            return options;
        }

        /// <summary>
        /// Options de lecture. Mises en cache : construire un <see cref="JsonSerializerOptions"/>
        /// reconstruit tout son cache de métadonnées par réflexion, et cet objet était auparavant
        /// recréé à chaque import — 95 ms au lieu de 63 ms sur une sauvegarde de fin de partie, pour
        /// un travail rigoureusement identique d'un chargement à l'autre.
        /// </summary>
        private static JsonSerializerOptions MakeDeserializationOptions()
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            AddSaveConverters(options);
            return options;
        }

        public string Export(MainGameState state)
        {
            StampSaveMetadata(state);
            var json = JsonSerializer.Serialize(state, _serializationOptions);
            return Encrypt(json);
        }

        /// <summary>
        /// Première moitié d'<see cref="Export"/> : le JSON de l'état, en UTF-8, non brouillé.
        /// <para>
        /// C'est la <b>seule</b> partie de la sauvegarde qui lit le modèle vivant, donc la seule qui
        /// doive tourner sous le verrou du runtime. Le brouillage
        /// (<see cref="EncryptUtf8"/>) ne travaille plus que sur les octets rendus ici et peut partir
        /// sur un autre thread — voir la sauvegarde automatique dans <c>GameScreen</c>.
        /// </para>
        /// <para>
        /// La mémoire retournée appartient à l'appelant : elle porte sur un tampon dédié qu'aucun
        /// autre code ne touche, et reste donc valide et immuable une fois passée à un autre thread.
        /// Rien n'est jamais matérialisé en <see cref="string"/> : sur une partie de fin de jeu, la
        /// sauvegarde automatique allouait ~13,5 Mo à chaque passage — dont deux chaînes de plusieurs
        /// mégaoctets partant droit dans le tas des grands objets, toutes les 5 secondes. Les
        /// appelants qui ont besoin d'un texte (export manuel) gardent <see cref="Export"/>.
        /// </para>
        /// </summary>
        public ReadOnlyMemory<byte> SerializeUtf8(MainGameState state)
        {
            StampSaveMetadata(state);

            var buffer = new ArrayBufferWriter<byte>(InitialJsonBufferBytes);
            // SkipValidation : le graphe est écrit par JsonSerializer, qui ne produit pas de JSON
            // déséquilibré — la validation ne ferait que coûter sur ~1 Mo de sortie.
            using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { SkipValidation = true }))
                JsonSerializer.Serialize(writer, state, _serializationOptions);

            return buffer.WrittenMemory;
        }

        private static void StampSaveMetadata(MainGameState state)
        {
            state.Clock.LastSaveTime = DateTimeOffset.UtcNow;
            state.Clock.WasPausedAtSave = state.Clock.SpeedMultiplier == 0;
            state.SavedGameVersion = GameVersion.Current;
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
                unXored = Convert.FromBase64String(data);
                XorCycle(unXored, unXored);
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
            XorCycle(data, data);
            return Convert.ToBase64String(data);
        }

        /// <summary>
        /// Même brouillage que <see cref="Encrypt"/>, d'un JSON déjà en UTF-8 vers du Base64 en
        /// UTF-8 : octet pour octet, le résultat est celui qu'aurait produit
        /// <c>Encrypt(Encoding.UTF8.GetString(json))</c>, sans les deux chaînes intermédiaires.
        /// </summary>
        public static byte[] EncryptUtf8(ReadOnlySpan<byte> json)
        {
            // Base64.GetMaxEncodedToUtf8Length est la longueur exacte pour un bloc final complet
            // (4 × ⌈n/3⌉, remplissage compris) : le tableau retourné est plein, pas surdimensionné.
            var result = new byte[Base64.GetMaxEncodedToUtf8Length(json.Length)];
            var scrambled = ArrayPool<byte>.Shared.Rent(json.Length);
            try
            {
                XorCycle(json, scrambled.AsSpan(0, json.Length));
                Base64.EncodeToUtf8(scrambled.AsSpan(0, json.Length), result, out _, out _);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(scrambled);
            }
            return result;
        }

        /// <summary>
        /// Brouillage XOR à clé cyclique. Sur une sauvegarde de fin de partie (~3 Mo de JSON), la
        /// version octet-par-octet avec modulo (<c>i % key.Length</c>) coûtait à elle seule ~14 ms —
        /// quasiment autant que la sérialisation JSON — car la longueur de clé (21 octets) n'est pas
        /// une puissance de 2 et chaque octet payait une division. On matérialise ici la clé répétée
        /// sur toute la longueur du buffer (copie doublante, O(n) séquentiel) puis on XOR par blocs
        /// SIMD (<see cref="Vector{T}"/>) ; le résultat reste identique octet pour octet à l'ancienne
        /// implémentation, seule la méthode de calcul change.
        /// <para>
        /// <paramref name="source"/> et <paramref name="destination"/> peuvent désigner le même
        /// tampon : chaque octet n'est lu qu'une fois, à la position où il est réécrit.
        /// </para>
        /// </summary>
        private static void XorCycle(ReadOnlySpan<byte> source, Span<byte> destination)
        {
            int len = source.Length;
            if (len == 0) return;

            var rented = ArrayPool<byte>.Shared.Rent(len);
            try
            {
                var keyStream = rented.AsSpan(0, len);
                int filled = Math.Min(_key.Length, len);
                _key.AsSpan(0, filled).CopyTo(keyStream);
                while (filled < len)
                {
                    int toCopy = Math.Min(filled, len - filled);
                    keyStream[..toCopy].CopyTo(keyStream[filled..]);
                    filled += toCopy;
                }

                int vectorSize = Vector<byte>.Count;
                int i = 0;
                for (; i <= len - vectorSize; i += vectorSize)
                {
                    var vData = new Vector<byte>(source[i..]);
                    var vKey = new Vector<byte>(keyStream[i..]);
                    (vData ^ vKey).CopyTo(destination[i..]);
                }
                for (; i < len; i++)
                    destination[i] = (byte)(source[i] ^ keyStream[i]);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }

        private static MainGameState DeserializeJson(string json)
        {
            return JsonSerializer.Deserialize<MainGameState>(json, _deserializationOptions)
                   ?? throw new InvalidOperationException("Échec de la désérialisation du MainGameState.");
        }
    }
}
