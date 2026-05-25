using SettlersOfIdlestan.Model.HexGrid;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace SettlersOfIdlestan.Services
{
    internal class SerializationService
    {
        static JsonSerializerOptions DefaultOptions = MakeDefaultOptions();

        public static JsonSerializerOptions SerializationOptions()
        {
            return DefaultOptions;
        }

        private static JsonSerializerOptions MakeDefaultOptions()
        {
            var options = new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            };
            options.Converters.Add(new SettlersOfIdlestan.Model.HexGrid.HexCoordJsonConverter());
            options.Converters.Add(new SettlersOfIdlestan.Model.HexGrid.EdgeJsonConverter());
            options.Converters.Add(new SettlersOfIdlestan.Model.Buildings.BuildingJsonConverter());
            options.Converters.Add(new SettlersOfIdlestan.Model.IslandMap.IslandMapJsonConverter());
            options.Converters.Add(new SettlersOfIdlestan.Model.HexGrid.VertexJsonConverter());

            return options;
        }
    }
}
