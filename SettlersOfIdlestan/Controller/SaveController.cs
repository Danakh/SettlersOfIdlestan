using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using System;
using System.Text;
using System.Text.Json;

namespace SettlersOfIdlestan.Controller
{
    /// <summary>
    /// Gère la sérialisation/désérialisation du MainGameState avec brouillage XOR.
    /// Pipeline export : JSON → Base64 → XOR brouillé (Base64).
    /// Pipeline import : débrouillage → Base64 → JSON, avec fallback JSON brut pour les anciennes sauvegardes.
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
            state.Clock.WasPausedAtSave = state.Clock.SpeedMultiplier == 0;
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
            var data = Encoding.UTF8.GetBytes(plaintext);
            var xored = XorCycle(data, _key);
            return Convert.ToBase64String(xored);
        }

        private static string Decrypt(string encrypted)
        {
            var data = Convert.FromBase64String(encrypted);
            var xored = XorCycle(data, _key);
            return Encoding.UTF8.GetString(xored);
        }

        private static byte[] XorCycle(byte[] data, byte[] key)
        {
            var result = new byte[data.Length];
            for (int i = 0; i < data.Length; i++)
                result[i] = (byte)(data[i] ^ key[i % key.Length]);
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
