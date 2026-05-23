using Jint.Runtime;
using SettlersOfIdlestan.Controller;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Services.Localization;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Services;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SettlersOfIdlestanSkia.Renderers
{
    public class TooltipRenderer : IGameRenderer
    {
        private string[] _tooltipTexts = new string[0];
        private SKPoint _tooltipScreenPosition = SKPoint.Empty;
        private SKSize _canvasSize;
        private SKFont _font10 = new SKFont { Size = 10 };

        private IslandMainRenderer? _islandRendererContext;
        private GameRenderContext? _gameRenderContext;
        private ILocalizationService _localizationService;
        private CityBuilderController _cityController;
        private RoadController _roadController;


        public TooltipRenderer(ILocalizationService localizationService, GameControllerService gameControllerService)
        {
            _localizationService = localizationService;
            _cityController = gameControllerService.MainGameController.CityBuilderController;
            _roadController = gameControllerService.MainGameController.RoadController;
        }

        public void Initialize(SKSize canvasSize)
        {
            _canvasSize = canvasSize;
        }

        public void ClearTooltip()
        {
            _tooltipTexts = new string[0];
        }

        public void SetIslandRenderContext(IslandMainRenderer? islandRenderer, GameRenderContext? context)
        {
            _islandRendererContext = islandRenderer;
            _gameRenderContext = context;
        }

        public void SetTooltip(string text, SkiaSharp.SKPoint screenPosition)
        {
            _tooltipTexts = new string[] { text };
            _tooltipScreenPosition = screenPosition;
        }

        public void SetTooltipLines(string[] lines, SKPoint screenPosition)
        {
            _tooltipTexts = lines;
            _tooltipScreenPosition = screenPosition;
        }

        public bool HasTooltip()
        {
            return _tooltipTexts.Length > 0;
        }

        public void SetRoadConstructionTooltip(Edge roadPosition)
        {
            if ((_islandRendererContext != null) && (_gameRenderContext != null))
            {
                var cost = _roadController.GetPlayerRoadCost(roadPosition);
                var costText = SkiaTextUtils.computeCostString(_localizationService, cost);
                _tooltipTexts = new string[] { _localizationService.Get("road_construction"), costText };

                var islandPosition = _islandRendererContext.EdgeToIslandPoint(roadPosition);
                _tooltipScreenPosition = _islandRendererContext.IslandToScreen(islandPosition, _gameRenderContext.ZoomLevel, _gameRenderContext.CameraPosition);
            }
        }

        public void SetOutpostConstructionTooltip(Vertex cityPosition)
        {
            if ((_islandRendererContext != null) && (_gameRenderContext != null))
            {
                var cost = _cityController.NewCityBuildingCost();
                var costText = SkiaTextUtils.computeCostString(_localizationService, cost);
                _tooltipTexts = new string[] { _localizationService.Get("outpost_construction"), costText };

                var islandPosition = _islandRendererContext.VertexToIslandPoint(cityPosition);
                _tooltipScreenPosition = _islandRendererContext.IslandToScreen(islandPosition, _gameRenderContext.ZoomLevel, _gameRenderContext.CameraPosition);
            }
        }

        public void SetHexHarvestTooltip(HexCoord coord, HarvestController harvestController, IslandState islandState, DateTimeOffset currentTime)
        {
            if (_islandRendererContext == null || _gameRenderContext == null)
                return;

            var playerIdx = islandState.PlayerCivilization.Index;
            var manualResources = harvestController.GetManualHarvestableResources(playerIdx, coord);
            var autoResources = harvestController.GetAutomaticHarvestableResources(playerIdx, coord);

            if (manualResources.Count == 0 && autoResources.Count == 0)
                return;

            var lines = new List<string>();

            var allResources = manualResources.Union(autoResources).Distinct();
            lines.Add(string.Join(", ", allResources.Select(r => _localizationService.Get($"resource_{r.ToString().ToLower()}"))));

            if (manualResources.Count > 0)
            {
                islandState.HarvestLastTimesByCivilization.TryGetValue(playerIdx, out var manualTimes);
                var manualCooldown = harvestController.GetManualHarvestCooldown(playerIdx);
                lines.Add(FormatCooldownLine(_localizationService.Get("hex_tooltip_manual"), coord, manualTimes, currentTime, manualCooldown));
            }

            if (autoResources.Count > 0)
            {
                islandState.AutomaticHarvestLastTimesByCivilization.TryGetValue(playerIdx, out var autoTimes);
                var autoCooldown = harvestController.GetEffectiveAutoHarvestCooldown(playerIdx, coord);
                lines.Add(FormatCooldownLine(_localizationService.Get("hex_tooltip_auto"), coord, autoTimes, currentTime, autoCooldown));
            }

            _tooltipTexts = lines.ToArray();
            var islandPos = _islandRendererContext.HexCoordToIslandPoint(coord);
            _tooltipScreenPosition = _islandRendererContext.IslandToScreen(islandPos, _gameRenderContext.ZoomLevel, _gameRenderContext.CameraPosition);
        }

        private string FormatCooldownLine(string label, HexCoord coord, Dictionary<HexCoord, DateTimeOffset>? times, DateTimeOffset currentTime, TimeSpan cooldown)
        {
            var max = $"{cooldown.TotalSeconds:0.#}s";

            if (times == null || !times.TryGetValue(coord, out var lastTime))
                return $"{label}: {_localizationService.Get("hex_tooltip_ready")} / {max}";

            var remaining = cooldown.TotalSeconds - (currentTime - lastTime).TotalSeconds;
            if (remaining <= 0)
                return $"{label}: {_localizationService.Get("hex_tooltip_ready")} / {max}";

            return $"{label}: {remaining:F1}s / {max}";
        }

        public void Render(SKCanvas canvas, GameRenderContext context)
        {
            if (_tooltipTexts.Length > 0)
            {
                TooltipRenderUtils.DrawTooltip(canvas, _canvasSize, _tooltipScreenPosition, _tooltipTexts, _font10);
                _tooltipTexts = new string[0];
            }
        }

        public void Dispose()
        {
            // no op
        }
    }
}
