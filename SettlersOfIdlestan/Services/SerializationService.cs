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
            // register converters for hex coord and island map types
            options.Converters.Add(new SettlersOfIdlestan.Model.HexGrid.HexCoordJsonConverter());
            options.Converters.Add(new SettlersOfIdlestan.Model.HexGrid.EdgeJsonConverter());
            // ensure Building polymorphic types are serialized
            options.Converters.Add(new SettlersOfIdlestan.Model.Buildings.BuildingJsonConverter());
            options.Converters.Add(new SettlersOfIdlestan.Model.IslandMap.IslandMapJsonConverter());
            // ensure Vertex (city positions) are properly serialized when exporting
            options.Converters.Add(new SettlersOfIdlestan.Model.HexGrid.VertexJsonConverter());

            return options;
        }
    }
}
