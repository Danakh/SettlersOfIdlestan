using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SettlersOfIdlestan.Controller
{
    /// <summary>
    /// Gère la sérialisation/désérialisation du MainGameState avec chiffrement AES.
    /// Pipeline export : JSON → Base64 → AES chiffré (Base64).
    /// Pipeline import : déchiffrement → Base64 → JSON, avec fallback JSON brut pour les anciennes sauvegardes.
    /// </summary>
    public class SaveController
    {
        // Clé AES-256 dérivée d'une passphrase via SHA-256
        private static readonly byte[] _key = SHA256.HashData(Encoding.UTF8.GetBytes("SettlersOfIdlestan_SaveKey_v1_Danakh"));

        private static readonly JsonSerializerOptions _serializationOptions = MakeSerializationOptions();

        public static JsonSerializerOptions SerializationOptions() => _serializationOptions;

        private static JsonSerializerOptions MakeSerializationOptions()
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
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
            var json = JsonSerializer.Serialize(state, _serializationOptions);
            var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
            return Encrypt(base64);
        }

        public MainGameState Import(string data)
        {
            string json;
            try
            {
                var base64 = Decrypt(data);
                json = Encoding.UTF8.GetString(Convert.FromBase64String(base64));
            }
            catch
            {
                // Fallback pour les sauvegardes en JSON brut (avant chiffrement)
                json = data;
            }
            return DeserializeJson(json);
        }

        private static string Encrypt(string plaintext)
        {
            using var aes = Aes.Create();
            aes.Key = _key;
            aes.GenerateIV();

            var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
            var encryptor = aes.CreateEncryptor();
            var ciphertext = encryptor.TransformFinalBlock(plaintextBytes, 0, plaintextBytes.Length);

            // IV (16 octets) préfixé au texte chiffré
            var result = new byte[aes.IV.Length + ciphertext.Length];
            aes.IV.CopyTo(result, 0);
            ciphertext.CopyTo(result, aes.IV.Length);

            return Convert.ToBase64String(result);
        }

        private static string Decrypt(string encrypted)
        {
            var data = Convert.FromBase64String(encrypted);
            var iv = data[..16];
            var ciphertext = data[16..];

            using var aes = Aes.Create();
            aes.Key = _key;
            aes.IV = iv;

            var decryptor = aes.CreateDecryptor();
            var plaintextBytes = decryptor.TransformFinalBlock(ciphertext, 0, ciphertext.Length);
            return Encoding.UTF8.GetString(plaintextBytes);
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
